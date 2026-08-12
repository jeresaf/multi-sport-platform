import '../../../../core/config/app_config.dart';
import '../../../../core/services/api_client.dart';

class CompetitionRemoteDataSource {
  final ApiClient _apiClient;

  CompetitionRemoteDataSource(this._apiClient);

  String get _publicRoot => '/api/public/tenants/${Environment.tenantSlug}';

  Future<List<Map<String, dynamic>>> getCompetitions({
    String? search,
    String? sportCode,
    String? status,
    int page = 1,
    int pageSize = 20,
  }) async {
    final response = await _apiClient.get(
      '$_publicRoot/competitions',
      queryParameters: {
        if (search != null && search.isNotEmpty) 'search': search,
        if (sportCode != null) 'sportId': _sportIds[sportCode],
      },
    );
    final items = _asList(response.data);
    return status == null
        ? items
        : items.where((item) => item['status']?.toString() == status).toList();
  }

  Future<Map<String, dynamic>?> getCompetition(String id) async {
    final response = await _apiClient.get('$_publicRoot/competitions/$id');
    return response.data is Map
        ? Map<String, dynamic>.from(response.data as Map)
        : null;
  }

  /// The public prototype does not expose a competition-to-contest route yet.
  Future<List<Map<String, dynamic>>> getCompetitionContests(
    String competitionId, {
    String? status,
  }) async =>
      const [];

  List<Map<String, dynamic>> _asList(dynamic value) => value is List
      ? value
          .whereType<Map>()
          .map((item) => Map<String, dynamic>.from(item))
          .toList()
      : const [];
}

const _sportIds = {
  'football': '00000000-0000-0000-0000-000000000100',
  'basketball': '00000000-0000-0000-0000-000000000101',
  'golf': '00000000-0000-0000-0000-000000000102',
  'athletics': '00000000-0000-0000-0000-000000000103',
  'cricket': '00000000-0000-0000-0000-000000000104',
  'combat': '00000000-0000-0000-0000-000000000105',
  'rugby': '00000000-0000-0000-0000-000000000106',
};
