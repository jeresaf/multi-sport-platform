import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'database.dart';

/// Provider for the app database
final appDatabaseProvider = Provider<AppDatabase>((ref) {
  final database = AppDatabase();

  ref.onDispose(() {
    database.close();
  });

  return database;
});

/// Provider for offline events operations
final offlineEventsProvider = FutureProvider<List<OfflineEvent>>((ref) async {
  final db = ref.watch(appDatabaseProvider);
  return db.getPendingEvents();
});

/// Provider for cached contests
final cachedContestsProvider =
    FutureProvider.family<List<CachedContest>, String>((ref, tenantId) async {
  final db = ref.watch(appDatabaseProvider);
  return db.getCachedContests(tenantId);
});

/// Provider for active scoring sessions
final activeScoringSessionsProvider =
    FutureProvider<List<ScoringSession>>((ref) async {
  final db = ref.watch(appDatabaseProvider);
  return db.getActiveSessions();
});

/// Provider for current user session
final currentUserSessionProvider = FutureProvider<UserSession?>((ref) async {
  final db = ref.watch(appDatabaseProvider);
  return db.getCurrentUserSession();
});

/// Notifier for managing offline events
class OfflineEventsNotifier
    extends StateNotifier<AsyncValue<List<OfflineEvent>>> {
  final AppDatabase _database;

  OfflineEventsNotifier(this._database) : super(const AsyncValue.loading()) {
    _loadEvents();
  }

  Future<void> _loadEvents() async {
    try {
      final events = await _database.getPendingEvents();
      state = AsyncValue.data(events);
    } catch (e, st) {
      state = AsyncValue.error(e, st);
    }
  }

  Future<void> refresh() async {
    await _loadEvents();
  }

  Future<void> deleteEvent(String id) async {
    await _database.deleteOfflineEvent(id);
    await _loadEvents();
  }

  Future<void> clearSynced() async {
    await _database.clearSyncedEvents();
    await _loadEvents();
  }
}

/// Provider for offline events notifier
final offlineEventsNotifierProvider = StateNotifierProvider<
    OfflineEventsNotifier, AsyncValue<List<OfflineEvent>>>((ref) {
  final db = ref.watch(appDatabaseProvider);
  return OfflineEventsNotifier(db);
});
