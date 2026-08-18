import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sports_platform_mobile/features/supporter/domain/supporter_models.dart';
import 'package:sports_platform_mobile/features/supporter/presentation/providers/supporter_provider.dart';
import 'package:sports_platform_mobile/features/supporter/presentation/screens/fight_card_screen.dart';

void main() {
  testWidgets('fight card groups sections in billing order and renders results',
      (tester) async {
    await tester.binding.setSurfaceSize(const Size(800, 1800));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(ProviderScope(
      overrides: [
        fightCardProvider('competition-1').overrideWith((ref) async => _card()),
      ],
      child: const MaterialApp(
        home: FightCardScreen(competitionId: 'competition-1'),
      ),
    ));
    await tester.pumpAndSettle();

    expect(find.text('Main Event'), findsOneWidget);
    expect(find.text('Co-Main Event'), findsOneWidget);
    expect(find.text('Main Card'), findsOneWidget);
    expect(find.text('Preliminaries'), findsOneWidget);
    expect(find.text('Amina Njoroge — Knockout · R2 1:35'), findsOneWidget);

    final main = tester.getTopLeft(find.text('Main Event')).dy;
    final coMain = tester.getTopLeft(find.text('Co-Main Event')).dy;
    final mainCard = tester.getTopLeft(find.text('Main Card')).dy;
    expect(main, lessThan(coMain));
    expect(coMain, lessThan(mainCard));
  });
}

FightCard _card() => const FightCard(
      id: 'card-1',
      name: 'Africa Fight Night',
      status: 'InProgress',
      boutCount: 4,
      completedBoutCount: 1,
      bouts: [
        FightCardBout(
          id: 'prelim',
          name: 'Preliminary',
          boutNumber: 1,
          cardSection: 'PreliminaryCard',
          weightClass: 'Flyweight',
          scheduledRounds: 3,
          status: 'Scheduled',
        ),
        FightCardBout(
          id: 'main-card',
          name: 'Main card',
          boutNumber: 2,
          cardSection: 'MainCard',
          weightClass: 'Welterweight',
          scheduledRounds: 3,
          status: 'Scheduled',
        ),
        FightCardBout(
          id: 'co-main',
          name: 'Co-main',
          boutNumber: 3,
          cardSection: 'CoMainEvent',
          weightClass: 'Middleweight',
          scheduledRounds: 3,
          status: 'Scheduled',
        ),
        FightCardBout(
          id: 'main',
          name: 'Main event',
          boutNumber: 4,
          cardSection: 'MainEvent',
          weightClass: 'Heavyweight',
          scheduledRounds: 5,
          status: 'Completed',
          winnerId: 'fighter-1',
          winnerName: 'Amina Njoroge',
          resultMethod: 'Knockout',
          resultRound: 2,
          resultTimeSeconds: 95,
          fighters: [
            FightCardFighter(
                participantId: 'fighter-1',
                name: 'Amina Njoroge',
                corner: 'Red'),
            FightCardFighter(
                participantId: 'fighter-2',
                name: 'Zuri Okafor',
                corner: 'Blue'),
          ],
        ),
      ],
    );
