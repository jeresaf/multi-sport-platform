import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/services/signalr_service.dart'
    show SignalRConnectionStatus;
import '../../../../core/formatters/public_date_time.dart';
import '../../../../core/widgets/contest_status_label.dart';
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
        title: Text(contestState.contest?.name ?? 'Contest'),
        actions: [
          IconButton(
            icon: const Icon(Icons.share),
            onPressed: () {
              // TODO: Share contest
            },
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

    return DefaultTabController(
      length: 3,
      child: Column(
        children: [
          const TabBar(
            tabs: [
              Tab(text: 'Overview'),
              Tab(text: 'Timeline'),
              Tab(text: 'Statistics'),
            ],
          ),
          Expanded(
            child: TabBarView(
              children: [
                ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.all(16),
                  children: [
                    _ContestHeader(contest: contest!),
                    const SizedBox(height: 16),
                    _ScoreDisplay(contest: contest!),
                  ],
                ),
                ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.all(16),
                  children: [
                    _EventTimeline(
                      events: events,
                      homeParticipantId: contest!.homeParticipantId,
                    ),
                  ],
                ),
                ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.all(16),
                  children: [
                    _MatchStats(
                      metadata: contest!.metadata ?? const {},
                      homeParticipantId: contest!.homeParticipantId,
                      awayParticipantId: contest!.awayParticipantId,
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
}

class _ContestHeader extends StatelessWidget {
  final Contest contest;

  const _ContestHeader({required this.contest});

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
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
                    color: Colors.grey.shade100,
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Icon(Icons.sports, size: 14, color: Colors.grey.shade600),
                      const SizedBox(width: 4),
                      Text(
                        contest.sportCode.toUpperCase(),
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
              contest.name,
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
            ),
            if (contest.scheduledAt != null) ...[
              const SizedBox(height: 8),
              Row(
                children: [
                  Icon(Icons.schedule, size: 16, color: Colors.grey.shade600),
                  const SizedBox(width: 4),
                  Text(
                    _formatDateTime(contest.scheduledAt!),
                    style: TextStyle(color: Colors.grey.shade600),
                  ),
                ],
              ),
            ],
            if (contest.currentSegment != null) ...[
              const SizedBox(height: 4),
              Row(
                children: [
                  Icon(Icons.timer, size: 16, color: Colors.grey.shade600),
                  const SizedBox(width: 4),
                  Text(
                    contest.currentSegment!,
                    style: TextStyle(color: Colors.grey.shade600),
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
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceEvenly,
          children: [
            // Home/Away 1
            Expanded(
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
                    style: const TextStyle(
                      fontSize: 36,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ],
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
                    style: const TextStyle(
                      fontSize: 36,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ],
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

  const _EventTimeline({
    required this.events,
    this.homeParticipantId,
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

    return Card(
      child: ListView.separated(
        shrinkWrap: true,
        physics: const NeverScrollableScrollPhysics(),
        itemCount: events.length,
        separatorBuilder: (_, __) => const Divider(height: 1),
        itemBuilder: (context, index) {
          final event = events[index];
          final isHome = event.participantId == homeParticipantId;
          return ListTile(
            leading: CircleAvatar(
              backgroundColor:
                  _getEventColor(event.eventType).withValues(alpha: 0.1),
              child: Icon(
                _getEventIcon(event.eventType),
                color: _getEventColor(event.eventType),
                size: 20,
              ),
            ),
            title: Text([
              _formatEventType(event.eventType),
              if (event.data?['primaryPlayerName'] != null)
                event.data!['primaryPlayerName'],
              if (event.data?['primaryPlayerName'] == null &&
                  event.data?['secondaryParticipantName'] != null)
                event.data!['secondaryParticipantName'],
              if (event.data?['primaryPlayerName'] == null &&
                  event.data?['secondaryParticipantName'] == null &&
                  event.data?['primaryParticipantName'] != null)
                event.data!['primaryParticipantName'],
            ].join(' · ')),
            subtitle: Text([
              if (event.eventType == 'SUBSTITUTION' &&
                  event.data?['secondaryPlayerName'] != null)
                '${event.data!['secondaryPlayerName']} on',
              if (event.data?['clock'] != null) event.data!['clock'],
              if (event.data?['category'] != null)
                _formatEventType(event.data!['category'].toString()),
              if (event.data?['status'] != null &&
                  event.data!['status'] != 'CONFIRMED')
                _formatEventType(event.data!['status'].toString()),
              _formatTime(event.timestamp),
            ].join(' · ')),
            trailing: event.data?['sportCode'] == 'FOOTBALL' &&
                    event.data?['category'] == 'SCORING'
                ? Text(
                    '${event.data?['homeScoreAfter'] ?? 0}–${event.data?['awayScoreAfter'] ?? 0}',
                    style: const TextStyle(fontWeight: FontWeight.bold),
                  )
                : event.participantId != null
                ? Text(
                    isHome ? 'Home' : 'Away',
                    style: TextStyle(color: Colors.grey.shade600),
                  )
                : null,
          );
        },
      ),
    );
  }

  Color _getEventColor(String eventType) {
    switch (eventType.toLowerCase()) {
      case 'goal':
      case 'score':
        return Colors.green;
      case 'foul':
        return Colors.orange;
      case 'card':
      case 'yellow_card':
      case 'second_yellow_card':
      case 'red_card':
        return Colors.red;
      default:
        return Colors.blue;
    }
  }

  IconData _getEventIcon(String eventType) {
    switch (eventType.toLowerCase()) {
      case 'goal':
      case 'score':
        return Icons.sports_soccer;
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
