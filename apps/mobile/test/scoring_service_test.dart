import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sports_platform_mobile/core/services/api_client.dart';
import 'package:sports_platform_mobile/core/services/connectivity_service.dart';
import 'package:sports_platform_mobile/core/services/scoring_service.dart';

void main() {
  test('scoring schema is loaded from the contest API contract', () async {
    final api = _CapturingApiClient();
    final connectivity = ConnectivityService(
      checkConnectivity: () async => [ConnectivityResult.wifi],
    );
    final service = ScoringService(api, connectivityService: connectivity);

    final schema = await service.getSchema('contest-1');

    expect(api.path, '/api/livescoring/contests/contest-1/schema');
    expect(schema.sportCode, 'athletics');
    expect(schema.actions.single.inputType, 'number');
    expect(schema.actions.single.step, 0.001);
    expect(schema.segments, hasLength(2));
    expect(schema.segments.last.parentSegmentId, 'round-1');
    expect(schema.segments.last.depth, 1);
    expect(schema.eventDefinitions.single.code, 'TIME_RECORDED');
    expect(schema.eventDefinitions.single.requiredMetric, 'time_seconds');
    service.dispose();
    connectivity.stopMonitoring();
  });

  test('online score uses the live-scoring API contract', () async {
    final api = _CapturingApiClient();
    final connectivity = ConnectivityService(
      checkConnectivity: () async => [ConnectivityResult.wifi],
    );
    final service = ScoringService(
      api,
      connectivityService: connectivity,
    );

    final result = await service.submitEvent(
      contestId: 'contest-1',
      participantId: 'participant-1',
      metricName: 'score',
      value: 1,
      scoredBy: 'scorer@example.com',
      eventId: 'stable-event-id',
      segmentId: 'segment-1',
      clockSeconds: 12.5,
      eventType: 'GOAL',
    );

    expect(result.isSuccess, isTrue);
    expect(result.queuedOffline, isFalse);
    expect(api.path, '/api/livescoring/contests/contest-1/events');
    expect(api.data, {
      'eventId': 'stable-event-id',
      'participantId': 'participant-1',
      'metricName': 'score',
      'value': 1,
      'scoredBy': 'scorer@example.com',
      'deviceTimestamp': isA<String>(),
      'idempotencyKey': 'stable-event-id',
      'segmentId': 'segment-1',
      'clockSeconds': 12.5,
      'eventType': 'GOAL',
    });
    service.dispose();
    connectivity.stopMonitoring();
  });

  test('session context and clock use the authoritative API contract',
      () async {
    final api = _CapturingApiClient();
    final connectivity = ConnectivityService(
      checkConnectivity: () async => [ConnectivityResult.wifi],
    );
    final service = ScoringService(api, connectivityService: connectivity);

    final updated = await service.setSessionContext(
      contestId: 'contest-1',
      segmentId: 'segment-2',
      clockSeconds: 45,
    );
    expect(updated, isTrue);
    expect(api.path, '/api/livescoring/contests/contest-1/session/context');
    expect(api.data, {'segmentId': 'segment-2', 'clockSeconds': 45});

    expect(await service.startClock('contest-1'), isTrue);
    expect(api.path, '/api/livescoring/contests/contest-1/session/clock/start');
    expect(await service.pauseClock('contest-1'), isTrue);
    expect(api.path, '/api/livescoring/contests/contest-1/session/clock/pause');
    expect(await service.advanceSegment('contest-1'), isTrue);
    expect(
        api.path, '/api/livescoring/contests/contest-1/session/segment/next');

    service.dispose();
    connectivity.stopMonitoring();
  });

  test('check-in and result submission use queue-safe workflow endpoints',
      () async {
    final api = _CapturingApiClient();
    final connectivity = ConnectivityService(
      checkConnectivity: () async => [ConnectivityResult.wifi],
    );
    final service = ScoringService(api, connectivityService: connectivity);

    final checkIn = await service.checkInParticipant(
      contestId: 'contest-1',
      participantId: 'participant-1',
    );
    expect(checkIn.isSuccess, isTrue);
    expect(
      api.path,
      '/api/checkin/contests/contest-1/participants/participant-1/checkin',
    );

    final submission = await service.submitContestResults('contest-1');
    expect(submission.isSuccess, isTrue);
    expect(api.path, '/api/results/contest/contest-1/submit');

    service.dispose();
    connectivity.stopMonitoring();
  });

  test('corrections and reversals enter the approval workflow', () async {
    final api = _CapturingApiClient();
    final connectivity = ConnectivityService(
      checkConnectivity: () async => [ConnectivityResult.wifi],
    );
    final service = ScoringService(api, connectivityService: connectivity);

    expect(
      await service.correctEvent(
        contestId: 'contest-1',
        eventId: 'event-1',
        newValue: 3,
        reason: 'Signed sheet',
      ),
      isTrue,
    );
    expect(api.path,
        '/api/livescoring/contests/contest-1/events/event-1/corrections');
    expect(api.data, {
      'kind': 'Correct',
      'value': 3,
      'reason': 'Signed sheet',
    });

    expect(
      await service.reverseEvent(
        contestId: 'contest-1',
        eventId: 'event-1',
        reason: 'Video review',
      ),
      isTrue,
    );
    expect(api.data, {
      'kind': 'Reverse',
      'reason': 'Video review',
    });

    service.dispose();
    connectivity.stopMonitoring();
  });
}

