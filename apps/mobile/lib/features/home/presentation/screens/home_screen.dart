import 'package:flutter/material.dart';
import '../../../../core/widgets/contest_status_label.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../../core/router/app_router.dart';
import '../../../../core/formatters/public_date_time.dart';
import '../../../auth/presentation/providers/auth_provider.dart';
import '../../../branding/presentation/providers/branding_provider.dart';
import '../../../notifications/presentation/providers/notification_provider.dart';
import '../../../supporter/data/supporter_preferences.dart';
import '../../../supporter/domain/supporter_models.dart';
import '../../../supporter/presentation/providers/supporter_provider.dart';
import '../../../supporter/presentation/widgets/alert_preferences_sheet.dart';

({bool loading, String? error, String structure}) _feedSignal(
    AsyncValue<SupporterFeed> feed) {
  final data = feed.valueOrNull;
  final structure = data == null
      ? ''
      : '${data.isFromCache}|${data.fixtures.map((fixture) => [
            fixture.id,
            fixture.competitionId,
            fixture.competitionName,
            fixture.sportCode,
            fixture.sportName,
            fixture.status,
            fixture.startTime?.toIso8601String() ?? '',
            fixture.participantDetails.map((item) => item.id).join(','),
          ].join(':')).join('|')}';
  return (
    loading: feed.isLoading && !feed.hasValue,
    error: feed.hasError && !feed.hasValue ? feed.error.toString() : null,
    structure: structure,
  );
}

class HomeScreen extends ConsumerStatefulWidget {
  const HomeScreen({super.key, this.initialDate, this.initialSportSlug});

  final DateTime? initialDate;
  final String? initialSportSlug;

  @override
  ConsumerState<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends ConsumerState<HomeScreen> {
  late DateTime selectedDate;
  String filter = 'all';
  String? sport;

  @override
  void initState() {
    super.initState();
    selectedDate = DateUtils.dateOnly(widget.initialDate ?? DateTime.now());
    sport = widget.initialSportSlug;
  }

  @override
  void didUpdateWidget(covariant HomeScreen oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.initialDate != null &&
        !DateUtils.isSameDay(widget.initialDate, selectedDate)) {
      selectedDate = DateUtils.dateOnly(widget.initialDate!);
    }
    if (widget.initialSportSlug != oldWidget.initialSportSlug) {
      sport = widget.initialSportSlug;
    }
  }

  void _changeDate(DateTime value) {
    final next = DateUtils.dateOnly(value);
    setState(() => selectedDate = next);
    final current = GoRouterState.of(context).uri;
    final parameters = Map<String, String>.from(current.queryParameters)
      ..['date'] = DateFormat('yyyy-MM-dd').format(next);
    context.go(current.replace(queryParameters: parameters).toString());
  }

