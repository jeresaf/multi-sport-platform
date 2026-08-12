import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../domain/supporter_models.dart';
import '../providers/supporter_provider.dart';

class GolfLeaderboardScreen extends ConsumerWidget {
  final String competitionId;

  const GolfLeaderboardScreen({super.key, required this.competitionId});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final leaderboard = ref.watch(golfLeaderboardProvider(competitionId));
    return Scaffold(
      appBar: AppBar(title: const Text('Golf leaderboard')),
      body: leaderboard.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => _ErrorView(
          message: error.toString(),
          onRetry: () => ref.invalidate(golfLeaderboardProvider(competitionId)),
        ),
        data: (data) => RefreshIndicator(
          onRefresh: () async =>
              ref.invalidate(golfLeaderboardProvider(competitionId)),
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              if (data.isFromCache)
                Card(
                  child: ListTile(
                    leading: const Icon(Icons.offline_bolt),
                    title: const Text('Showing saved leaderboard'),
                    subtitle: data.cachedAt == null
                        ? null
                        : Text(DateFormat.MMMd()
                            .add_Hm()
                            .format(data.cachedAt!.toLocal())),
                  ),
                ),
              Card(
                color: Colors.green.shade900,
                child: Padding(
                  padding: const EdgeInsets.all(20),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(data.status.toUpperCase(),
                          style: TextStyle(color: Colors.green.shade200)),
                      const SizedBox(height: 4),
                      Text(data.name,
                          style: Theme.of(context)
                              .textTheme
                              .headlineSmall
                              ?.copyWith(color: Colors.white)),
                      const SizedBox(height: 8),
                      Text(
                        '${data.courseName ?? 'Championship Course'}${data.courseLocation == null ? '' : ' · ${data.courseLocation}'}',
                        style: const TextStyle(color: Colors.white70),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        'Par ${data.parPerRound} · Cut after round ${data.cutAfterRound} · ${data.cutApplied ? 'Cut applied' : 'Cut pending'}',
                        style: const TextStyle(color: Colors.white70),
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 16),
              if (data.entries.isEmpty)
                const Padding(
                  padding: EdgeInsets.all(32),
                  child: Center(child: Text('No leaderboard entries yet')),
                )
              else
                Card(
                  child: SingleChildScrollView(
                    scrollDirection: Axis.horizontal,
                    child: DataTable(
                      columns: const [
                        DataColumn(label: Text('#')),
                        DataColumn(label: Text('Golfer')),
                        DataColumn(label: Text('R1'), numeric: true),
                        DataColumn(label: Text('R2'), numeric: true),
                        DataColumn(label: Text('R3'), numeric: true),
                        DataColumn(label: Text('R4'), numeric: true),
                        DataColumn(label: Text('Total'), numeric: true),
                        DataColumn(label: Text('To par'), numeric: true),
                        DataColumn(label: Text('Status')),
                      ],
                      rows: data.entries.map(_row).toList(),
                    ),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }

  DataRow _row(GolfLeaderboardEntry entry) {
    final rounds = [...entry.roundScores, 0, 0, 0, 0];
    return DataRow(cells: [
      DataCell(Text('${entry.isTied ? 'T' : ''}${entry.position}')),
      DataCell(Text(entry.name)),
      for (var index = 0; index < 4; index++)
        DataCell(Text(rounds[index] == 0 ? '—' : '${rounds[index]}')),
      DataCell(Text(entry.totalStrokes == 0 ? '—' : '${entry.totalStrokes}')),
      DataCell(Text(entry.roundsPlayed == 0 ? '—' : entry.toParDisplay)),
      DataCell(Text(entry.status)),
    ]);
  }
}

class _ErrorView extends StatelessWidget {
  final String message;
  final VoidCallback onRetry;

  const _ErrorView({required this.message, required this.onRetry});

  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.golf_course, size: 56),
              const SizedBox(height: 12),
              const Text('No golf leaderboard is available'),
              const SizedBox(height: 8),
              Text(message, textAlign: TextAlign.center),
              const SizedBox(height: 16),
              FilledButton(onPressed: onRetry, child: const Text('Try again')),
            ],
          ),
        ),
      );
}
