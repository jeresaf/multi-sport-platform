import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../domain/supporter_models.dart';
import '../providers/supporter_provider.dart';

class FightCardScreen extends ConsumerWidget {
  const FightCardScreen({super.key, required this.competitionId});
  final String competitionId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final card = ref.watch(fightCardProvider(competitionId));
    return Scaffold(
      appBar: AppBar(title: const Text('Fight card')),
      body: card.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => _FightCardMessage(
          title: 'Fight card unavailable',
          detail: error.toString(),
          onRetry: () => ref.invalidate(fightCardProvider(competitionId)),
        ),
        data: (data) => RefreshIndicator(
          onRefresh: () async =>
              ref.refresh(fightCardProvider(competitionId).future),
          child: CustomScrollView(slivers: [
            SliverPadding(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
              sliver: SliverToBoxAdapter(
                  child: Column(children: [
                if (data.isFromCache)
                  Card(
                      child: ListTile(
                    leading: const Icon(Icons.offline_bolt),
                    title: const Text('Showing saved fight card'),
                    subtitle: data.cachedAt == null
                        ? null
                        : Text(DateFormat.MMMd()
                            .add_Hm()
                            .format(data.cachedAt!.toLocal())),
                  )),
                Card(
                  color: const Color(0xFF111827),
                  child: Padding(
                    padding: const EdgeInsets.all(20),
                    child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(data.status.toUpperCase(),
                              style: const TextStyle(
                                  color: Color(0xFFFCA5A5),
                                  fontWeight: FontWeight.bold)),
                          const SizedBox(height: 4),
                          Text(data.name,
                              style: Theme.of(context)
                                  .textTheme
                                  .headlineSmall
                                  ?.copyWith(
                                      color: Colors.white,
                                      fontWeight: FontWeight.bold)),
                          const SizedBox(height: 8),
                          Text(
                              '${data.completedBoutCount} of ${data.boutCount} bouts completed',
                              style: const TextStyle(color: Colors.white70)),
                        ]),
                  ),
                ),
              ])),
            ),
            if (data.bouts.isEmpty)
              const SliverFillRemaining(
                  hasScrollBody: false,
                  child: _FightCardMessage(
                      title: 'No bouts published',
                      detail: 'Published bouts will appear here.'))
            else
              ..._sections(data.bouts).expand((section) => [
                    SliverToBoxAdapter(
                        child: Padding(
                      padding: const EdgeInsets.fromLTRB(16, 22, 16, 8),
                      child: Row(children: [
                        Expanded(
                            child: Text(section.label,
                                style: Theme.of(context)
                                    .textTheme
                                    .titleLarge
                                    ?.copyWith(fontWeight: FontWeight.bold))),
                        Chip(
                            label: Text(
                                '${section.bouts.length} ${section.bouts.length == 1 ? 'bout' : 'bouts'}')),
                      ]),
                    )),
                    SliverPadding(
                      padding: const EdgeInsets.symmetric(horizontal: 16),
                      sliver: SliverList.separated(
                        itemCount: section.bouts.length,
                        separatorBuilder: (_, __) => const SizedBox(height: 10),
                        itemBuilder: (_, index) =>
                            _BoutCard(bout: section.bouts[index]),
                      ),
                    ),
                  ]),
            const SliverToBoxAdapter(child: SizedBox(height: 24)),
          ]),
        ),
      ),
    );
  }
}

class _BoutCard extends StatelessWidget {
  const _BoutCard({required this.bout});
  final FightCardBout bout;

  @override
  Widget build(BuildContext context) => Semantics(
        label: '${_sectionLabel(bout.cardSection)}, bout ${bout.boutNumber}',
        child: Card(
          clipBehavior: Clip.antiAlias,
          shape: RoundedRectangleBorder(
            side: BorderSide(
                color: bout.cardSection == 'MainEvent'
                    ? Colors.red
                    : Theme.of(context).colorScheme.outlineVariant),
            borderRadius: BorderRadius.circular(12),
          ),
          child: Column(children: [
            Container(
              color: Theme.of(context).colorScheme.surfaceContainerHighest,
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
              child: Row(children: [
                Expanded(
                    child: Text('BOUT ${bout.boutNumber}',
                        style: const TextStyle(fontWeight: FontWeight.bold))),
                Text('${bout.weightClass} · ${bout.scheduledRounds} rounds'),
              ]),
            ),
            Padding(
              padding: const EdgeInsets.all(16),
              child: Row(children: [
                Expanded(
                    child: _Fighter(
                        fighter:
                            bout.fighters.isEmpty ? null : bout.fighters.first,
                        winnerId: bout.winnerId)),
                const Padding(
                    padding: EdgeInsets.symmetric(horizontal: 10),
                    child: Text('VS',
                        style: TextStyle(
                            fontWeight: FontWeight.bold, color: Colors.grey))),
                Expanded(
                    child: _Fighter(
                        fighter:
                            bout.fighters.length < 2 ? null : bout.fighters[1],
                        winnerId: bout.winnerId,
                        alignEnd: true)),
              ]),
            ),
            const Divider(height: 1),
            Padding(
              padding: const EdgeInsets.all(16),
              child: Align(
                  alignment: Alignment.centerLeft,
                  child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(_result(bout),
                            style:
                                const TextStyle(fontWeight: FontWeight.bold)),
                        if (bout.officials.isNotEmpty) ...[
                          const SizedBox(height: 6),
                          Text(
                              'Officials: ${bout.officials.map((item) => '${item.displayName} (${item.role})').join(', ')}',
                              style: Theme.of(context).textTheme.bodySmall),
                        ],
                      ])),
            ),
          ]),
        ),
      );
}

