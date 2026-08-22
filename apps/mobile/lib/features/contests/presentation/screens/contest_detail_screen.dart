import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../../core/services/signalr_service.dart'
    show SignalRConnectionStatus;
import '../../../../core/formatters/public_date_time.dart';
import '../../../../core/widgets/contest_status_label.dart';
import '../../../../core/router/app_router.dart';
import '../../../supporter/data/supporter_share_service.dart';
import '../providers/contest_provider.dart';

/// Contest detail screen - shows contest info, timeline, and live updates
class ContestDetailScreen extends ConsumerWidget {
  final String contestId;

  const ContestDetailScreen({
    super.key,
    required this.contestId,
  });

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final contestState = ref.watch(contestDetailProvider(contestId));
    final connectionStatus =
        ref.watch(signalRConnectionStatusProvider).valueOrNull ??
            SignalRConnectionStatus.disconnected;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Match centre'),
        actions: [
          IconButton(
            tooltip: 'Share contest',
            icon: const Icon(Icons.share),
            onPressed: contestState.contest == null
                ? null
                : () => supporterShareService.share(
                      title: contestState.contest!.name,
                      description: _contestParticipants(contestState.contest!),
                      path: Routes.contestDetail(contestId),
                    ),
          ),
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () {
              ref.read(contestDetailProvider(contestId).notifier).refresh();
            },
          ),
        ],
      ),
      body: Column(
        children: [
          if (connectionStatus != SignalRConnectionStatus.connected)
            _LiveConnectionBanner(status: connectionStatus),
          Expanded(
            child: contestState.isLoading
                ? const Center(child: CircularProgressIndicator())
                : contestState.error != null
                    ? _ErrorView(
                        error: contestState.error!,
                        onRetry: () {
                          ref
                              .read(contestDetailProvider(contestId).notifier)
                              .refresh();
                        },
                      )
                    : RefreshIndicator(
                        onRefresh: () async {
                          await ref
                              .read(contestDetailProvider(contestId).notifier)
                              .refresh();
                        },
                        child: _ContestDetailContent(
                          contest: contestState.contest,
                          events: contestState.events,
                        ),
                      ),
          ),
        ],
      ),
    );
  }
}

String _contestParticipants(Contest contest) {
  final home = contest.homeParticipantName;
  final away = contest.awayParticipantName;
  if (home != null && away != null) return '$home vs $away';
  return '${contest.sportCode.toUpperCase()} contest';
}

class _LiveConnectionBanner extends StatelessWidget {
  final SignalRConnectionStatus status;

  const _LiveConnectionBanner({required this.status});

  @override
  Widget build(BuildContext context) {
    final reconnecting = status == SignalRConnectionStatus.reconnecting;
    final connecting = status == SignalRConnectionStatus.connecting;
    final message = reconnecting
        ? 'Live updates interrupted. Reconnecting…'
        : connecting
            ? 'Connecting to live updates…'
            : 'Live updates unavailable. Pull to refresh.';
    return Material(
      color: reconnecting || connecting
          ? Theme.of(context).colorScheme.tertiaryContainer
          : Theme.of(context).colorScheme.errorContainer,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        child: Row(
          children: [
            if (reconnecting || connecting) ...[
              const SizedBox.square(
                dimension: 16,
                child: CircularProgressIndicator(strokeWidth: 2),
              ),
            ] else
              const Icon(Icons.cloud_off_outlined, size: 18),
            const SizedBox(width: 8),
            Expanded(child: Text(message)),
          ],
        ),
      ),
    );
  }
}

class _ContestDetailContent extends StatelessWidget {
  final Contest? contest;
  final List<ContestEvent> events;

  const _ContestDetailContent({
    required this.contest,
    required this.events,
  });

