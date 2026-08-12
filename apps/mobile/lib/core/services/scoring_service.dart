import 'dart:async';

import 'package:dio/dio.dart';
import 'package:uuid/uuid.dart';

import 'api_client.dart';
import 'connectivity_service.dart';
import 'offline_sync_service.dart';

/// Sends the same idempotent live-score request online or through durable
/// offline replay.
class ScoringService {
  final ApiClient _apiClient;
  final OfflineSyncService? _offlineSyncService;
  final ConnectivityService _connectivityService;

  final StreamController<ScoringEvent> _eventController =
      StreamController<ScoringEvent>.broadcast();
  Stream<ScoringEvent> get events => _eventController.stream;

  ScoringService(
    this._apiClient, {
    OfflineSyncService? offlineSyncService,
    required ConnectivityService connectivityService,
  })  : _offlineSyncService = offlineSyncService,
        _connectivityService = connectivityService;

  Future<ScoringSchema> getSchema(String contestId) async {
    final response = await _apiClient.get<Map<String, dynamic>>(
      '/api/livescoring/contests/$contestId/schema',
    );
    final data = response.data;
    if (data == null) {
      throw StateError('The scoring schema response was empty.');
    }
    return ScoringSchema.fromJson(data);
  }

  Future<ScoringSessionContext?> getSession(String contestId) async {
    try {
      final response = await _apiClient.get<Map<String, dynamic>>(
        '/api/livescoring/contests/$contestId/session',
      );
      final data = response.data;
      return data == null ? null : ScoringSessionContext.fromJson(data);
    } on DioException catch (error) {
      if (error.response?.statusCode == 404) return null;
      rethrow;
    }
  }

  Future<bool> setSessionContext({
    required String contestId,
    required String? segmentId,
    required num clockSeconds,
  }) async {
    try {
      final response = await _apiClient.put(
        '/api/livescoring/contests/$contestId/session/context',
        data: {
          'segmentId': segmentId,
          'clockSeconds': clockSeconds,
        },
      );
      return _isSuccessful(response.statusCode);
    } catch (_) {
      return false;
    }
  }

  Future<bool> startClock(String contestId) =>
      _sessionAction(contestId, 'clock/start');

  Future<bool> pauseClock(String contestId) =>
      _sessionAction(contestId, 'clock/pause');

  Future<bool> advanceSegment(String contestId) =>
      _sessionAction(contestId, 'segment/next');

  Future<WorkflowResult> checkInParticipant({
    required String contestId,
    required String participantId,
  }) =>
      _queueableWorkflow(
        type: 'participant_check_in',
        endpoint:
            '/api/checkin/contests/$contestId/participants/$participantId/checkin',
      );

  Future<WorkflowResult> submitContestResults(String contestId) =>
      _queueableWorkflow(
        type: 'result_submission',
        endpoint: '/api/results/contest/$contestId/submit',
      );

  Future<ScoringResult> submitEvent({
    required String contestId,
    required String participantId,
    required String metricName,
    num value = 1,
    String? scoredBy,
    String? eventId,
    String? segmentId,
    num? clockSeconds,
    String? eventType,
  }) async {
    final id = eventId ?? const Uuid().v4();
    final timestamp = DateTime.now().toUtc();
    final endpoint = '/api/livescoring/contests/$contestId/events';
    final eventData = <String, dynamic>{
      'eventId': id,
      'participantId': participantId,
      'metricName': metricName,
      'value': value,
      'scoredBy': scoredBy ?? 'mobile',
      'deviceTimestamp': timestamp.toIso8601String(),
      'idempotencyKey': id,
      if (segmentId != null) 'segmentId': segmentId,
      if (clockSeconds != null) 'clockSeconds': clockSeconds,
      if (eventType != null && eventType.isNotEmpty) 'eventType': eventType,
    };
    final event = ScoringEvent(
      id: id,
      contestId: contestId,
      metricName: metricName,
      participantId: participantId,
      value: value,
      timestamp: timestamp,
      isSynced: false,
      segmentId: segmentId,
      clockSeconds: clockSeconds,
    );

    final connectivityState = await _connectivityService.checkConnectivity();
    if (connectivityState.isOnline) {
      try {
        final response = await _apiClient.post(endpoint, data: eventData);
        final status = response.statusCode ?? 500;
        if (status >= 200 && status < 300) {
          final synced = event.copyWith(isSynced: true);
          _eventController.add(synced);
          return ScoringResult.success(synced);
        }
      } on DioException catch (error) {
        if (error.response != null) {
          return ScoringResult.failure(_serverError(error));
        }
        // Network failures are safe to queue because the event ID is stable.
      } catch (_) {
        // A transport failure may have happened after the server committed.
        // Replay remains safe because the backend treats eventId idempotently.
      }
    }

    if (_offlineSyncService == null) {
      return ScoringResult.failure(
        'Cannot submit event while offline: synchronization is unavailable.',
      );
    }
    await _offlineSyncService.queueEvent(
      id: id,
      type: 'scoring_event',
      endpoint: endpoint,
      method: 'POST',
      data: eventData,
      syncImmediately: false,
    );
    _eventController.add(event);
    return ScoringResult.success(event, queuedOffline: true);
  }

