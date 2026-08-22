import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../../core/router/app_router.dart';
import '../../../../core/services/notification_service.dart';
import '../../../auth/presentation/providers/auth_provider.dart';
import '../../../tenant/presentation/providers/tenant_provider.dart';
import '../../../supporter/data/supporter_preferences.dart';
import '../../../supporter/presentation/providers/supporter_provider.dart';
import '../../../competitions/presentation/providers/competition_provider.dart';
import '../../../contests/presentation/providers/contest_provider.dart';
import '../../../../core/database/database.dart';
import '../../../../core/services/service_locator.dart';

/// Settings screen
class SettingsScreen extends ConsumerWidget {
  const SettingsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authStateProvider);
    final supporterPreferences = ref.watch(supporterPreferencesProvider);
    final notificationService = NotificationService();
    return Scaffold(
      appBar: AppBar(
        title: const Text('Settings'),
      ),
      body: ListView(
        children: [
          // Account section
          const _SectionHeader(title: 'Account'),
          ListTile(
            leading: const CircleAvatar(
              child: Icon(Icons.person),
            ),
            title: const Text('Profile'),
            subtitle: Text(auth.email ?? 'Not signed in'),
            trailing: const Icon(Icons.chevron_right),
            onTap: () => context.go(
              auth.isAuthenticated ? Routes.profile : Routes.login,
            ),
          ),
          ListTile(
            leading: const Icon(Icons.assignment_outlined),
            title: const Text('My registrations'),
            subtitle: Text(auth.isAuthenticated
                ? 'Drafts, submissions, and official decisions'
                : 'Sign in to register athletes or teams'),
            trailing: const Icon(Icons.chevron_right),
            onTap: () => context.push(
              auth.isAuthenticated ? Routes.myRegistrations : Routes.login,
            ),
          ),
          const Divider(),

          const _SectionHeader(title: 'Organisation'),
          ListTile(
            leading: const Icon(Icons.apartment),
            title: Text(
              ref.watch(tenantStateProvider).selectedTenant?.name ??
                  'Choose organisation',
            ),
            subtitle: const Text('Change tenant and branding'),
            trailing: const Icon(Icons.chevron_right),
            onTap: () => context.push(Routes.tenant),
          ),
          const Divider(),

          // Preferences section
          const _SectionHeader(title: 'Preferences'),
          SwitchListTile(
            secondary: const Icon(Icons.notifications_outlined),
            title: const Text('Push notifications'),
            subtitle: Text(
              notificationService.pushAvailable
                  ? 'Updates for your organisation and favourites'
                  : 'Requires a Firebase-enabled production build',
            ),
            value: supporterPreferences.notificationsEnabled,
            onChanged: notificationService.pushAvailable
                ? (value) async {
                    final enabled = await ref
                        .read(supporterPreferencesProvider.notifier)
                        .setNotificationsEnabled(value);
                    if (value && !enabled && context.mounted) {
                      ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(
                          content: Text(
                            'Notification permission was not granted.',
                          ),
                        ),
                      );
                    }
                  }
                : null,
          ),
          ListTile(
            leading: const Icon(Icons.inbox_outlined),
            title: const Text('Notification inbox'),
            subtitle: Text(
              auth.isAuthenticated
                  ? 'Review account and competition updates'
                  : 'Sign in to view account notifications',
            ),
            trailing: const Icon(Icons.chevron_right),
            onTap: () {
              context
                  .push(auth.isAuthenticated ? '/notifications' : Routes.login);
            },
          ),
          SwitchListTile(
            secondary: const Icon(Icons.data_saver_on),
            title: const Text('Low-data mode'),
            subtitle: const Text(
              'Prefer saved supporter data, pause live polling, and skip remote logos',
            ),
            value: supporterPreferences.lowDataMode,
            onChanged: (value) => ref
                .read(supporterPreferencesProvider.notifier)
                .setLowDataMode(value),
          ),
          ListTile(
            leading: const Icon(Icons.language),
            title: const Text('Language'),
            subtitle: const Text('English'),
            trailing: const Icon(Icons.chevron_right),
            onTap: () => _showInformation(
              context,
              'Language',
              'English is currently the supported interface language. More languages will appear here when translations are available.',
            ),
          ),
          ListTile(
            leading: const Icon(Icons.dark_mode_outlined),
            title: const Text('Theme'),
            subtitle: Text(_themeLabel(supporterPreferences.themeMode)),
            trailing: const Icon(Icons.chevron_right),
            onTap: () => _showThemeDialog(context, ref),
          ),
          const Divider(),

          // Data section
          const _SectionHeader(title: 'Data & Storage'),
          ListTile(
            leading: const Icon(Icons.sync),
            title: const Text('Sync Settings'),
            trailing: const Icon(Icons.chevron_right),
            subtitle: const Text('Refresh public data and review data usage'),
            onTap: () => _showSyncSettings(context, ref),
          ),
          ListTile(
            leading: const Icon(Icons.storage),
            title: const Text('Clear Cache'),
            trailing: const Icon(Icons.chevron_right),
            onTap: () {
              _showClearCacheDialog(context, ref);
            },
          ),
          const Divider(),

          // Support section
          const _SectionHeader(title: 'Support'),
          ListTile(
            leading: const Icon(Icons.help_outline),
            title: const Text('Help & FAQ'),
            trailing: const Icon(Icons.chevron_right),
            onTap: () => _showInformation(
              context,
              'Help & FAQ',
              'Change organisation from the home icon. Use Schedule for upcoming contests and Results for completed contests. Pull down on lists to refresh. Scoring tools require an authorised scorer account.',
            ),
          ),
          ListTile(
            leading: const Icon(Icons.feedback_outlined),
            title: const Text('Send Feedback'),
            trailing: const Icon(Icons.chevron_right),
            onTap: () async {
              await Clipboard.setData(const ClipboardData(
                text: 'support@sportsplatform.local',
              ));
              if (context.mounted) {
                ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
                  content: Text('Support email copied to clipboard'),
                ));
              }
            },
          ),
          ListTile(
            leading: const Icon(Icons.info_outline),
            title: const Text('About'),
            subtitle: const Text('Version 1.0.2'),
            trailing: const Icon(Icons.chevron_right),
            onTap: () {
              _showAboutDialog(context);
            },
          ),
          const Divider(),

          // Logout
          ListTile(
            leading: Icon(Icons.logout, color: Colors.red.shade400),
            title: Text(
              'Sign Out',
              style: TextStyle(color: Colors.red.shade400),
            ),
            onTap: () {
              _showLogoutDialog(context, ref);
            },
          ),
          const SizedBox(height: 24),
        ],
      ),
    );
  }

  String _themeLabel(String value) => switch (value) {
        'light' => 'Light',
        'dark' => 'Dark',
        _ => 'System',
      };

  void _showThemeDialog(BuildContext context, WidgetRef ref) {
    showDialog(
      context: context,
      builder: (dialogContext) => SimpleDialog(
        title: const Text('Choose theme'),
        children: [
          for (final option in const [
            ('system', 'System'),
            ('light', 'Light'),
            ('dark', 'Dark'),
          ])
            RadioListTile<String>(
              value: option.$1,
              groupValue: ref.read(supporterPreferencesProvider).themeMode,
              title: Text(option.$2),
              onChanged: (value) {
                if (value != null) {
                  ref
                      .read(supporterPreferencesProvider.notifier)
                      .setThemeMode(value);
                }
                Navigator.pop(dialogContext);
              },
            ),
        ],
      ),
    );
  }

  void _showSyncSettings(BuildContext context, WidgetRef ref) {
    showModalBottomSheet(
      context: context,
      showDragHandle: true,
      builder: (sheetContext) => Padding(
        padding: const EdgeInsets.fromLTRB(24, 0, 24, 24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Sync Settings',
                style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 8),
            const Text(
                'Public data refreshes when lists are opened or pulled down. Low-data mode prefers saved data.'),
            const SizedBox(height: 16),
            FilledButton.icon(
              icon: const Icon(Icons.sync),
              label: const Text('Sync now'),
              onPressed: () {
                ref.invalidate(supporterFeedProvider);
                ref.invalidate(competitionsProvider);
                ref.invalidate(liveContestsProvider);
                Navigator.pop(sheetContext);
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(content: Text('Refreshing sports data')),
                );
              },
            ),
          ],
        ),
      ),
    );
  }

  void _showInformation(BuildContext context, String title, String content) {
    showDialog(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(title),
        content: Text(content),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext),
            child: const Text('Close'),
          ),
        ],
      ),
    );
  }

  void _showClearCacheDialog(BuildContext context, WidgetRef ref) {
    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Clear Cache'),
        content: const Text(
          'This will clear all cached data. You may need to reload some content.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () async {
              await ServiceLocator.get<AppDatabase>().clearReadCaches();
              ref.invalidate(supporterFeedProvider);
              ref.invalidate(competitionsProvider);
              ref.invalidate(liveContestsProvider);
              if (!context.mounted) return;
              Navigator.pop(context);
              ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(content: Text('Cache cleared')),
              );
            },
            child: const Text('Clear'),
          ),
        ],
      ),
    );
  }

  void _showAboutDialog(BuildContext context) {
    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('About'),
        content: const Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Sports Platform'),
            SizedBox(height: 8),
            Text('Version 1.0.0'),
            SizedBox(height: 16),
            Text(
              'A multi-sport competition management platform.',
              style: TextStyle(color: Colors.grey),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('Close'),
          ),
        ],
      ),
    );
  }

  void _showLogoutDialog(BuildContext context, WidgetRef ref) {
    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Sign Out'),
        content: const Text('Are you sure you want to sign out?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () async {
              Navigator.pop(context);
              await ref.read(authStateProvider.notifier).logout();
              if (context.mounted) {
                context.go(Routes.home);
              }
            },
            child: Text(
              'Sign Out',
              style: TextStyle(color: Colors.red.shade400),
            ),
          ),
        ],
      ),
    );
  }
}

class _SectionHeader extends StatelessWidget {
  final String title;

  const _SectionHeader({required this.title});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
      child: Text(
        title,
        style: TextStyle(
          color: Theme.of(context).colorScheme.primary,
          fontWeight: FontWeight.bold,
          fontSize: 14,
        ),
      ),
    );
  }
}
