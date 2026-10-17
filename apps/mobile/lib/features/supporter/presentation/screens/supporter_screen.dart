import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../domain/supporter_models.dart';
import '../providers/supporter_provider.dart';
import '../../data/supporter_preferences.dart';
import '../../data/supporter_share_service.dart';
import '../widgets/supporter_contest_card.dart';

class SupporterScreen extends ConsumerWidget {
  final int initialTab;
  final bool liveOnly;

  const SupporterScreen(
      {super.key, this.initialTab = 0, this.liveOnly = false});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final feed = ref.watch(supporterFeedProvider);
    final preferences = ref.watch(supporterPreferencesProvider);
    final scaffold = Scaffold(
      appBar: AppBar(
        title: Text(liveOnly
            ? 'Live'
            : initialTab == 3
                ? 'Favourites'
                : 'Schedule & results'),
        actions: [
          IconButton(
            tooltip: preferences.lowDataMode
                ? 'Disable low-data mode'
                : 'Enable low-data mode',
            onPressed: () => ref
                .read(supporterPreferencesProvider.notifier)
                .setLowDataMode(!preferences.lowDataMode),
            icon: Icon(preferences.lowDataMode
                ? Icons.data_saver_on
                : Icons.data_saver_off),
          ),
        ],
        bottom: liveOnly
            ? null
            : const PreferredSize(
                preferredSize: Size.fromHeight(52),
                child: Padding(
                  padding: EdgeInsets.symmetric(horizontal: 12),
                  child: TabBar(
                    isScrollable: true,
                    dividerColor: Colors.transparent,
                    indicatorSize: TabBarIndicatorSize.tab,
                    tabs: [
                      Tab(text: 'Schedule'),
                      Tab(text: 'Results'),
                      Tab(text: 'Standings'),
                      Tab(text: 'Statistics'),
                      Tab(text: 'Favourites'),
                    ],
                  ),
                ),
              ),
      ),
      body: feed.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => _ErrorView(
          message: error.toString(),
          onRetry: () => ref.read(supporterFeedProvider.notifier).refresh(),
        ),
        data: (data) {
          final fixtures = liveOnly
              ? data.fixtures.where((item) {
                  final status = item.status.toLowerCase();
                  return status == 'live' || status == 'inprogress';
                }).toList()
              : data.fixtures;
          return Column(
            children: [
              if (data.isFromCache)
                MaterialBanner(
                  leading: const Icon(Icons.offline_bolt),
                  content: Text(
                    data.cachedAt == null
                        ? 'Showing saved data'
                        : 'Showing saved data from ${DateFormat.MMMd().add_Hm().format(data.cachedAt!.toLocal())}',
                  ),
                  actions: [
                    TextButton(
                      onPressed: () =>
                          ref.read(supporterFeedProvider.notifier).refresh(),
                      child: const Text('RETRY'),
                    ),
                  ],
                ),
              Expanded(
                child: liveOnly
                    ? _RefreshableList(
                        emptyMessage: 'No contests are live right now',
                        itemCount: fixtures.length,
                        onRefresh: () =>
                            ref.read(supporterFeedProvider.notifier).refresh(),
                        itemBuilder: (context, index) =>
                            _FixtureCard(fixture: fixtures[index]),
                      )
                    : TabBarView(
                        children: [
                          _RefreshableList(
                            emptyMessage: 'No upcoming contests',
                            itemCount: fixtures.length,
                            onRefresh: () => ref
                                .read(supporterFeedProvider.notifier)
                                .refresh(),
                            itemBuilder: (context, index) =>
                                _FixtureCard(fixture: fixtures[index]),
                          ),
                          _RefreshableList(
                            emptyMessage: 'No published results',
                            itemCount: data.results.length,
                            onRefresh: () => ref
                                .read(supporterFeedProvider.notifier)
                                .refresh(),
                            itemBuilder: (context, index) =>
                                _ResultTile(result: data.results[index]),
                          ),
                          _RefreshableList(
                            emptyMessage: 'No standings available',
                            itemCount: data.standings.length,
                            onRefresh: () => ref
                                .read(supporterFeedProvider.notifier)
                                .refresh(),
                            itemBuilder: (context, index) =>
                                _StandingTile(standing: data.standings[index]),
                          ),
                          _StatisticsView(data: data),
                          _FavouritesList(
                            favourites: preferences.favourites.values.toList(),
                          ),
                        ],
                      ),
              ),
            ],
          );
        },
      ),
    );
    if (liveOnly) return scaffold;
    return DefaultTabController(
      length: 5,
      initialIndex: initialTab,
      child: scaffold,
    );
  }
}

