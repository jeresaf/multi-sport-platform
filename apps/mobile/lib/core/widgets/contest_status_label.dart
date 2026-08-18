import 'package:flutter/material.dart';

enum ContestStatusTone { upcoming, live, finished, warning, neutral }

String normalizeContestStatus(String value) => value
    .trim()
    .replaceAll('_', '')
    .replaceAll('-', '')
    .replaceAll(' ', '')
    .toLowerCase();

String contestStatusLabel(String value, {bool compact = false}) {
  final status = normalizeContestStatus(value);
  if (compact) {
    return switch (status) {
      'live' || 'inprogress' => 'LIVE',
      'ended' || 'completed' => 'FT',
      'closed' || 'approved' || 'published' => 'FINAL',
      'startingsoon' || 'ready' => 'SOON',
      'interrupted' || 'paused' => 'INT.',
      'postponed' => 'POSTP.',
      'suspended' => 'SUSP.',
      'cancelled' || 'canceled' => 'CANC.',
      'abandoned' => 'ABD.',
      _ => contestStatusLabel(value).toUpperCase(),
    };
  }
  return switch (status) {
    'notstarted' || 'scheduled' || 'draft' => 'Not started',
    'startingsoon' || 'ready' => 'Starting soon',
    'checkinopen' => 'Check-in open',
    'live' || 'inprogress' => 'Live',
    'delayed' => 'Delayed',
    'interrupted' || 'paused' => 'Interrupted',
    'suspended' => 'Suspended',
    'postponed' => 'Postponed',
    'ended' || 'completed' => 'Ended',
    'closed' || 'approved' || 'published' => 'Final',
    'corrected' => 'Corrected',
    'cancelled' || 'canceled' => 'Cancelled',
    'abandoned' => 'Abandoned',
    _ => 'Status unavailable',
  };
}

ContestStatusTone contestStatusTone(String value) {
  final status = normalizeContestStatus(value);
  if (status == 'live' || status == 'inprogress') return ContestStatusTone.live;
  if ({'ended', 'completed', 'closed', 'approved', 'published', 'corrected'}
      .contains(status)) {
    return ContestStatusTone.finished;
  }
  if ({'delayed', 'interrupted', 'paused', 'suspended', 'postponed'}
      .contains(status)) {
    return ContestStatusTone.warning;
  }
  if ({'cancelled', 'canceled', 'abandoned'}.contains(status)) {
    return ContestStatusTone.neutral;
  }
  return ContestStatusTone.upcoming;
}

bool contestStatusIsLive(String value) =>
    contestStatusTone(value) == ContestStatusTone.live;

Color contestStatusColor(BuildContext context, String value) =>
    switch (contestStatusTone(value)) {
      ContestStatusTone.live => Theme.of(context).colorScheme.error,
      ContestStatusTone.finished => Colors.green.shade700,
      ContestStatusTone.warning => Colors.orange.shade800,
      ContestStatusTone.neutral => Theme.of(context).colorScheme.outline,
      ContestStatusTone.upcoming => Theme.of(context).colorScheme.primary,
    };

class ContestStatusLabel extends StatelessWidget {
  const ContestStatusLabel({
    super.key,
    required this.status,
    this.compact = false,
  });

  final String status;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final label = contestStatusLabel(status, compact: compact);
    final color = contestStatusColor(context, status);
    return Semantics(
      label: 'Contest status: ${contestStatusLabel(status)}',
      liveRegion: contestStatusIsLive(status),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
        decoration: BoxDecoration(
          color: color.withValues(alpha: .12),
          borderRadius: BorderRadius.circular(99),
        ),
        child: Text(
          label,
          style: TextStyle(
            color: color,
            fontSize: compact ? 10 : 12,
            fontWeight: FontWeight.bold,
          ),
        ),
      ),
    );
  }
}
