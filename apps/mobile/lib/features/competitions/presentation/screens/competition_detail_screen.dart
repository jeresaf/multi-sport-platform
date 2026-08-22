import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../data/repositories/competition_repository_impl.dart';
import '../providers/competition_provider.dart';
import '../../../supporter/presentation/providers/supporter_provider.dart';
import '../../../../core/router/app_router.dart';
import '../../../supporter/data/supporter_preferences.dart';
import '../../../supporter/data/supporter_share_service.dart';
import '../widgets/competition_output_renderer.dart';
import '../../../supporter/presentation/widgets/alert_preferences_sheet.dart';
import '../../../../core/formatters/public_date_time.dart';
import '../../../../core/widgets/contest_status_label.dart';
import '../../../registration/data/registration_repository.dart';

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
        title: const Text('Competition centre'),
        actions: [
          if (favourite != null)
            IconButton(
              tooltip:
                  isFavourite ? 'Choose alerts' : 'Favourite to enable alerts',
              onPressed: isFavourite
                  ? () => showSupporterAlertPreferences(context, ref,
                      favourite: favourite, sportCode: competition!.sportCode)
                  : null,
              icon: Icon(ref.watch(supporterPreferencesProvider.select(
                      (value) => value
                          .alertsFor('competition', competitionId)
                          .isNotEmpty))
                  ? Icons.notifications_active
                  : Icons.notifications_none),
            ),
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
    final registrationForms =
        ref.watch(competitionRegistrationFormsProvider(competition.id));
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
    final bracket = competition.availableTabs.contains('bracket') ||
            competition.availableTabs.contains('series')
        ? ref.watch(supporterBracketProvider(competition.id)).valueOrNull
        : null;
    final leaderboard = competition.availableTabs.contains('leaderboard')
        ? ref.watch(golfLeaderboardProvider(competition.id)).valueOrNull
        : null;
    final hasGames = fixtures.isNotEmpty ||
        results.isNotEmpty ||
        competition.availableTabs.any(
          const {
            'fixtures',
            'results',
            'bracket',
            'series',
            'leaderboard',
            'medals',
          }.contains,
        );
    final hasTable = standings.isNotEmpty ||
        competition.availableTabs.any(
          const {'standings', 'groups', 'conference'}.contains,
        );
    final completedCount = fixtures
        .where((item) =>
            contestStatusTone(item.status) == ContestStatusTone.finished)
        .length;
    final liveCount =
        fixtures.where((item) => contestStatusIsLive(item.status)).length;
    return DefaultTabController(
      length: 1 + (hasGames ? 1 : 0) + (hasTable ? 1 : 0),
      child: Column(
        children: [
          registrationForms.maybeWhen(
            data: (forms) => forms.isEmpty
                ? const SizedBox.shrink()
                : Container(
                    margin: const EdgeInsets.fromLTRB(16, 12, 16, 0),
                    padding: const EdgeInsets.all(14),
                    decoration: BoxDecoration(
                      color: Theme.of(context).colorScheme.primaryContainer,
                      borderRadius: BorderRadius.circular(16),
                    ),
                    child: Row(children: [
                      Icon(Icons.assignment_add,
                          color: Theme.of(context).colorScheme.primary),
                      const SizedBox(width: 10),
                      const Expanded(child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [Text('Registration is open', style: TextStyle(fontWeight: FontWeight.w800)), Text('Enter an athlete or team and track the review.')],
                      )),
                      FilledButton(
                        onPressed: () => context.push(
                            Routes.competitionRegistration(competition.id)),
                        child: const Text('Register'),
                      ),
                    ]),
                  ),
            orElse: () => const SizedBox.shrink(),
          ),
          Container(
            margin: const EdgeInsets.fromLTRB(16, 12, 16, 0),
            decoration: BoxDecoration(
              color: Theme.of(context).colorScheme.surface,
              borderRadius: BorderRadius.circular(16),
              border: Border.all(
                color: Theme.of(context).colorScheme.outlineVariant,
              ),
            ),
            child: TabBar(
              dividerColor: Colors.transparent,
              indicatorSize: TabBarIndicatorSize.tab,
              indicator: BoxDecoration(
                color: Theme.of(context).colorScheme.primaryContainer,
                borderRadius: BorderRadius.circular(12),
              ),
              tabs: [
                const Tab(text: 'Overview'),
                if (hasGames) const Tab(text: 'Games'),
                if (hasTable) const Tab(text: 'Table'),
              ],
            ),
          ),
          Expanded(
            child: TabBarView(
              children: [
                if (hasGames)
                  ListView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.all(16),
                    children: [
                      // Competition header
                      Container(
                        decoration: BoxDecoration(
                          gradient: LinearGradient(
                            colors: [
                              Theme.of(context).colorScheme.primary,
                              Theme.of(context).colorScheme.primaryContainer,
                            ],
                            begin: Alignment.topLeft,
                            end: Alignment.bottomRight,
                          ),
                          borderRadius: BorderRadius.circular(24),
                        ),
                        child: Padding(
                          padding: const EdgeInsets.all(20),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                children: [
                                  if (competition.status != 'Published')
                                    Container(
                                      padding: const EdgeInsets.symmetric(
                                          horizontal: 8, vertical: 4),
                                      decoration: BoxDecoration(
                                        color: Colors.white
                                            .withValues(alpha: 0.14),
                                        borderRadius: BorderRadius.circular(12),
                                      ),
                                      child: Text(
                                        competition.status,
                                        style: const TextStyle(
                                          color: Colors.white,
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
                                      color:
                                          Colors.white.withValues(alpha: 0.14),
                                      borderRadius: BorderRadius.circular(12),
                                    ),
                                    child: Row(
                                      mainAxisSize: MainAxisSize.min,
                                      children: [
                                        const Icon(Icons.sports,
                                            size: 14, color: Colors.white70),
                                        const SizedBox(width: 4),
                                        Text(
                                          competition.sportCode.toUpperCase(),
                                          style: const TextStyle(
                                            color: Colors.white70,
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
                                style: Theme.of(context)
                                    .textTheme
                                    .headlineSmall
                                    ?.copyWith(
                                      fontWeight: FontWeight.w900,
                                      color: Colors.white,
                                    ),
                              ),
                              if (competition.description != null) ...[
                                const SizedBox(height: 8),
                                Text(
                                  competition.description!,
                                  style: const TextStyle(color: Colors.white70),
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
                      const SizedBox(height: 14),
                      Row(
                        children: [
                          Expanded(
                            child: _CompetitionMetric(
                              label: 'Live',
                              value: liveCount,
                              icon: Icons.bolt,
                            ),
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: _CompetitionMetric(
                              label: 'Completed',
                              value: completedCount,
                              icon: Icons.check_circle_outline,
                            ),
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: _CompetitionMetric(
                              label: 'Participants',
                              value: competition.participantCount,
                              icon: Icons.groups_outlined,
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 24),

                      // Format info
                      Text(
                        'Format',
                        style:
                            Theme.of(context).textTheme.titleMedium?.copyWith(
                                  fontWeight: FontWeight.bold,
                                ),
                      ),
                      const SizedBox(height: 12),
                      Card(
                        elevation: 0,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(18),
                          side: BorderSide(
                            color: Theme.of(context).colorScheme.outlineVariant,
                          ),
                        ),
                        child: Padding(
                          padding: const EdgeInsets.all(16),
                          child: Column(
                            children: [
                              _FormatRow(
                                  label: 'Type',
                                  value: competition.format ?? 'N/A'),
                              _FormatRow(
                                  label: 'Participants',
                                  value: '${competition.participantCount}'),
                              _FormatRow(
                                  label: 'Contests',
                                  value: '${competition.contestCount}'),
                            ],
                          ),
                        ),
                      ),
                      const SizedBox(height: 24),
                    ],
                  ),
                if (hasTable)
                  ListView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.all(16),
                    children: [
                      if (competition.availableTabs.contains('bracket') ||
                          competition.availableTabs.contains('series') ||
                          competition.availableTabs.contains('leaderboard') ||
                          competition.availableTabs.contains('medals')) ...[
                        Text(
                          'Tournament views',
                          style:
                              Theme.of(context).textTheme.titleMedium?.copyWith(
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
                                onPressed: () => context
                                    .push(Routes.bracket(competition.id)),
                                icon: const Icon(Icons.account_tree),
                                label: const Text('Open playoff bracket'),
                              ),
                            if (competition.availableTabs
                                .contains('leaderboard'))
                              FilledButton.tonalIcon(
                                onPressed: () => context.push(
                                    Routes.golfLeaderboard(competition.id)),
                                icon: const Icon(Icons.leaderboard),
                                label: const Text('Open leaderboard'),
                              ),
                            if (competition.sportCode.toLowerCase() ==
                                    'combat' ||
                                competition.sportCode.toLowerCase() == 'mma')
                              FilledButton.tonalIcon(
                                onPressed: () => context
                                    .push(Routes.fightCard(competition.id)),
                                icon: const Icon(Icons.sports_mma),
                                label: const Text('Open fight card'),
                              ),
                          ],
                        ),
                        if (competition.availableTabs.contains('bracket')) ...[
                          const SizedBox(height: 16),
                          CompetitionOutputRenderer(
                            kind: CompetitionOutputKind.bracket,
                            bracket: bracket,
                            onContest: (id) => context.push('/contest/$id'),
                          ),
                        ],
                        if (competition.availableTabs.contains('series')) ...[
                          const SizedBox(height: 16),
                          CompetitionOutputRenderer(
                            kind: CompetitionOutputKind.series,
                            bracket: bracket,
                          ),
                        ],
                        if (competition.availableTabs
                            .contains('leaderboard')) ...[
                          const SizedBox(height: 16),
                          CompetitionOutputRenderer(
                            kind: CompetitionOutputKind.leaderboard,
                            leaderboard: leaderboard,
                            compact: true,
                          ),
                        ],
                        if (competition.availableTabs.contains('medals')) ...[
                          const SizedBox(height: 16),
                          CompetitionOutputRenderer(
                            kind: CompetitionOutputKind.medals,
                            medals: competition.medals,
                          ),
                        ],
                        const SizedBox(height: 24),
                      ],

                      // Schedule
                      Text(
                        'Schedule & Results',
                        style:
                            Theme.of(context).textTheme.titleMedium?.copyWith(
                                  fontWeight: FontWeight.bold,
                                ),
                      ),
                      const SizedBox(height: 12),
                      Card(
                        elevation: 0,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(18),
                          side: BorderSide(
                            color: Theme.of(context).colorScheme.outlineVariant,
                          ),
                        ),
                        child: fixtures.isEmpty
                            ? const Padding(
                                padding: EdgeInsets.all(24),
                                child: Center(
                                    child: Text('No published contests yet')),
                              )
                            : Column(
                                children: fixtures.map((fixture) {
                                  final fixtureResults = results
                                      .where((item) =>
                                          item.contestId == fixture.id)
                                      .toList();
                                  return ListTile(
                                    contentPadding: const EdgeInsets.symmetric(
                                      horizontal: 16,
                                      vertical: 6,
                                    ),
                                    leading: CircleAvatar(
                                      backgroundColor: Theme.of(context)
                                          .colorScheme
                                          .surfaceContainerHighest,
                                      child: Icon(
                                        contestStatusTone(fixture.status) ==
                                                ContestStatusTone.finished
                                            ? Icons.check_circle_outline
                                            : Icons.schedule,
                                      ),
                                    ),
                                    title: Text(fixture.participants.isEmpty
                                        ? fixture.name
                                        : fixture.participants.join(' vs ')),
                                    subtitle: Text(fixtureResults.isEmpty
                                        ? contestStatusLabel(fixture.status)
                                        : fixtureResults
                                            .map((item) =>
                                                '${item.participantName} ${item.score}')
                                            .join(' · ')),
                                    trailing: const Icon(Icons.chevron_right),
                                    onTap: () =>
                                        context.push('/contest/${fixture.id}'),
                                  );
                                }).toList(),
                              ),
                      ),
                      const SizedBox(height: 24),
                    ],
                  ),
                ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.all(16),
                  children: [
                    Container(
                      width: double.infinity,
                      padding: const EdgeInsets.all(16),
                      decoration: BoxDecoration(
                        color: Theme.of(context).colorScheme.surface,
                        borderRadius: BorderRadius.circular(18),
                        border: Border.all(
                          color: Theme.of(context).colorScheme.outlineVariant,
                        ),
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              Icon(
                                Icons.leaderboard_outlined,
                                color: Theme.of(context).colorScheme.primary,
                              ),
                              const SizedBox(width: 8),
                              Text(
                                competition.availableTabs.contains('groups')
                                    ? 'Groups & standings'
                                    : 'Standings',
                                style: Theme.of(context)
                                    .textTheme
                                    .titleMedium
                                    ?.copyWith(fontWeight: FontWeight.w800),
                              ),
                            ],
                          ),
                          const SizedBox(height: 14),
                          CompetitionOutputRenderer(
                            kind: competition.availableTabs.contains('groups')
                                ? CompetitionOutputKind.groups
                                : CompetitionOutputKind.standings,
                            standings: standings,
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  String _formatDate(DateTime? date) {
    return formatPublicDate(date);
  }
}

class _CompetitionMetric extends StatelessWidget {
  final String label;
  final int value;
  final IconData icon;

  const _CompetitionMetric({
    required this.label,
    required this.value,
    required this.icon,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 12),
      decoration: BoxDecoration(
        color: Theme.of(context).colorScheme.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: Theme.of(context).colorScheme.outlineVariant,
        ),
      ),
      child: Column(
        children: [
          Icon(icon, size: 20, color: Theme.of(context).colorScheme.primary),
          const SizedBox(height: 6),
          Text(
            '$value',
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  fontWeight: FontWeight.w900,
                ),
          ),
          Text(
            label,
            style: Theme.of(context).textTheme.labelSmall?.copyWith(
                  color: Theme.of(context).colorScheme.outline,
                ),
          ),
        ],
      ),
    );
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
