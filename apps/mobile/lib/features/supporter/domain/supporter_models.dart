import 'dart:convert';

class SupporterFixture {
  final String id;
  final String competitionId;
  final String competitionName;
  final String name;
  final String sportCode;
  final String sportName;
  final String renderer;
  final String status;
  final DateTime? startTime;
  final String? venue;
  final List<String> participants;
  final List<SupporterFixtureParticipant> participantDetails;
  final SupporterContestPresentation presentation;

  const SupporterFixture({
    required this.id,
    required this.competitionId,
    required this.competitionName,
    required this.name,
    required this.sportCode,
    this.sportName = 'Sport',
    this.renderer = 'head-to-head',
    required this.status,
    this.startTime,
    this.venue,
    this.participants = const [],
    this.participantDetails = const [],
    this.presentation = const SupporterContestPresentation(),
  });

  factory SupporterFixture.fromJson(Map<String, dynamic> json) {
    final sportId = json['sportId']?.toString().toLowerCase() ?? '';
    final participantDetails = _maps(json['participants'])
        .map(SupporterFixtureParticipant.fromJson)
        .toList();
    return SupporterFixture(
      id: json['id']?.toString() ?? '',
      competitionId: json['competitionId']?.toString() ?? '',
      competitionName: json['competitionName']?.toString() ?? 'Competition',
      name: json['name']?.toString().trim().isNotEmpty == true
          ? json['name'].toString()
          : json['contestShape']?.toString() ?? 'Fixture',
      sportCode: json['sportCode']?.toString().toLowerCase() ??
          sportCodes[sportId] ??
          'sport',
      sportName:
          json['sportName']?.toString() ?? sportNames[sportId] ?? 'Sport',
      renderer: json['renderer']?.toString() ?? 'head-to-head',
      status: json['contestStatus']?.toString() ??
          json['status']?.toString() ??
          'NOT_STARTED',
      startTime: DateTime.tryParse(json['startTime']?.toString() ?? ''),
      venue: json['venue']?.toString(),
      participants: participantDetails.map((item) => item.name).toList(),
      participantDetails: participantDetails,
      presentation: SupporterContestPresentation.fromJson(
        json['presentation'] is Map<String, dynamic>
            ? json['presentation'] as Map<String, dynamic>
            : const {},
      ),
    );
  }

  SupporterFixture applyLivePatch(
          {String? nextStatus,
          String? summary,
          required Map<String, ({num? score, String? display})> scores}) =>
      SupporterFixture(
        id: id,
        competitionId: competitionId,
        competitionName: competitionName,
        name: name,
        sportCode: sportCode,
        sportName: sportName,
        renderer: renderer,
        status: nextStatus?.isNotEmpty == true ? nextStatus! : status,
        startTime: startTime,
        venue: venue,
        participants: participants,
        participantDetails: participantDetails.map((item) {
          final patch = scores[item.id];
          return patch == null
              ? item
              : item.copyWith(score: patch.score, scoreDisplay: patch.display);
        }).toList(),
        presentation: summary?.isNotEmpty == true
            ? presentation.copyWith(summary: summary)
            : presentation,
      );
}

class SupporterContestPresentation {
  final String? scoreVariant;
  final String? stageLabel;
  final String? periodLabel;
  final String? clock;
  final String? summary;
  final int? seriesGameNumber;
  final int? seriesBestOf;
  final List<SupporterEventSummary> eventSummary;

  const SupporterContestPresentation({
    this.scoreVariant,
    this.stageLabel,
    this.periodLabel,
    this.clock,
    this.summary,
    this.seriesGameNumber,
    this.seriesBestOf,
    this.eventSummary = const [],
  });

  factory SupporterContestPresentation.fromJson(Map<String, dynamic> json) =>
      SupporterContestPresentation(
        scoreVariant: json['scoreVariant']?.toString(),
        stageLabel: json['stageLabel']?.toString(),
        periodLabel: json['periodLabel']?.toString(),
        clock: json['clock']?.toString(),
        summary: json['summary']?.toString(),
        seriesGameNumber: _integer(json['seriesGameNumber']),
        seriesBestOf: _integer(json['seriesBestOf']),
        eventSummary: _maps(json['eventSummary'])
            .map(SupporterEventSummary.fromJson)
            .toList(),
      );