  Future<bool> startSession(String contestId, {String? name}) async {
    try {
      final response = await _apiClient.post(
        '/api/livescoring/contests/$contestId/session/start',
        data: {
          'eventId': const Uuid().v4(),
          'name': name ?? 'Mobile scoring',
        },
      );
      return _isSuccessful(response.statusCode);
    } catch (_) {
      return false;
    }
  }

  Future<bool> pauseSession(String contestId) =>
      _sessionAction(contestId, 'pause');

  Future<bool> resumeSession(String contestId) =>
      _sessionAction(contestId, 'resume');

  Future<bool> endSession(String contestId) => _sessionAction(contestId, 'end');

  Future<bool> correctEvent({
    required String contestId,
    required String eventId,
    required num newValue,
    required String reason,
    String? updatedBy,
  }) async {
    try {
      final response = await _apiClient.post(
        '/api/livescoring/contests/$contestId/events/$eventId/corrections',
        data: {
          'kind': 'Correct',
          'value': newValue,
          'reason': reason,
        },
      );
      return _isSuccessful(response.statusCode);
    } catch (_) {
      return false;
    }
  }

  Future<bool> reverseEvent({
    required String contestId,
    required String eventId,
    required String reason,
    String? reversedBy,
  }) async {
    try {
      final response = await _apiClient.post(
        '/api/livescoring/contests/$contestId/events/$eventId/corrections',
        data: {
          'kind': 'Reverse',
          'reason': reason,
        },
      );
      return _isSuccessful(response.statusCode);
    } catch (_) {
      return false;
    }
  }

  Future<bool> _sessionAction(String contestId, String action) async {
    try {
      final response = await _apiClient.post(
        '/api/livescoring/contests/$contestId/session/$action',
      );
      return _isSuccessful(response.statusCode);
    } catch (_) {
      return false;
    }
  }

  Future<WorkflowResult> _queueableWorkflow({
    required String type,
    required String endpoint,
  }) async {
    final connectivityState = await _connectivityService.checkConnectivity();
    if (connectivityState.isOnline) {
      try {
        final response = await _apiClient.post(endpoint);
        if (_isSuccessful(response.statusCode)) {
          return const WorkflowResult.success();
        }
      } on DioException catch (error) {
        if (error.response != null) {
          return WorkflowResult.failure(_serverError(error));
        }
      } catch (_) {
        // Queueing is safe because both workflow endpoints are idempotent.
      }
    }

    if (_offlineSyncService == null) {
      return WorkflowResult.failure(
        'Cannot save this operation while offline: synchronization is unavailable.',
      );
    }
    await _offlineSyncService.queueEvent(
      id: const Uuid().v4(),
      type: type,
      endpoint: endpoint,
      method: 'POST',
      data: const {},
      syncImmediately: false,
    );
    return const WorkflowResult.success(queuedOffline: true);
  }

  bool _isSuccessful(int? status) =>
      status != null && status >= 200 && status < 300;

  String _serverError(DioException error) {
    final data = error.response?.data;
    if (data is Map) {
      return (data['message'] ?? data['error'] ?? error.message).toString();
    }
    return error.message ?? 'The scoring event was rejected.';
  }

  void dispose() => _eventController.close();
}

