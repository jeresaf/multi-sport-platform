import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:sports_platform_mobile/core/config/app_config.dart';
import 'package:sports_platform_mobile/core/database/database.dart';
import 'package:sports_platform_mobile/core/services/api_client.dart';
import 'package:sports_platform_mobile/core/services/connectivity_service.dart';
import 'package:sports_platform_mobile/core/services/offline_sync_service.dart';
import 'package:sports_platform_mobile/core/services/scoring_service.dart';
import 'package:sports_platform_mobile/core/services/signalr_service.dart';
import 'package:sports_platform_mobile/features/auth/data/datasources/auth_local_datasource.dart';
import 'package:sports_platform_mobile/features/auth/domain/repositories/auth_repository.dart';
import 'package:sports_platform_mobile/features/auth/presentation/providers/auth_provider.dart'
    as auth;
import 'package:sports_platform_mobile/features/contests/data/datasources/contest_remote_datasource.dart';
import 'package:sports_platform_mobile/features/contests/data/repositories/contest_repository_impl.dart';
import 'package:sports_platform_mobile/features/contests/presentation/providers/contest_provider.dart'
    as contests;
import 'package:sports_platform_mobile/features/scoring/presentation/screens/connected_scoring_screen.dart';
import 'package:uuid/uuid.dart';

const _configuredPhase = String.fromEnvironment(
  'MOBILE_UI_RESTART_PHASE',
  defaultValue: 'auto',
);
const _contestKey = 'mobile_ui_restart_contest_id';
const _contestNameKey = 'mobile_ui_restart_contest_name';
const _footballSportId = '00000000-0000-0000-0000-000000000100';

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets(
    'mobile UI offline contest survives a process restart',
    (tester) async {
      final preferences = await SharedPreferences.getInstance();
      final phase = _configuredPhase == 'auto'
          ? preferences.getString(_contestKey) == null
              ? 'queue'
              : 'reconnect'
          : _configuredPhase;
      expect(
        const ['queue', 'reconnect'],
        contains(phase),
        reason: 'Run through scripts/test-mobile-offline-restart.ps1.',
      );
      await Environment.initialize();

      if (phase == 'queue') {
        await _queueThroughVisibleConsole(tester);
      } else {
        await _reconnectAndVerifyVisibleConsole(tester);
      }
    },
  );
}

Future<void> _queueThroughVisibleConsole(WidgetTester tester) async {
  final api = ApiClient();
  final createdContest = await _createFreshContest(api);
  final scorer = await _login(
    api,
    'scorer@premier.com',
    'Scorer123!',
    'mobile-ui-offline-${const Uuid().v4()}',
  );
  await api.setAccessToken(scorer.accessToken);
  await api.setRefreshToken(scorer.refreshToken);
  final fixture = _ContestFixture(
    contestId: createdContest.contestId,
    name: createdContest.name,
    user: scorer.user,
  );

  final preferences = await SharedPreferences.getInstance();
  await preferences.setString(_contestKey, fixture.contestId);
  await preferences.setString(_contestNameKey, fixture.name);

  final database = AppDatabase();
  await database.delete(database.offlineEvents).go();
  var online = true;
  final connectivity = ConnectivityService(
    checkConnectivity: () async => [
      online ? ConnectivityResult.wifi : ConnectivityResult.none,
    ],
  );
  final sync = OfflineSyncService(
    api,
    database,
    Connectivity(),
    autoStart: false,
    connectivityCheck: () async => online,
  );
  final scoring = ScoringService(
    api,
    offlineSyncService: sync,
    connectivityService: connectivity,
  );

  await _pumpConsole(
    tester,
    fixture: fixture,
    api: api,
    scoring: scoring,
    sync: sync,
  );
  await _waitForText(tester, fixture.name);

  // The contest and controls are loaded online. Every following scorer action
  // is performed through the visible Flutter UI while connectivity is forced
  // offline.
  online = false;

  final checkIn = find.text('Check in').first;
  await tester.ensureVisible(checkIn);
  await tester.tap(checkIn);
  await tester.pumpAndSettle();
  expect(
      find.text('Check-in saved offline and queued in order.'), findsOneWidget);

  final firstGoal = find.text('Goal').first;
  await tester.ensureVisible(firstGoal);
  await tester.tap(firstGoal);
  await tester.pumpAndSettle();
  expect(await database.getPendingEvents(), hasLength(2));
  expect(find.text('1 goals'), findsOneWidget);

  final lastGoal = find.text('Goal').last;
  await tester.ensureVisible(lastGoal);
  await tester.tap(lastGoal);
  await tester.pumpAndSettle();
  expect(await database.getPendingEvents(), hasLength(3));
  expect(find.text('1 goals'), findsNWidgets(2));

  final submitResults = find.text('Submit results');
  await tester.scrollUntilVisible(
    submitResults,
    -300,
    scrollable: find.byType(Scrollable).first,
  );
  await tester.tap(submitResults);
  await tester.pumpAndSettle();
  await tester.tap(find.text('Submit'));
  await tester.pumpAndSettle();

  final pending = await database.getPendingEvents();
  expect(pending, hasLength(4));
  expect(
    pending.map((event) => event.type),
    [
      'participant_check_in',
      'scoring_event',
      'scoring_event',
      'result_submission',
    ],
  );

  // Commit and release native handles so the instrumentation process can
  // report success. The host harness then force-stops the installed Android
  // package before launching phase two against this same database file.
  await tester.pumpWidget(const SizedBox.shrink());
  await tester.pump();
  scoring.dispose();
  sync.dispose();
  connectivity.stopMonitoring();
  await database.close();
}