class _RefreshableList extends StatelessWidget {
  final int itemCount;
  final String emptyMessage;
  final Future<void> Function() onRefresh;
  final IndexedWidgetBuilder itemBuilder;

  const _RefreshableList({
    required this.itemCount,
    required this.emptyMessage,
    required this.onRefresh,
    required this.itemBuilder,
  });

  @override
  Widget build(BuildContext context) => RefreshIndicator(
        onRefresh: onRefresh,
        child: itemCount == 0
            ? ListView(
                children: [
                  const SizedBox(height: 120),
                  Icon(Icons.sports_score,
                      size: 56, color: Colors.grey.shade400),
                  const SizedBox(height: 12),
                  Center(child: Text(emptyMessage)),
                ],
              )
            : ListView.separated(
                padding: const EdgeInsets.all(12),
                itemCount: itemCount,
                itemBuilder: itemBuilder,
                separatorBuilder: (_, __) => const SizedBox(height: 8),
              ),
      );
}

class _FixtureCard extends ConsumerWidget {
  final SupporterFixture fixture;

  const _FixtureCard({required this.fixture});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final favourite = SupporterFavourite(
      type: 'contest',
      id: fixture.id,
      title: fixture.participants.isEmpty
          ? fixture.name
          : fixture.participants.join(' vs '),
      subtitle: fixture.competitionName,
      route: '/contest/${fixture.id}',
    );
    final isFavourite = ref.watch(
      supporterPreferencesProvider.select(
        (state) => state.isFavourite(favourite.type, favourite.id),
      ),
    );
    return SupporterContestCard(
      fixture: fixture,
      isFavourite: isFavourite,
      onTap: () => context.push('/contest/${fixture.id}'),
      onFavourite: () => ref
          .read(supporterPreferencesProvider.notifier)
          .toggleFavourite(favourite),
      onShare: () => supporterShareService.share(
        title: favourite.title,
        description: fixture.competitionName,
        path: favourite.route,
      ),
    );
  }
}

class _FavouritesList extends ConsumerWidget {
  final List<SupporterFavourite> favourites;

