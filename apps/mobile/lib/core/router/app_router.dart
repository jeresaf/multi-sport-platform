import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../features/auth/presentation/screens/login_screen.dart';
import '../../features/auth/presentation/screens/register_screen.dart';
import '../../features/auth/presentation/screens/splash_screen.dart';
import '../../features/home/presentation/screens/home_screen.dart';
import '../../features/competitions/presentation/screens/competition_list_screen.dart';
import '../../features/competitions/presentation/screens/competition_detail_screen.dart';
import '../../features/contests/presentation/screens/contest_detail_screen.dart';
import '../../features/contests/presentation/screens/live_scoring_screen.dart';
import '../../features/participants/presentation/screens/participant_detail_screen.dart';
import '../../features/scoring/presentation/screens/scoring_console_screen.dart';
import '../../features/scoring/presentation/screens/scoring_selection_screen.dart';
import '../../features/settings/presentation/screens/settings_screen.dart';
import '../../features/settings/presentation/screens/profile_screen.dart';
import '../../features/notifications/presentation/screens/notifications_screen.dart';
import '../../features/tenant/presentation/screens/tenant_code_screen.dart';
import '../../features/supporter/presentation/screens/supporter_screen.dart';
import '../../features/supporter/presentation/screens/bracket_screen.dart';
import '../../features/supporter/presentation/screens/golf_leaderboard_screen.dart';
import '../../features/supporter/presentation/screens/fight_card_screen.dart';
import '../../features/search/presentation/screens/search_screen.dart';
import '../../features/registration/presentation/screens/competition_registration_screen.dart';
import '../../features/registration/presentation/screens/my_registrations_screen.dart';
import '../../features/auth/presentation/providers/auth_provider.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../widgets/main_scaffold.dart';

/// Application router configuration
class AppRouter {
  late final GoRouter router;

  AppRouter() {
    router = GoRouter(
      initialLocation: '/splash',
      debugLogDiagnostics: true,
      routes: [
        // Splash screen
        GoRoute(
          path: '/splash',
          name: 'splash',
          builder: (context, state) => const SplashScreen(),
        ),

        // Login screen
        GoRoute(
          path: '/login',
          name: 'login',
          builder: (context, state) => const LoginScreen(),
        ),

        GoRoute(
          path: '/register',
          name: 'register',
          builder: (context, state) => const RegisterScreen(),
        ),

        GoRoute(
          path: '/tenant',
          name: 'tenant',
          builder: (context, state) => const TenantCodeScreen(),
        ),

        // Stateful public shell preserves the scroll/navigation stack of every tab.
        StatefulShellRoute.indexedStack(
          builder: (context, state, navigationShell) =>
              MainScaffold(navigationShell: navigationShell),
          branches: [
            StatefulShellBranch(routes: [
              GoRoute(
                path: '/',
                name: 'home',
                builder: (context, state) => HomeScreen(
                  initialDate: _publicDate(state),
                  initialSportSlug: state.uri.queryParameters['sport'],
                ),
              ),
              GoRoute(
                path: '/home',
                name: 'public-home',
                builder: (context, state) => HomeScreen(
                  initialDate: _publicDate(state),
                  initialSportSlug: state.uri.queryParameters['sport'],
                ),
              ),
              GoRoute(
                path: '/sport/:sportSlug',
                name: 'sport-home',
                builder: (context, state) => HomeScreen(
                  initialDate: _publicDate(state),
                  initialSportSlug: state.pathParameters['sportSlug'],
                ),
              ),
            ]),
            StatefulShellBranch(routes: [
              GoRoute(
                path: '/live',
                name: 'public-live',
                builder: (context, state) =>
                    const SupporterScreen(liveOnly: true),
              ),
            ]),
            StatefulShellBranch(routes: [
              GoRoute(
                path: '/favourites',
                name: 'favourites',
                builder: (context, state) =>
                    const SupporterScreen(initialTab: 3),
              ),
            ]),
            StatefulShellBranch(routes: [
              GoRoute(
                path: '/competitions',
                name: 'competitions',
                builder: (context, state) => const CompetitionListScreen(),
                routes: [
                  GoRoute(
                    path: ':competitionId',
                    name: 'competition-detail',
                    builder: (context, state) => CompetitionDetailScreen(
                      competitionId: state.pathParameters['competitionId']!,
                    ),
                  ),
                ],
              ),
              GoRoute(
                path: '/competition/:competitionId',
                name: 'canonical-competition-detail',
                builder: (context, state) => CompetitionDetailScreen(
                  competitionId: state.pathParameters['competitionId']!,
                ),
              ),
            ]),
            StatefulShellBranch(routes: [
              GoRoute(
                path: '/search',
                name: 'search',
                builder: (context, state) => const SearchScreen(),
              ),
            ]),
            StatefulShellBranch(routes: [
              GoRoute(
                path: '/settings',
                name: 'settings',
                builder: (context, state) => const SettingsScreen(),
                routes: [
                  GoRoute(
                    path: 'profile',
                    name: 'profile',
                    builder: (context, state) => const ProfileScreen(),
                  ),
                ],
              ),
              GoRoute(
                path: '/support',
                name: 'support',
                builder: (context, state) => const SupporterScreen(),
              ),
              GoRoute(
                path: '/scoring',
                name: 'scoring',
                builder: (context, state) => const _ScoringGate(),
                routes: [
                  GoRoute(
                    path: 'contest/:contestId',
                    name: 'scoring-console',
                    builder: (context, state) => ScoringConsoleScreen(
                      contestId: state.pathParameters['contestId']!,
                      sportCode: state.uri.queryParameters['sport'],
                    ),
                  ),
                ],
              ),
            ]),
          ],
        ),

        // Contest detail (outside shell for full-screen experience)
        GoRoute(
          path: '/contest/:contestId',
          name: 'contest-detail',
          builder: (context, state) {
            final contestId = state.pathParameters['contestId']!;
            return ContestDetailScreen(contestId: contestId);
          },
        ),

        // Live scoring screen
        GoRoute(
          path: '/live/:contestId',
          name: 'live-scoring',
          builder: (context, state) {
            final contestId = state.pathParameters['contestId']!;
            return LiveScoringScreen(contestId: contestId);
          },
        ),

        // Participant detail
        GoRoute(
          path: '/participant/:participantId',
          name: 'participant-detail',
          builder: (context, state) {
            final participantId = state.pathParameters['participantId']!;
            return ParticipantDetailScreen(participantId: participantId);
          },
        ),
        GoRoute(
          path: '/team/:participantId',
          name: 'team-detail',
          builder: (context, state) => ParticipantDetailScreen(
            participantId: state.pathParameters['participantId']!,
          ),
        ),
        GoRoute(
          path: '/athlete/:participantId',
          name: 'athlete-detail',
          builder: (context, state) => ParticipantDetailScreen(
            participantId: state.pathParameters['participantId']!,
          ),
        ),

        GoRoute(
          path: '/bracket/:competitionId',
          name: 'bracket',
          builder: (context, state) => BracketScreen(
            competitionId: state.pathParameters['competitionId']!,
          ),
        ),

        GoRoute(
          path: '/leaderboard/:competitionId',
          name: 'golf-leaderboard',
          builder: (context, state) => GolfLeaderboardScreen(
            competitionId: state.pathParameters['competitionId']!,
          ),
        ),
        GoRoute(
          path: '/fight-card/:competitionId',
          name: 'fight-card',
          builder: (context, state) => FightCardScreen(
            competitionId: state.pathParameters['competitionId']!,
          ),
        ),

        // Notifications
        GoRoute(
          path: '/notifications',
          name: 'notifications',
          builder: (context, state) => const NotificationsScreen(),
        ),
        GoRoute(
          path: '/registration/:competitionId',
          name: 'competition-registration',
          builder: (context, state) => CompetitionRegistrationScreen(
            competitionId: state.pathParameters['competitionId']!,
            registrationId: state.uri.queryParameters['registrationId'],
          ),
        ),
        GoRoute(
          path: '/my-registrations',
          name: 'my-registrations',
          builder: (context, state) => const MyRegistrationsScreen(),
        ),
      ],
      redirect: (context, state) {
        // Add authentication redirect logic here
        return null;
      },
      errorBuilder: (context, state) => Scaffold(
        body: Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(
                Icons.error_outline,
                size: 64,
                color: Colors.red,
              ),
              const SizedBox(height: 16),
              Text(
                'Page not found',
                style: Theme.of(context).textTheme.headlineSmall,
              ),
              const SizedBox(height: 8),
              Text(
                state.uri.toString(),
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: Colors.grey,
                    ),
              ),
              const SizedBox(height: 24),
              ElevatedButton(
                onPressed: () => context.go('/'),
                child: const Text('Go Home'),
              ),
            ],
          ),
        ),
      ),
    );
  }

  /// Get router configuration with auth state
  GoRouter getRouter({
    required bool isAuthenticated,
    String? selectedTenantId,
    bool isOnline = true,
  }) {
    return router;
  }
}