class ScoringEvent {
  final String id;
  final String contestId;
  final String metricName;
  final String participantId;
  final num value;
  final DateTime timestamp;
  final bool isSynced;
  final String? segmentId;
  final num? clockSeconds;

  const ScoringEvent({
    required this.id,
    required this.contestId,
    required this.metricName,
    required this.participantId,
    required this.value,
    required this.timestamp,
    required this.isSynced,
    this.segmentId,
    this.clockSeconds,
  });

  ScoringEvent copyWith({bool? isSynced}) => ScoringEvent(
        id: id,
        contestId: contestId,
        metricName: metricName,
        participantId: participantId,
        value: value,
        timestamp: timestamp,
        isSynced: isSynced ?? this.isSynced,
        segmentId: segmentId,
        clockSeconds: clockSeconds,
      );
}

class ScoringResult {
  final bool isSuccess;
  final ScoringEvent? event;
  final String? errorMessage;
  final bool queuedOffline;

  const ScoringResult._({
    required this.isSuccess,
    this.event,
    this.errorMessage,
    this.queuedOffline = false,
  });

  factory ScoringResult.success(
    ScoringEvent event, {
    bool queuedOffline = false,
  }) =>
      ScoringResult._(
        isSuccess: true,
        event: event,
        queuedOffline: queuedOffline,
      );

  factory ScoringResult.failure(String message) =>
      ScoringResult._(isSuccess: false, errorMessage: message);
}

class WorkflowResult {
  final bool isSuccess;
  final bool queuedOffline;
  final String? errorMessage;

  const WorkflowResult._({
    required this.isSuccess,
    this.queuedOffline = false,
    this.errorMessage,
  });

  const WorkflowResult.success({bool queuedOffline = false})
      : this._(isSuccess: true, queuedOffline: queuedOffline);

  factory WorkflowResult.failure(String message) =>
      WorkflowResult._(isSuccess: false, errorMessage: message);
}

class ScoringSchema {
  final String sportCode;
  final String name;
  final String primaryMetric;
  final String scoreLabel;
  final bool lowerIsBetter;
  final bool supportsClock;
  final bool clockCountsDown;
  final List<ScoringAction> actions;
  final List<ScoringSegment> segments;
  final List<ScoringEventDefinition> eventDefinitions;

  const ScoringSchema({
    required this.sportCode,
    required this.name,
    required this.primaryMetric,
    required this.scoreLabel,
    required this.lowerIsBetter,
    required this.supportsClock,
    required this.clockCountsDown,
    required this.actions,
    required this.segments,
    required this.eventDefinitions,
  });

  factory ScoringSchema.fromJson(Map<String, dynamic> json) => ScoringSchema(
        sportCode: json['sportCode']?.toString() ?? '',
        name: json['name']?.toString() ?? 'Sport',
        primaryMetric: json['primaryMetric']?.toString() ?? 'score',
        scoreLabel: json['scoreLabel']?.toString() ?? 'score',
        lowerIsBetter: json['lowerIsBetter'] == true,
        supportsClock: json['supportsClock'] == true,
        clockCountsDown: json['clockCountsDown'] == true,
        actions: (json['actions'] as List<dynamic>? ?? const [])
            .map((item) =>
                ScoringAction.fromJson(Map<String, dynamic>.from(item as Map)))
            .toList(growable: false),
        segments: (json['segments'] as List<dynamic>? ?? const [])
            .map((item) =>
                ScoringSegment.fromJson(Map<String, dynamic>.from(item as Map)))
            .toList(growable: false),
        eventDefinitions:
            (json['eventDefinitions'] as List<dynamic>? ?? const [])
                .map((item) => ScoringEventDefinition.fromJson(
                    Map<String, dynamic>.from(item as Map)))
                .toList(growable: false),
      );
}

class ScoringEventDefinition {
  final String code;
  final String name;
  final List<String> segmentApplicability;
  final bool requiresParticipant;
  final bool requiresSecondaryParticipant;
  final String? requiredMetric;
  final String timingRequirement;
  final String correctionPolicy;
  final Map<String, dynamic> validation;

  const ScoringEventDefinition({
    required this.code,
    required this.name,
    required this.segmentApplicability,
    required this.requiresParticipant,
    required this.requiresSecondaryParticipant,
    required this.requiredMetric,
    required this.timingRequirement,
    required this.correctionPolicy,
    required this.validation,
  });

