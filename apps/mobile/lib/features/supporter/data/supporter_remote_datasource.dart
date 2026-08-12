import '../../../core/config/app_config.dart';
import '../../../core/services/api_client.dart';

class SupporterRemoteDataSource {
  final ApiClient _apiClient;

  SupporterRemoteDataSource(this._apiClient);

  String get _root => '/api/public/tenants/${Environment.tenantSlug}';

  Future<List<Map<String, dynamic>>> getFixtures() async =>
      _list((await _apiClient.get('$_root/fixtures')).data);

  Future<List<Map<String, dynamic>>> getResults() async =>
      _list((await _apiClient.get('$_root/results')).data);

  Future<List<Map<String, dynamic>>> getStandings() async =>
      _list((await _apiClient.get('$_root/standings')).data);

  Future<Map<String, dynamic>> getBracket(String competitionId) async =>
      _map((await _apiClient.get(
        '$_root/playoffs',
        queryParameters: {'competitionId': competitionId},
      ))
          .data);

  Future<Map<String, dynamic>> getGolfLeaderboard(
    String competitionId,
  ) async =>
      _map((await _apiClient.get(
        '$_root/golf/leaderboard',
        queryParameters: {'competitionId': competitionId},
      ))
          .data);

  List<Map<String, dynamic>> _list(dynamic value) => value is List
      ? value
          .whereType<Map>()
          .map((item) => Map<String, dynamic>.from(item))
          .toList()
      : const [];

  Map<String, dynamic> _map(dynamic value) =>
      value is Map ? Map<String, dynamic>.from(value) : const {};
}
