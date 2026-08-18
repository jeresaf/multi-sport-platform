import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sports_platform_mobile/features/supporter/domain/supporter_models.dart';
import 'package:sports_platform_mobile/features/supporter/presentation/providers/supporter_provider.dart';

void main() {
  test('a score patch only notifies the affected fixture selector', () async {
    final notifier = _TestFeedNotifier();
    final container = ProviderContainer(overrides: [
      supporterFeedProvider.overrideWith(() => notifier),
    ]);
    addTearDown(container.dispose);

    await container.read(supporterFeedProvider.future);
    var firstChanges = 0;
    var secondChanges = 0;
    final firstSubscription = container.listen(
      supporterFixtureProvider('first'),
      (_, __) => firstChanges++,
    );
    final secondSubscription = container.listen(
      supporterFixtureProvider('second'),
      (_, __) => secondChanges++,
    );
    addTearDown(firstSubscription.close);
    addTearDown(secondSubscription.close);

    notifier.patchFirst();
    await Future<void>.delayed(Duration.zero);

    expect(firstChanges, 1);
    expect(secondChanges, 0);
  });
}

class _TestFeedNotifier extends SupporterFeedNotifier {
  static const first = SupporterFixture(
    id: 'first',
    competitionId: 'competition',
    competitionName: 'Competition',
    name: 'First contest',
    sportCode: 'rugby',
    status: 'InProgress',
  );
  static const second = SupporterFixture(
    id: 'second',
    competitionId: 'competition',
    competitionName: 'Competition',
    name: 'Second contest',
    sportCode: 'rugby',
    status: 'Scheduled',
  );

  @override
  Future<SupporterFeed> build() async =>
      const SupporterFeed(fixtures: [first, second]);

  void patchFirst() {
    final current = state.requireValue;
    state = AsyncData(SupporterFeed(
      fixtures: [
        first.applyLivePatch(
          nextStatus: 'InProgress',
          summary: 'Try scored',
          scores: const {},
        ),
        second,
      ],
      results: current.results,
      standings: current.standings,
    ));
  }
}
