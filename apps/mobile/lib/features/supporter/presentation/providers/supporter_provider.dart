import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/database/database_provider.dart';
import '../../../../core/services/api_client.dart';
import '../../../tenant/presentation/providers/tenant_provider.dart';
import '../../data/supporter_remote_datasource.dart';
import '../../data/supporter_repository.dart';
import '../../domain/supporter_models.dart';
import '../../data/supporter_preferences.dart';

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
  @override
  Future<SupporterFeed> build() {
    final lowData = ref.watch(
      supporterPreferencesProvider.select((state) => state.lowDataMode),
    );
    return ref.watch(supporterRepositoryProvider).getFeed(preferCache: lowData);
  }

  Future<void> refresh() async {
    state = const AsyncLoading();
    state = await AsyncValue.guard(
      () => ref.read(supporterRepositoryProvider).getFeed(forceRefresh: true),
    );
  }
}

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