  @override
  Widget build(BuildContext context) {
    final auth = ref.watch(authStateProvider);
    final branding = ref.watch(tenantBrandingProvider).valueOrNull;
    final feedSignal = ref.watch(supporterFeedProvider.select(_feedSignal));
    final feed = ref.read(supporterFeedProvider);
    final unread = ref.watch(unreadNotificationCountProvider);
    final lowData = ref.watch(
      supporterPreferencesProvider.select((state) => state.lowDataMode),
    );
    return Scaffold(
      appBar: AppBar(
        title: Text(branding?.tenantName ?? 'Sports Platform'),
        leading: branding?.logoUrl == null || lowData
            ? IconButton(
                tooltip: 'Change organisation',
                onPressed: () => context.push(Routes.tenant),
                icon: const Icon(Icons.sports),
              )
            : Padding(
                padding: const EdgeInsets.all(8),
                child: InkWell(
                  onTap: () => context.push(Routes.tenant),
                  child: Image.network(
                    branding!.logoUrl!,
                    cacheWidth:
                        (36 * MediaQuery.devicePixelRatioOf(context)).round(),
                    errorBuilder: (_, __, ___) => const Icon(Icons.sports),
                  ),
                ),
              ),
        actions: [
          IconButton(
            tooltip: 'Search sports, teams and competitions',
            onPressed: () => context.push('/search'),
            icon: const Icon(Icons.search),
          ),
          if (auth.isAuthenticated)
            Padding(
              padding: const EdgeInsets.only(right: 6),
              child: Badge(
                alignment: Alignment.topRight,
                offset: const Offset(-3, 6),
                isLabelVisible: unread > 0,
                label: Text(unread > 9 ? '9+' : '$unread'),
                child: IconButton(
                  tooltip: 'Notifications',
                  onPressed: () => context.push('/notifications'),
                  icon: const Icon(Icons.notifications_outlined),
                ),
              ),
            )
          else
            TextButton.icon(
              onPressed: () => context.push(Routes.login),
              icon: const Icon(Icons.login),
              label: const Text('Login'),
            ),
        ],
        bottom: auth.isAuthenticated
            ? PreferredSize(
                preferredSize: const Size.fromHeight(44),
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(16, 0, 8, 8),
                  child: Row(children: [
                    Expanded(
                      child: Text(
                        'Welcome, ${auth.displayName ?? auth.email ?? 'supporter'}',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    if (auth.canScore)
                      TextButton.icon(
                        onPressed: () => context.go('/scoring'),
                        icon: const Icon(Icons.scoreboard_outlined),
                        label: const Text('Scoring'),
                      ),
                  ]),
                ),
              )
            : null,
      ),
      body: feedSignal.loading
          ? const _LoadingFeed()
          : feedSignal.error != null
              ? _FeedMessage(
                  icon: Icons.cloud_off,
                  title: 'Coverage unavailable',
                  detail: feedSignal.error!,
                  action: FilledButton.icon(
                    onPressed: () =>
                        ref.read(supporterFeedProvider.notifier).refresh(),
                    icon: const Icon(Icons.refresh),
                    label: const Text('Try again'),
                  ),
                )
              : _buildFeed(context, feed.valueOrNull ?? const SupporterFeed()),
    );
  }

  Widget _buildFeed(BuildContext context, SupporterFeed data) {
    final sports = <String, String>{};
    for (final fixture in data.fixtures) {
      sports[fixture.sportCode] = fixture.sportName;
    }
    final favouriteKeys = ref.watch(supporterPreferencesProvider
        .select((value) => value.favourites.keys.join('|')));
    final favourites = favouriteKeys.isEmpty
        ? const <String>{}
        : favouriteKeys.split('|').toSet();
    final visible = data.fixtures.where((fixture) {
      final date = fixture.startTime?.toLocal();
      if (date == null || !DateUtils.isSameDay(date, selectedDate)) {
        return false;
      }
      if (sport != null &&
          !_matchesSport(fixture.sportCode, fixture.sportName, sport)) {
        return false;
      }
      final status = normalizeContestStatus(fixture.status);
      if (filter == 'live' && !contestStatusIsLive(fixture.status)) {
        return false;
      }
      if (filter == 'upcoming' &&
          !['notstarted', 'startingsoon', 'scheduled', 'ready', 'checkinopen']
              .contains(status)) {
        return false;
      }
      if (filter == 'finished' &&
          contestStatusTone(fixture.status) != ContestStatusTone.finished) {
        return false;
      }
      if (filter == 'favourites' &&
          !favourites.contains('contest:${fixture.id}') &&
          !favourites.contains('competition:${fixture.competitionId}') &&
          !fixture.participantDetails
              .any((item) => favourites.contains('participant:${item.id}'))) {
        return false;
      }
      return true;
    }).toList();
    final grouped = <String, List<SupporterFixture>>{};
    for (final fixture in visible) {
      grouped.putIfAbsent(fixture.competitionName, () => []).add(fixture);
    }

    return RefreshIndicator(
      onRefresh: () => ref.read(supporterFeedProvider.notifier).refresh(),
      child: CustomScrollView(
        slivers: [
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
              child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Match centre',
                        style: Theme.of(context).textTheme.labelLarge?.copyWith(
                            color: Theme.of(context).colorScheme.primary,
                            fontWeight: FontWeight.bold)),
                    const SizedBox(height: 4),
                    Text('Scores & schedule',
                        style: Theme.of(context)
                            .textTheme
                            .headlineMedium
                            ?.copyWith(fontWeight: FontWeight.w800)),
                    const SizedBox(height: 16),
                    _DateNavigator(
                      date: selectedDate,
                      onChanged: _changeDate,
                    ),
                    const SizedBox(height: 12),
                    SingleChildScrollView(
                      scrollDirection: Axis.horizontal,
                      child: SegmentedButton<String>(
                        segments: const [
                          ButtonSegment(value: 'all', label: Text('All')),
                          ButtonSegment(value: 'live', label: Text('Live')),
                          ButtonSegment(
                              value: 'upcoming', label: Text('Upcoming')),
                          ButtonSegment(
                              value: 'finished', label: Text('Finished')),
                          ButtonSegment(
                              value: 'favourites', label: Text('Favourites')),
                        ],
                        selected: {filter},
                        onSelectionChanged: (value) =>
                            setState(() => filter = value.first),
                      ),
                    ),
                    if (sports.isNotEmpty) ...[
                      const SizedBox(height: 12),
                      SizedBox(
                        height: 42,
                        child: ListView(
                          scrollDirection: Axis.horizontal,
                          children: [
                            ChoiceChip(
                                label: const Text('All sports'),
                                selected: sport == null,
                                onSelected: (_) =>
                                    setState(() => sport = null)),
                            const SizedBox(width: 8),
                            ...sports.entries.expand((entry) => [
                                  ChoiceChip(
                                      label: Text(entry.value),
                                      selected: _matchesSport(
                                        entry.key,
                                        entry.value,
                                        sport,
                                      ),
                                      onSelected: (_) =>
                                          setState(() => sport = entry.key)),
                                  const SizedBox(width: 8),
                                ]),
                          ],
                        ),
                      ),
                    ],
                    if (data.isFromCache)
                      const Padding(
                          padding: EdgeInsets.only(top: 12),
                          child: Row(children: [
                            Icon(Icons.offline_bolt, size: 18),
                            SizedBox(width: 6),
                            Text('Showing saved data')
                          ])),
                  ]),
            ),
          ),
          if (grouped.isEmpty)
            const SliverFillRemaining(
              hasScrollBody: false,
              child: _FeedMessage(
                  icon: Icons.event_busy,
                  title: 'No contests found',
                  detail: 'Try another date, sport or filter.'),
            )
          else
            ...grouped.entries.expand((group) => [
                  SliverToBoxAdapter(
                      child: Padding(
                          padding: const EdgeInsets.fromLTRB(16, 20, 16, 8),
                          child: Text(group.key,
                              style: Theme.of(context)
                                  .textTheme
                                  .titleLarge
                                  ?.copyWith(fontWeight: FontWeight.bold)))),
                  SliverPadding(
                    padding: const EdgeInsets.symmetric(horizontal: 16),
                    sliver:
                        SliverLayoutBuilder(builder: (context, constraints) {
                      final columns = constraints.crossAxisExtent >= 900
                          ? 3
                          : constraints.crossAxisExtent >= 600
                              ? 2
                              : 1;
                      return SliverGrid(
                        gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
                            crossAxisCount: columns,
                            mainAxisSpacing: 10,
                            crossAxisSpacing: 10,
                            mainAxisExtent: 196),
                        delegate: SliverChildBuilderDelegate(
                            (context, index) =>
                                _ContestCard(fixtureId: group.value[index].id),
                            childCount: group.value.length),
                      );
                    }),
                  ),
                ]),
          const SliverToBoxAdapter(child: SizedBox(height: 24)),
        ],
      ),
    );
  }
}

