import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Participant detail screen - shows team or athlete info
class ParticipantDetailScreen extends ConsumerWidget {
  final String participantId;

  const ParticipantDetailScreen({
    super.key,
    required this.participantId,
  });

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    // TODO: Implement participant provider
    return Scaffold(
      appBar: AppBar(
        title: const Text('Participant'),
        actions: [
          IconButton(
            icon: const Icon(Icons.share),
            onPressed: () {
              // TODO: Share participant
            },
          ),
        ],
      ),
      body: const _ParticipantContent(),
    );
  }
}

class _ParticipantContent extends StatelessWidget {
  const _ParticipantContent();

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        // Profile header
        Card(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              children: [
                CircleAvatar(
                  radius: 48,
                  backgroundColor: Theme.of(context).colorScheme.primary,
                  child: const Icon(
                    Icons.person,
                    size: 48,
                    color: Colors.white,
                  ),
                ),
                const SizedBox(height: 16),
                Text(
                  'Participant Name',
                  style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                        fontWeight: FontWeight.bold,
                      ),
                ),
                const SizedBox(height: 4),
                Text(
                  'Team / Club',
                  style: TextStyle(
                    color: Colors.grey.shade600,
                    fontSize: 16,
                  ),
                ),
                const SizedBox(height: 16),
                const Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    _StatChip(label: 'Matches', value: '0'),
                    SizedBox(width: 16),
                    _StatChip(label: 'Wins', value: '0'),
                    SizedBox(width: 16),
                    _StatChip(label: 'Losses', value: '0'),
                  ],
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 24),

        // Upcoming matches
        Text(
          'Upcoming Matches',
          style: Theme.of(context).textTheme.titleMedium?.copyWith(
                fontWeight: FontWeight.bold,
              ),
        ),
        const SizedBox(height: 12),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Center(
              child: Text(
                'No upcoming matches',
                style: TextStyle(color: Colors.grey.shade600),
              ),
            ),
          ),
        ),
        const SizedBox(height: 24),

        // Recent results
        Text(
          'Recent Results',
          style: Theme.of(context).textTheme.titleMedium?.copyWith(
                fontWeight: FontWeight.bold,
              ),
        ),
        const SizedBox(height: 12),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Center(
              child: Text(
                'No recent results',
                style: TextStyle(color: Colors.grey.shade600),
              ),
            ),
          ),
        ),
        const SizedBox(height: 24),

        // Statistics
        Text(
          'Statistics',
          style: Theme.of(context).textTheme.titleMedium?.copyWith(
                fontWeight: FontWeight.bold,
              ),
        ),
        const SizedBox(height: 12),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Center(
              child: Text(
                'No statistics available',
                style: TextStyle(color: Colors.grey.shade600),
              ),
            ),
          ),
        ),
      ],
    );
  }
}

class _StatChip extends StatelessWidget {
  final String label;
  final String value;

  const _StatChip({
    required this.label,
    required this.value,
  });

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text(
          value,
          style: const TextStyle(
            fontSize: 24,
            fontWeight: FontWeight.bold,
          ),
        ),
        Text(
          label,
          style: TextStyle(
            color: Colors.grey.shade600,
            fontSize: 12,
          ),
        ),
      ],
    );
  }
}
