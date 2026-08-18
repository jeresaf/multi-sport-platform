import 'package:flutter/material.dart';

import '../../../supporter/domain/supporter_models.dart';
import '../../data/repositories/competition_repository_impl.dart';

enum CompetitionOutputKind {
  standings,
  groups,
  bracket,
  series,
  leaderboard,
  medals,
}

class CompetitionOutputRenderer extends StatelessWidget {
  const CompetitionOutputRenderer({
    super.key,
    required this.kind,
    this.standings = const [],
    this.bracket,
    this.leaderboard,
    this.compact = false,
    this.onContest,
    this.medals = const [],
  });

  final CompetitionOutputKind kind;
  final List<SupporterStanding> standings;
  final SupporterBracket? bracket;
  final GolfLeaderboard? leaderboard;
  final bool compact;
  final ValueChanged<String>? onContest;
  final List<CompetitionMedal> medals;

  @override
  Widget build(BuildContext context) => switch (kind) {
        CompetitionOutputKind.standings =>
          CompactStandingsTable(rows: standings, compact: compact),
        CompetitionOutputKind.groups => GroupStandings(rows: standings),
        CompetitionOutputKind.bracket => ResponsiveKnockoutBracket(
            bracket: bracket,
            onContest: onContest,
          ),
        CompetitionOutputKind.series => SeriesView(bracket: bracket),
        CompetitionOutputKind.leaderboard =>
          LeaderboardTable(leaderboard: leaderboard),
        CompetitionOutputKind.medals => MedalTable(rows: medals),
      };
}

class CompactStandingsTable extends StatelessWidget {
  const CompactStandingsTable({
    super.key,
    required this.rows,
    this.compact = false,
  });

  final List<SupporterStanding> rows;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    if (rows.isEmpty) {
      return const _EmptyOutput(message: 'No standings available yet');
    }
    return Card(
      clipBehavior: Clip.antiAlias,
      child: Column(
        children: rows.map((row) {
          return ExpansionTile(
            leading: CircleAvatar(child: Text('${row.position}')),
            title: Row(children: [
              if (row.qualificationCode != null)
                const Padding(
                  padding: EdgeInsets.only(right: 6),
                  child: Icon(Icons.verified, size: 16, color: Colors.green),
                ),
              Expanded(
                child: Text(row.participantName,
                    maxLines: 1, overflow: TextOverflow.ellipsis),
              ),
              if (row.pointsDeduction > 0)
                Text(' −${row.pointsDeduction}',
                    style: const TextStyle(color: Colors.red)),
            ]),
            subtitle: Text('P ${row.played} · ${_signed(row.differential)}'),
            trailing: Text('${row.points}',
                style: const TextStyle(fontWeight: FontWeight.bold)),
            children: compact
                ? const []
                : [
                    Padding(
                      padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
                      child: Wrap(
                        spacing: 16,
                        runSpacing: 8,
                        children: [
                          Text('Won ${row.won}'),
                          Text('Drawn ${row.drawn}'),
                          Text('Lost ${row.lost}'),
                          Text('Difference ${_signed(row.differential)}'),
                          if (row.bonusPoints != null)
                            Text('Bonus ${row.bonusPoints}'),
                          if (row.form.isNotEmpty) _FormChips(values: row.form),
                        ],
                      ),
                    ),
                  ],
          );
        }).toList(),
      ),
    );
  }
}

class _FormChips extends StatelessWidget {
  const _FormChips({required this.values});

  final List<String> values;

  @override
  Widget build(BuildContext context) => Row(
        mainAxisSize: MainAxisSize.min,
        children: values.map((value) {
          final color = value == 'W'
              ? Colors.green
              : value == 'D'
                  ? Colors.blueGrey
                  : Colors.red;
          return Container(
            width: 22,
            height: 22,
            margin: const EdgeInsets.only(right: 4),
            alignment: Alignment.center,
            decoration: BoxDecoration(color: color, shape: BoxShape.circle),
            child: Text(value,
                style: const TextStyle(
                    color: Colors.white,
                    fontSize: 10,
                    fontWeight: FontWeight.bold)),
          );
        }).toList(),
      );
}

class GroupStandings extends StatefulWidget {
  const GroupStandings({super.key, required this.rows});

  final List<SupporterStanding> rows;

  @override
  State<GroupStandings> createState() => _GroupStandingsState();
}

class _GroupStandingsState extends State<GroupStandings> {
  String? selected;

