import 'dart:io';
import 'package:drift/drift.dart';
import 'package:drift/native.dart';
import 'package:path_provider/path_provider.dart';
import 'package:path/path.dart' as p;

part 'database.g.dart';

/// Offline event table for queued operations
class OfflineEvents extends Table {
  TextColumn get id => text()();
  TextColumn get type => text()();
  TextColumn get endpoint => text()();
  TextColumn get method => text()();
  TextColumn get data => text()(); // JSON string
  DateTimeColumn get createdAt => dateTime()();
  IntColumn get sequenceNumber => integer()();
  TextColumn get status => text().withDefault(const Constant('pending'))();
  TextColumn get errorMessage => text().nullable()();
  IntColumn get retryCount => integer().withDefault(const Constant(0))();

  @override
  Set<Column> get primaryKey => {id};
}

/// Cached contest data
class CachedContests extends Table {
  TextColumn get id => text()();
  TextColumn get tenantId => text()();
  TextColumn get competitionId => text()();
  TextColumn get name => text()();
  TextColumn get status => text()();
  TextColumn get sportCode => text()();
  TextColumn get scheduledAt => text().nullable()();
  TextColumn get homeParticipantId => text().nullable()();
  TextColumn get homeParticipantName => text().nullable()();
  TextColumn get awayParticipantId => text().nullable()();
  TextColumn get awayParticipantName => text().nullable()();
  IntColumn get homeScore => integer().nullable()();
  IntColumn get awayScore => integer().nullable()();
  DateTimeColumn get cachedAt => dateTime()();

  @override
  Set<Column> get primaryKey => {id};
}

/// Cached participant data
class CachedParticipants extends Table {
  TextColumn get id => text()();
  TextColumn get tenantId => text()();
  TextColumn get name => text()();
  TextColumn get type => text()(); // athlete, team
  TextColumn get sportCode => text().nullable()();
  TextColumn get avatarUrl => text().nullable()();
  TextColumn get metadata => text().nullable()(); // JSON string
  DateTimeColumn get cachedAt => dateTime()();

  @override
  Set<Column> get primaryKey => {id};
}

/// Cached competition data
class CachedCompetitions extends Table {
  TextColumn get id => text()();
  TextColumn get tenantId => text()();
  TextColumn get name => text()();
  TextColumn get sportCode => text()();
  TextColumn get status => text()();
  TextColumn get format => text().nullable()();
  DateTimeColumn get cachedAt => dateTime()();

  @override
  Set<Column> get primaryKey => {id};
}

/// Tenant-scoped JSON snapshots used by read-only supporter screens.
class CachedSupporterResponses extends Table {
  TextColumn get key => text()();
  TextColumn get tenantId => text()();
  TextColumn get kind => text()();
  TextColumn get data => text()();
  DateTimeColumn get cachedAt => dateTime()();

  @override
  Set<Column> get primaryKey => {key};
}

/// Scoring session for offline scoring
class ScoringSessions extends Table {
  TextColumn get id => text()();
  TextColumn get contestId => text()();
  TextColumn get userId => text()();
  TextColumn get tenantId => text()();
  TextColumn get sportCode => text()();
  TextColumn get status => text().withDefault(const Constant('active'))();
  DateTimeColumn get startedAt => dateTime()();
  DateTimeColumn get lastUpdatedAt => dateTime()();
  TextColumn get currentState => text()(); // JSON string with current scores
  IntColumn get syncedEventsCount => integer().withDefault(const Constant(0))();

  @override
  Set<Column> get primaryKey => {id};
}

/// User session data
class UserSessions extends Table {
  TextColumn get id => text()();
  TextColumn get userId => text()();
  TextColumn get tenantId => text()();
  TextColumn get email => text()();
  TextColumn get displayName => text()();
  TextColumn get role => text()();
  TextColumn get accessToken => text()();
  TextColumn get refreshToken => text().nullable()();
  DateTimeColumn get expiresAt => dateTime()();
  DateTimeColumn get createdAt => dateTime()();

  @override
  Set<Column> get primaryKey => {id};
}

@DriftDatabase(tables: [
  OfflineEvents,
  CachedContests,
  CachedParticipants,
  CachedCompetitions,
  CachedSupporterResponses,
  ScoringSessions,
  UserSessions,
])
class AppDatabase extends _$AppDatabase {
  AppDatabase() : super(_openConnection());
  AppDatabase.forTesting(super.executor);

  @override
  int get schemaVersion => 2;

