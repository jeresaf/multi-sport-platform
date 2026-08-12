import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/router/app_router.dart';
import '../../../auth/presentation/providers/auth_provider.dart';
import '../../../contests/presentation/providers/contest_provider.dart';

class ScoringSelectionScreen extends ConsumerWidget {
  const ScoringSelectionScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authStateProvider);
    final contests = ref.watch(assignedContestsProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Assigned contests'),
        actions: [
          IconButton(
            tooltip: 'Refresh',
            onPressed: () => ref.invalidate(assignedContestsProvider),
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: !auth.isAuthenticated
          ? _SignInRequired(onSignIn: () => context.go(Routes.login))
          : contests.when(
              loading: () => const Center(child: CircularProgressIndicator()),
              error: (error, _) => _LoadError(
                message: error.toString(),
                onRetry: () => ref.invalidate(assignedContestsProvider),
              ),
              data: (items) => items.isEmpty
                  ? const Center(
                      child: Text('No contests are assigned to your tenant.'),
                    )
                  : RefreshIndicator(
                      onRefresh: () =>
                          ref.refresh(assignedContestsProvider.future),
                      child: ListView.separated(
                        padding: const EdgeInsets.all(16),
                        itemCount: items.length,
                        separatorBuilder: (_, __) => const SizedBox(height: 10),
                        itemBuilder: (context, index) {
                          final contest = items[index];
                          return Card(
                            child: ListTile(
                              leading: CircleAvatar(
                                child: Icon(_sportIcon(contest.sportCode)),
                              ),
                              title: Text(contest.name),
                              subtitle: Text(
                                '${_sportName(contest.sportCode)} · '
                                '${contest.status} · '
                                '${contest.participants.length} participants',
                              ),
                              trailing: const Icon(Icons.chevron_right),
                              onTap: () => context.go(
                                Routes.scoringConsole(
                                  contest.id,
                                  sport: contest.sportCode,
                                ),
                              ),
                            ),
                          );
                        },
                      ),
                    ),
            ),
    );
  }

  IconData _sportIcon(String code) => switch (code) {
        'football' => Icons.sports_soccer,
        'basketball' => Icons.sports_basketball,
        'golf' => Icons.sports_golf,
        'combat' => Icons.sports_mma,
        'athletics' => Icons.directions_run,
        'cricket' => Icons.sports_cricket,
        'rugby' => Icons.sports_rugby,
        _ => Icons.scoreboard,
      };

  String _sportName(String code) =>
      code.isEmpty ? 'Sport' : '${code[0].toUpperCase()}${code.substring(1)}';
}

class _SignInRequired extends StatelessWidget {
  final VoidCallback onSignIn;
  const _SignInRequired({required this.onSignIn});

  @override
  Widget build(BuildContext context) => Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.lock_outline, size: 48),
            const SizedBox(height: 12),
            const Text('Sign in with a scorer account to view assignments.'),
            const SizedBox(height: 16),
            ElevatedButton(onPressed: onSignIn, child: const Text('Sign in')),
          ],
        ),
      );
}

class _LoadError extends StatelessWidget {
  final String message;
  final VoidCallback onRetry;
  const _LoadError({required this.message, required this.onRetry});

  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(message, textAlign: TextAlign.center),
              const SizedBox(height: 16),
              ElevatedButton(onPressed: onRetry, child: const Text('Retry')),
            ],
          ),
        ),
      );
}