  SupporterContestPresentation copyWith({String? summary}) =>
      SupporterContestPresentation(
        scoreVariant: scoreVariant,
        stageLabel: stageLabel,
        periodLabel: periodLabel,
        clock: clock,
        summary: summary ?? this.summary,
        seriesGameNumber: seriesGameNumber,
        seriesBestOf: seriesBestOf,
        eventSummary: eventSummary,
      );
}

class SupporterEventSummary {
  final String participantId;
  final String eventType;
  final int count;

  const SupporterEventSummary({
    required this.participantId,
    required this.eventType,
    required this.count,
  });

  factory SupporterEventSummary.fromJson(Map<String, dynamic> json) =>
      SupporterEventSummary(
        participantId: json['participantId']?.toString() ?? '',
        eventType: json['eventType']?.toString() ?? '',
        count: _integer(json['count']) ?? 0,
      );
}

class SupporterFixtureParticipant {
  final String id;
  final String name;
  final num? score;
  final String? scoreDisplay;
  final String? position;
  final int? rank;

  const SupporterFixtureParticipant({
    required this.id,
    required this.name,
    this.score,
    this.scoreDisplay,
    this.position,
    this.rank,
  });

  factory SupporterFixtureParticipant.fromJson(Map<String, dynamic> json) =>
      SupporterFixtureParticipant(
        id: json['participantId']?.toString() ?? '',
        name: json['name']?.toString() ?? 'Participant',
        score: json['score'] is num ? json['score'] as num : null,
        scoreDisplay: json['scoreDisplay']?.toString(),
        position: json['position']?.toString(),
        rank: _integer(json['rank']),
      );

  SupporterFixtureParticipant copyWith({num? score, String? scoreDisplay}) =>
      SupporterFixtureParticipant(
        id: id,
        name: name,
        score: score ?? this.score,
        scoreDisplay: scoreDisplay ?? this.scoreDisplay,
        position: position,
        rank: rank,
      );

  String get result => scoreDisplay?.trim().isNotEmpty == true
      ? scoreDisplay!
      : score?.toString() ?? position ?? (rank == null ? '—' : '#$rank');
}

class SupporterResult {
  final String id;
  final String contestId;
  final String contestName;
  final String participantName;
  final int? rank;
  final String score;
  final String status;
  final DateTime? approvedAt;

  const SupporterResult({
    required this.id,
    required this.contestId,
    required this.contestName,
    required this.participantName,
    this.rank,
    required this.score,
    required this.status,
    this.approvedAt,
  });

  factory SupporterResult.fromJson(Map<String, dynamic> json) =>
      SupporterResult(
        id: json['id']?.toString() ?? '',
        contestId: json['contestId']?.toString() ?? '',
        contestName: json['contestName']?.toString() ?? 'Contest',
        participantName: json['participantName']?.toString() ?? 'Participant',
        rank: _integer(json['rank'] ?? json['position']),
        score: json['scoreDisplay']?.toString().trim().isNotEmpty == true
            ? json['scoreDisplay'].toString()
            : json['score']?.toString() ?? json['time']?.toString() ?? '—',
        status: json['status']?.toString() ?? 'Official',
        approvedAt: DateTime.tryParse(json['approvedAt']?.toString() ?? ''),
      );
}

class SupporterStanding {
  final String id;
  final String competitionId;
  final String competitionName;
  final String? groupName;
  final String participantName;
  final int position;
  final int played;
  final int won;
  final int drawn;
  final int lost;
  final num points;
  final num differential;
  final int? bonusPoints;
  final List<String> form;
  final String? qualificationCode;
  final int pointsDeduction;

  const SupporterStanding({
    required this.id,
    required this.competitionId,
    required this.competitionName,
    this.groupName,
    required this.participantName,
    required this.position,
    required this.played,
    required this.won,
    required this.drawn,
    required this.lost,
    required this.points,
    required this.differential,
    this.bonusPoints,
    this.form = const [],
    this.qualificationCode,
    this.pointsDeduction = 0,
  });

