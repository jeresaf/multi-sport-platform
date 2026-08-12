import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/services/api_client.dart';
import '../../data/datasources/competition_remote_datasource.dart';
import '../../data/repositories/competition_repository_impl.dart';
import '../../../tenant/presentation/providers/tenant_provider.dart';

/// Provider for API client
final apiClientProvider = Provider<ApiClient>((ref) {
  return ApiClient();
});

/// Provider for competition remote data source
final competitionRemoteDataSourceProvider =
    Provider<CompetitionRemoteDataSource>((ref) {
  ref.watch(tenantStateProvider.select((state) => state.selectedTenant?.slug));
  final apiClient = ref.watch(apiClientProvider);
  return CompetitionRemoteDataSource(apiClient);
});

/// Provider for competition repository
final competitionRepositoryProvider = Provider<CompetitionRepository>((ref) {
  final remoteDataSource = ref.watch(competitionRemoteDataSourceProvider);
  return CompetitionRepository(remoteDataSource);
});

/// State for competitions list
class CompetitionsState {
  final List<Competition> competitions;
  final bool isLoading;
  final String? error;
  final String? searchQuery;
  final String? selectedSportFilter;
  final String? selectedStatusFilter;

  const CompetitionsState({
    this.competitions = const [],
    this.isLoading = false,
    this.error,
    this.searchQuery,
    this.selectedSportFilter,
    this.selectedStatusFilter,
  });

  CompetitionsState copyWith({
    List<Competition>? competitions,
    bool? isLoading,
    String? error,
    String? searchQuery,
    String? selectedSportFilter,
    String? selectedStatusFilter,
  }) {
    return CompetitionsState(
      competitions: competitions ?? this.competitions,
      isLoading: isLoading ?? this.isLoading,
      error: error,
      searchQuery: searchQuery ?? this.searchQuery,
      selectedSportFilter: selectedSportFilter ?? this.selectedSportFilter,
      selectedStatusFilter: selectedStatusFilter ?? this.selectedStatusFilter,
    );
  }
}

/// Notifier for competitions list
class CompetitionsNotifier extends StateNotifier<CompetitionsState> {
  final CompetitionRepository _repository;

  CompetitionsNotifier(this._repository) : super(const CompetitionsState()) {
    loadCompetitions();
  }

  Future<void> loadCompetitions({bool refresh = false}) async {
    if (state.isLoading && !refresh) return;

    state = state.copyWith(isLoading: true, error: null);

    try {
      final competitions = await _repository.getCompetitions(
        search: state.searchQuery,
        sportCode: state.selectedSportFilter,
        status: state.selectedStatusFilter,
      );

      state = state.copyWith(
        competitions: competitions,
        isLoading: false,
      );
    } catch (e) {
      state = state.copyWith(
        isLoading: false,
        error: e.toString(),
      );
    }
  }

  void setSearchQuery(String? query) {
    state = state.copyWith(searchQuery: query);
    loadCompetitions(refresh: true);
  }

  void setSportFilter(String? sportCode) {
    state = state.copyWith(selectedSportFilter: sportCode);
    loadCompetitions(refresh: true);
  }

  void setStatusFilter(String? status) {
    state = state.copyWith(selectedStatusFilter: status);
    loadCompetitions(refresh: true);
  }

  void clearFilters() {
    state = CompetitionsState(searchQuery: state.searchQuery);
    loadCompetitions(refresh: true);
  }
}

/// Provider for competitions list
final competitionsProvider =
    StateNotifierProvider<CompetitionsNotifier, CompetitionsState>((ref) {
  final repository = ref.watch(competitionRepositoryProvider);
  return CompetitionsNotifier(repository);
});

/// Provider for single competition
final competitionDetailProvider =
    FutureProvider.family<Competition?, String>((ref, id) async {
  final repository = ref.watch(competitionRepositoryProvider);
  return repository.getCompetition(id);
});

/// Available sport filters
const List<Map<String, dynamic>> sportFilters = [
  {'code': 'football', 'name': 'Football', 'icon': '⚽'},
  {'code': 'basketball', 'name': 'Basketball', 'icon': '🏀'},
  {'code': 'golf', 'name': 'Golf', 'icon': '⛳'},
  {'code': 'combat', 'name': 'Combat Sports', 'icon': '🥊'},
  {'code': 'athletics', 'name': 'Athletics', 'icon': '🏃'},
  {'code': 'cricket', 'name': 'Cricket', 'icon': '🏏'},
  {'code': 'rugby', 'name': 'Rugby Union (7s & 15s)', 'icon': '🏉'},
];

/// Available status filters
const List<Map<String, dynamic>> statusFilters = [
  {'value': 'Draft', 'name': 'Draft'},
  {'value': 'Published', 'name': 'Published'},
  {'value': 'RegistrationOpen', 'name': 'Registration Open'},
  {'value': 'InProgress', 'name': 'In Progress'},
  {'value': 'Completed', 'name': 'Completed'},
  {'value': 'Cancelled', 'name': 'Cancelled'},
];
