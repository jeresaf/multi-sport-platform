import 'dart:async';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/services/api_client.dart';
import '../../../../core/services/signalr_service.dart';
import '../../data/datasources/contest_remote_datasource.dart';
import '../../data/repositories/contest_repository_impl.dart' as repo;
import '../../../auth/presentation/providers/auth_provider.dart';
import '../../../tenant/presentation/providers/tenant_provider.dart';
import '../../../supporter/data/supporter_preferences.dart';

// Re-export types for convenience
export '../../data/repositories/contest_repository_impl.dart'
    show Contest, ContestEvent, ContestRepository;

/// Provider for contest remote data source
final contestRemoteDataSourceProvider =
    Provider<ContestRemoteDataSource>((ref) {
  ref.watch(tenantStateProvider.select((state) => state.selectedTenant?.slug));
  final apiClient = ref.watch(apiClientProvider);
  return ContestRemoteDataSource(apiClient);
});

/// Provider for API client (shared)
final apiClientProvider = Provider<ApiClient>((ref) {
  return ApiClient();
});

/// Provider for contest repository
final contestRepositoryProvider = Provider<repo.ContestRepository>((ref) {
  final remoteDataSource = ref.watch(contestRemoteDataSourceProvider);
  return repo.ContestRepository(remoteDataSource);
});

/// Provider for SignalR service
final signalRServiceProvider = Provider<SignalRService>((ref) {
  final apiClient = ref.watch(apiClientProvider);
  final service = SignalRService(apiClient);
  ref.onDispose(service.dispose);
  return service;
});

final signalRConnectionStatusProvider =
    StreamProvider<SignalRConnectionStatus>((ref) async* {
  final service = ref.watch(signalRServiceProvider);
  yield service.status;
  yield* service.connectionStatuses;
});

/// State for live contests
class LiveContestsState {
  final List<repo.Contest> contests;
  final bool isLoading;
  final String? error;

  const LiveContestsState({
    this.contests = const [],
    this.isLoading = false,
    this.error,
  });

  LiveContestsState copyWith({
    List<repo.Contest>? contests,
    bool? isLoading,
    String? error,
  }) {
    return LiveContestsState(
      contests: contests ?? this.contests,
      isLoading: isLoading ?? this.isLoading,
      error: error,
    );
  }
}

/// Notifier for live contests
class LiveContestsNotifier extends StateNotifier<LiveContestsState> {
  final repo.ContestRepository _repository;
  Timer? _refreshTimer;

  LiveContestsNotifier(
    this._repository, {
    bool lowDataMode = false,
  }) : super(const LiveContestsState()) {
    loadLiveContests();
    if (!lowDataMode) _startAutoRefresh();
  }

  void _startAutoRefresh() {
    _refreshTimer = Timer.periodic(const Duration(seconds: 30), (_) {
      loadLiveContests();
    });
  }

  Future<void> loadLiveContests() async {
    if (state.isLoading) return;

    state = state.copyWith(isLoading: true, error: null);

    try {
      final contests = await _repository.getLiveContests();
      state = state.copyWith(
        contests: contests,
        isLoading: false,
      );
    } catch (e) {
      state = state.copyWith(
        isLoading: false,
        error: e.toString(),
      );
    }
  }

  @override
  void dispose() {
    _refreshTimer?.cancel();
    super.dispose();
  }
}

/// Provider for live contests
final liveContestsProvider =
    StateNotifierProvider<LiveContestsNotifier, LiveContestsState>((ref) {
  final repository = ref.watch(contestRepositoryProvider);
  final lowDataMode = ref.watch(
    supporterPreferencesProvider.select((state) => state.lowDataMode),
  );
  return LiveContestsNotifier(repository, lowDataMode: lowDataMode);
});

/// State for single contest
class ContestDetailState {
  final repo.Contest? contest;
  final List<repo.ContestEvent> events;
  final bool isLoading;
  final String? error;

  const ContestDetailState({
    this.contest,
    this.events = const [],
    this.isLoading = false,
    this.error,
  });

