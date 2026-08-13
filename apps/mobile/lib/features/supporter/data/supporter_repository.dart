import 'dart:convert';

import '../../../core/config/app_config.dart';
import '../../../core/database/database.dart';
import '../domain/supporter_models.dart';
import 'supporter_remote_datasource.dart';

class SupporterRepository {
  final SupporterRemoteDataSource _remote;
  final AppDatabase _database;

  SupporterRepository(this._remote, this._database);

  Future<SupporterFeed> getFeed({
    bool forceRefresh = false,
    bool preferCache = false,
  }) async {
    final snapshots = await Future.wait([
      _load('fixtures', _remote.getFixtures,
          forceRefresh: forceRefresh, preferCache: preferCache),
      _load('results', _remote.getResults,
          forceRefresh: forceRefresh, preferCache: preferCache),
      _load('standings', _remote.getStandings,
          forceRefresh: forceRefresh, preferCache: preferCache),
    ]);
    final cached = snapshots.where((item) => item.fromCache).toList();
    return SupporterFeed(
      fixtures: snapshots[0].rows.map(SupporterFixture.fromJson).toList(),
      results: snapshots[1].rows.map(SupporterResult.fromJson).toList(),
      standings: snapshots[2].rows.map(SupporterStanding.fromJson).toList(),
      isFromCache: cached.isNotEmpty,
      cachedAt: cached.isEmpty
          ? null
          : cached
              .map((item) => item.cachedAt)
              .reduce((a, b) => a.isBefore(b) ? a : b),
    );
  }

  Future<SupporterBracket> getBracket(
    String competitionId, {
    bool forceRefresh = false,
    bool preferCache = false,
  }) async {
    final snapshot = await _loadObject(
      'bracket:$competitionId',
      () => _remote.getBracket(competitionId),
      forceRefresh: forceRefresh,
      preferCache: preferCache,
    );
    return SupporterBracket.fromJson(
      snapshot.data,
      isFromCache: snapshot.fromCache,
      cachedAt: snapshot.cachedAt,
    );
  }

  Future<GolfLeaderboard> getGolfLeaderboard(
    String competitionId, {
    bool forceRefresh = false,
    bool preferCache = false,
  }) async {
    final snapshot = await _loadObject(
      'golf-leaderboard:$competitionId',
      () => _remote.getGolfLeaderboard(competitionId),
      forceRefresh: forceRefresh,
      preferCache: preferCache,
    );
    return GolfLeaderboard.fromJson(
      snapshot.data,
      isFromCache: snapshot.fromCache,
      cachedAt: snapshot.cachedAt,
    );
  }

  Future<FightCard> getFightCard(
    String competitionId, {
    bool forceRefresh = false,
    bool preferCache = false,
  }) async {
    final snapshot = await _loadObject(
      'fight-card:$competitionId',
      () => _remote.getFightCard(competitionId),
      forceRefresh: forceRefresh,
      preferCache: preferCache,
    );
    return FightCard.fromJson(snapshot.data,
        isFromCache: snapshot.fromCache, cachedAt: snapshot.cachedAt);
  }

  Future<_Snapshot> _load(
    String kind,
    Future<List<Map<String, dynamic>>> Function() remoteLoad, {
    required bool forceRefresh,
    bool preferCache = false,
  }) async {
    final tenant = Environment.tenantSlug;
    final key = '$tenant:$kind';
    final cached = await _database.getCachedSupporterResponse(key);
    if (!forceRefresh &&
        cached != null &&
        (preferCache ||
            DateTime.now().difference(cached.cachedAt) <
                const Duration(minutes: 2))) {
      return _decode(cached, fromCache: true);
    }
    try {
      final rows = await remoteLoad();
      final now = DateTime.now().toUtc();
      await _database.upsertCachedSupporterResponse(
        CachedSupporterResponsesCompanion.insert(
          key: key,
          tenantId: tenant,
          kind: kind,
          data: jsonEncode(rows),
          cachedAt: now,
        ),
      );
      return _Snapshot(rows, false, now);
    } catch (_) {
      if (cached != null) return _decode(cached, fromCache: true);
      rethrow;
    }
  }

  _Snapshot _decode(
    CachedSupporterResponse cached, {
    required bool fromCache,
  }) {
    final decoded = jsonDecode(cached.data);
    final rows = decoded is List
        ? decoded
            .whereType<Map>()
            .map((item) => Map<String, dynamic>.from(item))
            .toList()
        : <Map<String, dynamic>>[];
    return _Snapshot(rows, fromCache, cached.cachedAt);
  }

  Future<_ObjectSnapshot> _loadObject(
    String kind,
    Future<Map<String, dynamic>> Function() remoteLoad, {
    required bool forceRefresh,
    bool preferCache = false,
  }) async {
    final tenant = Environment.tenantSlug;
    final key = '$tenant:$kind';
    final cached = await _database.getCachedSupporterResponse(key);
    if (!forceRefresh &&
        cached != null &&
        (preferCache ||
            DateTime.now().difference(cached.cachedAt) <
                const Duration(minutes: 2))) {
      return _decodeObject(cached, fromCache: true);
    }
    try {
      final data = await remoteLoad();
      final now = DateTime.now().toUtc();
      await _database.upsertCachedSupporterResponse(
        CachedSupporterResponsesCompanion.insert(
          key: key,
          tenantId: tenant,
          kind: kind,
          data: jsonEncode(data),
          cachedAt: now,
        ),
      );
      return _ObjectSnapshot(data, false, now);
    } catch (_) {
      if (cached != null) return _decodeObject(cached, fromCache: true);
      rethrow;
    }
  }

  _ObjectSnapshot _decodeObject(
    CachedSupporterResponse cached, {
    required bool fromCache,
  }) {
    final decoded = jsonDecode(cached.data);
    return _ObjectSnapshot(
      decoded is Map ? Map<String, dynamic>.from(decoded) : const {},
      fromCache,
      cached.cachedAt,
    );
  }
}

class _Snapshot {
  final List<Map<String, dynamic>> rows;
  final bool fromCache;
  final DateTime cachedAt;

  const _Snapshot(this.rows, this.fromCache, this.cachedAt);
}

class _ObjectSnapshot {
  final Map<String, dynamic> data;
  final bool fromCache;
  final DateTime cachedAt;

  const _ObjectSnapshot(this.data, this.fromCache, this.cachedAt);
}
