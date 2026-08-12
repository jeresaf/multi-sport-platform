import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../data/repositories/competition_repository_impl.dart';
import '../providers/competition_provider.dart';
import '../../../supporter/presentation/providers/supporter_provider.dart';
import '../../../../core/router/app_router.dart';
import '../../../supporter/data/supporter_preferences.dart';
import '../../../supporter/data/supporter_share_service.dart';

/// Competition detail screen
class CompetitionDetailScreen extends ConsumerWidget {
  final String competitionId;

  const CompetitionDetailScreen({
    super.key,
    required this.competitionId,
  });

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final competitionAsync =
        ref.watch(competitionDetailProvider(competitionId));
    final competition = competitionAsync.valueOrNull;
    final favourite = competition == null
        ? null
        : SupporterFavourite(
            type: 'competition',
            id: competition.id,
            title: competition.name,
            subtitle: competition.sportCode.toUpperCase(),
            route: Routes.competitionDetail(competition.id),
          );
    final isFavourite = favourite != null &&
        ref.watch(supporterPreferencesProvider).isFavourite(
              favourite.type,
              favourite.id,
            );

    return Scaffold(
      appBar: AppBar(
        title: const Text('Competition'),
        actions: [
          if (favourite != null)
            IconButton(
              tooltip:
                  isFavourite ? 'Remove from favourites' : 'Add to favourites',
              onPressed: () => ref
                  .read(supporterPreferencesProvider.notifier)
                  .toggleFavourite(favourite),
              icon: Icon(
                isFavourite ? Icons.favorite : Icons.favorite_border,
              ),
            ),
          IconButton(
            tooltip: 'Share competition',
            icon: const Icon(Icons.share),
            onPressed: competition == null
                ? null
                : () => supporterShareService.share(
                      title: competition.name,
                      description:
                          '${competition.sportCode.toUpperCase()} competition',
                      path: Routes.competitionDetail(competition.id),
                    ),
          ),
        ],
      ),
      body: competitionAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, stack) => _ErrorView(
          error: error.toString(),
          onRetry: () =>
              ref.invalidate(competitionDetailProvider(competitionId)),
        ),
        data: (competition) {
          if (competition == null) {
            return const Center(child: Text('Competition not found'));
          }
          return _CompetitionContent(competition: competition);
        },
      ),
    );
  }
}

class _CompetitionContent extends ConsumerWidget {
  final Competition competition;

