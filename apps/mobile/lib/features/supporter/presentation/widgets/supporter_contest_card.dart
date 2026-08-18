import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/formatters/public_date_time.dart';
import '../../../../core/widgets/contest_status_label.dart';
import '../../domain/supporter_models.dart';

enum RugbyVariant { sevens, fifteens }

class SupporterContestCard extends StatelessWidget {
  const SupporterContestCard({
    super.key,
    required this.fixture,
    required this.isFavourite,
    required this.onTap,
    required this.onFavourite,
    required this.onShare,
  });

  final SupporterFixture fixture;
  final bool isFavourite;
  final VoidCallback onTap;
  final VoidCallback onFavourite;
  final VoidCallback onShare;

  @override
  Widget build(BuildContext context) {
    final variant =
        (fixture.presentation.scoreVariant ?? fixture.sportCode).toUpperCase();
    if (variant.startsWith('RUGBY_')) {
      return RugbyContestCard(
        fixture: fixture,
        variant: variant.contains('15')
            ? RugbyVariant.fifteens
            : RugbyVariant.sevens,
        isFavourite: isFavourite,
        onTap: onTap,
        onFavourite: onFavourite,
        onShare: onShare,
      );
    }
    return HeadToHeadContestCard(
      fixture: fixture,
      adapterLabel: _adapterLabel(variant),
      secondarySummary: _secondarySummary(fixture, variant),
      isFavourite: isFavourite,
      onTap: onTap,
      onFavourite: onFavourite,
      onShare: onShare,
    );
  }
}

class RugbyContestCard extends StatelessWidget {
  const RugbyContestCard({
    super.key,
    required this.fixture,
    required this.variant,
    required this.isFavourite,
    required this.onTap,
    required this.onFavourite,
    required this.onShare,
  });

  final SupporterFixture fixture;
  final RugbyVariant variant;
  final bool isFavourite;
  final VoidCallback onTap;
  final VoidCallback onFavourite;
  final VoidCallback onShare;

  @override
  Widget build(BuildContext context) => HeadToHeadContestCard(
        fixture: fixture,
        adapterLabel: variant == RugbyVariant.sevens ? 'Rugby 7s' : 'Rugby 15s',
        secondarySummary: RugbyScoreSummary.summary(fixture),
        stageBadge: fixture.presentation.stageLabel,
        isFavourite: isFavourite,
        onTap: onTap,
        onFavourite: onFavourite,
        onShare: onShare,
      );
}

class HeadToHeadContestCard extends StatelessWidget {
  const HeadToHeadContestCard({
    super.key,
    required this.fixture,
    required this.adapterLabel,
    required this.isFavourite,
    required this.onTap,
    required this.onFavourite,
    required this.onShare,
    this.secondarySummary,
    this.stageBadge,
  });

  final SupporterFixture fixture;
  final String adapterLabel;
  final String? secondarySummary;
  final String? stageBadge;
  final bool isFavourite;
  final VoidCallback onTap;
  final VoidCallback onFavourite;
  final VoidCallback onShare;

