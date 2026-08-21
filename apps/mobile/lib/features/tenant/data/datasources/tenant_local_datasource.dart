import 'dart:convert';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import '../../domain/models/tenant.dart';
import '../../../../core/services/recovering_secure_storage.dart';

/// Local data source for tenant data
class TenantLocalDataSource {
  final FlutterSecureStorage _secureStorage;

  static const String _selectedTenantKey = 'selected_tenant_id';
  static const String _tenantsCacheKey = 'tenants_cache';

  TenantLocalDataSource({FlutterSecureStorage? secureStorage})
      : _secureStorage = secureStorage ?? appSecureStorage;

  /// Get selected tenant ID
  Future<String?> getSelectedTenantId() async {
    return _secureStorage.readRecovering(key: _selectedTenantKey);
  }

  /// Set selected tenant ID
  Future<void> setSelectedTenantId(String tenantId) async {
    await _secureStorage.writeRecovering(
        key: _selectedTenantKey, value: tenantId);
  }

  /// Clear selected tenant ID
  Future<void> clearSelectedTenantId() async {
    await _secureStorage.deleteRecovering(key: _selectedTenantKey);
  }

  /// Cache tenants
  Future<void> cacheTenants(List<Tenant> tenants) async {
    final jsonList = tenants.map((t) => t.toJson()).toList();
    await _secureStorage.writeRecovering(
        key: _tenantsCacheKey, value: jsonEncode(jsonList));
  }

  /// Get cached tenants
  Future<List<Tenant>> getCachedTenants() async {
    final jsonString =
        await _secureStorage.readRecovering(key: _tenantsCacheKey);
    if (jsonString == null) return [];

    final jsonList = jsonDecode(jsonString) as List;
    return jsonList
        .map((json) => Tenant.fromJson(json as Map<String, dynamic>))
        .toList();
  }

  /// Clear tenants cache
  Future<void> clearCache() async {
    await _secureStorage.deleteRecovering(key: _tenantsCacheKey);
  }
}
