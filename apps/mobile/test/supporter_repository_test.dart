import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sports_platform_mobile/core/config/app_config.dart';
import 'package:sports_platform_mobile/core/database/database.dart';
import 'package:sports_platform_mobile/core/services/api_client.dart';
import 'package:sports_platform_mobile/features/supporter/data/supporter_remote_datasource.dart';
import 'package:sports_platform_mobile/features/supporter/data/supporter_repository.dart';

void main() {
  late AppDatabase database;

  setUp(() {
    Environment.selectTenant('six-nations');
    database = AppDatabase.forTesting(NativeDatabase.memory());
  });

  tearDown(() => database.close());

  test('supporter responses remain available after a network failure',
      () async {
    final online = SupporterRepository(_WorkingRemote(), database);
    final fresh = await online.getFeed(forceRefresh: true);

    expect(fresh.isFromCache, isFalse);
    expect(fresh.fixtures.single.sportCode, 'rugby');
    expect(fresh.results.single.participantName, 'Lions');
    expect(fresh.standings.single.points, 4);

    final offline = SupporterRepository(_FailingRemote(), database);
    final restored = await offline.getFeed(forceRefresh: true);

    expect(restored.isFromCache, isTrue);
    expect(restored.fixtures.single.competitionName, 'Six Nations');
    expect(restored.results.single.score, '24');
    expect(restored.standings.single.position, 1);
  });

  test('bracket and leaderboard remain available after network failure',
      () async {
    final online = SupporterRepository(_WorkingRemote(), database);
    final bracket =
        await online.getBracket('competition-1', forceRefresh: true);
    final leaderboard = await online.getGolfLeaderboard(
      'competition-1',
      forceRefresh: true,
    );

    expect(bracket.rounds.single.matches.single.winnerName, 'Lions');
    expect(leaderboard.entries.single.toParDisplay, '-4');

    final offline = SupporterRepository(_FailingRemote(), database);
    final cachedBracket =
        await offline.getBracket('competition-1', forceRefresh: true);
    final cachedLeaderboard = await offline.getGolfLeaderboard(
      'competition-1',
      forceRefresh: true,
    );

    expect(cachedBracket.isFromCache, isTrue);
    expect(cachedBracket.championName, 'Lions');
    expect(cachedLeaderboard.isFromCache, isTrue);
    expect(cachedLeaderboard.entries.single.roundScores, [68, 72, 70, 70]);
  });

  test('low-data mode reads stale snapshots without network requests',
      () async {
    final old = DateTime.now().toUtc().subtract(const Duration(days: 1));
    for (final kind in ['fixtures', 'results', 'standings']) {
      await database.upsertCachedSupporterResponse(
        CachedSupporterResponsesCompanion.insert(
          key: 'six-nations:$kind',
          tenantId: 'six-nations',
          kind: kind,
          data: '[]',
          cachedAt: old,
        ),
      );
    }
    final remote = _CountingRemote();
    final feed = await SupporterRepository(remote, database).getFeed(
      preferCache: true,
    );

    expect(feed.isFromCache, isTrue);
    expect(remote.calls, 0);
  });
}

class _WorkingRemote extends SupporterRemoteDataSource {
  _WorkingRemote() : super(ApiClient());

  @override
  Future<List<Map<String, dynamic>>> getFixtures() async => [
        {
          'id': 'fixture-1',
          'competitionId': 'competition-1',
          'competitionName': 'Six Nations',
          'sportId': '00000000-0000-0000-0000-000000000106',
          'name': 'Round 1',
          'status': 'Scheduled',
          'participants': [
            {'name': 'Lions'},
            {'name': 'Leopards'},
          ],
        }
      ];

  @override
  Future<List<Map<String, dynamic>>> getResults() async => [
        {
          'id': 'result-1',
          'contestId': 'fixture-1',
          'contestName': 'Round 1',
          'participantName': 'Lions',
          'rank': 1,
          'score': 24,
          'status': 'Official',
        }
      ];

  @override
  Future<List<Map<String, dynamic>>> getStandings() async => [
        {
          'id': 'standing-1',
          'competitionId': 'competition-1',
          'competitionName': 'Six Nations',
          'participantName': 'Lions',
          'position': 1,
          'played': 1,
          'won': 1,
          'drawn': 0,
          'lost': 0,
          'points': 4,
          'pointDifferential': 8,
        }
      ];

  @override
  Future<Map<String, dynamic>> getBracket(String competitionId) async => {
        'id': 'bracket-1',
        'competitionId': competitionId,
        'name': 'Cup playoffs',
        'status': 'Completed',
        'championName': 'Lions',
        'rounds': [
          {
            'id': 'round-1',
            'roundNumber': 1,
            'name': 'Final',
            'matches': [
              {
                'id': 'match-1',
                'contestId': 'fixture-1',
                'matchNumber': 1,
                'participant1Name': 'Lions',
                'participant2Name': 'Leopards',
                'score1': '24',
                'score2': '16',
                'winnerName': 'Lions',
                'status': 'Completed',
              }
            ],
          }
        ],
      };

  @override
  Future<Map<String, dynamic>> getGolfLeaderboard(
    String competitionId,
  ) async =>
      {
        'id': 'contest-1',
        'name': 'Open',
        'status': 'Completed',
        'parPerRound': 71,
        'courseName': 'Test Links',
        'cutAfterRound': 2,
        'cutAppliedAt': '2026-08-01T12:00:00Z',
        'entries': [
          {
            'participantId': 'golfer-1',
            'name': 'A. Golfer',
            'position': 1,
            'isTied': false,
            'status': 'MadeCut',
            'totalStrokes': 280,
            'toPar': -4,
            'roundsPlayed': 4,
            'roundScores': [68, 72, 70, 70],
          }
        ],
      };
}

class _FailingRemote extends SupporterRemoteDataSource {
  _FailingRemote() : super(ApiClient());

  Never _fail() => throw Exception('offline');

  @override
  Future<List<Map<String, dynamic>>> getFixtures() async => _fail();

  @override
  Future<List<Map<String, dynamic>>> getResults() async => _fail();

  @override
  Future<List<Map<String, dynamic>>> getStandings() async => _fail();

  @override
  Future<Map<String, dynamic>> getBracket(String competitionId) async =>
      _fail();

  @override
  Future<Map<String, dynamic>> getGolfLeaderboard(
    String competitionId,
  ) async =>
      _fail();
}

class _CountingRemote extends SupporterRemoteDataSource {
  _CountingRemote() : super(ApiClient());

  int calls = 0;

  Future<List<Map<String, dynamic>>> _count() async {
    calls++;
    return const [];
  }

  @override
  Future<List<Map<String, dynamic>>> getFixtures() => _count();

  @override
  Future<List<Map<String, dynamic>>> getResults() => _count();

  @override
  Future<List<Map<String, dynamic>>> getStandings() => _count();
}
