import 'package:uuid/uuid.dart';

import '../../../../core/config/app_config.dart';
import '../../../../core/services/api_client.dart';

class ContestRemoteDataSource {
  final ApiClient _apiClient;

  ContestRemoteDataSource(this._apiClient);

  String get _publicRoot => '/api/public/tenants/${Environment.tenantSlug}';

  Future<List<Map<String, dynamic>>> getAssignedContests({
    String? tenantId,
  }) async {
    final response = await _apiClient.get(
      '/api/contests',
      queryParameters: tenantId == null ? null : {'tenantId': tenantId},
    );
    return _asList(response.data);
  }

  Future<Map<String, dynamic>?> getContest(String id) async {
    final authenticated = await _apiClient.isAuthenticated();
    dynamic response;
    try {
      // Published contest pages use one stable contract for guests and signed-in
      // supporters. Internal/draft contests still fall back to the admin API.
      response = await _apiClient.get('$_publicRoot/contests/$id');
    } catch (_) {
      if (!authenticated) rethrow;
      response = await _apiClient.get('/api/contests/$id');
    }
    return response.data is Map
        ? Map<String, dynamic>.from(response.data as Map)
        : null;
  }

  Future<List<Map<String, dynamic>>> getLiveContests() async {
    final response = await _apiClient.get('$_publicRoot/home');
    if (response.data is! Map) return const [];
    return _asList((response.data as Map)['liveContests']);
  }

  Future<List<Map<String, dynamic>>> getUpcomingContests() async {
    final response = await _apiClient.get('$_publicRoot/home');
    if (response.data is! Map) return const [];
    return _asList((response.data as Map)['upcomingContests']);
  }

  Future<List<Map<String, dynamic>>> getContestEvents(String contestId) async {
    try {
      final response = await _apiClient.get(
        '$_publicRoot/contests/$contestId/timeline',
        queryParameters: {'mode': 'all', 'limit': 200},
      );
      if (response.data is Map) {
        return _asList((response.data as Map)['events']);
      }
      return const [];
    } catch (_) {
      if (!await _apiClient.isAuthenticated()) rethrow;
      final response = await _apiClient.get(
        '/api/v1/contests/$contestId/timeline',
        queryParameters: {'mode': 'all', 'limit': 200},
      );
      return response.data is Map
          ? _asList((response.data as Map)['events'])
          : const [];
    }
  }

  Future<List<Map<String, dynamic>>> getContestParticipants(
    String contestId,
  ) async {
    final contest = await getContest(contestId);
    return _asList(contest?['participants']);
  }

  Future<Map<String, dynamic>?> submitLiveEvent({
    required String contestId,
    required String eventType,
    required Map<String, dynamic> data,
  }) async {
    final eventId = data['eventId']?.toString() ?? const Uuid().v4();
    final participantId = data['participantId']?.toString();
    if (participantId == null || participantId.isEmpty) {
      throw ArgumentError('participantId is required');
    }
    final response = await _apiClient.post(
      '/api/livescoring/contests/$contestId/events',
      data: {
        'eventId': eventId,
        'participantId': participantId,
        'metricName': data['metricName'] ?? eventType,
        'value': data['value'] ?? 1,
        'scoredBy': data['scoredBy'] ?? 'mobile',
        'eventType': eventType,
        if (data['secondaryParticipantId'] != null)
          'secondaryParticipantId': data['secondaryParticipantId'],
        if (data['segmentId'] != null) 'segmentId': data['segmentId'],
        'deviceTimestamp':
            data['deviceTimestamp'] ?? DateTime.now().toUtc().toIso8601String(),
        if (data['deviceId'] != null) 'deviceId': data['deviceId'],
        if (data['sequenceNumber'] != null)
          'sequenceNumber': data['sequenceNumber'],
        'idempotencyKey': data['idempotencyKey'] ?? eventId,
      },
    );
    if (response.data is! Map) return null;
    return {
      ...Map<String, dynamic>.from(response.data as Map),
      'id': (response.data as Map)['eventId'] ?? eventId,
      'contestId': contestId,
      'participantId': participantId,
      'metricName': data['metricName'] ?? eventType,
      'value': data['value'] ?? 1,
      'createdAt': DateTime.now().toUtc().toIso8601String(),
    };
  }

  Future<Map<String, dynamic>?> getContestStats(String contestId) async {
    final scores = await getContestEvents(contestId);
    return {'scores': scores};
  }

  List<Map<String, dynamic>> _asList(dynamic value) {
    if (value is List) {
      return value
          .whereType<Map>()
          .map((item) => Map<String, dynamic>.from(item))
          .toList();
    }
    if (value is Map && value['items'] is List) {
      return _asList(value['items']);
    }
    if (value is Map && value['events'] is List) {
      return _asList(value['events']);
    }
    return [];
  }
}
