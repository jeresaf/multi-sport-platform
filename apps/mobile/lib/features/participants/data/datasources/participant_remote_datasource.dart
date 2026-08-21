import '../../../../core/services/api_client.dart';
import '../../../../core/config/app_config.dart';

/// Remote data source for participants
class ParticipantRemoteDataSource {
  final ApiClient _apiClient;

  ParticipantRemoteDataSource(this._apiClient);

  /// Get all participants
  Future<List<Map<String, dynamic>>> getParticipants({
    String? search,
    String? type,
    String? sportCode,
    int page = 1,
    int pageSize = 20,
  }) async {
    if (!await _apiClient.isAuthenticated()) return const [];
    final queryParams = <String, dynamic>{
      'page': page,
      'pageSize': pageSize,
    };

    if (search != null && search.isNotEmpty) queryParams['search'] = search;
    if (type != null) queryParams['type'] = type;
    if (sportCode != null) queryParams['sportCode'] = sportCode;

    final response = await _apiClient.get(
      '/api/participants',
      queryParameters: queryParams,
    );

    if (response.data is Map && response.data['items'] != null) {
      return List<Map<String, dynamic>>.from(response.data['items']);
    }

    if (response.data is List) {
      return List<Map<String, dynamic>>.from(response.data);
    }

    return [];
  }

  /// Get participant by ID
  Future<Map<String, dynamic>?> getParticipant(String id) async {
    final publicPath =
        '/api/public/tenants/${Environment.tenantSlug}/participants/$id';
    try {
      // A participant opened from a public contest remains a public resource,
      // even when an administrator happens to be signed in. This also avoids
      // applying an unrelated account tenant to a supporter link.
      final response = await _apiClient.get(publicPath);
      return response.data as Map<String, dynamic>?;
    } catch (_) {
      if (!await _apiClient.isAuthenticated()) return null;
    }
    try {
      final response = await _apiClient.get('/api/participants/$id');
      return response.data as Map<String, dynamic>?;
    } catch (_) {
      return null;
    }
  }

  /// Get participant statistics
  Future<Map<String, dynamic>?> getParticipantStats(String id) async {
    try {
      final response = await _apiClient.get('/api/participants/$id/stats');
      return response.data as Map<String, dynamic>?;
    } catch (e) {
      return null;
    }
  }

  /// Get participant contests
  Future<List<Map<String, dynamic>>> getParticipantContests(
    String participantId, {
    int page = 1,
    int pageSize = 20,
  }) async {
    try {
      final response = await _apiClient.get(
        '/api/participants/$participantId/contests',
        queryParameters: {'page': page, 'pageSize': pageSize},
      );

      if (response.data is List) {
        return List<Map<String, dynamic>>.from(response.data);
      }

      return [];
    } catch (e) {
      return [];
    }
  }
}