  @override
  Widget build(BuildContext context) {
    final groups = <String, List<SupporterStanding>>{};
    for (final row in widget.rows) {
      groups.putIfAbsent(row.groupName ?? 'Overall', () => []).add(row);
    }
    if (groups.isEmpty) {
      return const _EmptyOutput(message: 'No group standings available yet');
    }
    selected ??= groups.keys.first;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: SegmentedButton<String>(
            segments: groups.keys
                .map((name) => ButtonSegment(value: name, label: Text(name)))
                .toList(),
            selected: {selected!},
            onSelectionChanged: (value) =>
                setState(() => selected = value.first),
          ),
        ),
        const SizedBox(height: 12),
        CompactStandingsTable(rows: groups[selected] ?? const []),
      ],
    );
  }
}

class ResponsiveKnockoutBracket extends StatefulWidget {
  const ResponsiveKnockoutBracket({
    super.key,
    required this.bracket,
    this.onContest,
  });

  final SupporterBracket? bracket;
  final ValueChanged<String>? onContest;

  @override
  State<ResponsiveKnockoutBracket> createState() =>
      _ResponsiveKnockoutBracketState();
}

class _ResponsiveKnockoutBracketState extends State<ResponsiveKnockoutBracket> {
  int selectedRound = 0;

  @override
  Widget build(BuildContext context) {
    final bracket = widget.bracket;
    if (bracket == null || bracket.rounds.isEmpty) {
      return const _EmptyOutput(message: 'No bracket is published.');
    }
    final wide = MediaQuery.sizeOf(context).width >= 900;
    if (wide) {
      return SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: bracket.rounds
              .map((round) => SizedBox(
                    width: 300,
                    child: _RoundColumn(
                      round: round,
                      onContest: widget.onContest,
                    ),
                  ))
              .toList(),
        ),
      );
    }
    selectedRound = selectedRound.clamp(0, bracket.rounds.length - 1);
    return Column(
      children: [
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: SegmentedButton<int>(
            segments: [
              for (var index = 0; index < bracket.rounds.length; index++)
                ButtonSegment(
                  value: index,
                  label: Text(bracket.rounds[index].name),
                ),
            ],
            selected: {selectedRound},
            onSelectionChanged: (value) =>
                setState(() => selectedRound = value.first),
          ),
        ),
        const SizedBox(height: 12),
        _RoundColumn(
          round: bracket.rounds[selectedRound],
          onContest: widget.onContest,
        ),
      ],
    );
  }
}

class _RoundColumn extends StatelessWidget {
  const _RoundColumn({required this.round, this.onContest});

  final SupporterBracketRound round;
  final ValueChanged<String>? onContest;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(right: 12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Padding(
              padding: const EdgeInsets.all(8),
              child: Text(round.name,
                  style: Theme.of(context).textTheme.titleMedium),
            ),
            ...round.matches.map((match) => Card(
                  child: ListTile(
                    onTap: match.contestId == null
                        ? null
                        : () => onContest?.call(match.contestId!),
                    title:
                        Text('${match.participant1}  ${match.score1 ?? '—'}'),
                    subtitle:
                        Text('${match.participant2}  ${match.score2 ?? '—'}'),
                    trailing: const Icon(Icons.chevron_right),
                  ),
                )),
          ],
        ),
      );
}

class SeriesView extends StatelessWidget {
  const SeriesView({super.key, required this.bracket});

  final SupporterBracket? bracket;

  @override
  Widget build(BuildContext context) {
    final series = bracket?.rounds
            .expand((round) => round.matches)
            .where((match) => match.bestOf > 1)
            .toList() ??
        const [];
    if (series.isEmpty) {
      return const _EmptyOutput(message: 'No series are published.');
    }
    return Column(
      children: series
          .map((match) => Card(
                child: ListTile(
                  title: Text('${match.participant1} vs ${match.participant2}'),
                  subtitle: Text('Best of ${match.bestOf} · ${match.status}'),
                  trailing: Text(
                    '${match.score1 ?? '0'}–${match.score2 ?? '0'}',
                    style: const TextStyle(fontWeight: FontWeight.bold),
                  ),
                ),
              ))
          .toList(),
    );
  }
}

class LeaderboardTable extends StatelessWidget {
  const LeaderboardTable({super.key, required this.leaderboard});

  final GolfLeaderboard? leaderboard;

