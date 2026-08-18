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
                child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Alert preferences',
                          style: Theme.of(context)
                              .textTheme
                              .titleLarge
                              ?.copyWith(fontWeight: FontWeight.bold)),
                      const SizedBox(height: 4),
                      Text(favourite.title,
                          maxLines: 2, overflow: TextOverflow.ellipsis),
                      const SizedBox(height: 12),
                      ...options.entries.map((entry) => SwitchListTile(
                            contentPadding: EdgeInsets.zero,
                            title: Text(entry.value),
                            value: selected.contains(entry.key),
                            onChanged: (enabled) => setState(() {
                              if (enabled) {
                                selected.add(entry.key);
                              } else {
                                selected.remove(entry.key);
                              }
                            }),
                          )),
                      const SizedBox(height: 8),
                      SizedBox(
                          width: double.infinity,
                          child: FilledButton(
                            onPressed: () => Navigator.pop(context, selected),
                            child: const Text('Save alerts'),
                          )),
                    ]),
              ),
            )),
  );
  if (saved == null) return;
  await notifier.setAlertPreferences(favourite, saved);
  if (saved.isNotEmpty &&
      !ref.read(supporterPreferencesProvider).notificationsEnabled) {
    await notifier.setNotificationsEnabled(true);
  }
}
