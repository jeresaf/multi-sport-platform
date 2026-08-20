import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/supporter_preferences.dart';

Future<void> showSupporterAlertPreferences(
  BuildContext context,
  WidgetRef ref, {
  required SupporterFavourite favourite,
  required String sportCode,
}) async {
  final notifier = ref.read(supporterPreferencesProvider.notifier);
  var selected = Set<String>.from(ref
      .read(supporterPreferencesProvider)
      .alertsFor(favourite.type, favourite.id));
  final options = supporterAlertOptions(sportCode);
  final saved = await showModalBottomSheet<Set<String>>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    builder: (context) => StatefulBuilder(
      builder: (context, setState) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 0, 20, 20),
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Container(
                      width: 42,
                      height: 42,
                      decoration: BoxDecoration(
                        color: Theme.of(context).colorScheme.primaryContainer,
                        borderRadius: BorderRadius.circular(14),
                      ),
                      child: Icon(
                        Icons.notifications_active_outlined,
                        color: Theme.of(context).colorScheme.primary,
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            'Alert preferences',
                            style: Theme.of(context)
                                .textTheme
                                .titleLarge
                                ?.copyWith(fontWeight: FontWeight.w800),
                          ),
                          Text(
                            favourite.title,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: Theme.of(context)
                                .textTheme
                                .bodyMedium
                                ?.copyWith(
                                  color: Theme.of(context).colorScheme.outline,
                                ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 16),
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(14),
                  decoration: BoxDecoration(
                    color:
                        Theme.of(context).colorScheme.surfaceContainerHighest,
                    borderRadius: BorderRadius.circular(16),
                  ),
                  child: Row(
                    children: [
                      Icon(
                        Icons.tune,
                        size: 18,
                        color: Theme.of(context).colorScheme.primary,
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          selected.isEmpty
                              ? 'No alerts selected'
                              : '${selected.length} alert${selected.length == 1 ? '' : 's'} selected',
                          style: const TextStyle(fontWeight: FontWeight.w700),
                        ),
                      ),
                      TextButton(
                        onPressed: () => setState(() {
                          selected = options.keys.toSet();
                        }),
                        child: const Text('All'),
                      ),
                      TextButton(
                        onPressed: selected.isEmpty
                            ? null
                            : () => setState(selected.clear),
                        child: const Text('None'),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 12),
                ...options.entries.map(
                  (entry) => Container(
                    margin: const EdgeInsets.only(bottom: 8),
                    decoration: BoxDecoration(
                      color: Theme.of(context).colorScheme.surface,
                      borderRadius: BorderRadius.circular(16),
                      border: Border.all(
                        color: Theme.of(context).colorScheme.outlineVariant,
                      ),
                    ),
                    child: SwitchListTile(
                      contentPadding: const EdgeInsets.symmetric(
                        horizontal: 14,
                        vertical: 2,
                      ),
                      secondary: Icon(_alertIcon(entry.key)),
                      title: Text(
                        entry.value,
                        style: const TextStyle(fontWeight: FontWeight.w600),
                      ),
                      subtitle: Text(_alertDescription(entry.key)),
                      value: selected.contains(entry.key),
                      onChanged: (enabled) => setState(() {
                        if (enabled) {
                          selected.add(entry.key);
                        } else {
                          selected.remove(entry.key);
                        }
                      }),
                    ),
                  ),
                ),
                const SizedBox(height: 8),
                SizedBox(
                  width: double.infinity,
                  child: FilledButton.icon(
                    onPressed: () => Navigator.pop(context, selected),
                    icon: const Icon(Icons.check),
                    label: const Text('Save alert preferences'),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    ),
  );
  if (saved == null) return;
  await notifier.setAlertPreferences(favourite, saved);
  if (saved.isNotEmpty &&
      !ref.read(supporterPreferencesProvider).notificationsEnabled) {
    await notifier.setNotificationsEnabled(true);
  }
}

IconData _alertIcon(String kind) => switch (kind) {
      'start' => Icons.play_circle_outline,
      'score' => Icons.sports_score_outlined,
      'periodEnd' => Icons.timer_outlined,
      'final' => Icons.emoji_events_outlined,
      'postponement' => Icons.event_busy_outlined,
      'goal' => Icons.sports_soccer,
      'wicket' => Icons.sports_cricket,
      'roundResult' => Icons.flag_outlined,
      'teeTime' => Icons.golf_course_outlined,
      'fightResult' => Icons.sports_mma_outlined,
      _ => Icons.notifications_outlined,
    };

String _alertDescription(String kind) => switch (kind) {
      'start' => 'When the contest begins.',
      'score' => 'Whenever the score changes.',
      'periodEnd' => 'At the end of a half, quarter, period or set.',
      'final' => 'When the final result is confirmed.',
      'postponement' => 'When the contest is postponed or rescheduled.',
      'goal' => 'When a goal is scored.',
      'wicket' => 'When a wicket falls.',
      'roundResult' => 'When a round result is confirmed.',
      'teeTime' => 'When a tee time is approaching or changes.',
      'fightResult' => 'When a fight result is confirmed.',
      _ => 'Receive this event update.',
    };
