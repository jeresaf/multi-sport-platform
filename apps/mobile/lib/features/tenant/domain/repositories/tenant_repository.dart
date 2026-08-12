import '../models/tenant.dart';

/// Tenant repository interface
abstract class TenantRepository {
  /// Get all available tenants
  Future<List<Tenant>> getTenants();

  /// Get tenant by ID
  Future<Tenant?> getTenantById(String id);

  /// Get selected tenant
  Future<Tenant?> getSelectedTenant();

  /// Resolve a public tenant code/slug and cache the result.
  Future<Tenant> discoverTenant(String code);

  /// Select a tenant
  Future<void> selectTenant(String tenantId);

  /// Clear selected tenant
  Future<void> clearSelectedTenant();
}
