import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'package:sports_platform_mobile/core/widgets/main_scaffold.dart';
import 'package:sports_platform_mobile/features/auth/data/datasources/auth_local_datasource.dart';
import 'package:sports_platform_mobile/features/auth/domain/repositories/auth_repository.dart';
import 'package:sports_platform_mobile/features/auth/presentation/providers/auth_provider.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  for (final target in <({String name, Size size, bool tablet})>[
    (name: 'phone_portrait', size: const Size(390, 844), tablet: false),
    (name: 'phone_landscape', size: const Size(844, 390), tablet: false),
    (name: 'tablet_portrait', size: const Size(800, 1280), tablet: true),
    (name: 'tablet_landscape', size: const Size(1280, 800), tablet: true),
  ]) {
    testWidgets('main shell adapts - ${target.name}', (tester) async {
      tester.view.physicalSize = target.size;
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      final router = GoRouter(
        initialLocation: '/',
        routes: [
          StatefulShellRoute.indexedStack(
            builder: (context, state, shell) =>
                MainScaffold(navigationShell: shell),
            branches: [
              StatefulShellBranch(
                routes: [
                  GoRoute(
                    path: '/',
                    builder: (_, __) =>
                        const Scaffold(body: Center(child: Text('Home body'))),
                  ),
                ],
              ),
              StatefulShellBranch(
                routes: [
                  GoRoute(
                    path: '/live',
                    builder: (_, __) =>
                        const Scaffold(body: Center(child: Text('Live body'))),
                  ),
                ],
              ),
              StatefulShellBranch(
                routes: [
                  GoRoute(
                    path: '/favourites',
                    builder: (_, __) => const Scaffold(
                      body: Center(child: Text('Favourites body')),
                    ),
                  ),
                ],
              ),
              StatefulShellBranch(
                routes: [
                  GoRoute(
                    path: '/competitions',
                    builder: (_, __) => const Scaffold(
                      body: Center(child: Text('Competitions body')),
                    ),
                  ),
                ],
              ),
              StatefulShellBranch(
                routes: [
                  GoRoute(
                    path: '/search',
                    builder: (_, __) => const Scaffold(
                      body: Center(child: Text('Search body')),
                    ),
                  ),
                ],
              ),
              StatefulShellBranch(
                routes: [
                  GoRoute(
                    path: '/settings',
                    builder: (_, __) => const Scaffold(
                      body: Center(child: Text('Settings body')),
                    ),
                  ),
                ],
              ),
            ],
          ),
          GoRoute(
            path: '/login',
            builder: (_, __) =>
                const Scaffold(body: Center(child: Text('Login body'))),
          ),
        ],
      );

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            authStateProvider.overrideWith(
              (_) => AuthNotifier(_GuestAuthRepository()),
            ),
          ],
          child: MaterialApp.router(routerConfig: router),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Home body'), findsOneWidget);

      if (target.tablet) {
        expect(find.byType(NavigationRail), findsOneWidget);
        expect(find.byType(NavigationBar), findsNothing);
        expect(find.text('Search'), findsOneWidget);
      } else {
        expect(find.byType(NavigationBar), findsOneWidget);
        expect(find.byType(NavigationRail), findsNothing);
        expect(find.text('Home'), findsOneWidget);
        expect(find.text('Live'), findsOneWidget);
        expect(find.text('Favourites'), findsOneWidget);
        expect(find.text('Events'), findsOneWidget);
        expect(find.text('More'), findsOneWidget);
      }

      expect(tester.takeException(), isNull);
    });
  }
}

class _GuestAuthRepository implements AuthRepository {
  @override
  Future<User?> login(String email, String password) async => null;

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

  @override
  Future<User> updateProfile(String firstName, String lastName) async =>
      throw UnimplementedError();
}