class _Fighter extends StatelessWidget {
  const _Fighter(
      {required this.fighter, required this.winnerId, this.alignEnd = false});
  final FightCardFighter? fighter;
  final String? winnerId;
  final bool alignEnd;
  @override
  Widget build(BuildContext context) => Column(
          crossAxisAlignment:
              alignEnd ? CrossAxisAlignment.end : CrossAxisAlignment.start,
          children: [
            Text(fighter?.corner.toUpperCase() ?? 'CORNER',
                style: Theme.of(context).textTheme.labelSmall),
            const SizedBox(height: 4),
            Text(fighter?.name ?? 'TBA',
                textAlign: alignEnd ? TextAlign.right : TextAlign.left,
                style: TextStyle(
                    fontSize: 17,
                    fontWeight: FontWeight.bold,
                    color: fighter?.participantId == winnerId
                        ? Colors.red.shade700
                        : null)),
          ]);
}

class _FightCardMessage extends StatelessWidget {
  const _FightCardMessage(
      {required this.title, required this.detail, this.onRetry});
  final String title;
  final String detail;
  final VoidCallback? onRetry;
  @override
  Widget build(BuildContext context) => Center(
      child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            const Icon(Icons.sports_mma, size: 52),
            const SizedBox(height: 12),
            Text(title, style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 8),
            Text(detail, textAlign: TextAlign.center),
            if (onRetry != null) ...[
              const SizedBox(height: 16),
              FilledButton(onPressed: onRetry, child: const Text('Try again'))
            ],
          ])));
}

class _FightSection {
  const _FightSection(this.key, this.label, this.bouts);
  final String key;
  final String label;
  final List<FightCardBout> bouts;
}

List<_FightSection> _sections(List<FightCardBout> bouts) {
  const order = ['MainEvent', 'CoMainEvent', 'MainCard', 'PreliminaryCard'];
  final groups = <String, List<FightCardBout>>{};
  for (final bout in bouts) {
    groups.putIfAbsent(bout.cardSection, () => []).add(bout);
  }
  final keys = groups.keys.toList()
    ..sort((a, b) {
      final left = order.indexOf(a);
      final right = order.indexOf(b);
      return (left < 0 ? order.length : left)
          .compareTo(right < 0 ? order.length : right);
    });
  return keys.map((key) {
    final values = [...groups[key]!]
      ..sort((a, b) => b.boutNumber.compareTo(a.boutNumber));
    return _FightSection(key, _sectionLabel(key), values);
  }).toList();
}

String _sectionLabel(String value) => switch (value) {
      'MainEvent' => 'Main Event',
      'CoMainEvent' => 'Co-Main Event',
      'MainCard' => 'Main Card',
      'PreliminaryCard' || 'Preliminaries' => 'Preliminaries',
      _ => value.replaceAllMapped(
          RegExp(r'([a-z])([A-Z])'), (match) => '${match[1]} ${match[2]}'),
    };

String _result(FightCardBout bout) {
  if (bout.resultMethod == null) return 'Result pending';
  final winner = bout.winnerName == null ? '' : '${bout.winnerName} — ';
  if (bout.resultRound == null) return '$winner${bout.resultMethod}';
  if (bout.resultTimeSeconds == null) {
    return '$winner${bout.resultMethod} · R${bout.resultRound}';
  }
  final minutes = bout.resultTimeSeconds! ~/ 60;
  final seconds = '${bout.resultTimeSeconds! % 60}'.padLeft(2, '0');
  return '$winner${bout.resultMethod} · R${bout.resultRound} $minutes:$seconds';
}