class _DateNavigator extends StatelessWidget {
  final DateTime date;
  final ValueChanged<DateTime> onChanged;
  const _DateNavigator({required this.date, required this.onChanged});

  @override
  Widget build(BuildContext context) {
    final tablet = MediaQuery.sizeOf(context).shortestSide >= 600;
    final radius = tablet ? 2 : 1;
    final days = [
      for (var offset = -radius; offset <= radius; offset++)
        date.add(Duration(days: offset)),
    ];
    return GestureDetector(
      onHorizontalDragEnd: (details) {
        final velocity = details.primaryVelocity ?? 0;
        if (velocity.abs() < 200) return;
        onChanged(date.add(Duration(days: velocity < 0 ? 1 : -1)));
      },
      child: Card(
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 6),
          child: Column(children: [
            Row(children: [
              IconButton(
                  tooltip: 'Previous day',
                  onPressed: () =>
                      onChanged(date.subtract(const Duration(days: 1))),
                  icon: const Icon(Icons.chevron_left)),
              Expanded(
                child: TextButton.icon(
                  onPressed: () async {
                    final selected = await showDatePicker(
                        context: context,
                        firstDate: DateTime(2020),
                        lastDate: DateTime(2035),
                        initialDate: date);
                    if (selected != null) onChanged(selected);
                  },
                  icon: const Icon(Icons.calendar_today_outlined, size: 18),
                  label: Text(DateUtils.isSameDay(date, DateTime.now())
                      ? 'Today'
                      : DateFormat.MMMEd().format(date)),
                ),
              ),
              IconButton(
                  tooltip: 'Next day',
                  onPressed: () => onChanged(date.add(const Duration(days: 1))),
                  icon: const Icon(Icons.chevron_right)),
            ]),
            Row(
              children: days
                  .map((item) => Expanded(
                        child: Padding(
                          padding: const EdgeInsets.symmetric(horizontal: 2),
                          child: TextButton(
                            onPressed: () => onChanged(item),
                            style: TextButton.styleFrom(
                              backgroundColor: DateUtils.isSameDay(item, date)
                                  ? Theme.of(context)
                                      .colorScheme
                                      .primaryContainer
                                  : null,
                              padding: const EdgeInsets.symmetric(vertical: 6),
                            ),
                            child: Column(children: [
                              Text(DateFormat.E().format(item),
                                  style:
                                      Theme.of(context).textTheme.labelSmall),
                              Text('${item.day}',
                                  style:
                                      Theme.of(context).textTheme.labelLarge),
                            ]),
                          ),
                        ),
                      ))
                  .toList(),
            ),
          ]),
        ),
      ),
    );
  }
}

