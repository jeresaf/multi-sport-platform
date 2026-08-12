import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'core/config/app_config.dart';
import 'core/router/app_router.dart';
import 'core/theme/app_theme.dart';
import 'core/services/service_locator.dart';
import 'core/services/connectivity_service.dart';
import 'core/services/notification_service.dart';
import 'core/widgets/offline_indicator.dart';
import 'features/auth/presentation/providers/auth_provider.dart';
import 'features/tenant/presentation/providers/tenant_provider.dart';
import 'features/branding/presentation/providers/branding_provider.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Initialize compile-time environment values.
  await Environment.initialize();

  // Initialize Firebase if configured
  if (AppConfig.useFirebase) {
    await Firebase.initializeApp();
  }

  // Initialize shared preferences
  final sharedPreferences = await SharedPreferences.getInstance();

  // Setup service locator
  await setupServiceLocator(sharedPreferences);

  runApp(
    const ProviderScope(
      child: SportsPlatformApp(),
    ),
  );
}

class SportsPlatformApp extends ConsumerStatefulWidget {
  const SportsPlatformApp({super.key});

  @override
  ConsumerState<SportsPlatformApp> createState() => _SportsPlatformAppState();
}

class _SportsPlatformAppState extends ConsumerState<SportsPlatformApp> {
  late final AppRouter _appRouter;
  late final ConnectivityService _connectivityService;
  late final NotificationService _notificationService;
  StreamSubscription<NotificationData>? _notificationInteractionSubscription;
  String? _subscribedTenantTopic;

  @override
  void initState() {
    super.initState();
    _appRouter = AppRouter();
    _connectivityService = ServiceLocator.get<ConnectivityService>();
    _notificationService = ServiceLocator.get<NotificationService>();
    _notificationInteractionSubscription = _notificationService.interactions
        .listen(_handleNotificationInteraction);

    // Initialize services
    _initializeServices();
  }

  Future<void> _initializeServices() async {
    // Start connectivity monitoring
    await _connectivityService.startMonitoring();

    // Initialize push notifications
    await _notificationService.initialize();

    // Check authentication state
    final authState = ref.read(authStateProvider);
    if (authState.isAuthenticated) {
      // Refresh token if needed
      _refreshTokenIfNeeded();
    }
  }

  Future<void> _refreshTokenIfNeeded() async {
    final authNotifier = ref.read(authStateProvider.notifier);
    await authNotifier.refreshToken();
  }

  void _handleNotificationInteraction(NotificationData notification) {
    final payload = notification.payload;
    if (payload == null || payload.isEmpty) return;
    if (payload.startsWith('contest:')) {
      final id = payload.substring('contest:'.length);
      if (id.isNotEmpty) _appRouter.router.go(Routes.contestDetail(id));
    } else if (payload.startsWith('competition:')) {
      final id = payload.substring('competition:'.length);
      if (id.isNotEmpty) _appRouter.router.go(Routes.competitionDetail(id));
    }
  }

  void _synchronizeTenantTopic(String? tenantSlug) {
    if (!_notificationService.pushAvailable ||
        tenantSlug == null ||
        tenantSlug.isEmpty) {
      return;
    }
    final next = 'tenant-$tenantSlug';
    if (_subscribedTenantTopic == next) return;
    final previous = _subscribedTenantTopic;
    _subscribedTenantTopic = next;
    if (previous != null) {
      unawaited(_notificationService.unsubscribeFromTopic(previous));
    }
    unawaited(_notificationService.subscribeToTopic(next));
  }

  @override
  void dispose() {
    _connectivityService.stopMonitoring();
    _notificationInteractionSubscription?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final authState = ref.watch(authStateProvider);
    final tenantState = ref.watch(tenantStateProvider);
    final connectivityState = ref.watch(connectivityServiceProvider);
    final branding = ref.watch(tenantBrandingProvider).valueOrNull;
    _synchronizeTenantTopic(tenantState.selectedTenant?.slug);

    return MaterialApp.router(
      title: branding?.tenantName ?? AppConfig.appName,
      debugShowCheckedModeBanner: false,
      theme: AppTheme.lightThemeFor(
        primary: branding?.primaryColor,
        secondary: branding?.secondaryColor,
        accent: branding?.accentColor,
      ),
      darkTheme: AppTheme.darkThemeFor(
        primary: branding?.primaryColor,
        secondary: branding?.secondaryColor,
        accent: branding?.accentColor,
      ),
      themeMode: ThemeMode.system,
      routerConfig: _appRouter.getRouter(
        isAuthenticated: authState.isAuthenticated,
        selectedTenantId: tenantState.selectedTenantId,
        isOnline: connectivityState.isOnline,
      ),
      builder: (context, child) {
        return OfflineIndicator(
          child: child ?? const SizedBox.shrink(),
        );
      },
    );
  }
}
