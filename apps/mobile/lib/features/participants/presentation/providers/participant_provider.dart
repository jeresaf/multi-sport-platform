import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/services/api_client.dart';
import '../../data/datasources/participant_remote_datasource.dart';

/// Participant model
class Participant {
  final String id;
  final String name;
  final String type; // 'athlete' or 'team'
  final String? sportCode;
  final String? avatarUrl;
  final String? country;
  final String? homeVenue;
  final Map<String, dynamic>? metadata;
  final int matchesPlayed;
  final int wins;
  final int losses;
  final int draws;

  const Participant({
    required this.id,
    required this.name,
    required this.type,
    this.sportCode,
    this.avatarUrl,
    this.country,
    this.homeVenue,
    this.metadata,
    this.matchesPlayed = 0,
    this.wins = 0,
    this.losses = 0,
    this.draws = 0,
  });

  factory Participant.fromJson(Map<String, dynamic> json) {
    return Participant(
      id: json['id']?.toString() ?? '',
      name: json['name'] ?? '',
      type: json['participantType']?.toString().toLowerCase() ?? 'athlete',
      sportCode: json['sportCode'],
      avatarUrl: json['logoUrl'] ?? json['avatarUrl'],
      country: json['country'],
      homeVenue: json['homeVenue'],
      metadata: json['metadata'],
      matchesPlayed: json['matchesPlayed'] ?? 0,
      wins: json['wins'] ?? 0,
      losses: json['losses'] ?? 0,
      draws: json['draws'] ?? 0,
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'name': name,
        'type': type,
        'sportCode': sportCode,
        'avatarUrl': avatarUrl,
        'country': country,
        'homeVenue': homeVenue,
        'metadata': metadata,
        'matchesPlayed': matchesPlayed,
        'wins': wins,
        'losses': losses,
        'draws': draws,
      };
}

/// Participant state
class ParticipantState {
  final Participant? participant;
  final List<Map<String, dynamic>> upcomingMatches;
  final List<Map<String, dynamic>> recentResults;
  final Map<String, dynamic>? statistics;
  final bool isLoading;
  final String? error;

  const ParticipantState({
    this.participant,
    this.upcomingMatches = const [],
    this.recentResults = const [],
    this.statistics,
    this.isLoading = false,
    this.error,
  });

  ParticipantState copyWith({
    Participant? participant,
    List<Map<String, dynamic>>? upcomingMatches,
    List<Map<String, dynamic>>? recentResults,
    Map<String, dynamic>? statistics,
    bool? isLoading,
    String? error,
  }) {
    return ParticipantState(
      participant: participant ?? this.participant,
      upcomingMatches: upcomingMatches ?? this.upcomingMatches,
      recentResults: recentResults ?? this.recentResults,
      statistics: statistics ?? this.statistics,
      isLoading: isLoading ?? this.isLoading,
      error: error,
    );
  }
}

/// Participant notifier
class ParticipantNotifier extends StateNotifier<ParticipantState> {
  final String participantId;

  ParticipantNotifier(ApiClient apiClient, this.participantId)
      : super(const ParticipantState()) {
    loadParticipant();
  }

  Future<void> loadParticipant() async {
    state = state.copyWith(isLoading: true, error: null);

    try {
      // In a real app, this would call the API
      // For now, simulate loading
      await Future.delayed(const Duration(milliseconds: 500));

      // Mock participant data
      final participant = Participant(
        id: participantId,
        name: 'Team ${participantId.substring(0, 8)}',
        type: 'team',
        sportCode: 'football',
        matchesPlayed: 10,
        wins: 7,
        losses: 2,
        draws: 1,
      );

      state = ParticipantState(
        participant: participant,
        upcomingMatches: const [],
        recentResults: const [],
        statistics: const {},
        isLoading: false,
      );
    } catch (e) {
      state = state.copyWith(
        isLoading: false,
        error: e.toString(),
      );
    }
  }

  Future<void> refresh() async {
    await loadParticipant();
  }
}

/// Provider for API client
final participantApiClientProvider = Provider<ApiClient>((ref) {
  return ApiClient();
});

/// Provider for participant remote data source
final participantRemoteDataSourceProvider =
    Provider<ParticipantRemoteDataSource>((ref) {
  final apiClient = ref.watch(participantApiClientProvider);
  return ParticipantRemoteDataSource(apiClient);
});

/// Provider for participant detail
final participantDetailProvider =
    StateNotifierProvider.family<ParticipantNotifier, ParticipantState, String>(
  (ref, participantId) {
    final apiClient = ref.watch(participantApiClientProvider);
    return ParticipantNotifier(apiClient, participantId);
  },
);

/// Participants list state
class ParticipantsListState {
  final List<Participant> participants;
  final bool isLoading;
  final String? error;
  final String? searchQuery;
  final String? selectedTypeFilter;
  final String? selectedSportFilter;

  const ParticipantsListState({
    this.participants = const [],
    this.isLoading = false,
    this.error,
    this.searchQuery,
    this.selectedTypeFilter,
    this.selectedSportFilter,
  });

  ParticipantsListState copyWith({
    List<Participant>? participants,
    bool? isLoading,
    String? error,
    String? searchQuery,
    String? selectedTypeFilter,
    String? selectedSportFilter,
  }) {
    return ParticipantsListState(
      participants: participants ?? this.participants,
      isLoading: isLoading ?? this.isLoading,
      error: error,
      searchQuery: searchQuery ?? this.searchQuery,
      selectedTypeFilter: selectedTypeFilter ?? this.selectedTypeFilter,
      selectedSportFilter: selectedSportFilter ?? this.selectedSportFilter,
    );
  }
}

/// Participants list notifier
class ParticipantsListNotifier extends StateNotifier<ParticipantsListState> {
  final ParticipantRemoteDataSource _remoteDataSource;

  ParticipantsListNotifier(this._remoteDataSource)
      : super(const ParticipantsListState()) {
    loadParticipants();
  }

  Future<void> loadParticipants({bool refresh = false}) async {
    if (state.isLoading && !refresh) return;

    state = state.copyWith(isLoading: true, error: null);

    try {
      final data = await _remoteDataSource.getParticipants(
        search: state.searchQuery,
        type: state.selectedTypeFilter,
        sportCode: state.selectedSportFilter,
      );

      final participants =
          data.map((json) => Participant.fromJson(json)).toList();

      state = state.copyWith(
        participants: participants,
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
    loadParticipants(refresh: true);
  }

  void setTypeFilter(String? type) {
    state = state.copyWith(selectedTypeFilter: type);
    loadParticipants(refresh: true);
  }

  void setSportFilter(String? sportCode) {
    state = state.copyWith(selectedSportFilter: sportCode);
    loadParticipants(refresh: true);
  }

  void clearFilters() {
    state = ParticipantsListState(searchQuery: state.searchQuery);
    loadParticipants(refresh: true);
  }
}

/// Provider for participants list
final participantsListProvider =
    StateNotifierProvider<ParticipantsListNotifier, ParticipantsListState>(
        (ref) {
  final remoteDataSource = ref.watch(participantRemoteDataSourceProvider);
  return ParticipantsListNotifier(remoteDataSource);
});

/// Provider for participant search
final participantSearchProvider =
    FutureProvider.family<List<Participant>, String>(
  (ref, query) async {
    final remoteDataSource = ref.watch(participantRemoteDataSourceProvider);

    final data = await remoteDataSource.getParticipants(search: query);
    return data.map((json) => Participant.fromJson(json)).toList();
  },
);