bool _matchesSport(String code, String name, String? selected) {
  if (selected == null) return false;
  final normalized = selected.toLowerCase();
  return code.toLowerCase() == normalized || _sportSlug(name) == normalized;
}

String _sportSlug(String value) => value
    .toLowerCase()
    .replaceAll(RegExp(r'[^a-z0-9]+'), '-')
    .replaceAll(RegExp(r'^-|-$'), '');

class _ContestCard extends ConsumerWidget {
  final String fixtureId;
  const _ContestCard({required this.fixtureId});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final fixture = ref.watch(supporterFixtureProvider(fixtureId));
    if (fixture == null) return const SizedBox.shrink();
    final favourite = SupporterFavourite(
        type: 'contest',
        id: fixture.id,
        title: fixture.participants.isEmpty
            ? fixture.name
            : fixture.participants.join(' vs '),
        subtitle: fixture.competitionName,
        route: '/contest/${fixture.id}');
    final saved = ref.watch(supporterPreferencesProvider
        .select((value) => value.isFavourite('contest', fixture.id)));
    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => context.push('/contest/${fixture.id}'),
        child: Padding(
          padding: const EdgeInsets.all(14),
          child:
              Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Row(children: [
              Expanded(
                  child: Text(fixture.sportName.toUpperCase(),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: Theme.of(context).textTheme.labelSmall?.copyWith(
                          color: Theme.of(context).colorScheme.primary,
                          fontWeight: FontWeight.bold))),
              ContestStatusLabel(status: fixture.status, compact: true),
            ]),
            const SizedBox(height: 10),
            if (fixture.participantDetails.isEmpty)
              Expanded(
                  child: Text(fixture.name,
                      style: Theme.of(context)
                          .textTheme
                          .titleMedium
                          ?.copyWith(fontWeight: FontWeight.bold)))
            else
              Expanded(
                  child: Column(children: [
                ...fixture.participantDetails.take(2).map((participant) =>
                    Padding(
                      padding: const EdgeInsets.symmetric(vertical: 4),
                      child: InkWell(
                        onTap: participant.id.isEmpty
                            ? null
                            : () =>
                                context.push('/participant/${participant.id}'),
                        borderRadius: BorderRadius.circular(8),
                        child: Row(children: [
                          CircleAvatar(
                              radius: 13,
                              child: Text(
                                  participant.name
                                      .substring(0,
                                          participant.name.length.clamp(1, 2))
                                      .toUpperCase(),
                                  style: const TextStyle(fontSize: 9))),
                          const SizedBox(width: 8),
                          Expanded(
                              child: Text(participant.name,
                                  maxLines: 1,
                                  overflow: TextOverflow.ellipsis,
                                  style: const TextStyle(
                                      fontWeight: FontWeight.w600))),
                          Text(participant.result,
                              style:
                                  const TextStyle(fontWeight: FontWeight.bold))
                        ]),
                      ),
                    )),
                if (fixture.participantDetails.length > 2)
                  Align(
                    alignment: Alignment.centerLeft,
                    child: Padding(
                      padding: const EdgeInsets.only(top: 2, left: 34),
                      child: Text(
                        '+${fixture.participantDetails.length - 2} more',
                        style: Theme.of(context).textTheme.labelSmall,
                      ),
                    ),
                  ),
              ])),
            Row(children: [
              const Icon(Icons.schedule, size: 15),
              const SizedBox(width: 4),
              Text(formatPublicDateTime(fixture.startTime),
                  style: Theme.of(context).textTheme.bodySmall),
              const Spacer(),
              IconButton(
                  visualDensity: VisualDensity.compact,
                  tooltip:
                      saved ? 'Choose alerts' : 'Favourite to enable alerts',
                  onPressed: saved
                      ? () => showSupporterAlertPreferences(context, ref,
                          favourite: favourite, sportCode: fixture.sportCode)
                      : null,
                  icon: Icon(
                    ref.watch(supporterPreferencesProvider.select((value) =>
                            value.alertsFor('contest', fixture.id).isNotEmpty))
                        ? Icons.notifications_active
                        : Icons.notifications_none,
                  )),
              IconButton(
                  visualDensity: VisualDensity.compact,
                  tooltip: saved ? 'Remove favourite' : 'Add favourite',
                  onPressed: () => ref
                      .read(supporterPreferencesProvider.notifier)
                      .toggleFavourite(favourite),
                  icon: Icon(saved ? Icons.favorite : Icons.favorite_border,
                      color: saved
                          ? Theme.of(context).colorScheme.tertiary
                          : null)),
            ]),
          ]),
        ),
      ),
    );
  }
}

class _LoadingFeed extends StatelessWidget {
  const _LoadingFeed();
  @override
  Widget build(BuildContext context) =>
      ListView(padding: const EdgeInsets.all(16), children: [
        Container(
            height: 48,
            decoration: BoxDecoration(
                color: Theme.of(context).colorScheme.surfaceContainerHighest,
                borderRadius: BorderRadius.circular(12))),
        const SizedBox(height: 20),
        ...List.generate(3, (_) => const Card(child: SizedBox(height: 180)))
      ]);
}

class _FeedMessage extends StatelessWidget {
  final IconData icon;
  final String title;
  final String detail;
  final Widget? action;
  const _FeedMessage(
      {required this.icon,
      required this.title,
      required this.detail,
      this.action});
  @override
  Widget build(BuildContext context) => Center(
      child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            Icon(icon, size: 52, color: Theme.of(context).colorScheme.outline),
            const SizedBox(height: 12),
            Text(title,
                style: Theme.of(context)
                    .textTheme
                    .titleMedium
                    ?.copyWith(fontWeight: FontWeight.bold)),
            const SizedBox(height: 4),
            Text(detail, textAlign: TextAlign.center),
            if (action != null) ...[const SizedBox(height: 16), action!]
          ])));
}
