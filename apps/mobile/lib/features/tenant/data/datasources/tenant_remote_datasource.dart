import 'package:dio/dio.dart';

import '../../../../core/services/api_client.dart';
import '../../domain/models/tenant.dart';

class TenantRemoteDataSource {
  final ApiClient _apiClient;

  TenantRemoteDataSource(this._apiClient);

  Future<Tenant> discover(String code) async {
    final slug = code.trim().toLowerCase();
    if (!RegExp(r'^[a-z0-9]+(?:-[a-z0-9]+)*$').hasMatch(slug)) {
      throw const FormatException('Enter a valid tenant code.');
    }

    try {
      final response = await _apiClient.get<Map<String, dynamic>>(
        '/api/public/tenants/$slug',
      );
      final data = response.data;
      if (data == null) throw StateError('Tenant response was empty.');
      return Tenant.fromJson({...data, 'slug': data['slug'] ?? slug});
    } on DioException catch (error) {
      if (error.response?.statusCode == 404) {
        throw StateError('No active tenant uses that code.');
      }
      rethrow;
    }
  }
}