Future<void> _reconnectAndVerifyVisibleConsole(WidgetTester tester) async {
  final preferences = await SharedPreferences.getInstance();
  final contestId = preferences.getString(_contestKey);
  final contestName = preferences.getString(_contestNameKey);
  expect(contestId, isNotNull);
  expect(contestName, isNotNull);

  final api = ApiClient();
  final scorer = await _login(
    api,
    'scorer@premier.com',
    'Scorer123!',
    'mobile-ui-reconnect-${const Uuid().v4()}',
  );
  await api.setAccessToken(scorer.accessToken);
  await api.setRefreshToken(scorer.refreshToken);

  final database = AppDatabase();
  final pendingBeforeReconnect = await database.getPendingEvents();
  expect(pendingBeforeReconnect, hasLength(4));
  expect(
    pendingBeforeReconnect.map((event) => event.sequenceNumber),
    [1, 2, 3, 4],
  );

  final connectivity = ConnectivityService(
    checkConnectivity: () async => [ConnectivityResult.wifi],
  );
  final sync = OfflineSyncService(
    api,
    database,
    Connectivity(),
    autoStart: false,
    connectivityCheck: () async => true,
  );
  await sync.syncPendingEvents();
  expect(await database.getPendingEvents(), isEmpty);
  expect(
    (await database.getOfflineEvents()).map((event) => event.status),
    everyElement(OfflineEventStatus.synced),
  );

  final contest =
      await api.get<Map<String, dynamic>>('/api/contests/$contestId');
  final contestData = contest.data!;
  final participants = List<Map<String, dynamic>>.from(
    (contestData['participants'] as List).map(
      (item) => Map<String, dynamic>.from(item as Map),
    ),
  );
  expect(
    participants.any((item) =>
        item['status'] == 1 || item['status'].toString() == 'CheckedIn'),
    isTrue,
  );
  final scores = await api.get<List<dynamic>>(
    '/api/livescoring/contests/$contestId/scores',
  );
  expect(scores.data, hasLength(2));
  final results = await api.get<List<dynamic>>(
    '/api/results/contest/$contestId',
  );
  expect(
    results.data!.every((item) {
      final status = Map<String, dynamic>.from(item as Map)['status'];
      return status == 2 || status.toString() == 'Submitted';
    }),
    isTrue,
  );

  // Continue the same isolated journey as an administrator: approve every
  // submitted participant result, then restore the scorer session before the
  // visible console is rebuilt.
  final admin = await _login(
    api,
    'admin@sportsplatform.com',
    'Admin123!',
    'mobile-ui-approval-${const Uuid().v4()}',
  );
  await api.setAccessToken(admin.accessToken);
  for (final item in results.data!) {
    final result = Map<String, dynamic>.from(item as Map);
    final approval = await api.post<Map<String, dynamic>>(
      '/api/results/${result['id']}/approve',
      data: {'approvedBy': admin.user.id},
    );
    expect(
      approval.data!['status'] == 3 ||
          approval.data!['status'].toString() == 'Approved',
      isTrue,
    );
  }
  final approvedResults = await api.get<List<dynamic>>(
    '/api/results/contest/$contestId',
  );
  expect(
    approvedResults.data!.every((item) {
      final status = Map<String, dynamic>.from(item as Map)['status'];
      return status == 3 || status.toString() == 'Approved';
    }),
    isTrue,
  );
  final approvedContest = await api.get<Map<String, dynamic>>(
    '/api/contests/$contestId',
  );
  expect(
    approvedContest.data!['status'] == 9 ||
        approvedContest.data!['status'].toString() == 'Approved',
    isTrue,
  );
  await api.setAccessToken(scorer.accessToken);
  await api.setRefreshToken(scorer.refreshToken);

  final scoring = ScoringService(
    api,
    offlineSyncService: sync,
    connectivityService: connectivity,
  );
  await _pumpConsole(
    tester,
    fixture: _ContestFixture(
      contestId: contestId!,
      name: contestName!,
      user: scorer.user,
    ),
    api: api,
    scoring: scoring,
    sync: sync,
  );
  await _waitForText(tester, contestName);
  expect(find.text('Checked in'), findsOneWidget);
  expect(find.textContaining('goals'), findsNWidgets(2));

  await tester.pumpWidget(const SizedBox.shrink());
  await tester.pump();
  scoring.dispose();
  sync.dispose();
  connectivity.stopMonitoring();
  await database.close();
}