/// Route names for type-safe navigation
class Routes {
  Routes._();

  static const String splash = '/splash';
  static const String login = '/login';
  static const String register = '/register';
  static const String tenant = '/tenant';
  static const String home = '/';
  static const String competitions = '/competitions';
  static const String support = '/support';
  static const String settings = '/settings';
  static const String profile = '/settings/profile';
  static const String myRegistrations = '/my-registrations';

  static String competitionDetail(String competitionId) =>
      '/competition/$competitionId';
  static String competitionRegistration(String competitionId) =>
      '/registration/$competitionId';
  static String contestDetail(String contestId) => '/contest/$contestId';
  static String liveScoring(String contestId) => '/live/$contestId';
  static String participantDetail(String participantId) =>
      '/participant/$participantId';
  static String teamDetail(String participantId) => '/team/$participantId';
  static String athleteDetail(String participantId) =>
      '/athlete/$participantId';
  static String bracket(String competitionId) => '/bracket/$competitionId';
  static String golfLeaderboard(String competitionId) =>
      '/leaderboard/$competitionId';
  static String fightCard(String competitionId) => '/fight-card/$competitionId';
  static String scoringConsole(String contestId, {String? sport}) {
    final queryParams = sport != null ? '?sport=$sport' : '';
    return '/scoring/contest/$contestId$queryParams';
  }
}

DateTime? _publicDate(GoRouterState state) =>
    DateTime.tryParse(state.uri.queryParameters['date'] ?? '');

class _ScoringGate extends ConsumerWidget {
  const _ScoringGate();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authStateProvider);
    if (!auth.isAuthenticated) return const LoginScreen();
    if (auth.canScore) return const ScoringSelectionScreen();
    return Scaffold(
      appBar: AppBar(title: const Text('Scoring')),
      body: const Center(
        child: Padding(
          padding: EdgeInsets.all(24),
          child: Text('Your account does not have a scorer role.'),
        ),
      ),
    );
  }
}
