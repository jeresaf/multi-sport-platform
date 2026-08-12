import '../../domain/models/tenant.dart';
import '../../domain/repositories/tenant_repository.dart';
import '../datasources/tenant_local_datasource.dart';
import '../datasources/tenant_remote_datasource.dart';

/// Implementation of TenantRepository
class TenantRepositoryImpl implements TenantRepository {
  final TenantLocalDataSource _localDataSource;
  final TenantRemoteDataSource _remoteDataSource;

  TenantRepositoryImpl({
    required TenantLocalDataSource localDataSource,
    required TenantRemoteDataSource remoteDataSource,
  })  : _localDataSource = localDataSource,
        _remoteDataSource = remoteDataSource;

  @override
  Future<Tenant> discoverTenant(String code) async {
    final tenant = await _remoteDataSource.discover(code);
    final tenants = await _localDataSource.getCachedTenants();
    await _localDataSource.cacheTenants([
      tenant,
      ...tenants.where((item) => item.id != tenant.id),
    ]);
    return tenant;
  }

  @override
  Future<List<Tenant>> getTenants() async {
    return _localDataSource.getCachedTenants();
  }

  @override
  Future<Tenant?> getTenantById(String id) async {
    final tenants = await _localDataSource.getCachedTenants();
    try {
      return tenants.firstWhere((t) => t.id == id);
    } catch (_) {
      return null;
    }
  }

  @override
  Future<Tenant?> getSelectedTenant() async {
    final tenantId = await _localDataSource.getSelectedTenantId();
    if (tenantId == null) return null;
    return getTenantById(tenantId);
  }

  @override
  Future<void> selectTenant(String tenantId) async {
    await _localDataSource.setSelectedTenantId(tenantId);
  }

  @override
  Future<void> clearSelectedTenant() async {
    await _localDataSource.clearSelectedTenantId();
  }
}
