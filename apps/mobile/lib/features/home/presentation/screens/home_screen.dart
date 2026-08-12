import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../../core/router/app_router.dart';
import '../../../competitions/presentation/providers/competition_provider.dart';
import '../../../contests/presentation/providers/contest_provider.dart';
import '../../../auth/presentation/providers/auth_provider.dart';
import '../../../competitions/data/repositories/competition_repository_impl.dart';
import '../../../notifications/presentation/providers/notification_provider.dart';
import '../../../branding/presentation/providers/branding_provider.dart';
import '../../../supporter/data/supporter_preferences.dart';

/// Home screen - main dashboard
class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final authState = ref.watch(authStateProvider);
    final competitionsState = ref.watch(competitionsProvider);
    final liveContestsState = ref.watch(liveContestsProvider);
    final unreadCount = ref.watch(unreadNotificationCountProvider);
    final branding = ref.watch(tenantBrandingProvider).valueOrNull;
    final lowDataMode = ref.watch(
      supporterPreferencesProvider.select((state) => state.lowDataMode),
    );

    return Scaffold(
      appBar: AppBar(
        title: Text(branding?.tenantName ?? 'Sports Platform'),
        leading: branding?.logoUrl == null || lowDataMode
            ? IconButton(
                tooltip: 'Change organisation',
                onPressed: () => context.push(Routes.tenant),
                icon: const Icon(Icons.sports),
              )
            : Padding(
                padding: const EdgeInsets.all(8),
                child: InkWell(
                  onTap: () => context.push(Routes.tenant),
                  child: Image.network(
                    branding!.logoUrl!,
                    errorBuilder: (_, __, ___) => const Icon(Icons.sports),
                  ),
                ),
              ),
        actions: [
          if (authState.isAuthenticated)
            Stack(
              alignment: Alignment.center,
              children: [
                IconButton(
                  icon: const Icon(Icons.notifications_outlined),
                  onPressed: () => context.push('/notifications'),
                ),
                if (unreadCount > 0)
                  Positioned(
                    right: 8,
                    top: 8,
                    child: CircleAvatar(
                      radius: 8,
                      backgroundColor: Colors.red,
                      child: Text(
                        unreadCount > 9 ? '9+' : unreadCount.toString(),
                        style:
                            const TextStyle(color: Colors.white, fontSize: 9),
                      ),
                    ),
                  ),
              ],
            )
          else
            TextButton.icon(
              onPressed: () => context.push(Routes.login),
              icon: const Icon(Icons.login),
              label: const Text('Login'),
            ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(competitionsProvider);
          ref.invalidate(liveContestsProvider);
        },
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            // Welcome card
            _WelcomeCard(
              tenantName: branding?.tenantName,
              displayName: authState.displayName,
              email: authState.email,
              isAuthenticated: authState.isAuthenticated,
            ),
            const SizedBox(height: 24),

            // Quick actions
            Text(
              'Quick Actions',
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
            ),
            const SizedBox(height: 12),
            _QuickActionsGrid(canScore: authState.canScore),
            const SizedBox(height: 24),

            // Live contests
            Text(
              'Live Contests',
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
            ),
            const SizedBox(height: 12),
            _LiveContestsList(
              contests: liveContestsState.contests,
              isLoading: liveContestsState.isLoading,
              error: liveContestsState.error,
            ),
            const SizedBox(height: 24),

            // Recent competitions
            Text(
              'Recent Competitions',
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.bold,
                  ),
            ),
            const SizedBox(height: 12),
            _CompetitionsList(
              competitions: competitionsState.competitions.take(5).toList(),
              isLoading: competitionsState.isLoading,
              error: competitionsState.error,
            ),
          ],
        ),
      ),
    );
  }
}

class _WelcomeCard extends StatelessWidget {
  final String? tenantName;
  final String? displayName;
  final String? email;
  final bool isAuthenticated;

  const _WelcomeCard({
    this.tenantName,
    this.displayName,
    this.email,
    required this.isAuthenticated,
  });

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                CircleAvatar(
                  radius: 24,
                  backgroundColor: Theme.of(context).colorScheme.primary,
                  child: Text(
                    (displayName ?? email ?? 'S')[0].toUpperCase(),
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 20,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        isAuthenticated && displayName != null
                            ? 'Welcome, $displayName!'
                            : 'Follow live sport',
                        style:
                            Theme.of(context).textTheme.titleMedium?.copyWith(
                                  fontWeight: FontWeight.bold,
                                ),
                      ),
                      if (email != null)
                        Text(
                          email!,
                          style:
                              Theme.of(context).textTheme.bodySmall?.copyWith(
                                    color: Colors.grey,
                                  ),
                        ),
                      if (!isAuthenticated)
                        Text(
                          'Browse competitions and live results. Sign in only when you need scorer or account tools.',
                          style: Theme.of(context)
                              .textTheme
                              .bodySmall
                              ?.copyWith(color: Colors.grey),
                        ),
                      if (tenantName != null)
                        Text(
                          tenantName!,
                          style: Theme.of(context)
                              .textTheme
                              .labelMedium
                              ?.copyWith(
                                color: Theme.of(context).colorScheme.primary,
                                fontWeight: FontWeight.w700,
                              ),
                        ),
                    ],
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _QuickActionsGrid extends StatelessWidget {
  final bool canScore;

  const _QuickActionsGrid({required this.canScore});

  @override
  Widget build(BuildContext context) {
    return GridView.count(
      crossAxisCount: 2,
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      mainAxisSpacing: 12,
      crossAxisSpacing: 12,
      childAspectRatio: 1.5,
      children: [
        _QuickActionCard(
          icon: Icons.emoji_events,
          title: 'Competitions',
          color: Colors.blue,
          onTap: () => context.go('/competitions'),
        ),
        if (canScore)
          _QuickActionCard(
            icon: Icons.scoreboard,
            title: 'Scoring',
            color: Colors.green,
            onTap: () => context.go('/scoring'),
          ),
        _QuickActionCard(
          icon: Icons.sports_score,
          title: 'Scores & Fixtures',
          color: Colors.indigo,
          onTap: () => context.go(Routes.support),
        ),
        _QuickActionCard(
          icon: Icons.apartment,
          title: 'Organisation',
          color: Colors.teal,
          onTap: () => context.push(Routes.tenant),
        ),
        _QuickActionCard(
          icon: Icons.schedule,
          title: 'Schedule',
          color: Colors.orange,
          onTap: () {
            // TODO: Navigate to schedule
          },
        ),
        _QuickActionCard(
          icon: Icons.people,
          title: 'Participants',
          color: Colors.purple,
          onTap: () {
            // TODO: Navigate to participants
          },
        ),
      ],
    );
  }
}

class _QuickActionCard extends StatelessWidget {
  final IconData icon;
  final String title;
  final Color color;
  final VoidCallback onTap;