  factory SupporterStanding.fromJson(Map<String, dynamic> json) {
    final metadata = _standingMetadata(json['metadata']);
    return SupporterStanding(
      id: json['id']?.toString() ?? '',
      competitionId: json['competitionId']?.toString() ?? '',
      competitionName: json['competitionName']?.toString() ?? 'Competition',
      groupName: json['groupName']?.toString(),
      participantName: json['participantName']?.toString() ?? 'Participant',
      position: _integer(json['position']) ?? 0,
      played: _integer(json['played']) ?? 0,
      won: _integer(json['won']) ?? 0,
      drawn: _integer(json['drawn']) ?? 0,
      lost: _integer(json['lost']) ?? 0,
      points: json['points'] is num
          ? json['points'] as num
          : num.tryParse(json['points']?.toString() ?? '') ?? 0,
      differential: json['pointDifferential'] is num
          ? json['pointDifferential'] as num
          : num.tryParse(json['pointDifferential']?.toString() ?? '') ?? 0,
      bonusPoints: _integer(metadata['bonusPoints']),
      form: metadata['form'] is List
          ? (metadata['form'] as List).map((item) => item.toString()).toList()
          : metadata['form']
                  ?.toString()
                  .split('')
                  .where((item) => item.isNotEmpty)
                  .toList() ??
              const [],
      qualificationCode:
          metadata['qualificationCode']?.toString().trim().isNotEmpty == true
              ? metadata['qualificationCode'].toString().trim()
              : null,
      pointsDeduction: _integer(metadata['pointsDeduction']) ?? 0,
    );
  }
}

class SupporterFeed {
  final List<SupporterFixture> fixtures;
  final List<SupporterResult> results;
  final List<SupporterStanding> standings;
  final bool isFromCache;
  final DateTime? cachedAt;

  const SupporterFeed({
    this.fixtures = const [],
    this.results = const [],
    this.standings = const [],
    this.isFromCache = false,
    this.cachedAt,
  });
}

class SupporterBracket {
  final String id;
  final String competitionId;
  final String name;
  final String status;
  final String? championName;
  final List<SupporterBracketRound> rounds;
  final bool isFromCache;
  final DateTime? cachedAt;

  const SupporterBracket({
    required this.id,
    required this.competitionId,
    required this.name,
    required this.status,
    this.championName,
    this.rounds = const [],
    this.isFromCache = false,
    this.cachedAt,
  });

  factory SupporterBracket.fromJson(
    Map<String, dynamic> json, {
    bool isFromCache = false,
    DateTime? cachedAt,
  }) =>
      SupporterBracket(
        id: json['id']?.toString() ?? '',
        competitionId: json['competitionId']?.toString() ?? '',
        name: json['name']?.toString() ?? 'Playoffs',
        status: json['status']?.toString() ?? 'Pending',
        championName: json['championName']?.toString(),
        rounds:
            _maps(json['rounds']).map(SupporterBracketRound.fromJson).toList(),
        isFromCache: isFromCache,
        cachedAt: cachedAt,
      );
}

class SupporterBracketRound {
  final String id;
  final int number;
  final String name;
  final List<SupporterBracketMatch> matches;

  const SupporterBracketRound({
    required this.id,
    required this.number,
    required this.name,
    this.matches = const [],
  });

  factory SupporterBracketRound.fromJson(Map<String, dynamic> json) =>
      SupporterBracketRound(
        id: json['id']?.toString() ?? '',
        number: _integer(json['roundNumber']) ?? 0,
        name: json['name']?.toString() ?? 'Round',
        matches:
            _maps(json['matches']).map(SupporterBracketMatch.fromJson).toList(),
      );
}

class SupporterBracketMatch {
  final String id;
  final String? contestId;
  final int number;
  final String participant1;
  final String participant2;
  final String? score1;
  final String? score2;
  final String? winnerName;
  final String status;
  final bool isThirdPlace;
  final int bestOf;

  const SupporterBracketMatch({
    required this.id,
    this.contestId,
    required this.number,
    required this.participant1,
    required this.participant2,
    this.score1,
    this.score2,
    this.winnerName,
    required this.status,
    this.isThirdPlace = false,
    this.bestOf = 1,
  });

  factory SupporterBracketMatch.fromJson(Map<String, dynamic> json) =>
      SupporterBracketMatch(
        id: json['id']?.toString() ?? '',
        contestId: json['contestId']?.toString(),
        number: _integer(json['matchNumber']) ?? 0,
        participant1: json['participant1Name']?.toString() ?? 'TBD',
        participant2: json['participant2Name']?.toString() ?? 'TBD',
        score1: json['score1']?.toString(),
        score2: json['score2']?.toString(),
        winnerName: json['winnerName']?.toString(),
        status: json['status']?.toString() ?? 'Pending',
        isThirdPlace: json['isThirdPlace'] == true,
        bestOf: _integer(json['bestOf']) ?? 1,
      );
}

