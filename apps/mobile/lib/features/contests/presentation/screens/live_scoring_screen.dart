import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../providers/contest_provider.dart';

/// Live scoring screen - real-time score updates
class LiveScoringScreen extends ConsumerStatefulWidget {
  final String contestId;

  const LiveScoringScreen({
    super.key,
    required this.contestId,
  });

  @override
  ConsumerState<LiveScoringScreen> createState() => _LiveScoringScreenState();
}

class _LiveScoringScreenState extends ConsumerState<LiveScoringScreen> {
  bool _isFollowing = true;

  @override
  Widget build(BuildContext context) {
    final contestState = ref.watch(contestDetailProvider(widget.contestId));

    return Scaffold(
      appBar: AppBar(
        title: const Text('Live Scoring'),
        actions: [
          IconButton(
            icon: Icon(
              _isFollowing
                  ? Icons.notifications_active
                  : Icons.notifications_off,
            ),
            onPressed: () {
              setState(() {
                _isFollowing = !_isFollowing;
              });
              // TODO: Toggle notifications
            },
            tooltip: _isFollowing ? 'Following' : 'Not following',
          ),
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () {
              ref
                  .read(contestDetailProvider(widget.contestId).notifier)
                  .refresh();
            },
          ),
        ],
      ),
      body: contestState.isLoading
          ? const Center(child: CircularProgressIndicator())
          : contestState.error != null
              ? _ErrorView(
                  error: contestState.error!,
                  onRetry: () {
                    ref
                        .read(contestDetailProvider(widget.contestId).notifier)
                        .refresh();
                  },
                )
              : _LiveScoringContent(
                  contest: contestState.contest,
                  events: contestState.events,
                ),
    );
  }
}

class _LiveScoringContent extends StatelessWidget {
  final Contest? contest;
  final List<ContestEvent> events;

  const _LiveScoringContent({
    required this.contest,
    required this.events,
  });

  @override
  Widget build(BuildContext context) {
    if (contest == null) {
      return const Center(child: Text('Contest not found'));
    }

    return Column(
      children: [
        // Score header
        _ScoreHeader(contest: contest!),

        // Live indicator
        if (contest!.isLive)
          Container(
            width: double.infinity,
            padding: const EdgeInsets.symmetric(vertical: 8),
            color: Colors.red,
            child: const Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Icon(Icons.circle, color: Colors.white, size: 12),
                SizedBox(width: 8),
                Text(
                  'LIVE',
                  style: TextStyle(
                    color: Colors.white,
                    fontWeight: FontWeight.bold,
                    fontSize: 12,
                  ),
                ),
              ],
            ),
          ),

        // Events timeline
        Expanded(
          child: events.isEmpty
              ? Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Icon(
                        Icons.sports,
                        size: 64,
                        color: Colors.grey.shade300,
                      ),
                      const SizedBox(height: 16),
                      Text(
                        'Waiting for events...',
                        style: TextStyle(
                          color: Colors.grey.shade600,
                          fontSize: 16,
                        ),
                      ),
                    ],
                  ),
                )
              : ListView.builder(
                  padding: const EdgeInsets.all(16),
                  itemCount: events.length,
                  itemBuilder: (context, index) {
                    // Show newest events first
                    final event = events[events.length - 1 - index];
                    return _EventCard(
                      event: event,
                      isHome: event.participantId == contest!.homeParticipantId,
                    );
                  },
                ),
        ),
      ],
    );
  }
}

class _ScoreHeader extends StatelessWidget {
  final Contest contest;

  const _ScoreHeader({required this.contest});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(24),
      decoration: BoxDecoration(
        color: Theme.of(context)
            .colorScheme
            .primaryContainer
            .withValues(alpha: 0.3),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceEvenly,
        children: [
          // Home team
          Expanded(
            child: Column(
              children: [
                CircleAvatar(
                  radius: 36,
                  backgroundColor: Theme.of(context).colorScheme.primary,
                  child: Text(
                    contest.homeParticipantName
                            ?.substring(0, 1)
                            .toUpperCase() ??
                        '?',
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 28,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  contest.homeParticipantName ?? 'TBD',
                  style: const TextStyle(
                    fontWeight: FontWeight.w600,
                    fontSize: 14,
                  ),
                  textAlign: TextAlign.center,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                ),
                const SizedBox(height: 4),
                Text(
                  contest.homeScore?.toString() ?? '-',
                  style: const TextStyle(
                    fontSize: 48,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ],
            ),
          ),

          // VS and segment
          Column(
            children: [
              Text(
                'VS',
                style: TextStyle(
                  fontSize: 20,
                  fontWeight: FontWeight.bold,
                  color: Colors.grey.shade400,
                ),
              ),
              if (contest.currentSegment != null) ...[
                const SizedBox(height: 8),
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                  decoration: BoxDecoration(
                    color: Colors.grey.shade200,
                    borderRadius: BorderRadius.circular(16),
                  ),
                  child: Text(
                    contest.currentSegment!,
                    style: const TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                ),
              ],
            ],
          ),

          // Away team
          Expanded(
            child: Column(
              children: [
                CircleAvatar(
                  radius: 36,
                  backgroundColor: Theme.of(context).colorScheme.secondary,
                  child: Text(
                    contest.awayParticipantName
                            ?.substring(0, 1)
                            .toUpperCase() ??
                        '?',
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 28,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  contest.awayParticipantName ?? 'TBD',
                  style: const TextStyle(
                    fontWeight: FontWeight.w600,
                    fontSize: 14,
                  ),
                  textAlign: TextAlign.center,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                ),
                const SizedBox(height: 4),
                Text(
                  contest.awayScore?.toString() ?? '-',
                  style: const TextStyle(
                    fontSize: 48,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _EventCard extends StatelessWidget {
  final ContestEvent event;
  final bool isHome;

  const _EventCard({
    required this.event,
    required this.isHome,
  });

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      child: ListTile(
        leading: CircleAvatar(
          backgroundColor:
              _getEventColor(event.eventType).withValues(alpha: 0.1),
          child: Icon(
            _getEventIcon(event.eventType),
            color: _getEventColor(event.eventType),
          ),
        ),
        title: Text(
          _formatEventType(event.eventType),
          style: const TextStyle(fontWeight: FontWeight.w600),
        ),
        subtitle: Text(
          '${isHome ? 'Home' : 'Away'} • ${_formatTime(event.timestamp)}',
        ),
        trailing: _buildEventDetails(context),
      ),
    );
  }

  Widget? _buildEventDetails(BuildContext context) {
    if (event.data == null) return null;

    final score = event.data!['score'];
    if (score != null) {
      return Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
        decoration: BoxDecoration(
          color: _getEventColor(event.eventType).withValues(alpha: 0.1),
          borderRadius: BorderRadius.circular(16),
        ),
        child: Text(
          score.toString(),
          style: TextStyle(
            color: _getEventColor(event.eventType),
            fontWeight: FontWeight.bold,
            fontSize: 16,
          ),
        ),
      );
    }

    return null;
  }

  Color _getEventColor(String eventType) {
    switch (eventType.toLowerCase()) {
      case 'goal':
      case 'score':
        return Colors.green;
      case 'foul':
        return Colors.orange;
      case 'card':
        return Colors.red;
      case 'timeout':
        return Colors.blue;
      default:
        return Colors.grey;
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
        return Icons.card_giftcard;
      case 'timeout':
        return Icons.pause;
      case 'substitution':
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
              'Error loading live score',
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