  bool get _isLive => contestStatusIsLive(fixture.status);

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Container(
          decoration: _isLive
              ? BoxDecoration(
                  border: Border(
                    left: BorderSide(color: theme.colorScheme.error, width: 3),
                  ),
                )
              : null,
          padding: const EdgeInsets.fromLTRB(14, 10, 8, 12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(fixture.competitionName,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: theme.textTheme.labelLarge),
                  ),
                  ContestStatusLabel(status: fixture.status, compact: true),
                  IconButton(
                    visualDensity: VisualDensity.compact,
                    tooltip: isFavourite
                        ? 'Remove from favourites'
                        : 'Add to favourites',
                    onPressed: onFavourite,
                    icon: Icon(
                      isFavourite ? Icons.favorite : Icons.favorite_border,
                    ),
                  ),
                  IconButton(
                    visualDensity: VisualDensity.compact,
                    tooltip: 'Share fixture',
                    onPressed: onShare,
                    icon: const Icon(Icons.share_outlined),
                  ),
                ],
              ),
              Wrap(
                spacing: 6,
                runSpacing: 4,
                children: [
                  RugbyStageBadge(label: adapterLabel),
                  if (stageBadge?.trim().isNotEmpty == true)
                    RugbyStageBadge(label: stageBadge!),
                  if (fixture.presentation.periodLabel?.trim().isNotEmpty ==
                      true)
                    RugbyStageBadge(
                      label: [
                        fixture.presentation.periodLabel,
                        fixture.presentation.clock,
                      ]
                          .whereType<String>()
                          .where((item) => item.isNotEmpty)
                          .join(' · '),
                    ),
                ],
              ),
              const SizedBox(height: 10),
              if (fixture.participantDetails.isEmpty)
                Text(fixture.name, style: theme.textTheme.titleMedium)
              else
                ...fixture.participantDetails.take(2).map(
                      (participant) => _ParticipantScoreRow(
                        participant: participant,
                        onTap: participant.id.isEmpty
                            ? null
                            : () =>
                                context.push('/participant/${participant.id}'),
                      ),
                    ),
              if (secondarySummary?.trim().isNotEmpty == true) ...[
                const SizedBox(height: 7),
                Text(secondarySummary!,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: theme.colorScheme.onSurfaceVariant,
                    )),
              ],
              const SizedBox(height: 9),
              Wrap(
                spacing: 12,
                runSpacing: 4,
                children: [
                  if (fixture.startTime != null)
                    Text(formatPublicDateTime(fixture.startTime)),
                  if (fixture.venue?.trim().isNotEmpty == true)
                    Text(fixture.venue!),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class RugbyScoreSummary extends StatelessWidget {
  const RugbyScoreSummary({super.key, required this.fixture});

  final SupporterFixture fixture;

  static String? summary(SupporterFixture fixture) {
    if (fixture.presentation.summary?.trim().isNotEmpty == true) {
      return fixture.presentation.summary;
    }
    if (fixture.participantDetails.length < 2 ||
        fixture.presentation.eventSummary.isEmpty) {
      return null;
    }
    int count(String participantId, String type) =>
        fixture.presentation.eventSummary
            .where((item) =>
                item.participantId == participantId &&
                item.eventType.toUpperCase() == type)
            .fold(0, (sum, item) => sum + item.count);
    final first = fixture.participantDetails[0].id;
    final second = fixture.participantDetails[1].id;
    return 'Tries ${count(first, 'TRY')}–${count(second, 'TRY')} · '
        'Pens ${count(first, 'PENALTY_GOAL')}–${count(second, 'PENALTY_GOAL')}';
  }

  @override
  Widget build(BuildContext context) => Text(summary(fixture) ?? '');
}

class RugbyStageBadge extends StatelessWidget {
  const RugbyStageBadge({super.key, required this.label});

  final String label;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
        decoration: BoxDecoration(
          color: Theme.of(context).colorScheme.surfaceContainerHighest,
          borderRadius: BorderRadius.circular(999),
        ),
        child: Text(label.toUpperCase(),
            style: Theme.of(context).textTheme.labelSmall),
      );
}

class _ParticipantScoreRow extends StatelessWidget {
  const _ParticipantScoreRow({required this.participant, this.onTap});

  final SupporterFixtureParticipant participant;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) => InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(8),
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 4),
          child: Row(
            children: [
              CircleAvatar(
                radius: 14,
                child: Text(participant.name
                    .split(' ')
                    .where((part) => part.isNotEmpty)
                    .take(2)
                    .map((part) => part[0])
                    .join()
                    .toUpperCase()),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(participant.name,
                    maxLines: 1, overflow: TextOverflow.ellipsis),
              ),
              const SizedBox(width: 10),
              Text(participant.result,
                  style: Theme.of(context)
                      .textTheme
                      .titleMedium
                      ?.copyWith(fontWeight: FontWeight.bold)),
            ],
          ),
        ),
      );
}

String _adapterLabel(String variant) => switch (variant) {
      'FOOTBALL' || 'SOCCER' => 'Football',
      'BASKETBALL' => 'Basketball',
      'NETBALL' => 'Netball',
      'HOCKEY' => 'Hockey',
      'VOLLEYBALL' => 'Volleyball',
      'TENNIS' => 'Tennis',
      _ => 'Match',
    };

String? _secondarySummary(SupporterFixture fixture, String variant) {
  if (fixture.presentation.summary?.trim().isNotEmpty == true) {
    return fixture.presentation.summary;
  }
  if (variant == 'VOLLEYBALL' || variant == 'TENNIS') {
    final values = fixture.participantDetails
        .map((participant) => participant.scoreDisplay)
        .whereType<String>()
        .where((value) => value.trim().isNotEmpty)
        .toList();
    return values.isEmpty ? null : values.join(' · ');
  }
  if (fixture.presentation.seriesGameNumber != null &&
      fixture.presentation.seriesBestOf != null) {
    return 'Game ${fixture.presentation.seriesGameNumber} · '
        'Best of ${fixture.presentation.seriesBestOf}';
  }
  return null;
}
