import 'dart:io';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:dio/dio.dart';
import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';
import 'package:sports_platform_mobile/core/config/app_config.dart';
import 'package:sports_platform_mobile/core/database/database.dart';
import 'package:sports_platform_mobile/core/services/api_client.dart';
import 'package:sports_platform_mobile/core/services/connectivity_service.dart';
import 'package:sports_platform_mobile/core/services/offline_sync_service.dart';
import 'package:sports_platform_mobile/core/services/scoring_service.dart';
import 'package:uuid/uuid.dart';

const _runStagingDeviceTest = bool.fromEnvironment(
  'RUN_STAGING_DEVICE_TEST',
);

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets(
    'an offline contest survives restart and synchronizes in order',
    (_) async {
      final directory = await getTemporaryDirectory();
      final databaseFile = File(
        p.join(
          directory.path,
          'offline-contest-${DateTime.now().microsecondsSinceEpoch}.sqlite',
        ),
      );
      var isOnline = false;
      final api = _ReplayCapturingApiClient();

      final firstDatabase = AppDatabase.forTesting(
        NativeDatabase(databaseFile),
      );
      final firstSync = OfflineSyncService(
        api,
        firstDatabase,
        Connectivity(),
        autoStart: false,
        connectivityCheck: () async => isOnline,
      );
      final firstConnectivity = ConnectivityService(
        checkConnectivity: () async => [
          isOnline ? ConnectivityResult.wifi : ConnectivityResult.none,
        ],
      );
      final firstScoring = ScoringService(
        api,
        offlineSyncService: firstSync,
        connectivityService: firstConnectivity,
      );

      expect(
        (await firstScoring.checkInParticipant(
          contestId: 'contest-1',
          participantId: 'home',
        ))
            .queuedOffline,
        isTrue,
      );
      expect(
        (await firstScoring.checkInParticipant(
          contestId: 'contest-1',
          participantId: 'away',
        ))
            .queuedOffline,
        isTrue,
      );
      expect(
        (await firstScoring.submitEvent(
          contestId: 'contest-1',
          participantId: 'home',
          metricName: 'score',
          eventId: 'home-goal-1',
          segmentId: 'first-half',
          clockSeconds: 300,
        ))
            .queuedOffline,
        isTrue,
      );
      expect(
        (await firstScoring.submitEvent(
          contestId: 'contest-1',
          participantId: 'away',
          metricName: 'score',
          eventId: 'away-goal-1',
          segmentId: 'second-half',
          clockSeconds: 4200,
        ))
            .queuedOffline,
        isTrue,
      );

      // A retry of the same mobile event cannot create a second queue item.
      await firstScoring.submitEvent(
        contestId: 'contest-1',
        participantId: 'home',
        metricName: 'score',
        eventId: 'home-goal-1',
        segmentId: 'first-half',
        clockSeconds: 300,
      );
      expect(
        (await firstScoring.submitContestResults('contest-1')).queuedOffline,
        isTrue,
      );

      final beforeRestart = await firstDatabase.getOfflineEvents();
      expect(beforeRestart, hasLength(5));
      expect(
        beforeRestart.map((event) => event.type),
        [
          'participant_check_in',
          'participant_check_in',
          'scoring_event',
          'scoring_event',
          'result_submission',
        ],
      );
      expect(
        beforeRestart.map((event) => event.status),
        everyElement(OfflineEventStatus.pending),
      );

      firstScoring.dispose();
      firstConnectivity.stopMonitoring();
      firstSync.dispose();
      await firstDatabase.close();

      // Reopening the same SQLite file represents a terminated and restarted
      // application process.
      final restartedDatabase = AppDatabase.forTesting(
        NativeDatabase(databaseFile),
      );
      final restartedQueue = await restartedDatabase.getPendingEvents();
      expect(restartedQueue, hasLength(5));
      expect(
        restartedQueue.map((event) => event.sequenceNumber),
        [1, 2, 3, 4, 5],
      );

      isOnline = true;
      final restartedSync = OfflineSyncService(
        api,
        restartedDatabase,
        Connectivity(),
        autoStart: false,
        connectivityCheck: () async => isOnline,
      );
      await restartedSync.syncPendingEvents();

      expect(api.endpoints, [
        '/api/checkin/contests/contest-1/participants/home/checkin',
        '/api/checkin/contests/contest-1/participants/away/checkin',
        '/api/livescoring/contests/contest-1/events',
        '/api/livescoring/contests/contest-1/events',
        '/api/results/contest/contest-1/submit',
      ]);
      expect(api.eventIds, ['home-goal-1', 'away-goal-1']);
      expect(
        (await restartedDatabase.getOfflineEvents())
            .map((event) => event.status),
        everyElement(OfflineEventStatus.synced),
      );

      await restartedSync.syncPendingEvents();
      expect(api.endpoints, hasLength(5));

      restartedSync.dispose();
      await restartedDatabase.close();
      if (await databaseFile.exists()) {
        await databaseFile.delete();
      }
    },
  );

  testWidgets(
    'a restarted offline contest synchronizes with the Docker API',
    (_) async {
      await Environment.initialize();
      final api = ApiClient();
      final login = await api.post<Map<String, dynamic>>(
        '/api/auth/login',
        data: {
          'email': 'scorer@premier.com',
          'password': 'Scorer123!',
          'deviceId': 'android-emulator-${const Uuid().v4()}',
          'deviceName': 'Android integration test',
          'deviceType': 'emulator',
        },
      );
      final loginData = login.data!;
      await api.setAccessToken(loginData['accessToken'].toString());
      final user = Map<String, dynamic>.from(loginData['user'] as Map);
      final tenantId = user['tenantId'].toString();

      final contestsResponse = await api.get<List<dynamic>>(
        '/api/contests',
        queryParameters: {'tenantId': tenantId},
      );
      final contest = Map<String, dynamic>.from(
        contestsResponse.data!.first as Map,
      );
      final participants = List<dynamic>.from(
        contest['participants'] as List,
      );
      final segments = List<dynamic>.from(contest['segments'] as List);
      final participant = Map<String, dynamic>.from(participants.first as Map);
      final segment = Map<String, dynamic>.from(segments.first as Map);
      final contestId = contest['id'].toString();
      final participantId = participant['participantId'].toString();

      final directory = await getTemporaryDirectory();
      final databaseFile = File(
        p.join(
          directory.path,
          'staging-offline-${DateTime.now().microsecondsSinceEpoch}.sqlite',
        ),
      );
      final firstDatabase = AppDatabase.forTesting(
        NativeDatabase(databaseFile),
      );
      final firstSync = OfflineSyncService(
        api,
        firstDatabase,
        Connectivity(),
        autoStart: false,
        connectivityCheck: () async => false,
      );
      final offlineConnectivity = ConnectivityService(
        checkConnectivity: () async => [ConnectivityResult.none],
      );
      final scoring = ScoringService(
        api,
        offlineSyncService: firstSync,
        connectivityService: offlineConnectivity,
      );
      await scoring.checkInParticipant(
        contestId: contestId,
        participantId: participantId,
      );
      await scoring.submitEvent(
        contestId: contestId,
        participantId: participantId,
        metricName: 'score',
        eventId: const Uuid().v4(),
        segmentId: segment['id'].toString(),
        clockSeconds: 30,
      );
      await scoring.submitContestResults(contestId);
      expect(await firstSync.getPendingCount(), 3);

      scoring.dispose();
      offlineConnectivity.stopMonitoring();
      firstSync.dispose();
      await firstDatabase.close();

      final restartedDatabase = AppDatabase.forTesting(
        NativeDatabase(databaseFile),
      );
      final restartedSync = OfflineSyncService(
        api,
        restartedDatabase,
        Connectivity(),
        autoStart: false,
        connectivityCheck: () async => true,
      );
      expect(await restartedSync.getPendingCount(), 3);
      await restartedSync.syncPendingEvents();
      expect(await restartedSync.getPendingCount(), 0);
      expect(
        (await restartedDatabase.getOfflineEvents())
            .map((event) => event.status),
        everyElement(OfflineEventStatus.synced),
      );

      final checkIn = await api.get<Map<String, dynamic>>(
        '/api/checkin/contests/$contestId/status',
      );
      expect((checkIn.data!['checkedIn'] as num).toInt(), greaterThan(0));
      final results = await api.get<List<dynamic>>(
        '/api/results/contest/$contestId',
      );
      expect(
        results.data!.map((item) => Map<String, dynamic>.from(item as Map)).any(
              (item) =>
                  item['participantId'].toString() == participantId &&
                  (item['status'] == 2 ||
                      item['status'].toString() == 'Submitted'),
            ),
        isTrue,
      );

      restartedSync.dispose();
      await restartedDatabase.close();
      await api.clearTokens();
      if (await databaseFile.exists()) {
        await databaseFile.delete();
      }
    },
    skip: !_runStagingDeviceTest,
  );
}

class _ReplayCapturingApiClient extends ApiClient {
  final List<String> endpoints = [];
  final List<String> eventIds = [];

  @override
  Future<Response<T>> request<T>({
    required String endpoint,
    required String method,
    dynamic data,
  }) async {
    endpoints.add(endpoint);
    if (data is Map && data['eventId'] != null) {
      eventIds.add(data['eventId'].toString());
    }
    return Response<T>(
      requestOptions: RequestOptions(path: endpoint, method: method),
      statusCode: 200,
    );
  }
}