  const _CompetitionContent({required this.competition});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final supporter = ref.watch(supporterFeedProvider).valueOrNull;
    final fixtures = supporter?.fixtures
            .where((item) => item.competitionId == competition.id)
            .toList() ??
        const [];
    final fixtureIds = fixtures.map((item) => item.id).toSet();
    final results = supporter?.results
            .where((item) => fixtureIds.contains(item.contestId))
            .toList() ??
        const [];
    final standings = supporter?.standings
            .where((item) => item.competitionId == competition.id)
            .toList() ??
        const [];
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        // Competition header
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Container(
                      padding: const EdgeInsets.symmetric(
                          horizontal: 8, vertical: 4),
                      decoration: BoxDecoration(
                        color: _getStatusColor(competition.status)
                            .withValues(alpha: 0.1),
                        borderRadius: BorderRadius.circular(12),
                      ),
                      child: Text(
                        competition.status,
                        style: TextStyle(
                          color: _getStatusColor(competition.status),
                          fontSize: 12,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    ),
                    const Spacer(),
                    Container(
                      padding: const EdgeInsets.symmetric(
                          horizontal: 8, vertical: 4),
                      decoration: BoxDecoration(
                        color: Colors.grey.shade100,
                        borderRadius: BorderRadius.circular(12),
                      ),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Icon(Icons.sports,
                              size: 14, color: Colors.grey.shade600),
                          const SizedBox(width: 4),
                          Text(
                            competition.sportCode.toUpperCase(),
                            style: TextStyle(
                              color: Colors.grey.shade600,
                              fontSize: 12,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                Text(
                  competition.name,
                  style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                ),
                if (competition.description != null) ...[
                  const SizedBox(height: 8),
                  Text(
                    competition.description!,
                    style: TextStyle(color: Colors.grey.shade600),
                  ),
                ],
                const SizedBox(height: 16),
                Row(
                  children: [
                    _InfoChip(
                      icon: Icons.calendar_today,
                      label: _formatDate(competition.startDate),
                    ),
                    const SizedBox(width: 16),
                    _InfoChip(
                      icon: Icons.location_on,
                      label: competition.venue ?? 'TBD',
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 24),

        // Format info
        Text(
          'Format',
          style: Theme.of(context).textTheme.titleMedium?.copyWith(
                fontWeight: FontWeight.bold,
              ),
        ),
        const SizedBox(height: 12),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              children: [
                _FormatRow(label: 'Type', value: competition.format ?? 'N/A'),
                _FormatRow(
                    label: 'Participants',
                    value: '${competition.participantCount}'),
                _FormatRow(
                    label: 'Contests', value: '${competition.contestCount}'),
              ],
            ),
          ),
        ),
        const SizedBox(height: 24),

        if (competition.availableTabs.contains('bracket') ||
            competition.availableTabs.contains('leaderboard')) ...[
          Text(
            'Tournament views',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.bold,
                ),
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 12,
            runSpacing: 12,
            children: [
              if (competition.availableTabs.contains('bracket'))
                FilledButton.tonalIcon(
                  onPressed: () => context.push(Routes.bracket(competition.id)),
                  icon: const Icon(Icons.account_tree),
                  label: const Text('Open playoff bracket'),
                ),
              if (competition.availableTabs.contains('leaderboard'))
                FilledButton.tonalIcon(
                  onPressed: () =>
                      context.push(Routes.golfLeaderboard(competition.id)),
                  icon: const Icon(Icons.leaderboard),
                  label: const Text('Open leaderboard'),
                ),
            ],
          ),
          const SizedBox(height: 24),
        ],

        // Fixtures
        Text(
          'Fixtures & Results',
          style: Theme.of(context).textTheme.titleMedium?.copyWith(
                fontWeight: FontWeight.bold,
              ),
        ),
        const SizedBox(height: 12),
        Card(
          child: fixtures.isEmpty
              ? const Padding(
                  padding: EdgeInsets.all(24),
                  child: Center(child: Text('No published fixtures yet')),
                )
              : Column(
                  children: fixtures.map((fixture) {
                    final fixtureResults = results
                        .where((item) => item.contestId == fixture.id)
                        .toList();
                    return ListTile(
                      leading: Icon(
                        fixture.status == 'Completed'
                            ? Icons.check_circle_outline
                            : Icons.schedule,
                      ),
                      title: Text(fixture.participants.isEmpty
                          ? fixture.name
                          : fixture.participants.join(' vs ')),
                      subtitle: Text(fixtureResults.isEmpty
                          ? fixture.status
                          : fixtureResults
                              .map((item) =>
                                  '${item.participantName} ${item.score}')
                              .join(' · ')),
                      trailing: const Icon(Icons.chevron_right),
                      onTap: () => context.push('/contest/${fixture.id}'),
                    );
                  }).toList(),
                ),
        ),
        const SizedBox(height: 24),

        // Standings
        Text(
          'Standings',
          style: Theme.of(context).textTheme.titleMedium?.copyWith(
                fontWeight: FontWeight.bold,
              ),
        ),
        const SizedBox(height: 12),
        Card(
          child: standings.isEmpty
              ? const Padding(
                  padding: EdgeInsets.all(24),
                  child: Center(child: Text('No standings available yet')),
                )
              : Column(
                  children: standings
                      .map((standing) => ListTile(
                            leading: CircleAvatar(
                                child: Text('${standing.position}')),
                            title: Text(standing.participantName),
                            subtitle: Text(
                              'P ${standing.played} · W ${standing.won} · D ${standing.drawn} · L ${standing.lost}',
                            ),
                            trailing: Text(
                              '${standing.points}',
                              style:
                                  const TextStyle(fontWeight: FontWeight.bold),
                            ),
                          ))
                      .toList(),
                ),
        ),
      ],
    );
  }

  Color _getStatusColor(String status) {
    switch (status.toLowerCase()) {
      case 'live':
      case 'inprogress':
        return Colors.red;
      case 'scheduled':
      case 'upcoming':
        return Colors.blue;
      case 'completed':
        return Colors.green;
      case 'cancelled':
        return Colors.grey;
      default:
        return Colors.orange;
    }
  }

  String _formatDate(DateTime? date) {
    if (date == null) return 'TBD';
    return '${date.day}/${date.month}/${date.year}';
  }
}

class _InfoChip extends StatelessWidget {
  final IconData icon;
  final String label;

  const _InfoChip({
    required this.icon,
    required this.label,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 16, color: Colors.grey.shade600),
        const SizedBox(width: 4),
        Text(
          label,
          style: TextStyle(color: Colors.grey.shade600),
        ),
      ],
    );
  }
}

class _FormatRow extends StatelessWidget {
  final String label;
  final String value;

  const _FormatRow({
    required this.label,
    required this.value,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(
            label,
            style: TextStyle(color: Colors.grey.shade600),
          ),
          Text(
            value,
            style: const TextStyle(fontWeight: FontWeight.w600),
          ),
        ],
      ),
    );
  }
}

class _ErrorView extends StatelessWidget {
  final String error;
  final VoidCallback onRetry;

  const _ErrorView({
    required this.error,
    required this.onRetry,
  });

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(
              Icons.error_outline,
              size: 64,
              color: Colors.red.shade300,
            ),
            const SizedBox(height: 16),
            Text(
              'Error loading competition',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 8),
            Text(
              error,
              textAlign: TextAlign.center,
              style: TextStyle(color: Colors.grey.shade600),
            ),
            const SizedBox(height: 24),
            ElevatedButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('Retry'),
            ),
          ],
        ),
      ),
    );
  }
}
