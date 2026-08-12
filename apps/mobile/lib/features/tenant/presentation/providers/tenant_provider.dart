import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../data/datasources/tenant_local_datasource.dart';
import '../../data/datasources/tenant_remote_datasource.dart';
import '../../data/repositories/tenant_repository_impl.dart';
import '../../domain/models/tenant.dart';
import '../../domain/repositories/tenant_repository.dart';
import '../../../../core/config/app_config.dart';
import '../../../../core/services/api_client.dart';

/// Tenant state
class TenantState {
  final String? selectedTenantId;
  final Tenant? selectedTenant;
  final List<Tenant> availableTenants;
  final bool isLoading;
  final String? error;

  const TenantState({
    this.selectedTenantId,
    this.selectedTenant,
    this.availableTenants = const [],
    this.isLoading = false,
    this.error,
  });

  TenantState copyWith({
    String? selectedTenantId,
    Tenant? selectedTenant,
    List<Tenant>? availableTenants,
    bool? isLoading,
    String? error,
  }) {
    return TenantState(
      selectedTenantId: selectedTenantId ?? this.selectedTenantId,
      selectedTenant: selectedTenant ?? this.selectedTenant,
      availableTenants: availableTenants ?? this.availableTenants,
      isLoading: isLoading ?? this.isLoading,
      error: error,
    );
  }
}

/// Tenant state notifier
class TenantNotifier extends StateNotifier<TenantState> {
  final TenantRepository _repository;

  TenantNotifier(this._repository) : super(const TenantState()) {
    _loadState();
  }

  Future<void> _loadState() async {
    state = state.copyWith(isLoading: true);

    try {
      final selectedTenant = await _repository.getSelectedTenant();
      final tenants = await _repository.getTenants();
      if (selectedTenant != null) {
        Environment.selectTenant(selectedTenant.slug);
      }

      state = TenantState(
        selectedTenantId: selectedTenant?.id,
        selectedTenant: selectedTenant,
        availableTenants: tenants,
        isLoading: false,
      );
    } catch (e) {
      state = state.copyWith(
        isLoading: false,
        error: e.toString(),
      );
    }
  }

  Future<void> selectTenant(String tenantId) async {
    state = state.copyWith(isLoading: true);

    try {
      await _repository.selectTenant(tenantId);
      final tenant = await _repository.getTenantById(tenantId);
      if (tenant == null) throw StateError('Tenant is not cached.');
      Environment.selectTenant(tenant.slug);

      state = state.copyWith(
        selectedTenantId: tenantId,
        selectedTenant: tenant,
        isLoading: false,
      );
    } catch (e) {
      state = state.copyWith(
        isLoading: false,
        error: e.toString(),
      );
    }
  }

  Future<bool> discoverAndSelect(String code) async {
    state = state.copyWith(isLoading: true, error: null);
    try {
      final tenant = await _repository.discoverTenant(code);
      await _repository.selectTenant(tenant.id);
      Environment.selectTenant(tenant.slug);
      final tenants = await _repository.getTenants();
      state = TenantState(
        selectedTenantId: tenant.id,
        selectedTenant: tenant,
        availableTenants: tenants,
      );
      return true;
    } catch (error) {
      state = state.copyWith(isLoading: false, error: error.toString());
      return false;
    }
  }

  Future<void> clearTenant() async {
    await _repository.clearSelectedTenant();
    state = const TenantState();
  }

  Future<void> refreshTenants() async {
    await _loadState();
  }
}

/// Provider for tenant local data source
final tenantLocalDataSourceProvider = Provider<TenantLocalDataSource>((ref) {
  return TenantLocalDataSource();
});

final tenantApiClientProvider = Provider<ApiClient>((ref) => ApiClient());

final tenantRemoteDataSourceProvider = Provider<TenantRemoteDataSource>((ref) {
  return TenantRemoteDataSource(ref.watch(tenantApiClientProvider));
});

/// Provider for tenant repository
final tenantRepositoryProvider = Provider<TenantRepository>((ref) {
  return TenantRepositoryImpl(
    localDataSource: ref.read(tenantLocalDataSourceProvider),
    remoteDataSource: ref.read(tenantRemoteDataSourceProvider),
  );
});

/// Provider for tenant state
final tenantStateProvider =
    StateNotifierProvider<TenantNotifier, TenantState>((ref) {
  final repository = ref.watch(tenantRepositoryProvider);
  return TenantNotifier(repository);
});
