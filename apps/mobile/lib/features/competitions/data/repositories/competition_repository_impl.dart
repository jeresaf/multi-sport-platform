import '../datasources/competition_remote_datasource.dart';

/// Competition model
class Competition {
  final String id;
  final String name;
  final String sportCode;
  final String status;
  final String? format;
  final String? tenantId;
  final String? description;
  final String? venue;
  final DateTime? startDate;
  final DateTime? endDate;
  final int participantCount;
  final int contestCount;
  final List<String> availableTabs;
  final List<CompetitionMedal> medals;

  const Competition({
    required this.id,
    required this.name,
    required this.sportCode,
    required this.status,
    this.format,
    this.tenantId,
    this.description,
    this.venue,
    this.startDate,
    this.endDate,
    this.participantCount = 0,
    this.contestCount = 0,
    this.availableTabs = const ['overview'],
    this.medals = const [],
  });

  factory Competition.fromJson(Map<String, dynamic> json) {
    final sportId = json['sportId']?.toString().toLowerCase() ?? '';
    return Competition(
      id: json['id']?.toString() ?? '',
      name: json['name']?.toString() ?? '',
      sportCode:
          json['sportCode']?.toString() ?? _sportCodes[sportId] ?? 'sport',
      status: json['status']?.toString() ?? 'Draft',
      format: json['format']?.toString(),
      tenantId: json['tenantId']?.toString(),
      description: json['description']?.toString(),
      venue: json['venue']?.toString(),
      startDate: json['startDate'] != null
          ? DateTime.tryParse(json['startDate'])
          : null,
      endDate:
          json['endDate'] != null ? DateTime.tryParse(json['endDate']) : null,
      participantCount: _integer(json['participantCount']) ??
          (json['participants'] is List
              ? (json['participants'] as List).length
              : 0),
      contestCount: _integer(json['contestCount']) ??
          (json['statistics'] is Map
              ? _integer((json['statistics'] as Map)['contestCount']) ?? 0
              : 0),
      availableTabs: json['availableTabs'] is List
          ? (json['availableTabs'] as List)
              .map((item) => item.toString().toLowerCase())
              .toList()
          : const ['overview'],
      medals: json['medals'] is List
          ? (json['medals'] as List)
              .whereType<Map>()
              .map((item) => CompetitionMedal.fromJson(
                    Map<String, dynamic>.from(item),
                  ))
              .toList()
          : const [],
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'name': name,
        'sportCode': sportCode,
        'status': status,
        'format': format,
        'tenantId': tenantId,
        'description': description,
        'venue': venue,
        'startDate': startDate?.toIso8601String(),
        'endDate': endDate?.toIso8601String(),
        'participantCount': participantCount,
        'contestCount': contestCount,
        'availableTabs': availableTabs,
        'medals': medals.map((item) => item.toJson()).toList(),
      };
}

int? _integer(dynamic value) =>
    value is num ? value.toInt() : int.tryParse(value?.toString() ?? '');

class CompetitionMedal {
  final String participantId;
  final String participantName;
  final int gold;
  final int silver;
  final int bronze;
  final int total;

  const CompetitionMedal({
    required this.participantId,
    required this.participantName,
    required this.gold,
    required this.silver,
    required this.bronze,
    required this.total,
  });

  factory CompetitionMedal.fromJson(Map<String, dynamic> json) =>
      CompetitionMedal(
        participantId: json['participantId']?.toString() ?? '',
        participantName: json['participantName']?.toString() ?? 'Participant',
        gold: json['gold'] as int? ?? 0,
        silver: json['silver'] as int? ?? 0,
        bronze: json['bronze'] as int? ?? 0,
        total: json['total'] as int? ?? 0,
      );

  Map<String, dynamic> toJson() => {
        'participantId': participantId,
        'participantName': participantName,
        'gold': gold,
        'silver': silver,
        'bronze': bronze,
        'total': total,
      };
}

/// Repository for competitions
class CompetitionRepository {
  final CompetitionRemoteDataSource _remoteDataSource;

  CompetitionRepository(this._remoteDataSource);

  /// Get all competitions
  Future<List<Competition>> getCompetitions({
    String? search,
    String? sportCode,
    String? status,
    int page = 1,
    int pageSize = 20,
  }) async {
    final data = await _remoteDataSource.getCompetitions(
      search: search,
      sportCode: sportCode,
      status: status,
      page: page,
      pageSize: pageSize,
    );
    return data.map(Competition.fromJson).toList();
  }

  /// Get competition by ID
  Future<Competition?> getCompetition(String id) async {
    final data = await _remoteDataSource.getCompetition(id);
    return data != null ? Competition.fromJson(data) : null;
  }

  /// Get contests for a competition
  Future<List<Map<String, dynamic>>> getCompetitionContests(
    String competitionId, {
    String? status,
  }) async {
    return _remoteDataSource.getCompetitionContests(
      competitionId,
      status: status,
    );
  }
}

const _sportCodes = {
  '00000000-0000-0000-0000-000000000100': 'football',
  '00000000-0000-0000-0000-000000000101': 'basketball',
  '00000000-0000-0000-0000-000000000102': 'golf',
  '00000000-0000-0000-0000-000000000103': 'athletics',
  '00000000-0000-0000-0000-000000000104': 'cricket',
  '00000000-0000-0000-0000-000000000105': 'combat',
  '00000000-0000-0000-0000-000000000106': 'rugby',
};
