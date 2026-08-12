import 'dart:convert';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:dio/dio.dart';
import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sports_platform_mobile/core/database/database.dart';
import 'package:sports_platform_mobile/core/services/api_client.dart';
import 'package:sports_platform_mobile/core/services/offline_sync_service.dart';

void main() {
  late AppDatabase database;
  late _FakeApiClient api;
  late OfflineSyncService service;

  setUp(() {
    database = AppDatabase.forTesting(NativeDatabase.memory());
    api = _FakeApiClient();
    service = OfflineSyncService(
      api,
      database,
      Connectivity(),
      autoStart: false,
      connectivityCheck: () async => true,
    );
  });

  tearDown(() async {
    service.dispose();
    await database.close();
  });

  test('queue is durable, ordered, and locally idempotent', () async {
    await service.queueEvent(
      id: 'event-1',
      type: 'scoring_event',
      endpoint: '/events',
      method: 'POST',
      data: {'eventId': 'event-1'},
      syncImmediately: false,
    );
    await service.queueEvent(
      id: 'event-1',
      type: 'scoring_event',
      endpoint: '/events',
      method: 'POST',
      data: {'eventId': 'event-1'},
      syncImmediately: false,
    );
    await service.queueEvent(
      id: 'event-2',
      type: 'scoring_event',
      endpoint: '/events',
      method: 'POST',
      data: {'eventId': 'event-2'},
      syncImmediately: false,
    );

    final events = await database.getOfflineEvents();
    expect(events.map((event) => event.id), ['event-1', 'event-2']);
    expect(events.map((event) => event.sequenceNumber), [1, 2]);
  });

  test('transient failure prevents later events overtaking the first',
      () async {
    await _queueTwoEvents(service);
    api.failNextWithoutResponse();

    await service.syncPendingEvents();

    var events = await database.getOfflineEvents();
    expect(api.sentEventIds, ['event-1']);
    expect(events.map((event) => event.status),
        [OfflineEventStatus.pending, OfflineEventStatus.pending]);

    await service.syncPendingEvents();

    events = await database.getOfflineEvents();
    expect(api.sentEventIds, ['event-1', 'event-1', 'event-2']);
    expect(events.map((event) => event.status),
        [OfflineEventStatus.synced, OfflineEventStatus.synced]);
  });

  test('rejected and conflicting events remain reviewable', () async {
    await service.queueEvent(
      id: 'rejected',
      type: 'scoring_event',
      endpoint: '/events',
      method: 'POST',
      data: {'eventId': 'rejected'},
      syncImmediately: false,
    );
    await service.queueEvent(
      id: 'conflict',
      type: 'scoring_event',
      endpoint: '/events',
      method: 'POST',
      data: {'eventId': 'conflict'},
      syncImmediately: false,
    );
    api.failNextWithStatus(422);
    api.failNextWithStatus(409);

    await service.syncPendingEvents();

    final review = await service.getReviewableEvents();
    expect(review.map((event) => event.status),
        [OfflineEventStatus.rejected, OfflineEventStatus.conflict]);

    await service.supersedeEvent('rejected');
    final remaining = await service.getReviewableEvents();
    expect(remaining.map((event) => event.id), ['conflict']);
  });
}

Future<void> _queueTwoEvents(OfflineSyncService service) async {
  for (final id in ['event-1', 'event-2']) {
    await service.queueEvent(
      id: id,
      type: 'scoring_event',
      endpoint: '/events',
      method: 'POST',
      data: {'eventId': id},
      syncImmediately: false,
    );
  }
}

class _FakeApiClient extends ApiClient {
  final List<int?> _statuses = [];
  final List<String> sentEventIds = [];

  void failNextWithoutResponse() => _statuses.add(null);
  void failNextWithStatus(int status) => _statuses.add(status);

  @override
  Future<Response<T>> request<T>({
    required String endpoint,
    required String method,
    dynamic data,
  }) async {
    final request = RequestOptions(path: endpoint, method: method);
    final eventData = Map<String, dynamic>.from(data as Map);
    sentEventIds.add(eventData['eventId'].toString());
    if (_statuses.isNotEmpty) {
      final status = _statuses.removeAt(0);
      throw DioException(
        requestOptions: request,
        response: status == null
            ? null
            : Response(
                requestOptions: request,
                statusCode: status,
                data: {'message': 'server decision $status'},
              ),
      );
    }
    return Response<T>(
      requestOptions: request,
      statusCode: 200,
      data: jsonDecode('{}') as T?,
    );
  }
}