class GolfLeaderboard {
  final String id;
  final String name;
  final String status;
  final int parPerRound;
  final String? courseName;
  final String? courseLocation;
  final int cutAfterRound;
  final bool cutApplied;
  final List<GolfLeaderboardEntry> entries;
  final bool isFromCache;
  final DateTime? cachedAt;

  const GolfLeaderboard({
    required this.id,
    required this.name,
    required this.status,
    required this.parPerRound,
    this.courseName,
    this.courseLocation,
    required this.cutAfterRound,
    required this.cutApplied,
    this.entries = const [],
    this.isFromCache = false,
    this.cachedAt,
  });

  factory GolfLeaderboard.fromJson(
    Map<String, dynamic> json, {
    bool isFromCache = false,
    DateTime? cachedAt,
  }) =>
      GolfLeaderboard(
        id: json['id']?.toString() ?? '',
        name: json['name']?.toString() ?? 'Golf leaderboard',
        status: json['status']?.toString() ?? 'Scheduled',
        parPerRound: _integer(json['parPerRound']) ?? 72,
        courseName: json['courseName']?.toString(),
        courseLocation: json['courseLocation']?.toString(),
        cutAfterRound: _integer(json['cutAfterRound']) ?? 2,
        cutApplied: json['cutAppliedAt'] != null,
        entries:
            _maps(json['entries']).map(GolfLeaderboardEntry.fromJson).toList(),
        isFromCache: isFromCache,
        cachedAt: cachedAt,
      );
}

class GolfLeaderboardEntry {
  final String participantId;
  final String name;
  final int position;
  final bool isTied;
  final String status;
  final int totalStrokes;
  final int toPar;
  final int roundsPlayed;
  final int todayToPar;
  final int thru;
  final List<int> roundScores;

  const GolfLeaderboardEntry({
    required this.participantId,
    required this.name,
    required this.position,
    required this.isTied,
    required this.status,
    required this.totalStrokes,
    required this.toPar,
    required this.roundsPlayed,
    this.todayToPar = 0,
    this.thru = 0,
    this.roundScores = const [],
  });

  factory GolfLeaderboardEntry.fromJson(Map<String, dynamic> json) =>
      GolfLeaderboardEntry(
        participantId: json['participantId']?.toString() ?? '',
        name: json['name']?.toString() ?? 'Golfer',
        position: _integer(json['position']) ?? 0,
        isTied: json['isTied'] == true,
        status: json['status']?.toString() ?? 'Active',
        totalStrokes: _integer(json['totalStrokes']) ?? 0,
        toPar: _integer(json['toPar']) ?? 0,
        roundsPlayed: _integer(json['roundsPlayed']) ?? 0,
        todayToPar: _integer(json['todayToPar']) ?? 0,
        thru: _integer(json['thru']) ?? 0,
        roundScores: json['roundScores'] is List
            ? (json['roundScores'] as List)
                .map((item) => _integer(item) ?? 0)
                .toList()
            : const [],
      );

  String get toParDisplay => toPar == 0
      ? 'E'
      : toPar > 0
          ? '+$toPar'
          : '$toPar';

  String get todayDisplay => todayToPar == 0
      ? 'E'
      : todayToPar > 0
          ? '+$todayToPar'
          : '$todayToPar';

  String get thruDisplay => thru >= 18
      ? 'F'
      : thru == 0
          ? '—'
          : '$thru';
}

class FightCard {
  final String id;
  final String name;
  final String status;
  final int boutCount;
  final int completedBoutCount;
  final List<String> allowedResultMethods;
  final List<FightCardBout> bouts;
  final bool isFromCache;
  final DateTime? cachedAt;

  const FightCard({
    required this.id,
    required this.name,
    required this.status,
    required this.boutCount,
    required this.completedBoutCount,
    this.allowedResultMethods = const [],
    this.bouts = const [],
    this.isFromCache = false,
    this.cachedAt,
  });

  factory FightCard.fromJson(Map<String, dynamic> json,
          {bool isFromCache = false, DateTime? cachedAt}) =>
      FightCard(
        id: json['id']?.toString() ?? '',
        name: json['name']?.toString() ?? 'Fight card',
        status: json['status']?.toString() ?? 'Scheduled',
        boutCount: _integer(json['boutCount']) ?? 0,
        completedBoutCount: _integer(json['completedBoutCount']) ?? 0,
        allowedResultMethods: json['allowedResultMethods'] is List
            ? (json['allowedResultMethods'] as List)
                .map((item) => '$item')
                .toList()
            : const [],
        bouts: _maps(json['bouts']).map(FightCardBout.fromJson).toList(),
        isFromCache: isFromCache,
        cachedAt: cachedAt,
      );
}

