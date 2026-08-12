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
  });

  factory Competition.fromJson(Map<String, dynamic> json) {
    final sportId = json['sportId']?.toString().toLowerCase() ?? '';
    return Competition(
      id: json['id']?.toString() ?? '',
      name: json['name']?.toString() ?? '',
      sportCode:
          json['sportCode']?.toString() ?? _sportCodes[sportId] ?? 'sport',
      status: json['status']?.toString() ?? 'Draft',
      format: json['format'],
      tenantId: json['tenantId'],
      description: json['description'],
      venue: json['venue'],
      startDate: json['startDate'] != null
          ? DateTime.tryParse(json['startDate'])
          : null,
      endDate:
          json['endDate'] != null ? DateTime.tryParse(json['endDate']) : null,
      participantCount: json['participantCount'] ??
          (json['participants'] is List
              ? (json['participants'] as List).length
              : 0),
      contestCount: json['contestCount'] ??
          (json['statistics'] is Map
              ? (json['statistics'] as Map)['contestCount'] ?? 0
              : 0),
      availableTabs: json['availableTabs'] is List
          ? (json['availableTabs'] as List)
              .map((item) => item.toString().toLowerCase())
              .toList()
          : const ['overview'],
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
