import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../features/auth/presentation/providers/auth_provider.dart';
import '../theme/ui_tokens.dart';

/// Adaptive public-first navigation: bottom tabs on phones and a rail on tablets.
class MainScaffold extends ConsumerStatefulWidget {
  final StatefulNavigationShell navigationShell;
  const MainScaffold({super.key, required this.navigationShell});

  @override
  ConsumerState<MainScaffold> createState() => _MainScaffoldState();
}

class _MainScaffoldState extends ConsumerState<MainScaffold> {
  DateTime? _lastHomeBackPress;

  @override
  Widget build(BuildContext context) {
    final auth = ref.watch(authStateProvider);
    final morePath = auth.isAuthenticated ? '/settings' : '/login';
    final phoneItems = <_NavigationItem>[
      const _NavigationItem(0, '/', 'Home', Icons.home_outlined, Icons.home),
      const _NavigationItem(
          1, '/live', 'Live', Icons.sensors_outlined, Icons.sensors),
      const _NavigationItem(2, '/favourites', 'Favourites',
          Icons.favorite_border, Icons.favorite),
      const _NavigationItem(3, '/competitions', 'Events',
          Icons.emoji_events_outlined, Icons.emoji_events),
      _NavigationItem(
          5, morePath, 'More', Icons.grid_view_outlined, Icons.grid_view),
    ];
    final tabletItems = <_NavigationItem>[
      ...phoneItems.take(2),
      const _NavigationItem(
          4, '/search', 'Search', Icons.search, Icons.manage_search),
      ...phoneItems.skip(2),
    ];
    final currentPath = GoRouterState.of(context).uri.path;
    final isHome = currentPath == '/' || currentPath == '/home';
    final isShellRoot = isHome ||
        currentPath == '/live' ||
        currentPath == '/favourites' ||
        currentPath == '/competitions' ||
        currentPath == '/search' ||
        currentPath == '/settings' ||
        currentPath == '/support' ||
        currentPath == '/scoring' ||
        currentPath.startsWith('/sport/');

    return PopScope(
      canPop: !isShellRoot,
      onPopInvokedWithResult: (didPop, _) {
        if (didPop) return;
        if (!isHome) {
          widget.navigationShell.goBranch(0, initialLocation: true);
          return;
        }
        final now = DateTime.now();
        if (_lastHomeBackPress != null &&
            now.difference(_lastHomeBackPress!) < const Duration(seconds: 2)) {
          SystemNavigator.pop();
          return;
        }
        _lastHomeBackPress = now;
        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(
              const SnackBar(content: Text('Press back again to exit')));
      },
      child: LayoutBuilder(builder: (context, constraints) {
        final tablet = MediaQuery.sizeOf(context).shortestSide >=
            UiTokens.tabletShortestSide;
        final items = tablet ? tabletItems : phoneItems;
        final selected = _selectedIndex(context, items);
        if (tablet) {
          return Scaffold(
            body: Row(children: [
              SafeArea(
                child: NavigationRail(
                  selectedIndex: selected,
                  labelType: NavigationRailLabelType.all,
                  onDestinationSelected: (index) => _select(items[index]),
                  destinations: items
                      .map((item) => NavigationRailDestination(
                            icon: Icon(item.icon),
                            selectedIcon: Icon(item.selectedIcon),
                            label: Text(item.label),
                          ))
                      .toList(),
                ),
              ),
              const VerticalDivider(width: 1),
              Expanded(child: widget.navigationShell),
            ]),
          );
        }
        return Scaffold(
          body: widget.navigationShell,
          bottomNavigationBar: NavigationBar(
            selectedIndex: selected,
            onDestinationSelected: (index) => _select(items[index]),
            destinations: items
                .map((item) => NavigationDestination(
                      icon: Tooltip(
                        message: item.path == '/competitions'
                            ? 'Competitions'
                            : item.label,
                        child: Icon(item.icon),
                      ),
                      selectedIcon: Tooltip(
                        message: item.path == '/competitions'
                            ? 'Competitions'
                            : item.label,
                        child: Icon(item.selectedIcon),
                      ),
                      label: item.label,
                    ))
                .toList(),
          ),
        );
      }),
    );
  }

  int _selectedIndex(BuildContext context, List<_NavigationItem> items) {
    final selected = items.indexWhere(
      (item) => item.branchIndex == widget.navigationShell.currentIndex,
    );
    return selected < 0 ? 0 : selected;
  }

  void _select(_NavigationItem item) {
    if (item.path == '/login') {
      context.go(item.path);
      return;
    }
    widget.navigationShell.goBranch(
      item.branchIndex,
      initialLocation: item.branchIndex == widget.navigationShell.currentIndex,
    );
  }
}

class _NavigationItem {
  final int branchIndex;
  final String path;
  final String label;
  final IconData icon;
  final IconData selectedIcon;
  const _NavigationItem(
      this.branchIndex, this.path, this.label, this.icon, this.selectedIcon);
}
