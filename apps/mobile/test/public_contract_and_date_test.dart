import 'package:flutter_test/flutter_test.dart';
import 'package:sports_platform_mobile/core/formatters/public_date_time.dart';
import 'package:sports_platform_mobile/core/widgets/contest_status_label.dart';
import 'package:sports_platform_mobile/features/competitions/data/repositories/competition_repository_impl.dart';
import 'package:sports_platform_mobile/features/contests/data/repositories/contest_repository_impl.dart';

void main() {
  test('competition summary parses authoritative count and format fields', () {
    final competition = Competition.fromJson({
      'id': 'competition-1',
      'name': 'National Rugby Championship',
      'sportCode': 'RUGBY',
      'status': 'Published',
      'format': 'GroupAndKnockout',
      'participantCount': 8,
      'contestCount': 16,
      'startDate': '2026-08-13T08:00:00Z',
      'endDate': '2026-08-20T16:00:00Z',
    });

    expect(competition.format, 'GroupAndKnockout');
    expect(competition.participantCount, 8);
    expect(competition.contestCount, 16);
    expect(competition.startDate, isNotNull);
    expect(competition.endDate, isNotNull);
  });

  test('contest time uses clear relative date labels', () {
    final now = DateTime(2026, 8, 13, 10);

    expect(
      formatPublicDateTime(DateTime(2026, 8, 13, 14, 30), now: now),
      'Today · 14:30',
    );
    expect(
      formatPublicDateTime(DateTime(2026, 8, 14, 9), now: now),
      'Tomorrow · 09:00',
    );
    expect(formatPublicDateTime(null, now: now), 'TBC');
  });

  test('public contest contract supplies participants scores and statistics',
      () {
    final contest = Contest.fromJson({
      'id': 'contest-1',
      'name': 'Harbour Lions vs Valley RFC',
      'sportId': '00000000-0000-0000-0000-000000000106',
      'sportCode': 'RUGBY',
      'competitionId': 'competition-1',
      'status': 'Completed',
      'startTime': '2026-08-12T14:30:00Z',
      'participants': [
        {
          'participantId': 'home-1',
          'name': 'Harbour Lions',
          'score': 24,
          'scoreDisplay': '24',
        },
        {
          'participantId': 'away-1',
          'name': 'Valley RFC',
          'score': 19,
          'scoreDisplay': '19',
        },
      ],
      'statistics': {
        'totalEvents': 6,
        'eventBreakdown': [
          {'label': 'try', 'total': 4},
        ],
      },
    });

    expect(contest.competitionId, 'competition-1');
    expect(contest.sportCode, 'rugby');
    expect(contest.homeParticipantName, 'Harbour Lions');
    expect(contest.awayParticipantName, 'Valley RFC');
    expect(contest.homeScoreDisplay, '24');
    expect(contest.awayScoreDisplay, '19');
    expect(contest.scheduledAt, isNotNull);
    expect(contest.metadata?['stats'], isA<Map<String, dynamic>>());
  });

  test('canonical and legacy contest statuses share stable presentation', () {
    expect(contestStatusLabel('NOT_STARTED'), 'Not started');
    expect(contestStatusLabel('STARTING_SOON'), 'Starting soon');
    expect(contestStatusLabel('LIVE', compact: true), 'LIVE');
    expect(contestStatusLabel('INTERRUPTED', compact: true), 'INT.');
    expect(contestStatusLabel('ENDED', compact: true), 'FT');
    expect(contestStatusLabel('CLOSED', compact: true), 'FINAL');
    expect(contestStatusLabel('InProgress'), 'Live');
    expect(contestStatusTone('SUSPENDED'), ContestStatusTone.warning);
    expect(contestStatusTone('ABANDONED'), ContestStatusTone.neutral);
  });
}
