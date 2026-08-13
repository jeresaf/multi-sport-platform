import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sports_platform_mobile/features/supporter/domain/supporter_models.dart';
import 'package:sports_platform_mobile/features/supporter/presentation/widgets/supporter_contest_card.dart';

void main() {
  test('fixture parses backend-authoritative rugby presentation metadata', () {
    final fixture = SupporterFixture.fromJson(_rugbyFixtureJson());

    expect(fixture.presentation.scoreVariant, 'RUGBY_15');
    expect(fixture.presentation.periodLabel, '2H');
    expect(fixture.presentation.clock, "63'");
    expect(fixture.participantDetails.first.result, '24');
    expect(fixture.presentation.eventSummary, hasLength(2));
  });

  testWidgets('rugby adapter exposes variant, stage, clock and scores',
      (tester) async {
    final fixture = SupporterFixture.fromJson(_rugbyFixtureJson());
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: SupporterContestCard(
          fixture: fixture,
          isFavourite: false,
          onTap: () {},
          onFavourite: () {},
          onShare: () {},
        ),
      ),
    ));

    expect(find.text('RUGBY 15S'), findsOneWidget);
    expect(find.text('ROUND 5'), findsOneWidget);
    expect(find.text("2H · 63'"), findsOneWidget);
    expect(find.text('Kobs RFC'), findsOneWidget);
    expect(find.text('24'), findsOneWidget);
    expect(find.text('Tries 3–2 · Pens 0–0'), findsOneWidget);
  });
}

Map<String, dynamic> _rugbyFixtureJson() => {
      'id': 'rugby-live',
      'competitionId': 'rugby-premiership',
      'competitionName': 'Uganda Rugby Premiership',
      'sportCode': 'RUGBY',
      'sportName': 'Rugby Union (7s & 15s)',
      'contestShape': 'TeamVsTeam',
      'renderer': 'head-to-head',
      'name': 'Kobs RFC vs Heathens RFC',
      'status': 'InProgress',
      'presentation': {
        'scoreVariant': 'RUGBY_15',
        'stageLabel': 'Round 5',
        'periodLabel': '2H',
        'clock': "63'",
        'eventSummary': [
          {
            'participantId': 'kobs',
            'eventType': 'TRY',
            'count': 3,
          },
          {
            'participantId': 'heathens',
            'eventType': 'TRY',
            'count': 2,
          },
        ],
      },
      'participants': [
        {'participantId': 'kobs', 'name': 'Kobs RFC', 'score': 24},
        {'participantId': 'heathens', 'name': 'Heathens RFC', 'score': 19},
      ],
    };
