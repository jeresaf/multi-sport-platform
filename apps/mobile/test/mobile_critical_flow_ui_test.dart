import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:sports_platform_mobile/features/auth/data/datasources/auth_local_datasource.dart';
import 'package:sports_platform_mobile/features/auth/domain/repositories/auth_repository.dart';
import 'package:sports_platform_mobile/features/auth/presentation/providers/auth_provider.dart';
import 'package:sports_platform_mobile/features/auth/presentation/screens/login_screen.dart';
import 'package:sports_platform_mobile/features/branding/domain/tenant_branding.dart';
import 'package:sports_platform_mobile/features/branding/presentation/providers/branding_provider.dart';
import 'package:sports_platform_mobile/features/contests/presentation/providers/contest_provider.dart';
import 'package:sports_platform_mobile/features/home/presentation/screens/home_screen.dart';
import 'package:sports_platform_mobile/features/scoring/presentation/screens/scoring_selection_screen.dart';
import 'package:sports_platform_mobile/features/supporter/data/supporter_preferences.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  testWidgets(
    'scorer signs in, sees tenant branding, and opens an assigned contest',
    (tester) async {
      SharedPreferences.setMockInitialValues({});
      final preferences = await SharedPreferences.getInstance();
      final repository = _ScorerAuthRepository();
      final router = GoRouter(
        initialLocation: '/login',
        routes: [
          GoRoute(
            path: '/login',
            builder: (_, __) => const LoginScreen(),
          ),
          GoRoute(
            path: '/',
            builder: (_, __) => const HomeScreen(),
          ),
          GoRoute(
            path: '/scoring',
            builder: (_, __) => const ScoringSelectionScreen(),
          ),
          GoRoute(
            path: '/scoring/contest/:contestId',
            builder: (_, state) => Scaffold(
              body: Center(
                child: Text(
                  'Opened scoring contest ${state.pathParameters['contestId']}',
                ),
              ),
            ),
          ),
        ],
      );

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            authStateProvider.overrideWith(
              (_) => AuthNotifier(repository),
            ),
            tenantBrandingProvider.overrideWith(
              (_) async => const TenantBranding(
                tenantId: 'tenant-premier',
                tenantName: 'Premier League',
                shortName: 'PL',
                publicSlug: 'premier-league',
                primaryColor: Color(0xFF5B21B6),
              ),
            ),
            assignedContestsProvider.overrideWith(
              (_) async => const [
                Contest(
                  id: 'assigned-contest-1',
                  tenantId: 'tenant-premier',
                  competitionId: 'competition-1',
                  name: 'Premier League mobile assignment',
                  status: 'Scheduled',
                  sportId: '00000000-0000-0000-0000-000000000100',
                  sportCode: 'football',
                  metadata: {
                    'participants': [
                      {'participantId': 'home'},
                      {'participantId': 'away'},
                    ],
                  },
                ),
              ],
            ),
            supporterSharedPreferencesProvider.overrideWithValue(preferences),
          ],
          child: MaterialApp.router(routerConfig: router),
        ),
      );
      await tester.pump();

      expect(find.text('Welcome Back'), findsOneWidget);
      await tester.enterText(
        find.byType(TextFormField).at(0),
        'scorer@premier.com',
      );
      await tester.enterText(find.byType(TextFormField).at(1), 'Scorer123!');
      await tester.tap(find.widgetWithText(ElevatedButton, 'Sign In'));
      await _pumpUntil(tester, find.text('Premier League'));

      expect(repository.loginAttempts, 1);
      expect(repository.lastEmail, 'scorer@premier.com');
      expect(find.text('Premier League'), findsWidgets);
      expect(find.textContaining('Welcome, Mobile Scorer'), findsOneWidget);

      await tester.tap(find.text('Scoring').first);
      await _pumpUntil(tester, find.text('Assigned contests'));
      expect(find.text('Premier League mobile assignment'), findsOneWidget);
      expect(find.textContaining('Football · Scheduled · 2 participants'),
          findsOneWidget);

      await tester.tap(find.text('Premier League mobile assignment'));
      await _pumpUntil(
        tester,
        find.text('Opened scoring contest assigned-contest-1'),
      );
    },
  );
}

Future<void> _pumpUntil(WidgetTester tester, Finder finder) async {
  for (var attempt = 0; attempt < 30; attempt++) {
    await tester.pump(const Duration(milliseconds: 100));
    if (finder.evaluate().isNotEmpty) return;
  }
  throw TestFailure(
    'Timed out waiting for ${finder.describeMatch(Plurality.one)}.',
  );
}

class _ScorerAuthRepository implements AuthRepository {
  int loginAttempts = 0;
  String? lastEmail;

  @override
  Future<User?> login(String email, String password) async {
    loginAttempts++;
    lastEmail = email;
    if (email != 'scorer@premier.com' || password != 'Scorer123!') return null;
    return const User(
      id: 'mobile-scorer',
      email: 'scorer@premier.com',
      displayName: 'Mobile Scorer',
      tenantId: 'tenant-premier',
      roles: ['Scorer'],
    );
  }

  @override
  Future<bool> isAuthenticated() async => false;

  @override
  Future<User?> getCurrentUser() async => null;

  @override
  Future<void> logout() async {}

  @override
  Future<void> refreshToken() async {}

  @override
  Future<User?> register(
    String email,
    String password,
    String displayName,
  ) async =>
      null;

  @override
  Future<void> sendPasswordReset(String email) async {}
}