Future<void> _pumpConsole(
  WidgetTester tester, {
  required _ContestFixture fixture,
  required ApiClient api,
  required ScoringService scoring,
  required OfflineSyncService sync,
}) async {
  final repository = ContestRepository(ContestRemoteDataSource(api));
  final signalR = _SilentSignalRService(api);
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        auth.authStateProvider.overrideWith(
          (ref) => auth.AuthNotifier(_StaticAuthRepository(fixture.user)),
        ),
        contests.contestRepositoryProvider.overrideWithValue(repository),
        contests.signalRServiceProvider.overrideWithValue(signalR),
        scoringServiceProvider.overrideWithValue(scoring),
        offlineSyncServiceProvider.overrideWithValue(sync),
      ],
      child: MaterialApp(
        home: ConnectedScoringScreen(
          contestId: fixture.contestId,
          sportCode: 'football',
        ),
      ),
    ),
  );
}

Future<void> _waitForText(
  WidgetTester tester,
  String text, {
  Duration timeout = const Duration(seconds: 20),
}) async {
  final end = DateTime.now().add(timeout);
  while (DateTime.now().isBefore(end)) {
    await tester.pump(const Duration(milliseconds: 250));
    if (find.text(text).evaluate().isNotEmpty) return;
  }
  fail('Timed out waiting for "$text".');
}

Future<_ContestFixture> _createFreshContest(ApiClient api) async {
  final admin = await _login(
    api,
    'admin@sportsplatform.com',
    'Admin123!',
    'mobile-ui-fixture-${const Uuid().v4()}',
  );
  await api.setAccessToken(admin.accessToken);
  final tenants = await api.get<List<dynamic>>('/api/tenants');
  final premier = tenants.data!
      .map((item) => Map<String, dynamic>.from(item as Map))
      .singleWhere((item) => item['slug'] == 'premier-league');
  final tenantId = premier['id'].toString();
  final participants = await api.get<List<dynamic>>(
    '/api/participants',
    queryParameters: {'tenantId': tenantId},
  );
  final participantIds = participants.data!
      .take(2)
      .map((item) => Map<String, dynamic>.from(item as Map)['id'].toString())
      .toList();
  expect(participantIds, hasLength(2));

  final name = 'Mobile Offline ${DateTime.now().millisecondsSinceEpoch}';
  final created = await api.post<Map<String, dynamic>>(
    '/api/contests',
    data: {
      'tenantId': tenantId,
      'sportId': _footballSportId,
      'contestShape': 'HeadToHead',
      'name': name,
    },
  );
  final contestId = created.data!['id'].toString();
  for (final participantId in participantIds) {
    await api.post(
      '/api/contests/$contestId/participants',
      data: {'participantId': participantId, 'participantType': 'Team'},
    );
  }
  return _ContestFixture(
    contestId: contestId,
    name: name,
    user: admin.user,
  );
}

Future<_LoginFixture> _login(
  ApiClient api,
  String email,
  String password,
  String deviceId,
) async {
  final response = await api.post<Map<String, dynamic>>(
    '/api/auth/login',
    data: {
      'email': email,
      'password': password,
      'deviceId': deviceId,
      'deviceName': 'Android UI restart integration',
      'deviceType': 'emulator',
    },
  );
  final data = response.data!;
  return _LoginFixture(
    accessToken: data['accessToken'].toString(),
    refreshToken: data['refreshToken'].toString(),
    user: User.fromJson(Map<String, dynamic>.from(data['user'] as Map)),
  );
}

class _ContestFixture {
  final String contestId;
  final String name;
  final User user;

  const _ContestFixture({
    required this.contestId,
    required this.name,
    required this.user,
  });
}

class _LoginFixture {
  final String accessToken;
  final String refreshToken;
  final User user;

  const _LoginFixture({
    required this.accessToken,
    required this.refreshToken,
    required this.user,
  });
}

class _StaticAuthRepository implements AuthRepository {
  final User user;
  const _StaticAuthRepository(this.user);

  @override
  Future<User?> getCurrentUser() async => user;

  @override
  Future<bool> isAuthenticated() async => true;

  @override
  Future<User?> login(String email, String password) async => user;

  @override
  Future<void> logout() async {}

  @override
  Future<void> refreshToken() async {}

  @override
  Future<User?> register(
    String email,
    String password,
    String displayName,
  ) async =>
      user;

  @override
  Future<void> sendPasswordReset(String email) async {}

  @override
  Future<User> updateProfile(String firstName, String lastName) async => user;
}

class _SilentSignalRService extends SignalRService {
  _SilentSignalRService(super.apiClient);

  @override
  Future<void> connect({String? contestId}) async {}

  @override
  Future<void> subscribeToContest(String contestId) async {}

  @override
  Future<void> unsubscribeFromContest(String contestId) async {}
}