class FightCardBout {
  final String id;
  final String name;
  final int boutNumber;
  final String cardSection;
  final String weightClass;
  final int scheduledRounds;
  final String status;
  final List<FightCardFighter> fighters;
  final List<FightCardOfficial> officials;
  final String? winnerId;
  final String? winnerName;
  final String? resultMethod;
  final int? resultRound;
  final int? resultTimeSeconds;

  const FightCardBout({
    required this.id,
    required this.name,
    required this.boutNumber,
    required this.cardSection,
    required this.weightClass,
    required this.scheduledRounds,
    required this.status,
    this.fighters = const [],
    this.officials = const [],
    this.winnerId,
    this.winnerName,
    this.resultMethod,
    this.resultRound,
    this.resultTimeSeconds,
  });

  factory FightCardBout.fromJson(Map<String, dynamic> json) => FightCardBout(
        id: json['id']?.toString() ?? '',
        name: json['name']?.toString() ?? 'Bout',
        boutNumber: _integer(json['boutNumber']) ?? 0,
        cardSection: json['cardSection']?.toString() ?? 'MainCard',
        weightClass: json['weightClass']?.toString() ?? 'Open weight',
        scheduledRounds: _integer(json['scheduledRounds']) ?? 3,
        status: json['status']?.toString() ?? 'Scheduled',
        fighters:
            _maps(json['fighters']).map(FightCardFighter.fromJson).toList(),
        officials:
            _maps(json['officials']).map(FightCardOfficial.fromJson).toList(),
        winnerId: json['winnerId']?.toString(),
        winnerName: json['winnerName']?.toString(),
        resultMethod: json['resultMethod']?.toString(),
        resultRound: _integer(json['resultRound']),
        resultTimeSeconds: _integer(json['resultTimeSeconds']),
      );
}

class FightCardFighter {
  final String participantId;
  final String name;
  final String corner;
  const FightCardFighter(
      {required this.participantId, required this.name, required this.corner});
  factory FightCardFighter.fromJson(Map<String, dynamic> json) =>
      FightCardFighter(
        participantId: json['participantId']?.toString() ?? '',
        name: json['name']?.toString() ?? 'TBA',
        corner: json['corner']?.toString() ?? 'Corner',
      );
}

class FightCardOfficial {
  final String displayName;
  final String role;
  const FightCardOfficial({required this.displayName, required this.role});
  factory FightCardOfficial.fromJson(Map<String, dynamic> json) =>
      FightCardOfficial(
        displayName: json['displayName']?.toString() ?? 'Official',
        role: json['role']?.toString() ?? 'Official',
      );
}

const sportCodes = {
  '00000000-0000-0000-0000-000000000100': 'football',
  '00000000-0000-0000-0000-000000000101': 'basketball',
  '00000000-0000-0000-0000-000000000102': 'golf',
  '00000000-0000-0000-0000-000000000103': 'athletics',
  '00000000-0000-0000-0000-000000000104': 'cricket',
  '00000000-0000-0000-0000-000000000105': 'combat',
  '00000000-0000-0000-0000-000000000106': 'rugby',
};

const sportNames = {
  '00000000-0000-0000-0000-000000000100': 'Football',
  '00000000-0000-0000-0000-000000000101': 'Basketball',
  '00000000-0000-0000-0000-000000000102': 'Golf',
  '00000000-0000-0000-0000-000000000103': 'Athletics',
  '00000000-0000-0000-0000-000000000104': 'Cricket',
  '00000000-0000-0000-0000-000000000105': 'Combat',
  '00000000-0000-0000-0000-000000000106': 'Rugby 7s & 15s',
};

List<Map<String, dynamic>> _maps(dynamic value) => value is List
    ? value
        .whereType<Map>()
        .map((item) => Map<String, dynamic>.from(item))
        .toList()
    : const [];

int? _integer(dynamic value) =>
    value is num ? value.toInt() : int.tryParse(value?.toString() ?? '');

Map<String, dynamic> _standingMetadata(dynamic value) {
  if (value is Map) return Map<String, dynamic>.from(value);
  if (value is! String || value.trim().isEmpty) return const {};
  try {
    final decoded = jsonDecode(value);
    return decoded is Map ? Map<String, dynamic>.from(decoded) : const {};
  } on FormatException {
    return const {};
  }
}