  ContestDetailState copyWith({
    repo.Contest? contest,
    List<repo.ContestEvent>? events,
    bool? isLoading,
    String? error,
  }) {
    return ContestDetailState(
      contest: contest ?? this.contest,
      events: events ?? this.events,
      isLoading: isLoading ?? this.isLoading,
      error: error,
    );
  }
}

/// Notifier for contest detail
class ContestDetailNotifier extends StateNotifier<ContestDetailState> {
  final repo.ContestRepository _repository;
  final SignalRService _signalRService;
  StreamSubscription? _eventSubscription;
  StreamSubscription? _contestUpdateSubscription;
  final String contestId;

  ContestDetailNotifier(
    this._repository,
    this._signalRService,
    this.contestId,
  ) : super(const ContestDetailState()) {
    _loadContest();
    _subscribeToUpdates();
  }

  Future<void> _loadContest() async {
    state = state.copyWith(isLoading: true, error: null);

    try {
      final contest = await _repository.getContest(contestId);
      final events = await _repository.getContestEvents(contestId);

      state = state.copyWith(
        contest: contest,
        events: events,
        isLoading: false,
      );
    } catch (e) {
      state = state.copyWith(
        isLoading: false,
        error: e.toString(),
      );
    }
  }

  void _subscribeToUpdates() {
    unawaited(_signalRService.connect(contestId: contestId));

    _eventSubscription = _signalRService.contestEvents.listen((signalREvent) {
      if (signalREvent.contestId == contestId) {
        // Convert SignalR event to repository event
        final event = repo.ContestEvent(
          id: signalREvent.eventId,
          contestId: signalREvent.contestId,
          eventType: signalREvent.eventType,
          participantId: signalREvent.participantId,
          secondaryParticipantId: signalREvent.secondaryParticipantId,
          data: signalREvent.metrics,
          timestamp: signalREvent.timestamp,
          clientId: signalREvent.clientId,
        );
        state = state.copyWith(
          events: [
            ...state.events.where((item) => item.id != event.id),
            event,
          ]..sort((left, right) => left.timestamp.compareTo(right.timestamp)),
        );
      }
    });

    _contestUpdateSubscription =
        _signalRService.contestUpdates.listen((update) {
      if (update.contestId == contestId) {
        unawaited(_loadContest());
      }
    });
  }

  Future<void> submitEvent({
    required String eventType,
    required Map<String, dynamic> data,
  }) async {
    try {
      final event = await _repository.submitLiveEvent(
        contestId: contestId,
        eventType: eventType,
        data: data,
      );

      if (event != null) {
        state = state.copyWith(
          events: [...state.events, event],
        );
      }
    } catch (e) {
      state = state.copyWith(error: e.toString());
    }
  }

  Future<void> refresh() async {
    await _loadContest();
  }

  @override
  void dispose() {
    _eventSubscription?.cancel();
    _contestUpdateSubscription?.cancel();
    unawaited(_signalRService.unsubscribeFromContest(contestId));
    super.dispose();
  }
}

/// Provider for contest detail
final contestDetailProvider = StateNotifierProvider.family<
    ContestDetailNotifier, ContestDetailState, String>(
  (ref, contestId) {
    final repository = ref.watch(contestRepositoryProvider);
    final signalRService = ref.watch(signalRServiceProvider);
    return ContestDetailNotifier(repository, signalRService, contestId);
  },
);

/// Provider for upcoming contests
final upcomingContestsProvider =
    FutureProvider<List<repo.Contest>>((ref) async {
  final repository = ref.watch(contestRepositoryProvider);
  return repository.getUpcomingContests();
});

/// Contests available to the authenticated scorer's tenant.
final assignedContestsProvider =
    FutureProvider<List<repo.Contest>>((ref) async {
  final auth = ref.watch(authStateProvider);
  if (!auth.isAuthenticated) return const [];
  final repository = ref.watch(contestRepositoryProvider);
  return repository.getAssignedContests(tenantId: auth.tenantId);
});