  @override
  Widget build(BuildContext context) {
    if (contest == null) {
      return const Center(child: Text('Contest not found'));
    }
    final tabs = contest!.availableTabs.isEmpty
        ? const ['overview']
        : contest!.availableTabs;

    return DefaultTabController(
      length: tabs.length,
      child: Column(
        children: [
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
              tabs: tabs
                  .map((tab) => Tab(
                        text: contest!.tabLabels[tab] ?? _tabLabel(tab),
                      ))
                  .toList(),
            ),
          ),
          Expanded(
            child: TabBarView(
              children: tabs.map((tab) {
                if (tab == 'overview') {
                  return ListView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.all(16),
                    children: [
                      _ContestHeader(contest: contest!),
                      const SizedBox(height: 16),
                      _ScoreDisplay(contest: contest!),
                    ],
                  );
                }
                if (tab == 'timeline') {
                  return ListView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.all(16),
                    children: [
                      _EventTimeline(
                        events: events,
                        homeParticipantId: contest!.homeParticipantId,
                        sportCode: contest!.sportCode,
                      ),
                    ],
                  );
                }
                if (tab == 'statistics') {
                  return ListView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.all(16),
                    children: [
                      _MatchStats(
                        metadata: contest!.metadata ?? const {},
                        homeParticipantId: contest!.homeParticipantId,
                        awayParticipantId: contest!.awayParticipantId,
                      ),
                    ],
                  );
                }
                return ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.all(16),
                  children: [_ContestDetails(contest: contest!)],
                );
              }).toList(),
            ),
          ),
        ],
      ),
    );
  }

  static String _tabLabel(String tab) => tab
      .replaceAll('_', ' ')
      .split(' ')
      .map((word) =>
          word.isEmpty ? word : '${word[0].toUpperCase()}${word.substring(1)}')
      .join(' ');
}

class _ContestHeader extends StatelessWidget {
  final Contest contest;

  const _ContestHeader({required this.contest});