  const _FavouritesList({required this.favourites});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (favourites.isEmpty) {
      return ListView(
        padding: const EdgeInsets.all(24),
        children: [
          const SizedBox(height: 72),
          Icon(
            Icons.favorite_border,
            size: 56,
            color: Theme.of(context).colorScheme.outline,
          ),
          const SizedBox(height: 14),
          Text(
            'Your favourites live here',
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  fontWeight: FontWeight.w800,
                ),
          ),
          const SizedBox(height: 8),
          Text(
            'Follow fixtures, competitions, teams or athletes to reach them quickly and manage alerts.',
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: Theme.of(context).colorScheme.outline,
                ),
          ),
        ],
      );
    }
    return ListView.separated(
      padding: const EdgeInsets.all(12),
      itemCount: favourites.length,
      separatorBuilder: (_, __) => const SizedBox(height: 8),
      itemBuilder: (context, index) {
        final item = favourites[index];
        return Card(
          elevation: 0,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(16),
            side: BorderSide(
              color: Theme.of(context).colorScheme.outlineVariant,
            ),
          ),
          child: ListTile(
            contentPadding: const EdgeInsets.symmetric(
              horizontal: 14,
              vertical: 6,
            ),
            onTap: () => context.push(item.route),
            leading: Icon(
              item.type == 'competition' ? Icons.emoji_events : Icons.sports,
            ),
            title: Text(item.title),
            subtitle: item.subtitle == null ? null : Text(item.subtitle!),
            trailing: Wrap(
              spacing: 2,
              children: [
                IconButton(
                  tooltip: 'Share',
                  onPressed: () => supporterShareService.share(
                    title: item.title,
                    description: item.subtitle,
                    path: item.route,
                  ),
                  icon: const Icon(Icons.share_outlined),
                ),
                IconButton(
                  tooltip: 'Remove',
                  onPressed: () => ref
                      .read(supporterPreferencesProvider.notifier)
                      .toggleFavourite(item),
                  icon: const Icon(Icons.favorite),
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}

class _ResultTile extends StatelessWidget {
  final SupporterResult result;

  const _ResultTile({required this.result});

  @override
  Widget build(BuildContext context) => Card(
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(16),
          side: BorderSide(
            color: Theme.of(context).colorScheme.outlineVariant,
          ),
        ),
        child: ListTile(
          contentPadding: const EdgeInsets.symmetric(
            horizontal: 14,
            vertical: 6,
          ),
          onTap: () => context.push('/contest/${result.contestId}'),
          leading: CircleAvatar(child: Text(result.rank?.toString() ?? '–')),
          title: Text(result.participantName),
          subtitle: Text(result.contestName),
          trailing: Text(
            result.score,
            style: Theme.of(context)
                .textTheme
                .titleMedium
                ?.copyWith(fontWeight: FontWeight.bold),
          ),
        ),
      );
}

class _StandingTile extends StatelessWidget {
  final SupporterStanding standing;

  const _StandingTile({required this.standing});

  @override
  Widget build(BuildContext context) => Card(
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(16),
          side: BorderSide(
            color: Theme.of(context).colorScheme.outlineVariant,
          ),
        ),
        child: ListTile(
          contentPadding: const EdgeInsets.symmetric(
            horizontal: 14,
            vertical: 6,
          ),
          onTap: () => context.push('/competitions/${standing.competitionId}'),
          leading: CircleAvatar(child: Text('${standing.position}')),
          title: Text(standing.participantName),
          subtitle: Text(
            '${standing.competitionName}${standing.groupName == null ? '' : ' • ${standing.groupName}'} · P ${standing.played}  W ${standing.won}  D ${standing.drawn}  L ${standing.lost}',
          ),
          trailing: Text(
            '${standing.points}',
            style: Theme.of(context)
                .textTheme
                .titleMedium
                ?.copyWith(fontWeight: FontWeight.bold),
          ),
        ),
      );
}

class _StatisticsView extends ConsumerWidget {
  final SupporterFeed data;

  const _StatisticsView({required this.data});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final live = data.fixtures.where((fixture) {
      final status = fixture.status.toLowerCase().replaceAll('_', '');
      return status == 'live' || status == 'inprogress';
    }).length;
    final competitions = <String>{
      ...data.fixtures.map((item) => item.competitionId),
      ...data.standings.map((item) => item.competitionId),
    }..removeWhere((value) => value.isEmpty);
    final sports = <String>{
      ...data.fixtures.map((item) => item.sportCode),
    }..removeWhere((value) => value.isEmpty);
    final wins = <String, int>{};
    for (final result in data.results) {
      if (result.rank == 1) {
        wins.update(result.participantName, (value) => value + 1,
            ifAbsent: () => 1);
      }
    }
    final leaders = wins.entries.toList()
      ..sort((left, right) =>
          right.value.compareTo(left.value) != 0
              ? right.value.compareTo(left.value)
              : left.key.compareTo(right.key));

    return RefreshIndicator(
      onRefresh: () => ref.read(supporterFeedProvider.notifier).refresh(),
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Container(
            padding: const EdgeInsets.all(20),
            decoration: BoxDecoration(
              gradient: LinearGradient(
                colors: [
                  Theme.of(context).colorScheme.primary,
                  Theme.of(context).colorScheme.secondary,
                ],
              ),
              borderRadius: BorderRadius.circular(22),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Icon(Icons.insights, color: Colors.white),
                const SizedBox(height: 12),
                Text(
                  'Published performance',
                  style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                        color: Colors.white,
                        fontWeight: FontWeight.w900,
                      ),
                ),
                const SizedBox(height: 6),
                Text(
                  'A current snapshot from public fixtures, results and standings.',
                  style: Theme.of(context)
                      .textTheme
                      .bodyMedium
                      ?.copyWith(color: Colors.white70),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          GridView.count(
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            crossAxisCount: 2,
            childAspectRatio: 1.55,
            mainAxisSpacing: 10,
            crossAxisSpacing: 10,
            children: [
              _MobileMetric(
                  label: 'Live now', value: live, icon: Icons.sensors),
              _MobileMetric(
                  label: 'Fixtures',
                  value: data.fixtures.length,
                  icon: Icons.calendar_month_outlined),
              _MobileMetric(
                  label: 'Results',
                  value: data.results.length,
                  icon: Icons.emoji_events_outlined),
              _MobileMetric(
                  label: 'Tables',
                  value: competitions.length,
                  icon: Icons.table_chart_outlined),
              _MobileMetric(
                  label: 'Sports',
                  value: sports.length,
                  icon: Icons.sports_outlined),
              _MobileMetric(
                  label: 'Entries',
                  value: data.standings.length,
                  icon: Icons.groups_outlined),
            ],
          ),
          const SizedBox(height: 22),
          Text(
            'Recent winners',
            style: Theme.of(context)
                .textTheme
                .titleLarge
                ?.copyWith(fontWeight: FontWeight.w900),
          ),
          const SizedBox(height: 10),
          if (leaders.isEmpty)
            Card(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Text(
                  'Winner statistics will appear after ranked results are published.',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    color: Theme.of(context).colorScheme.outline,
                  ),
                ),
              ),
            )
          else
            ...leaders.take(8).toList().asMap().entries.map((entry) {
              final leader = entry.value;
              return Card(
                child: ListTile(
                  leading: CircleAvatar(child: Text('${entry.key + 1}')),
                  title: Text(leader.key),
                  subtitle: const Text('Published wins'),
                  trailing: Text(
                    '${leader.value}',
                    style: Theme.of(context)
                        .textTheme
                        .titleLarge
                        ?.copyWith(fontWeight: FontWeight.w900),
                  ),
                ),
              );
            }),
        ],
      ),
    );
  }
}

class _MobileMetric extends StatelessWidget {
  final String label;
  final int value;
  final IconData icon;

  const _MobileMetric({
    required this.label,
    required this.value,
    required this.icon,
  });

  @override
  Widget build(BuildContext context) => Card(
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Icon(icon, color: Theme.of(context).colorScheme.primary),
              const Spacer(),
              Text(
                '$value',
                style: Theme.of(context)
                    .textTheme
                    .headlineSmall
                    ?.copyWith(fontWeight: FontWeight.w900),
              ),
              Text(label, style: Theme.of(context).textTheme.labelMedium),
            ],
          ),
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
              const Icon(Icons.cloud_off, size: 56),
              const SizedBox(height: 12),
              const Text('Supporter data is unavailable'),
              const SizedBox(height: 8),
              Text(message, textAlign: TextAlign.center),
              const SizedBox(height: 16),
              FilledButton.icon(
                onPressed: onRetry,
                icon: const Icon(Icons.refresh),
                label: const Text('Try again'),
              ),
            ],
          ),
        ),
      );
}
