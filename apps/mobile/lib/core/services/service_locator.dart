import 'package:shared_preferences/shared_preferences.dart';
import 'package:connectivity_plus/connectivity_plus.dart';
import '../database/database.dart';
import 'connectivity_service.dart';
import 'notification_service.dart';
import 'api_client.dart';
import 'offline_sync_service.dart';
import 'signalr_service.dart';
import 'scoring_service.dart';

/// Simple service locator for dependency injection
class ServiceLocator {
  static final ServiceLocator _instance = ServiceLocator._internal();
  factory ServiceLocator() => _instance;
  ServiceLocator._internal();

  final Map<Type, dynamic> _services = {};

  /// Register a service
  static void register<T>(T service) {
    ServiceLocator()._services[T] = service;
  }

  /// Get a registered service
  static T get<T>() {
    final service = ServiceLocator()._services[T];
    if (service == null) {
      throw Exception('Service of type $T not registered');
    }
    return service as T;
  }

  /// Check if a service is registered
  static bool isRegistered<T>() {
    return ServiceLocator()._services.containsKey(T);
  }

  /// Unregister a service
  static void unregister<T>() {
    ServiceLocator()._services.remove(T);
  }

  /// Clear all services
  static void clear() {
    ServiceLocator()._services.clear();
  }
}

/// Setup all services
Future<void> setupServiceLocator(SharedPreferences prefs) async {
  // Core services
  ServiceLocator.register<SharedPreferences>(prefs);
  ServiceLocator.register<ConnectivityService>(ConnectivityService());
  ServiceLocator.register<NotificationService>(NotificationService());

  // API client
  final apiClient = ApiClient();
  ServiceLocator.register<ApiClient>(apiClient);

  // SignalR service for real-time updates
  final signalRService = SignalRService(apiClient);
  ServiceLocator.register<SignalRService>(signalRService);

  // Offline sync service
  final database = AppDatabase();
  ServiceLocator.register<AppDatabase>(database);
  final offlineSyncService = OfflineSyncService(
    apiClient,
    database,
    Connectivity(),
  );
  ServiceLocator.register<OfflineSyncService>(offlineSyncService);

  final scoringService = ScoringService(
    apiClient,
    offlineSyncService: offlineSyncService,
    connectivityService: ServiceLocator.get<ConnectivityService>(),
  );
  ServiceLocator.register<ScoringService>(scoringService);
}
