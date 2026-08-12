import 'dart:async';
import 'dart:convert';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:dio/dio.dart';
import 'package:drift/drift.dart';
import 'package:uuid/uuid.dart';

import '../database/database.dart';
import 'api_client.dart';

abstract final class OfflineEventStatus {
  static const pending = 'PENDING';
  static const syncing = 'SYNCING';
  static const synced = 'SYNCED';
  static const rejected = 'REJECTED';
  static const conflict = 'CONFLICT';
  static const superseded = 'SUPERSEDED';
}

/// Durable, ordered replay queue for scorer actions.
class OfflineSyncService {
  final ApiClient _apiClient;
  final AppDatabase _database;
  final Connectivity _connectivity;
  final Future<bool> Function()? _connectivityCheck;

  StreamSubscription<List<ConnectivityResult>>? _connectivitySubscription;
  Timer? _syncTimer;
  bool _isSyncing = false;

  final StreamController<SyncStatus> _syncStatusController =
      StreamController<SyncStatus>.broadcast();
  Stream<SyncStatus> get syncStatus => _syncStatusController.stream;

  OfflineSyncService(
    this._apiClient,
    this._database,
    this._connectivity, {
    bool autoStart = true,
    Future<bool> Function()? connectivityCheck,
  }) : _connectivityCheck = connectivityCheck {
    if (autoStart) _initConnectivityListener();
  }

  void _initConnectivityListener() {
    _connectivitySubscription =
        _connectivity.onConnectivityChanged.listen((results) {
      if (results.any((result) => result != ConnectivityResult.none)) {
        unawaited(syncPendingEvents());
      }
    });
    _syncTimer = Timer.periodic(
      const Duration(minutes: 1),
      (_) => unawaited(syncPendingEvents()),
    );
  }

  Future<bool> _isOnline() async {
    if (_connectivityCheck != null) return _connectivityCheck();
    final results = await _connectivity.checkConnectivity();
    return results.any((result) => result != ConnectivityResult.none);
  }

  /// Stores the exact API request. Reusing [id] makes local queuing idempotent.
  Future<String> queueEvent({
    String? id,
    required String type,
    required String endpoint,
    required String method,
    required Map<String, dynamic> data,
    bool syncImmediately = true,
  }) async {
    final queueId = id ?? const Uuid().v4();
    final events = await _database.getOfflineEvents();
    final sequenceNumber = events.isEmpty
        ? 1
        : events
                .map((event) => event.sequenceNumber)
                .reduce((left, right) => left > right ? left : right) +
            1;

    await _database.insertOfflineEvent(OfflineEventsCompanion.insert(
      id: queueId,
      type: type,
      endpoint: endpoint,
      method: method,
      data: jsonEncode(data),
      createdAt: DateTime.now().toUtc(),
      sequenceNumber: sequenceNumber,
      status: const Value(OfflineEventStatus.pending),
    ));
    await _emitStatus();

    if (syncImmediately) unawaited(syncPendingEvents());
    return queueId;
  }

  /// Replays events in sequence. A transient failure stops the pass so later
  /// events can never overtake an earlier event.
  Future<void> syncPendingEvents() async {
    if (_isSyncing || !await _isOnline()) return;
    _isSyncing = true;
    await _emitStatus(isSyncing: true);

    try {
      final pendingEvents = await _database.getPendingEvents();
      for (final event in pendingEvents) {
        await _replaceEvent(
          event,
          status: OfflineEventStatus.syncing,
          errorMessage: null,
        );
        try {
          await _sendEvent(event);
          await _replaceEvent(
            event,
            status: OfflineEventStatus.synced,
            errorMessage: null,
          );
        } on DioException catch (error) {
          final statusCode = error.response?.statusCode;
          if (statusCode == 409) {
            await _replaceEvent(
              event,
              status: OfflineEventStatus.conflict,
              errorMessage: _errorMessage(error),
              incrementRetry: true,
            );
            continue;
          }
          if (statusCode != null &&
              (statusCode == 400 || statusCode == 403 || statusCode == 422)) {
            await _replaceEvent(
              event,
              status: OfflineEventStatus.rejected,
              errorMessage: _errorMessage(error),
              incrementRetry: true,
            );
            continue;
          }
          await _replaceEvent(
            event,
            status: OfflineEventStatus.pending,
            errorMessage: _errorMessage(error),
            incrementRetry: true,
          );
          break;
        } catch (error) {
          await _replaceEvent(
            event,
            status: OfflineEventStatus.pending,
            errorMessage: error.toString(),
            incrementRetry: true,
          );
          break;
        }
      }
    } finally {
      _isSyncing = false;
      await _emitStatus(lastSyncTime: DateTime.now().toUtc());
    }
  }

  Future<void> _sendEvent(OfflineEvent event) async {
    final response = await _apiClient.request(
      endpoint: event.endpoint,
      method: event.method,
      data: jsonDecode(event.data),
    );
    final status = response.statusCode ?? 500;
    if (status < 200 || status >= 300) {
      throw DioException(
        requestOptions: response.requestOptions,
        response: response,
        type: DioExceptionType.badResponse,
      );
    }
  }

  Future<void> _replaceEvent(
    OfflineEvent event, {
    required String status,
    required String? errorMessage,
    bool incrementRetry = false,
  }) {
    return _database.updateOfflineEvent(OfflineEventsCompanion(
      id: Value(event.id),
      type: Value(event.type),
      endpoint: Value(event.endpoint),
      method: Value(event.method),
      data: Value(event.data),
      createdAt: Value(event.createdAt),
      sequenceNumber: Value(event.sequenceNumber),
      status: Value(status),
      errorMessage: Value(errorMessage),
      retryCount: Value(event.retryCount + (incrementRetry ? 1 : 0)),
    ));
  }

  String _errorMessage(DioException error) {
    final data = error.response?.data;
    if (data is Map) {
      return (data['message'] ?? data['error'] ?? error.message).toString();
    }
    return error.message ?? 'Synchronization failed';
  }

  Future<List<OfflineEvent>> getReviewableEvents() =>
      _database.getReviewableOfflineEvents();

  Future<void> supersedeEvent(String id) async {
    final events = await _database.getOfflineEvents();
    final event = events.where((item) => item.id == id).firstOrNull;
    if (event == null) return;
    await _replaceEvent(
      event,
      status: OfflineEventStatus.superseded,
      errorMessage: event.errorMessage,
    );
    await _emitStatus();
  }

  Future<int> getPendingCount() async =>
      (await _database.getPendingEvents()).length;

  Future<void> clearCompletedEvents() async {
    await _database.clearSyncedEvents();
    await _emitStatus();
  }

  Future<void> _emitStatus({
    bool isSyncing = false,
    DateTime? lastSyncTime,
  }) async {
    if (_syncStatusController.isClosed) return;
    _syncStatusController.add(SyncStatus(
      pendingCount: await getPendingCount(),
      reviewCount: (await getReviewableEvents()).length,
      lastSyncTime: lastSyncTime,
      isSyncing: isSyncing,
    ));
  }

  void dispose() {
    _connectivitySubscription?.cancel();
    _syncTimer?.cancel();
    _syncStatusController.close();
  }
}

class SyncStatus {
  final int pendingCount;
  final int reviewCount;
  final DateTime? lastSyncTime;
  final bool isSyncing;

  const SyncStatus({
    required this.pendingCount,
    this.reviewCount = 0,
    this.lastSyncTime,
    required this.isSyncing,
  });
}
