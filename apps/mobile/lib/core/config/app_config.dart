/// Application configuration
class AppConfig {
  AppConfig._();

  /// Application name
  static const String appName = 'Sports Platform';

  /// API base URL - configured via environment
  static String get apiBaseUrl => Environment.apiBaseUrl;

  /// SignalR hub URL for real-time updates
  static String get signalRHubUrl => '${Environment.apiBaseUrl}/hubs/contest';

  /// Public web origin used for supporter deep links shared from mobile.
  static String get publicWebBaseUrl => Environment.publicWebBaseUrl;

  /// Whether to use Firebase for push notifications
  static const bool useFirebase = bool.fromEnvironment(
    'USE_FIREBASE',
    defaultValue: false,
  );

  /// Whether to use Firebase Cloud Messaging
  static const bool useFCM = bool.fromEnvironment(
    'USE_FCM',
    defaultValue: false,
  );

  /// API timeout in seconds
  static const int apiTimeout = 30;

  /// Maximum retry attempts for API calls
  static const int maxRetries = 3;

  /// Offline event queue max size
  static const int maxOfflineQueueSize = 1000;

  /// Sync interval in minutes
  static const int syncIntervalMinutes = 5;

  /// Token refresh threshold in minutes
  static const int tokenRefreshThresholdMinutes = 10;

  /// Enable offline mode
  static const bool enableOfflineMode = true;

  /// Enable debug logging
  static const bool enableDebugLogging = true;

  /// Cache duration in hours
  static const int cacheDurationHours = 24;

  /// Image cache duration in days
  static const int imageCacheDurationDays = 7;

  /// Maximum image cache size in MB
  static const int maxImageCacheSizeMB = 100;
}

/// Environment configuration
class Environment {
  Environment._();

  static String _apiBaseUrl = 'http://localhost:8080';
  static String _publicWebBaseUrl = 'http://localhost:3001';

  /// Current tenant slug.
  static String tenantSlug = '';
  static bool _isProduction = false;
  static bool _isDebug = true;

  /// Initialize environment from platform channels or defaults
  static Future<void> initialize() async {
    // In production, these would be read from platform-specific configuration
    // For now, use environment variables or defaults

    _apiBaseUrl = const String.fromEnvironment(
      'API_BASE_URL',
      defaultValue: 'http://localhost:8080',
    );

    _publicWebBaseUrl = const String.fromEnvironment(
      'PUBLIC_WEB_BASE_URL',
      defaultValue: 'http://localhost:3001',
    );

    tenantSlug = const String.fromEnvironment(
      'TENANT_SLUG',
      defaultValue: 'premier-league',
    );

    _isProduction = const bool.fromEnvironment(
      'IS_PRODUCTION',
      defaultValue: false,
    );

    _isDebug = const bool.fromEnvironment(
      'IS_DEBUG',
      defaultValue: true,
    );
  }

  /// API base URL
  static String get apiBaseUrl => _apiBaseUrl;

  static String get publicWebBaseUrl => _publicWebBaseUrl;

  /// Change the active public tenant after a persisted or user-entered code is
  /// resolved. Public data sources read this value when each request is made.
  static void selectTenant(String slug) {
    final normalized = slug.trim().toLowerCase();
    if (normalized.isNotEmpty) tenantSlug = normalized;
  }

  /// Whether running in production
  static bool get isProduction => _isProduction;

  /// Whether running in debug mode
  static bool get isDebug => _isDebug;
}
