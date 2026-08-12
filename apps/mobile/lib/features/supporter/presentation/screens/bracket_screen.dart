import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../domain/supporter_models.dart';
import '../providers/supporter_provider.dart';

class BracketScreen extends ConsumerWidget {
  final String competitionId;

  const BracketScreen({super.key, required this.competitionId});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final bracket = ref.watch(supporterBracketProvider(competitionId));
    return Scaffold(
      appBar: AppBar(title: const Text('Playoff bracket')),
      body: bracket.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => _ErrorView(
          message: error.toString(),
          onRetry: () =>
              ref.invalidate(supporterBracketProvider(competitionId)),
        ),
        data: (data) => RefreshIndicator(
          onRefresh: () async =>
              ref.invalidate(supporterBracketProvider(competitionId)),
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              if (data.isFromCache) _CachedBanner(cachedAt: data.cachedAt),
              Card(
                color: Theme.of(context).colorScheme.primaryContainer,
                child: Padding(
                  padding: const EdgeInsets.all(20),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(data.status.toUpperCase(),
                          style: Theme.of(context).textTheme.labelMedium),
                      const SizedBox(height: 4),
                      Text(data.name,
                          style: Theme.of(context).textTheme.headlineSmall),
                      const SizedBox(height: 12),
                      Text(data.championName == null
                          ? 'Champion pending'
                          : 'Champion: ${data.championName}'),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 16),
              if (data.rounds.isEmpty)
                const Padding(
                  padding: EdgeInsets.all(32),
                  child: Center(child: Text('No playoff rounds available')),
                )
              else
                SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: data.rounds
                        .map((round) => _RoundColumn(round: round))
                        .toList(),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _RoundColumn extends StatelessWidget {
  final SupporterBracketRound round;

  const _RoundColumn({required this.round});

  @override
  Widget build(BuildContext context) => SizedBox(
        width: 286,
        child: Padding(
          padding: const EdgeInsets.only(right: 12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(round.name,
                  style: Theme.of(context)
                      .textTheme
                      .titleMedium
                      ?.copyWith(fontWeight: FontWeight.bold)),
              const SizedBox(height: 8),
              ...round.matches.map((match) => _MatchCard(match: match)),
            ],
          ),
        ),
      );
}

class _MatchCard extends StatelessWidget {
  final SupporterBracketMatch match;

  const _MatchCard({required this.match});

  @override
  Widget build(BuildContext context) => Card(
        margin: const EdgeInsets.only(bottom: 10),
        child: InkWell(
          onTap: match.contestId == null
              ? null
              : () => context.push('/contest/${match.contestId}'),
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Match ${match.number}${match.isThirdPlace ? ' · Third place' : ''}${match.bestOf > 1 ? ' · Best of ${match.bestOf}' : ''}',
                  style: Theme.of(context).textTheme.labelSmall,
                ),
                const Divider(),
                _TeamRow(name: match.participant1, score: match.score1),
                const SizedBox(height: 8),
                _TeamRow(name: match.participant2, score: match.score2),
                if (match.winnerName != null) ...[
                  const SizedBox(height: 8),
                  Text('Winner: ${match.winnerName}',
                      style: TextStyle(
                          color: Colors.green.shade700,
                          fontWeight: FontWeight.w600)),
                ],
              ],
            ),
          ),
        ),
      );
}

class _TeamRow extends StatelessWidget {
  final String name;
  final String? score;

  const _TeamRow({required this.name, this.score});

  @override
  Widget build(BuildContext context) => Row(
        children: [
          Expanded(child: Text(name)),
          if (score != null)
            Text(score!, style: const TextStyle(fontWeight: FontWeight.bold)),
        ],
      );
}

class _CachedBanner extends StatelessWidget {
  final DateTime? cachedAt;

  const _CachedBanner({this.cachedAt});

  @override
  Widget build(BuildContext context) => Card(
        child: ListTile(
          leading: const Icon(Icons.offline_bolt),
          title: const Text('Showing saved bracket'),
          subtitle: cachedAt == null
              ? null
              : Text(DateFormat.MMMd().add_Hm().format(cachedAt!.toLocal())),
        ),
      );
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
              const Icon(Icons.account_tree_outlined, size: 56),
              const SizedBox(height: 12),
              const Text('No playoff bracket is available'),
              const SizedBox(height: 8),
              Text(message, textAlign: TextAlign.center),
              const SizedBox(height: 16),
              FilledButton(onPressed: onRetry, child: const Text('Try again')),
            ],
          ),
        ),
      );
}
