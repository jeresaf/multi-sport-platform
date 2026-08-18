import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sports_platform_mobile/features/competitions/presentation/widgets/competition_output_renderer.dart';
import 'package:sports_platform_mobile/features/supporter/domain/supporter_models.dart';

void main() {
  testWidgets('compact standings expands full mobile row data', (tester) async {
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: CompetitionOutputRenderer(
          kind: CompetitionOutputKind.standings,
          standings: [_standing()],
        ),
      ),
    ));

    expect(find.text('Kampala Rhinos'), findsOneWidget);
    expect(find.text('Won 4'), findsNothing);
    await tester.tap(find.text('Kampala Rhinos'));
    await tester.pumpAndSettle();
    expect(find.text('Won 4'), findsOneWidget);
    expect(find.text('Difference +18'), findsOneWidget);
  });

  testWidgets('mobile bracket selects one vertical round at a time',
      (tester) async {
    await tester.binding.setSurfaceSize(const Size(390, 844));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: CompetitionOutputRenderer(
          kind: CompetitionOutputKind.bracket,
          bracket: _bracket(),
        ),
      ),
    ));

    expect(find.text('Semifinals'), findsWidgets);
    expect(find.text('Kampala Rhinos  24'), findsOneWidget);
    expect(find.text('Final'), findsOneWidget);
    await tester.tap(find.text('Final'));
    await tester.pumpAndSettle();
    expect(find.text('Winner SF1  —'), findsOneWidget);
  });

  testWidgets('mobile golf row expands round, today and thru details',
      (tester) async {
    await tester.binding.setSurfaceSize(const Size(390, 844));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: CompetitionOutputRenderer(
          kind: CompetitionOutputKind.leaderboard,
          leaderboard: _leaderboard(),
        ),
      ),
    ));

    expect(find.text('Amina Njoroge'), findsOneWidget);
    expect(find.text('Today'), findsNothing);
    await tester.tap(find.text('Amina Njoroge'));
    await tester.pumpAndSettle();
    expect(find.text('Today'), findsOneWidget);
    expect(find.text('Thru'), findsOneWidget);
    expect(find.text('R4  70'), findsOneWidget);
  });

  testWidgets('tablet golf table uses compact live columns', (tester) async {
    await tester.binding.setSurfaceSize(const Size(800, 1000));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: CompetitionOutputRenderer(
          kind: CompetitionOutputKind.leaderboard,
          leaderboard: _leaderboard(),
        ),
      ),
    ));

    expect(find.text('Today'), findsOneWidget);
    expect(find.text('Thru'), findsOneWidget);
    expect(find.text('Total'), findsOneWidget);
    expect(find.text('R1'), findsNothing);
  });
}

GolfLeaderboard _leaderboard() => const GolfLeaderboard(
      id: 'golf-1',
      name: 'Uganda Open',
      status: 'InProgress',
      parPerRound: 72,
      cutAfterRound: 2,
      cutApplied: true,
      entries: [
        GolfLeaderboardEntry(
          participantId: 'golfer-1',
          name: 'Amina Njoroge',
          position: 1,
          isTied: false,
          status: 'MadeCut',
          totalStrokes: 278,
          toPar: -10,
          roundsPlayed: 4,
          todayToPar: -2,
          thru: 18,
          roundScores: [68, 70, 70, 70],
        ),
      ],
    );

SupporterStanding _standing() => const SupporterStanding(
      id: 'standing-1',
      competitionId: 'competition-1',
      competitionName: 'Rugby Premiership',
      participantName: 'Kampala Rhinos',
      position: 1,
      played: 5,
      won: 4,
      drawn: 0,
      lost: 1,
      points: 18,
      differential: 18,
    );

SupporterBracket _bracket() => const SupporterBracket(
      id: 'bracket-1',
      competitionId: 'competition-1',
      name: 'Cup',
      status: 'InProgress',
      rounds: [
        SupporterBracketRound(
          id: 'semi',
          number: 1,
          name: 'Semifinals',
          matches: [
            SupporterBracketMatch(
              id: 'semi-1',
              number: 1,
              participant1: 'Kampala Rhinos',
              participant2: 'Jinja Nile',
              score1: '24',
              score2: '19',
              status: 'Completed',
            ),
          ],
        ),
        SupporterBracketRound(
          id: 'final',
          number: 2,
          name: 'Final',
          matches: [
            SupporterBracketMatch(
              id: 'final-1',
              number: 1,
              participant1: 'Winner SF1',
              participant2: 'Winner SF2',
              status: 'Scheduled',
            ),
          ],
        ),
      ],
    );