  factory ScoringEventDefinition.fromJson(Map<String, dynamic> json) =>
      ScoringEventDefinition(
        code: json['code']?.toString() ?? '',
        name: json['name']?.toString() ?? '',
        segmentApplicability:
            (json['segmentApplicability'] as List<dynamic>? ?? const [])
                .map((item) => item.toString())
                .toList(growable: false),
        requiresParticipant: json['requiresParticipant'] == true,
        requiresSecondaryParticipant:
            json['requiresSecondaryParticipant'] == true,
        requiredMetric: json['requiredMetric']?.toString(),
        timingRequirement: json['timingRequirement']?.toString() ?? 'None',
        correctionPolicy:
            json['correctionPolicy']?.toString() ?? 'ApprovalRequired',
        validation: Map<String, dynamic>.from(
            json['validation'] as Map? ?? const <String, dynamic>{}),
      );
}

class ScoringSessionContext {
  final int status;
  final String? activeSegmentId;
  final num currentClockSeconds;
  final bool clockCountsDown;
  final bool isClockRunning;

  const ScoringSessionContext({
    required this.status,
    required this.activeSegmentId,
    required this.currentClockSeconds,
    required this.clockCountsDown,
    required this.isClockRunning,
  });

  factory ScoringSessionContext.fromJson(Map<String, dynamic> json) =>
      ScoringSessionContext(
        status: json['status'] as int? ?? 0,
        activeSegmentId: json['activeSegmentId']?.toString(),
        currentClockSeconds: json['currentClockSeconds'] as num? ??
            json['clockSeconds'] as num? ??
            0,
        clockCountsDown: json['clockCountsDown'] == true,
        isClockRunning: json['isClockRunning'] == true,
      );
}

class ScoringAction {
  final String code;
  final String label;
  final String metric;
  final num defaultValue;
  final String inputType;
  final num? minimum;
  final num? maximum;
  final num? step;
  final String? unit;
  final String tone;
  final bool requiresSegment;
  final bool requiresParticipant;
  final bool requiresPlayer;
  final String? eventType;

  const ScoringAction({
    required this.code,
    required this.label,
    required this.metric,
    required this.defaultValue,
    required this.inputType,
    this.minimum,
    this.maximum,
    this.step,
    this.unit,
    required this.tone,
    required this.requiresSegment,
    required this.requiresParticipant,
    required this.requiresPlayer,
    required this.eventType,
  });

  factory ScoringAction.fromJson(Map<String, dynamic> json) => ScoringAction(
        code: json['code']?.toString() ?? '',
        label: json['label']?.toString() ?? '',
        metric: json['metric']?.toString() ?? '',
        defaultValue: json['defaultValue'] as num? ?? 1,
        inputType: json['inputType']?.toString() ?? 'fixed',
        minimum: json['minimum'] as num?,
        maximum: json['maximum'] as num?,
        step: json['step'] as num?,
        unit: json['unit']?.toString(),
        tone: json['tone']?.toString() ?? 'primary',
        requiresSegment: json['requiresSegment'] == true,
        requiresParticipant: json['requiresParticipant'] != false,
        requiresPlayer: json['requiresPlayer'] == true,
        eventType: json['eventType']?.toString(),
      );
}

class ScoringSegment {
  final String id;
  final String name;
  final String type;
  final int sortOrder;
  final String status;
  final String? parentSegmentId;
  final int depth;
  final bool isOptional;
  final String? activationCondition;

  const ScoringSegment({
    required this.id,
    required this.name,
    required this.type,
    required this.sortOrder,
    required this.status,
    required this.parentSegmentId,
    required this.depth,
    required this.isOptional,
    required this.activationCondition,
  });

  factory ScoringSegment.fromJson(Map<String, dynamic> json) => ScoringSegment(
        id: json['id']?.toString() ?? '',
        name: json['name']?.toString() ?? '',
        type: json['type']?.toString() ?? '',
        sortOrder: json['sortOrder'] as int? ?? 0,
        status: json['status']?.toString() ?? 'Pending',
        parentSegmentId: json['parentSegmentId']?.toString(),
        depth: json['depth'] as int? ?? 0,
        isOptional: json['isOptional'] == true,
        activationCondition: json['activationCondition']?.toString(),
      );
}