  const _QuickActionCard({
    required this.icon,
    required this.title,
    required this.color,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Card(
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(12),
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(icon, size: 32, color: color),
              const SizedBox(height: 8),
              Text(
                title,
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      fontWeight: FontWeight.w500,
                    ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _LiveContestsList extends StatelessWidget {
  final List<Contest> contests;
  final bool isLoading;
  final String? error;

  const _LiveContestsList({
    required this.contests,
    required this.isLoading,
    this.error,
  });

  @override
  Widget build(BuildContext context) {
    if (isLoading) {
      return const Card(
        child: Padding(
          padding: EdgeInsets.all(24),
          child: Center(child: CircularProgressIndicator()),
        ),
      );
    }

    if (error != null) {
      return Card(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            children: [
              Icon(Icons.error_outline, size: 48, color: Colors.red.shade300),
              const SizedBox(height: 12),
              Text(
                'Failed to load contests',
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: Colors.red,
                    ),
              ),
            ],
          ),
        ),
      );
    }

    if (contests.isEmpty) {
      return Card(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            children: [
              Icon(Icons.sports, size: 48, color: Colors.grey.shade400),
              const SizedBox(height: 12),
              Text(
                'No live contests',
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: Colors.grey,
                    ),
              ),
            ],
          ),
        ),
      );
    }

    return Column(
      children: contests.map((contest) {
        return Card(
          margin: const EdgeInsets.only(bottom: 8),
          child: ListTile(
            leading: CircleAvatar(
              backgroundColor:
                  Theme.of(context).colorScheme.primary.withValues(alpha: 0.1),
              child: Icon(
                Icons.sports,
                color: Theme.of(context).colorScheme.primary,
              ),
            ),
            title: Text(contest.name),
            subtitle: Text(contest.sportCode.toUpperCase()),
            trailing: Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: BoxDecoration(
                color: contest.status == 'Live' ? Colors.red : Colors.orange,
                borderRadius: BorderRadius.circular(12),
              ),
              child: Text(
                contest.status,
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: 12,
                  fontWeight: FontWeight.w500,
                ),
              ),
            ),
            onTap: () => context.push('/contest/${contest.id}'),
          ),
        );
      }).toList(),
    );
  }
}

class _CompetitionsList extends StatelessWidget {
  final List<Competition> competitions;
  final bool isLoading;
  final String? error;

  const _CompetitionsList({
    required this.competitions,
    required this.isLoading,
    this.error,
  });

  @override
  Widget build(BuildContext context) {
    if (isLoading) {
      return const Card(
        child: Padding(
          padding: EdgeInsets.all(24),
          child: Center(child: CircularProgressIndicator()),
        ),
      );
    }

    if (error != null) {
      return Card(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            children: [
              Icon(Icons.error_outline, size: 48, color: Colors.red.shade300),
              const SizedBox(height: 12),
              Text(
                'Failed to load competitions',
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: Colors.red,
                    ),
              ),
            ],
          ),
        ),
      );
    }

    if (competitions.isEmpty) {
      return Card(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            children: [
              Icon(Icons.emoji_events, size: 48, color: Colors.grey.shade400),
              const SizedBox(height: 12),
              Text(
                'No competitions available',
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: Colors.grey,
                    ),
              ),
            ],
          ),
        ),
      );
    }

    return Column(
      children: competitions.map((competition) {
        return Card(
          margin: const EdgeInsets.only(bottom: 8),
          child: ListTile(
            leading: CircleAvatar(
              backgroundColor: Colors.amber.withValues(alpha: 0.1),
              child: const Icon(
                Icons.emoji_events,
                color: Colors.amber,
              ),
            ),
            title: Text(competition.name),
            subtitle: Text(competition.sportCode.toUpperCase()),
            trailing: const Icon(Icons.chevron_right),
            onTap: () => context.push('/competitions/${competition.id}'),
          ),
        );
      }).toList(),
    );
  }
}
