import '../datasources/contest_remote_datasource.dart';

const _sportCodes = {
  '00000000-0000-0000-0000-000000000100': 'football',
  '00000000-0000-0000-0000-000000000101': 'basketball',
  '00000000-0000-0000-0000-000000000102': 'golf',
  '00000000-0000-0000-0000-000000000103': 'athletics',
  '00000000-0000-0000-0000-000000000104': 'cricket',
  '00000000-0000-0000-0000-000000000105': 'combat',
  '00000000-0000-0000-0000-000000000106': 'rugby',
};

class Contest {
  final String id;
  final String tenantId;
  final String competitionId;
  final String name;
  final String status;
  final String sportId;
  final String sportCode;
  final DateTime? scheduledAt;
  final String? homeParticipantId;
  final String? homeParticipantName;
  final String? awayParticipantId;
  final String? awayParticipantName;
  final int? homeScore;
  final int? awayScore;
  final String? currentSegment;
  final Map<String, dynamic>? metadata;

  const Contest({
    required this.id,
    this.tenantId = '',
    required this.competitionId,
    required this.name,
    required this.status,
    this.sportId = '',
    required this.sportCode,
    this.scheduledAt,
    this.homeParticipantId,
    this.homeParticipantName,
    this.awayParticipantId,
    this.awayParticipantName,
    this.homeScore,
    this.awayScore,
    this.currentSegment,
    this.metadata,
  });

  factory Contest.fromJson(Map<String, dynamic> json) {
    final participants = json['participants'] is List
        ? (json['participants'] as List)
            .whereType<Map>()
            .map((item) => Map<String, dynamic>.from(item))
            .toList()
        : <Map<String, dynamic>>[];
    final sportId = json['sportId']?.toString().toLowerCase() ?? '';
    return Contest(
      id: json['id']?.toString() ?? '',
      tenantId: json['tenantId']?.toString() ?? '',
      competitionId: json['competitionId']?.toString() ?? '',
      name: json['name']?.toString().trim().isNotEmpty == true
          ? json['name'].toString()
          : json['contestShape']?.toString() ?? 'Contest',
      status: _statusName(json['status']),
      sportId: sportId,
      sportCode: json['sportCode']?.toString().toLowerCase() ??
          _sportCodes[sportId] ??
          'scoring',
      scheduledAt: DateTime.tryParse(
        json['scheduledAt']?.toString() ??
            json['scheduledStart']?.toString() ??
            '',
      ),
      homeParticipantId: json['homeParticipantId']?.toString() ??
          _participantId(participants, 0),
      homeParticipantName: json['homeParticipantName']?.toString(),
      awayParticipantId: json['awayParticipantId']?.toString() ??
          _participantId(participants, 1),
      awayParticipantName: json['awayParticipantName']?.toString(),
      homeScore: _integer(json['homeScore']),
      awayScore: _integer(json['awayScore']),
      currentSegment: json['currentSegment']?.toString(),
      metadata: {
        if (json['metadata'] is Map)
          ...Map<String, dynamic>.from(json['metadata'] as Map),
        'participants': participants,
      },
    );
  }

  static String? _participantId(
    List<Map<String, dynamic>> participants,
    int index,
  ) =>
      participants.length > index
          ? participants[index]['participantId']?.toString()
          : null;

  static int? _integer(dynamic value) =>
      value is num ? value.toInt() : int.tryParse(value?.toString() ?? '');

  static String _statusName(dynamic value) {
    if (value is int) {
      return const ['Scheduled', 'InProgress', 'Completed', 'Cancelled']
              .elementAtOrNull(value) ??
          'Scheduled';
    }
    return value?.toString() ?? 'Scheduled';
  }

  List<Map<String, dynamic>> get participants =>
      (metadata?['participants'] as List?)
          ?.whereType<Map>()
          .map((item) => Map<String, dynamic>.from(item))
          .toList() ??
      const [];

  bool get isLive => status == 'InProgress' || status == 'Live';
  bool get isCompleted => status == 'Completed';
  bool get isScheduled => status == 'Scheduled';
}

class ContestEvent {
  final String id;
  final String contestId;
  final String eventType;
  final String? participantId;
  final String? secondaryParticipantId;
  final Map<String, dynamic>? data;
  final DateTime timestamp;
  final String? clientId;

  const ContestEvent({
    required this.id,
    required this.contestId,
    required this.eventType,
    this.participantId,
    this.secondaryParticipantId,
    this.data,
    required this.timestamp,
    this.clientId,
  });

  factory ContestEvent.fromJson(Map<String, dynamic> json) => ContestEvent(
        id: json['id']?.toString() ?? json['eventId']?.toString() ?? '',
        contestId: json['contestId']?.toString() ?? '',
        eventType: json['eventType']?.toString() ??
            json['metricName']?.toString() ??
            '',
        participantId: json['participantId']?.toString(),
        secondaryParticipantId: json['secondaryParticipantId']?.toString(),
        data: {
          if (json['data'] is Map)
            ...Map<String, dynamic>.from(json['data'] as Map),
          if (json.containsKey('value')) 'value': json['value'],
          if (json.containsKey('segmentId')) 'segmentId': json['segmentId'],
          if (json.containsKey('clockSeconds'))
            'clockSeconds': json['clockSeconds'],
        },
        timestamp: DateTime.tryParse(
              json['timestamp']?.toString() ??
                  json['createdAt']?.toString() ??
                  '',
            ) ??
            DateTime.now().toUtc(),
        clientId: json['clientId']?.toString() ?? json['eventId']?.toString(),
      );
}

class ContestRepository {
  final ContestRemoteDataSource _remoteDataSource;

  ContestRepository(this._remoteDataSource);

  Future<Contest?> getContest(String id) async {
    final data = await _remoteDataSource.getContest(id);
    return data == null ? null : Contest.fromJson(data);
  }

  Future<List<Contest>> getAssignedContests({String? tenantId}) async =>
      (await _remoteDataSource.getAssignedContests(tenantId: tenantId))
          .map(Contest.fromJson)
          .toList();

  Future<List<Contest>> getLiveContests() async =>
      (await _remoteDataSource.getLiveContests())
          .map(Contest.fromJson)
          .toList();

  Future<List<Contest>> getUpcomingContests() async =>
      (await _remoteDataSource.getUpcomingContests())
          .map(Contest.fromJson)
          .toList();

  Future<List<ContestEvent>> getContestEvents(String contestId) async =>
      (await _remoteDataSource.getContestEvents(contestId))
          .map(ContestEvent.fromJson)
          .toList();

  Future<List<Map<String, dynamic>>> getContestParticipants(
    String contestId,
  ) =>
      _remoteDataSource.getContestParticipants(contestId);

  Future<ContestEvent?> submitLiveEvent({
    required String contestId,
    required String eventType,
    required Map<String, dynamic> data,
  }) async {
    final result = await _remoteDataSource.submitLiveEvent(
      contestId: contestId,
      eventType: eventType,
      data: data,
    );
    return result == null ? null : ContestEvent.fromJson(result);
  }

  Future<Map<String, dynamic>?> getContestStats(String contestId) =>
      _remoteDataSource.getContestStats(contestId);
}