  @override
  MigrationStrategy get migration => MigrationStrategy(
        onCreate: (migrator) => migrator.createAll(),
        onUpgrade: (migrator, from, to) async {
          if (from < 2) {
            await migrator.createTable(cachedSupporterResponses);
          }
        },
      );

  // Offline Events operations
  Future<List<OfflineEvent>> getPendingEvents() {
    return (select(offlineEvents)
          ..where((e) =>
              e.status.isIn(const ['PENDING', 'SYNCING', 'pending', 'syncing']))
          ..orderBy([(e) => OrderingTerm.asc(e.sequenceNumber)]))
        .get();
  }

  Future<List<OfflineEvent>> getOfflineEvents() {
    return (select(offlineEvents)
          ..orderBy([(e) => OrderingTerm.asc(e.sequenceNumber)]))
        .get();
  }

  Future<List<OfflineEvent>> getReviewableOfflineEvents() {
    return (select(offlineEvents)
          ..where((e) => e.status.isIn(const ['REJECTED', 'CONFLICT']))
          ..orderBy([(e) => OrderingTerm.asc(e.sequenceNumber)]))
        .get();
  }

  Future<int> insertOfflineEvent(OfflineEventsCompanion event) {
    return into(offlineEvents).insert(
      event,
      mode: InsertMode.insertOrIgnore,
    );
  }

  Future<bool> updateOfflineEvent(OfflineEventsCompanion event) {
    return update(offlineEvents).replace(event);
  }

  Future<int> deleteOfflineEvent(String id) {
    return (delete(offlineEvents)..where((e) => e.id.equals(id))).go();
  }

  Future<int> clearSyncedEvents() {
    return (delete(offlineEvents)
          ..where((e) => e.status.isIn(const ['SYNCED', 'SUPERSEDED'])))
        .go();
  }

  // Cached Contests operations
  Future<List<CachedContest>> getCachedContests(String tenantId) {
    return (select(cachedContests)
          ..where((c) => c.tenantId.equals(tenantId))
          ..orderBy([(c) => OrderingTerm.desc(c.cachedAt)]))
        .get();
  }

  Future<CachedContest?> getCachedContest(String id) {
    return (select(cachedContests)..where((c) => c.id.equals(id)))
        .getSingleOrNull();
  }

  Future<void> upsertCachedContest(CachedContestsCompanion contest) {
    return into(cachedContests).insertOnConflictUpdate(contest);
  }

  Future<int> clearCachedContests(String tenantId) {
    return (delete(cachedContests)..where((c) => c.tenantId.equals(tenantId)))
        .go();
  }

  Future<CachedSupporterResponse?> getCachedSupporterResponse(String key) {
    return (select(cachedSupporterResponses)
          ..where((item) => item.key.equals(key)))
        .getSingleOrNull();
  }

  Future<void> upsertCachedSupporterResponse(
    CachedSupporterResponsesCompanion response,
  ) {
    return into(cachedSupporterResponses).insertOnConflictUpdate(response);
  }

  Future<int> clearCachedSupporterResponses(String tenantId) {
    return (delete(cachedSupporterResponses)
          ..where((item) => item.tenantId.equals(tenantId)))
        .go();
  }

  // Scoring Sessions operations
  Future<ScoringSession?> getActiveSession(String contestId) {
    return (select(scoringSessions)
          ..where(
              (s) => s.contestId.equals(contestId) & s.status.equals('active')))
        .getSingleOrNull();
  }

  Future<List<ScoringSession>> getActiveSessions() {
    return (select(scoringSessions)..where((s) => s.status.equals('active')))
        .get();
  }

  Future<void> upsertScoringSession(ScoringSessionsCompanion session) {
    return into(scoringSessions).insertOnConflictUpdate(session);
  }

  Future<int> closeScoringSession(String id) {
    return (update(scoringSessions)..where((s) => s.id.equals(id)))
        .write(const ScoringSessionsCompanion(status: Value('closed')));
  }

  // User Sessions operations
  Future<UserSession?> getCurrentUserSession() {
    return (select(userSessions)..limit(1)).getSingleOrNull();
  }

  Future<void> saveUserSession(UserSessionsCompanion session) {
    return into(userSessions).insertOnConflictUpdate(session);
  }

  Future<int> clearUserSessions() {
    return delete(userSessions).go();
  }
}

LazyDatabase _openConnection() {
  return LazyDatabase(() async {
    final dbFolder = await getApplicationDocumentsDirectory();
    final file = File(p.join(dbFolder.path, 'sports_platform.db'));
    return NativeDatabase.createInBackground(file);
  });
}
