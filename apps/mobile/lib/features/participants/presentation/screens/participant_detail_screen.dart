import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../supporter/data/supporter_preferences.dart';
import '../../../supporter/data/supporter_share_service.dart';
import '../providers/participant_provider.dart';

class ParticipantDetailScreen extends ConsumerWidget {
  final String participantId;
  const ParticipantDetailScreen({super.key, required this.participantId});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(participantDetailProvider(participantId));
    final participant = state.participant;
    final favourite = participant == null
        ? null
        : SupporterFavourite(
            type: 'participant',
            id: participant.id,
            title: participant.name,
            subtitle: participant.type,
            route: '/participant/${participant.id}',
          );
    final saved = favourite != null &&
        ref
            .watch(supporterPreferencesProvider)
            .isFavourite('participant', participantId);
    return Scaffold(
      appBar: AppBar(
          title: Text(participant == null
              ? 'Participant'
              : participant.type == 'team'
                  ? 'Team profile'
                  : 'Athlete profile'),
          actions: [
            if (favourite != null)
              IconButton(
                tooltip: saved ? 'Remove from favourites' : 'Add to favourites',
                onPressed: () => ref
                    .read(supporterPreferencesProvider.notifier)
                    .toggleFavourite(favourite),
                icon: Icon(saved ? Icons.favorite : Icons.favorite_border),
              ),
            IconButton(
                icon: const Icon(Icons.share),
                tooltip: 'Share participant',
                onPressed: participant == null
                    ? null
                    : () => supporterShareService.share(
                        title: participant.name,
                        description: participant.type,
                        path: '/participant/${participant.id}')),
          ]),
      body: state.isLoading
          ? const Center(child: CircularProgressIndicator())
          : state.error != null
              ? Center(
                  child: Padding(
                      padding: const EdgeInsets.all(24),
                      child: Column(mainAxisSize: MainAxisSize.min, children: [
                        Text(
                          state.error!.replaceFirst('Bad state: ', ''),
                          textAlign: TextAlign.center,
                        ),
                        const SizedBox(height: 12),
                        FilledButton(
                            onPressed: () => ref
                                .read(participantDetailProvider(participantId)
                                    .notifier)
                                .refresh(),
                            child: const Text('Try again'))
                      ])))
              : participant == null
                  ? const Center(child: Text('Participant not found'))
                  : ListView(
                      padding: const EdgeInsets.all(16),
                      children: [
                        Container(
                          width: double.infinity,
                          padding: const EdgeInsets.all(24),
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
                          child: Column(children: [
                            CircleAvatar(
                              radius: 48,
                              backgroundColor:
                                  Colors.white.withValues(alpha: 0.14),
                              backgroundImage: participant.avatarUrl == null
                                  ? null
                                  : NetworkImage(participant.avatarUrl!),
                              child: participant.avatarUrl == null
                                  ? Icon(
                                      participant.type == 'team'
                                          ? Icons.groups
                                          : Icons.person,
                                      size: 46,
                                      color: Colors.white,
                                    )
                                  : null,
                            ),
                            const SizedBox(height: 16),
                            Text(
                              participant.name,
                              textAlign: TextAlign.center,
                              style: Theme.of(context)
                                  .textTheme
                                  .headlineSmall
                                  ?.copyWith(
                                    fontWeight: FontWeight.w900,
                                    color: Colors.white,
                                  ),
                            ),
                            const SizedBox(height: 6),
                            Container(
                              padding: const EdgeInsets.symmetric(
                                horizontal: 10,
                                vertical: 5,
                              ),
                              decoration: BoxDecoration(
                                color: Colors.white.withValues(alpha: 0.14),
                                borderRadius: BorderRadius.circular(999),
                              ),
                              child: Text(
                                participant.type.toUpperCase(),
                                style: const TextStyle(
                                  color: Colors.white,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                            ),
                            if (participant.country != null)
                              Padding(
                                padding: const EdgeInsets.only(top: 8),
                                child: Text(
                                  participant.country!,
                                  style: const TextStyle(color: Colors.white70),
                                ),
                              ),
                          ]),
                        ),
                        const SizedBox(height: 16),
                        Row(children: [
                          Expanded(
                              child: _Stat(
                                  label: 'Results',
                                  value: participant.matchesPlayed)),
                          const SizedBox(width: 8),
                          Expanded(
                              child: _Stat(
                                  label: 'Wins', value: participant.wins)),
                          const SizedBox(width: 8),
                          Expanded(
                              child: _Stat(
                                  label: 'Upcoming',
                                  value: state.upcomingMatches.length)),
                        ]),
                        if (participant.homeVenue != null)
                          Card(
                            elevation: 0,
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(16),
                              side: BorderSide(
                                color: Theme.of(context)
                                    .colorScheme
                                    .outlineVariant,
                              ),
                            ),
                            child: ListTile(
                              leading: const Icon(Icons.stadium_outlined),
                              title: const Text('Home venue'),
                              subtitle: Text(participant.homeVenue!),
                            ),
                          ),
                        const SizedBox(height: 18),
                        Text('Upcoming contests',
                            style: Theme.of(context)
                                .textTheme
                                .titleLarge
                                ?.copyWith(fontWeight: FontWeight.bold)),
                        if (state.upcomingMatches.isEmpty)
                          const Padding(
                              padding: EdgeInsets.symmetric(vertical: 20),
                              child: Text('No upcoming public contests.'))
                        else
                          ...state.upcomingMatches.take(5).map(
                                (item) => Card(
                                  elevation: 0,
                                  margin: const EdgeInsets.only(bottom: 10),
                                  shape: RoundedRectangleBorder(
                                    borderRadius: BorderRadius.circular(16),
                                    side: BorderSide(
                                      color: Theme.of(context)
                                          .colorScheme
                                          .outlineVariant,
                                    ),
                                  ),
                                  child: ListTile(
                                    contentPadding: const EdgeInsets.symmetric(
                                      horizontal: 16,
                                      vertical: 8,
                                    ),
                                    leading: CircleAvatar(
                                      backgroundColor: Theme.of(context)
                                          .colorScheme
                                          .surfaceContainerHighest,
                                      child: const Icon(
                                          Icons.calendar_today_outlined),
                                    ),
                                    title: Text(
                                      item['name']?.toString() ?? 'Contest',
                                      style: const TextStyle(
                                          fontWeight: FontWeight.w700),
                                    ),
                                    subtitle: Text(
                                      item['venue']?.toString() ??
                                          item['status']?.toString() ??
                                          '',
                                    ),
                                    trailing:
                                        const Icon(Icons.chevron_right_rounded),
                                  ),
                                ),
                              ),
                      ],
                    ),
    );
  }
}

class _Stat extends StatelessWidget {
  final String label;
  final int value;
  const _Stat({required this.label, required this.value});
  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: Theme.of(context).colorScheme.surface,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(
            color: Theme.of(context).colorScheme.outlineVariant,
          ),
        ),
        child: Column(children: [
          Text(
            '$value',
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  fontWeight: FontWeight.w900,
                  color: Theme.of(context).colorScheme.primary,
                ),
          ),
          const SizedBox(height: 2),
          Text(
            label,
            style: Theme.of(context).textTheme.labelMedium?.copyWith(
                  color: Theme.of(context).colorScheme.outline,
                ),
          ),
        ]),
      );
}