class _CapturingApiClient extends ApiClient {
  String? path;
  Map<String, dynamic>? data;

  @override
  Future<Response<T>> get<T>(
    String path, {
    Map<String, dynamic>? queryParameters,
    Options? options,
  }) async {
    this.path = path;
    return Response<T>(
      requestOptions: RequestOptions(path: path),
      statusCode: 200,
      data: <String, dynamic>{
        'sportCode': 'athletics',
        'name': 'Athletics',
        'primaryMetric': 'time_seconds',
        'scoreLabel': 'seconds',
        'lowerIsBetter': true,
        'supportsClock': true,
        'clockCountsDown': false,
        'actions': [
          {
            'code': 'time',
            'label': 'Record time',
            'metric': 'time_seconds',
            'defaultValue': 10,
            'inputType': 'number',
            'minimum': 0,
            'maximum': 86400,
            'step': 0.001,
            'unit': 'seconds',
            'tone': 'primary',
            'requiresSegment': false,
            'requiresParticipant': true,
            'requiresPlayer': false,
            'eventType': 'TIME_RECORDED',
          }
        ],
        'eventDefinitions': [
          {
            'code': 'TIME_RECORDED',
            'name': 'Time recorded',
            'segmentApplicability': ['Race'],
            'requiresParticipant': true,
            'requiresSecondaryParticipant': false,
            'requiredMetric': 'time_seconds',
            'timingRequirement': 'Timestamp',
            'correctionPolicy': 'ApprovalRequired',
            'validation': {'minimumValue': 0},
          }
        ],
        'segments': <Map<String, dynamic>>[
          {
            'id': 'round-1',
            'name': 'Round 1',
            'type': 'Round',
            'sortOrder': 1,
            'status': 'Pending',
            'parentSegmentId': null,
            'depth': 0,
            'isOptional': false,
          },
          {
            'id': 'hole-1',
            'name': 'Hole 1',
            'type': 'Hole',
            'sortOrder': 1,
            'status': 'Pending',
            'parentSegmentId': 'round-1',
            'depth': 1,
            'isOptional': false,
          },
        ],
      } as T,
    );
  }

  @override
  Future<Response<T>> post<T>(
    String path, {
    dynamic data,
    Map<String, dynamic>? queryParameters,
    Options? options,
  }) async {
    this.path = path;
    this.data =
        data is Map ? Map<String, dynamic>.from(data) : <String, dynamic>{};
    return Response<T>(
      requestOptions: RequestOptions(path: path),
      statusCode: 200,
    );
  }

  @override
  Future<Response<T>> put<T>(
    String path, {
    dynamic data,
    Map<String, dynamic>? queryParameters,
    Options? options,
  }) async {
    this.path = path;
    this.data =
        data is Map ? Map<String, dynamic>.from(data) : <String, dynamic>{};
    return Response<T>(
      requestOptions: RequestOptions(path: path),
      statusCode: 200,
    );
  }
}