  @override
  Widget build(BuildContext context) {
    return Container(
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
                ContestStatusLabel(status: contest.status),
                const Spacer(),
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  decoration: BoxDecoration(
                    color: Colors.white.withValues(alpha: 0.14),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Icon(Icons.sports, size: 14, color: Colors.white70),
                      const SizedBox(width: 4),
                      Text(
                        contest.sportCode.toUpperCase(),
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
              contest.name,
              style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                    fontWeight: FontWeight.w900,
                    color: Colors.white,
                  ),
            ),
            if (contest.scheduledAt != null) ...[
              const SizedBox(height: 8),
              Row(
                children: [
                  const Icon(Icons.schedule, size: 16, color: Colors.white70),
                  const SizedBox(width: 4),
                  Text(
                    _formatDateTime(contest.scheduledAt!),
                    style: const TextStyle(color: Colors.white70),
                  ),
                ],
              ),
            ],
            if (contest.currentSegment != null) ...[
              const SizedBox(height: 4),
              Row(
                children: [
                  const Icon(Icons.timer, size: 16, color: Colors.white70),
                  const SizedBox(width: 4),
                  Text(
                    contest.currentSegment!,
                    style: const TextStyle(color: Colors.white70),
                  ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }

  String _formatDateTime(DateTime dateTime) {
    return formatPublicDateTime(dateTime);
  }
}

class _ScoreDisplay extends StatelessWidget {
  final Contest contest;

  const _ScoreDisplay({required this.contest});

  @override
  Widget build(BuildContext context) {
    return Card(
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(22),
        side: BorderSide(
          color: Theme.of(context).colorScheme.outlineVariant,
        ),
      ),
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceEvenly,
          children: [
            // Home/Away 1
            Expanded(
              child: InkWell(
                borderRadius: BorderRadius.circular(16),
                onTap: contest.homeParticipantId == null
                    ? null
                    : () => context.push(
                          '/participant/${contest.homeParticipantId}',
                        ),
                child: Column(
                  children: [
                    CircleAvatar(
                      radius: 32,
                      backgroundColor: Colors.grey.shade200,
                      child: Text(
                        contest.homeParticipantName
                                ?.substring(0, 1)
                                .toUpperCase() ??
                            '?',
                        style: const TextStyle(
                          fontSize: 24,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      contest.homeParticipantName ?? 'TBD',
                      style: const TextStyle(fontWeight: FontWeight.w600),
                      textAlign: TextAlign.center,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                    ),
                    const SizedBox(height: 4),
                    Text(
                      contest.homeScoreDisplay ??
                          contest.homeScore?.toString() ??
                          '-',
                      style: TextStyle(
                        fontSize: 42,
                        fontWeight: FontWeight.w900,
                        color: Theme.of(context).colorScheme.primary,
                      ),
                    ),
                  ],
                ),
              ),
            ),

            // VS divider
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: Text(
                'VS',
                style: TextStyle(
                  fontSize: 18,
                  fontWeight: FontWeight.bold,
                  color: Colors.grey.shade400,
                ),
              ),
            ),

            // Home/Away 2
            Expanded(
              child: InkWell(
                borderRadius: BorderRadius.circular(16),
                onTap: contest.awayParticipantId == null
                    ? null
                    : () => context.push(
                          '/participant/${contest.awayParticipantId}',
                        ),
                child: Column(
                  children: [
                    CircleAvatar(
                      radius: 32,
                      backgroundColor: Colors.grey.shade200,
                      child: Text(
                        contest.awayParticipantName
                                ?.substring(0, 1)
                                .toUpperCase() ??
                            '?',
                        style: const TextStyle(
                          fontSize: 24,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      contest.awayParticipantName ?? 'TBD',
                      style: const TextStyle(fontWeight: FontWeight.w600),
                      textAlign: TextAlign.center,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                    ),
                    const SizedBox(height: 4),
                    Text(
                      contest.awayScoreDisplay ??
                          contest.awayScore?.toString() ??
                          '-',
                      style: TextStyle(
                        fontSize: 42,
                        fontWeight: FontWeight.w900,
                        color: Theme.of(context).colorScheme.primary,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _EventTimeline extends StatelessWidget {
  final List<ContestEvent> events;
  final String? homeParticipantId;
  final String sportCode;

  const _EventTimeline({
    required this.events,
    this.homeParticipantId,
    required this.sportCode,
  });

  @override
  Widget build(BuildContext context) {
    if (events.isEmpty) {
      return Card(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Center(
            child: Text(
              'No events yet',
              style: TextStyle(color: Colors.grey.shade600),
            ),
          ),
        ),
      );
    }

    final groups = _eventGroups();
    return Card(
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(18),
        side: BorderSide(
          color: Theme.of(context).colorScheme.outlineVariant,
        ),
      ),
      child: ListView.separated(
        shrinkWrap: true,
        physics: const NeverScrollableScrollPhysics(),
        itemCount: groups.length,
        separatorBuilder: (_, __) => const Divider(height: 1),
        itemBuilder: (context, index) {
          final play = groups[index];
          final event = play.firstWhere(
            (item) => item.data?['category'] == 'SCORING',
            orElse: () => play.first,
          );
          final assist =
              play.where((item) => item.eventType == 'ASSIST').firstOrNull;
          return _buildTimelineEvent(context, event, assist);
        },
      ),
    );
  }

  Widget _buildTimelineEvent(
    BuildContext context,
    ContestEvent event,
    ContestEvent? assist,
  ) {
    final playerName = event.data?['primaryPlayerName']?.toString().trim();
    final hasPlayer = playerName?.isNotEmpty == true;
    final eventLabel = _formatEventType(event.eventType);
    final clock = _eventClock(event);

    // Lifecycle events (kick-off, interval, full time, and similar events) do
    // not have an actor. Keep those deliberately compact and neutral rather
    // than attributing them to whichever participant carried the event.
    if (!hasPlayer) {
      return Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(clock, style: Theme.of(context).textTheme.labelMedium),
            const SizedBox(width: 10),
            Icon(
              _getEventIcon(event.eventType),
              size: 18,
              color: Theme.of(context).colorScheme.onSurfaceVariant,
            ),
            const SizedBox(width: 8),
            Flexible(
              child: Text(
                eventLabel,
                style: const TextStyle(fontWeight: FontWeight.w600),
              ),
            ),
          ],
        ),
      );
    }

    final isHome = event.participantId == homeParticipantId;
    final colors = Theme.of(context).colorScheme;
    final sideColor = isHome ? colors.primary : colors.tertiary;
    final details = <String>[
      eventLabel,
      if (assist?.data?['primaryPlayerName']?.toString().trim().isNotEmpty ==
          true)
        'Assist: ${assist!.data!['primaryPlayerName']}',
      if (event.eventType.toUpperCase() == 'SUBSTITUTION' &&
          event.data?['secondaryPlayerName']?.toString().trim().isNotEmpty ==
              true)
        '${event.data!['secondaryPlayerName']} on',
      if (event.data?['status'] != null &&
          event.data!['status'].toString().toUpperCase() != 'CONFIRMED')
        _formatEventType(event.data!['status'].toString()),
    ];
    final score = event.data?['category'] == 'SCORING'
        ? '${event.data?['homeScoreAfter'] ?? 0}–${event.data?['awayScoreAfter'] ?? 0}'
        : null;
    final actor = Expanded(
      child: Column(
        crossAxisAlignment:
            isHome ? CrossAxisAlignment.start : CrossAxisAlignment.end,
        children: [
          Text(
            playerName!,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            textAlign: isHome ? TextAlign.left : TextAlign.right,
            style: const TextStyle(fontWeight: FontWeight.w700),
          ),
          Text(
            details.join(' · '),
            textAlign: isHome ? TextAlign.left : TextAlign.right,
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ],
      ),
    );
    final icon = CircleAvatar(
      radius: 18,
      backgroundColor: sideColor.withValues(alpha: 0.12),
      child: Icon(_getEventIcon(event.eventType), color: sideColor, size: 18),
    );
    final time = SizedBox(
      width: 46,
      child: Text(
        clock,
        textAlign: isHome ? TextAlign.left : TextAlign.right,
        style: Theme.of(context).textTheme.labelMedium,
      ),
    );
    final scoreLabel = score == null
        ? const SizedBox(width: 36)
        : SizedBox(
            width: 48,
            child: Text(
              score,
              textAlign: isHome ? TextAlign.right : TextAlign.left,
              style: TextStyle(color: sideColor, fontWeight: FontWeight.w800),
            ),
          );

    return ColoredBox(
      color: sideColor.withValues(alpha: 0.045),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
        child: Row(
          children: isHome
              ? [time, icon, const SizedBox(width: 10), actor, scoreLabel]
              : [scoreLabel, actor, const SizedBox(width: 10), icon, time],
        ),
      ),
    );
  }

  String _eventClock(ContestEvent event) {
    final clock = event.data?['clock']?.toString().trim();
    return clock?.isNotEmpty == true ? clock! : _formatTime(event.timestamp);
  }

  List<List<ContestEvent>> _eventGroups() {
    if (sportCode != 'basketball') {
      return events.map((event) => [event]).toList();
    }
    final groups = <String, List<ContestEvent>>{};
    for (final event in events) {
      final playId = event.data?['playId']?.toString();
      final key = playId == null || playId.isEmpty ? event.id : playId;
      groups.putIfAbsent(key, () => []).add(event);
    }
    return groups.values.toList();
  }

  IconData _getEventIcon(String eventType) {
    switch (eventType.toLowerCase()) {
      case 'goal':
      case 'score':
        return Icons.sports_soccer;
      case 'try':
      case 'penalty_try':
      case 'conversion_made':
      case 'penalty_goal':
      case 'drop_goal':
        return Icons.sports_rugby;
      case 'foul':
        return Icons.warning;
      case 'card':
      case 'yellow_card':
      case 'second_yellow_card':
      case 'red_card':
        return Icons.card_giftcard;
      case 'substitution':
      case 'player_in':
      case 'player_out':
        return Icons.swap_horiz;
      default:
        return Icons.info;
    }
  }

  String _formatEventType(String eventType) {
    return eventType.replaceAll('_', ' ').split(' ').map((word) {
      if (word.isEmpty) return word;
      return word[0].toUpperCase() + word.substring(1).toLowerCase();
    }).join(' ');
  }

  String _formatTime(DateTime timestamp) {
    return '${timestamp.hour}:${timestamp.minute.toString().padLeft(2, '0')}';
  }
}

class _ContestDetails extends StatelessWidget {
  final Contest contest;

  const _ContestDetails({required this.contest});

  @override
  Widget build(BuildContext context) => Card(
        elevation: 0,
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Match details',
                  style: Theme.of(context).textTheme.titleLarge),
              const SizedBox(height: 16),
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: const Icon(Icons.sports),
                title: const Text('Sport'),
                subtitle: Text(contest.sportCode.toUpperCase()),
              ),
              if (contest.scheduledAt != null)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  leading: const Icon(Icons.schedule),
                  title: const Text('Scheduled'),
                  subtitle: Text(formatPublicDateTime(contest.scheduledAt!)),
                ),
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: const Icon(Icons.flag_outlined),
                title: const Text('Status'),
                subtitle: Text(contest.status),
              ),
            ],
          ),
        ),
      );
}

class _MatchStats extends StatelessWidget {
  final Map<String, dynamic> metadata;
  final String? homeParticipantId;
  final String? awayParticipantId;

  const _MatchStats({
    required this.metadata,
    this.homeParticipantId,
    this.awayParticipantId,
  });

  @override
  Widget build(BuildContext context) {
    final stats = metadata['stats'] as Map<String, dynamic>? ?? {};
    final breakdown = stats['eventBreakdown'] is List
        ? (stats['eventBreakdown'] as List)
            .whereType<Map>()
            .map((item) => Map<String, dynamic>.from(item))
            .toList()
        : <Map<String, dynamic>>[];

    if (stats.isEmpty ||
        (breakdown.isEmpty && (stats['totalEvents'] as num? ?? 0) == 0)) {
      return const Card(
        child: Padding(
          padding: EdgeInsets.all(24),
          child: Column(
            children: [
              Icon(Icons.query_stats_outlined, size: 36),
              SizedBox(height: 8),
              Text('Statistics will appear when match events are recorded.'),
            ],
          ),
        ),
      );
    }

    return Card(
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
            if (breakdown.isNotEmpty)
              const Padding(
                padding: EdgeInsets.only(bottom: 8),
                child: Row(
                  children: [
                    Expanded(child: Text('Home', textAlign: TextAlign.center)),
                    Expanded(
                      flex: 2,
                      child: Text('Event', textAlign: TextAlign.center),
                    ),
                    Expanded(child: Text('Away', textAlign: TextAlign.center)),
                  ],
                ),
              ),
            ...breakdown.map((entry) {
              final participants = entry['participants'] is List
                  ? (entry['participants'] as List).whereType<Map>()
                  : const Iterable<Map>.empty();
              int participantCount(String? participantId) => participants
                  .where(
                    (item) =>
                        item['participantId']?.toString() == participantId,
                  )
                  .fold(
                    0,
                    (total, item) =>
                        total + ((item['count'] as num?)?.toInt() ?? 0),
                  );
              return Padding(
                padding: const EdgeInsets.symmetric(vertical: 8),
                child: Row(
                  children: [
                    Expanded(
                      child: Text(
                        participantCount(homeParticipantId).toString(),
                        textAlign: TextAlign.center,
                        style: const TextStyle(fontWeight: FontWeight.w600),
                      ),
                    ),
                    Expanded(
                      flex: 2,
                      child: Text(
                        entry['label']?.toString().replaceAll('_', ' ') ??
                            'Event',
                        textAlign: TextAlign.center,
                        style: TextStyle(color: Colors.grey.shade600),
                      ),
                    ),
                    Expanded(
                      child: Text(
                        participantCount(awayParticipantId).toString(),
                        textAlign: TextAlign.center,
                        style: const TextStyle(fontWeight: FontWeight.w600),
                      ),
                    ),
                  ],
                ),
              );
            }),
          ],
        ),
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
              'Error loading contest',
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
