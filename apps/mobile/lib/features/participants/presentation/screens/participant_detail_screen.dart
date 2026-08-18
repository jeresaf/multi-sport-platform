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
      appBar: AppBar(title: const Text('Participant'), actions: [
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
                        Text(state.error!),
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
                        Card(
                            child: Padding(
                                padding: const EdgeInsets.all(24),
                                child: Column(children: [
                                  CircleAvatar(
                                      radius: 48,
                                      backgroundImage:
                                          participant.avatarUrl == null
                                              ? null
                                              : NetworkImage(
                                                  participant.avatarUrl!),
                                      child: participant.avatarUrl == null
                                          ? Icon(
                                              participant.type == 'team'
                                                  ? Icons.groups
                                                  : Icons.person,
                                              size: 46)
                                          : null),
                                  const SizedBox(height: 16),
                                  Text(participant.name,
                                      textAlign: TextAlign.center,
                                      style: Theme.of(context)
                                          .textTheme
                                          .headlineSmall
                                          ?.copyWith(
                                              fontWeight: FontWeight.bold)),
                                  const SizedBox(height: 4),
                                  Text(participant.type.toUpperCase()),
                                  if (participant.country != null)
                                    Padding(
                                        padding: const EdgeInsets.only(top: 6),
                                        child: Text(participant.country!)),
                                ]))),
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
                              child: ListTile(
                                  leading: const Icon(Icons.stadium_outlined),
                                  title: const Text('Home venue'),
                                  subtitle: Text(participant.homeVenue!))),
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
                          ...state.upcomingMatches.take(5).map((item) =>
                              ListTile(
                                  title: Text(
                                      item['name']?.toString() ?? 'Contest'),
                                  subtitle: Text(item['venue']?.toString() ??
                                      item['status']?.toString() ??
                                      ''))),
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
  Widget build(BuildContext context) => Card(
      child: Padding(
          padding: const EdgeInsets.all(14),
          child: Column(children: [
            Text('$value',
                style: Theme.of(context)
                    .textTheme
                    .titleLarge
                    ?.copyWith(fontWeight: FontWeight.bold)),
            Text(label)
          ])));
}
