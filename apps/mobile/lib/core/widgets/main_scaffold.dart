import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../features/auth/presentation/providers/auth_provider.dart';

/// Main scaffold with bottom navigation
class MainScaffold extends ConsumerWidget {
  final Widget child;

  const MainScaffold({
    super.key,
    required this.child,
  });

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final auth = ref.watch(authStateProvider);
    final isAuthenticated = auth.isAuthenticated;
    final canScore = auth.canScore;
    final items = <_NavigationItem>[
      const _NavigationItem(
        path: '/',
        destination: NavigationDestination(
          icon: Icon(Icons.home_outlined),
          selectedIcon: Icon(Icons.home),
          label: 'Home',
        ),
      ),
      const _NavigationItem(
        path: '/support',
        destination: NavigationDestination(
          icon: Icon(Icons.sports_score_outlined),
          selectedIcon: Icon(Icons.sports_score),
          label: 'Scores',
        ),
      ),
      const _NavigationItem(
        path: '/competitions',
        destination: NavigationDestination(
          icon: Icon(Icons.emoji_events_outlined),
          selectedIcon: Icon(Icons.emoji_events),
          label: 'Events',
        ),
      ),
      if (canScore)
        const _NavigationItem(
          path: '/scoring',
          destination: NavigationDestination(
            icon: Icon(Icons.scoreboard_outlined),
            selectedIcon: Icon(Icons.scoreboard),
            label: 'Scoring',
          ),
        ),
      _NavigationItem(
        path: isAuthenticated ? '/settings' : '/login',
        destination: NavigationDestination(
          icon: Icon(isAuthenticated ? Icons.settings_outlined : Icons.login),
          selectedIcon: Icon(isAuthenticated ? Icons.settings : Icons.login),
          label: isAuthenticated ? 'Settings' : 'Login',
        ),
      ),
    ];

    return Scaffold(
      body: child,
      bottomNavigationBar: NavigationBar(
        selectedIndex: _calculateSelectedIndex(context, items),
        onDestinationSelected: (index) => context.go(items[index].path),
        destinations: items.map((item) => item.destination).toList(),
      ),
    );
  }

  int _calculateSelectedIndex(
    BuildContext context,
    List<_NavigationItem> items,
  ) {
    final location = GoRouterState.of(context).uri.path;
    for (var index = 1; index < items.length; index++) {
      if (location.startsWith(items[index].path)) return index;
    }
    return 0;
  }
}

class _NavigationItem {
  final String path;
  final NavigationDestination destination;

  const _NavigationItem({required this.path, required this.destination});
}
