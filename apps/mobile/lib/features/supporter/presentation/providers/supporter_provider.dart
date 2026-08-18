import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/database/database_provider.dart';
import '../../../../core/services/api_client.dart';
import '../../../../core/services/signalr_service.dart';
import '../../../tenant/presentation/providers/tenant_provider.dart';
import '../../data/supporter_remote_datasource.dart';
import '../../data/supporter_repository.dart';
import '../../domain/supporter_models.dart';
import '../../data/supporter_preferences.dart';
import '../../../contests/presentation/providers/contest_provider.dart';

final supporterApiClientProvider = Provider<ApiClient>((ref) => ApiClient());

final supporterRepositoryProvider = Provider<SupporterRepository>((ref) {
  ref.watch(tenantStateProvider.select((state) => state.selectedTenant?.slug));
  return SupporterRepository(
    SupporterRemoteDataSource(ref.watch(supporterApiClientProvider)),
    ref.watch(appDatabaseProvider),
  );
});

final supporterFeedProvider =
    AsyncNotifierProvider<SupporterFeedNotifier, SupporterFeed>(
  SupporterFeedNotifier.new,
);

class SupporterFeedNotifier extends AsyncNotifier<SupporterFeed> {
  StreamSubscription? _patchSubscription;
  final Map<String, int> _versions = {};

  @override
  Future<SupporterFeed> build() async {
    final lowData = ref.watch(
      supporterPreferencesProvider.select((state) => state.lowDataMode),
    );
    ref.onDispose(() => _patchSubscription?.cancel());
    final feed = await ref
        .watch(supporterRepositoryProvider)
        .getFeed(preferCache: lowData);
    final live = ref.read(signalRServiceProvider);
    _patchSubscription ??= live.livePatches.listen(_applyPatch);
    for (final fixture in feed.fixtures) {
      unawaited(live.subscribeToContest(fixture.id));
    }
    return feed;
  }

  Future<void> refresh() async {
    state = const AsyncLoading<SupporterFeed>().copyWithPrevious(state);
    state = await AsyncValue.guard(
      () => ref.read(supporterRepositoryProvider).getFeed(forceRefresh: true),
    );
  }

  void _applyPatch(ContestLivePatch value) {
    if (value.contestId.isEmpty) return;
    if ((_versions[value.contestId] ?? -1) > value.version) return;
    _versions[value.contestId] = value.version;
    final current = state.valueOrNull;
    if (current == null) return;
    final scores = <String, ({num? score, String? display})>{
      for (final item in value.participants)
        item.participantId: (
          score: num.tryParse(item.score ?? ''),
          display: item.scoreDisplay ?? item.score,
        ),
    };
    state = AsyncData(SupporterFeed(
      fixtures: current.fixtures
          .map((fixture) => fixture.id == value.contestId
              ? fixture.applyLivePatch(
                  nextStatus: value.status,
                  summary: value.summary,
                  scores: scores)
              : fixture)
          .toList(),
      results: current.results,
      standings: current.standings,
      isFromCache: false,
      cachedAt: current.cachedAt,
    ));
  }
}

final supporterFixtureProvider =
    Provider.family<SupporterFixture?, String>((ref, fixtureId) {
  return ref.watch(supporterFeedProvider.select((feed) {
    for (final fixture
        in feed.valueOrNull?.fixtures ?? const <SupporterFixture>[]) {
      if (fixture.id == fixtureId) return fixture;
    }
    return null;
  }));
});

final supporterBracketProvider =
    FutureProvider.family<SupporterBracket, String>((ref, competitionId) {
  final lowData = ref.watch(
    supporterPreferencesProvider.select((state) => state.lowDataMode),
  );
  return ref
      .watch(supporterRepositoryProvider)
      .getBracket(competitionId, preferCache: lowData);
});

final golfLeaderboardProvider =
    FutureProvider.family<GolfLeaderboard, String>((ref, competitionId) {
  final lowData = ref.watch(
    supporterPreferencesProvider.select((state) => state.lowDataMode),
  );
  return ref
      .watch(supporterRepositoryProvider)
      .getGolfLeaderboard(competitionId, preferCache: lowData);
});

final fightCardProvider =
    FutureProvider.family<FightCard, String>((ref, competitionId) {
  final lowData = ref.watch(
    supporterPreferencesProvider.select((state) => state.lowDataMode),
  );
  return ref
      .watch(supporterRepositoryProvider)
      .getFightCard(competitionId, preferCache: lowData);
});