  @override
  Widget build(BuildContext context) {
    final rows = leaderboard?.entries ?? const [];
    if (rows.isEmpty) {
      return const _EmptyOutput(message: 'No leaderboard is published.');
    }
    return LayoutBuilder(builder: (context, constraints) {
      if (constraints.maxWidth < 600) {
        return Card(
          clipBehavior: Clip.antiAlias,
          child: Column(
            children: rows
                .map((row) => ExpansionTile(
                      key: ValueKey(row.participantId),
                      leading: Text('${row.isTied ? 'T' : ''}${row.position}',
                          style: const TextStyle(fontWeight: FontWeight.bold)),
                      title: Text(row.name,
                          maxLines: 1, overflow: TextOverflow.ellipsis),
                      subtitle: Text(_golfStatus(row.status)),
                      trailing: Text(
                          row.roundsPlayed == 0 ? '—' : row.toParDisplay,
                          style: const TextStyle(fontWeight: FontWeight.bold)),
                      children: [
                        Padding(
                          padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
                          child: Column(children: [
                            const Divider(),
                            Row(
                                mainAxisAlignment:
                                    MainAxisAlignment.spaceAround,
                                children: [
                                  _GolfDetail(
                                      label: 'Today',
                                      value: row.thru == 0
                                          ? '—'
                                          : row.todayDisplay),
                                  _GolfDetail(
                                      label: 'Thru', value: row.thruDisplay),
                                  _GolfDetail(
                                      label: 'Strokes',
                                      value: row.totalStrokes == 0
                                          ? '—'
                                          : '${row.totalStrokes}'),
                                ]),
                            const SizedBox(height: 12),
                            Wrap(spacing: 8, runSpacing: 8, children: [
                              for (var index = 0; index < 4; index++)
                                Chip(
                                    label: Text(
                                        'R${index + 1}  ${index < row.roundScores.length && row.roundScores[index] > 0 ? row.roundScores[index] : '—'}')),
                            ]),
                          ]),
                        ),
                      ],
                    ))
                .toList(),
          ),
        );
      }
      final desktop = constraints.maxWidth >= 1000;
      return Card(
        clipBehavior: Clip.antiAlias,
        child: SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: DataTable(
            columns: [
              const DataColumn(label: Text('Pos')),
              const DataColumn(label: Text('Player')),
              if (desktop)
                for (var round = 1; round <= 4; round++)
                  DataColumn(label: Text('R$round'), numeric: true),
              const DataColumn(label: Text('Today'), numeric: true),
              const DataColumn(label: Text('Thru'), numeric: true),
              const DataColumn(label: Text('Total'), numeric: true),
              const DataColumn(label: Text('Status')),
            ],
            rows: rows
                .map((row) => DataRow(cells: [
                      DataCell(Text('${row.isTied ? 'T' : ''}${row.position}')),
                      DataCell(Text(row.name)),
                      if (desktop)
                        for (var index = 0; index < 4; index++)
                          DataCell(Text(index < row.roundScores.length &&
                                  row.roundScores[index] > 0
                              ? '${row.roundScores[index]}'
                              : '—')),
                      DataCell(Text(row.thru == 0 ? '—' : row.todayDisplay)),
                      DataCell(Text(row.thruDisplay)),
                      DataCell(
                          Text(row.roundsPlayed == 0 ? '—' : row.toParDisplay)),
                      DataCell(Text(_golfStatus(row.status))),
                    ]))
                .toList(),
          ),
        ),
      );
    });
  }
}

class _GolfDetail extends StatelessWidget {
  const _GolfDetail({required this.label, required this.value});
  final String label;
  final String value;
  @override
  Widget build(BuildContext context) => Column(children: [
        Text(label, style: Theme.of(context).textTheme.labelSmall),
        const SizedBox(height: 2),
        Text(value, style: const TextStyle(fontWeight: FontWeight.bold)),
      ]);
}

String _golfStatus(String value) => value.replaceAllMapped(
    RegExp(r'([a-z])([A-Z])'), (match) => '${match[1]} ${match[2]}');

class MedalTable extends StatelessWidget {
  const MedalTable({super.key, required this.rows});

  final List<CompetitionMedal> rows;

  @override
  Widget build(BuildContext context) {
    if (rows.isEmpty) {
      return const _EmptyOutput(message: 'No medal table is published.');
    }
    return Card(
      child: Column(
        children: rows
            .asMap()
            .entries
            .map((entry) => ListTile(
                  leading: Text('${entry.key + 1}',
                      style: const TextStyle(fontWeight: FontWeight.bold)),
                  title: Text(entry.value.participantName),
                  subtitle: Text(
                    'Gold ${entry.value.gold} · Silver ${entry.value.silver} · Bronze ${entry.value.bronze}',
                  ),
                  trailing: Text('${entry.value.total}',
                      style: const TextStyle(fontWeight: FontWeight.bold)),
                ))
            .toList(),
      ),
    );
  }
}

class _EmptyOutput extends StatelessWidget {
  const _EmptyOutput({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) => Card(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Center(child: Text(message)),
        ),
      );
}

String _signed(num value) => value > 0 ? '+$value' : '$value';
