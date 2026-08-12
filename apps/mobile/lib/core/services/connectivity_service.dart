import 'dart:async';
import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Connectivity state
class ConnectivityState {
  final bool isOnline;
  final List<ConnectivityResult> results;
  final DateTime? lastChecked;

  const ConnectivityState({
    this.isOnline = true,
    this.results = const [ConnectivityResult.wifi],
    this.lastChecked,
  });

  ConnectivityState copyWith({
    bool? isOnline,
    List<ConnectivityResult>? results,
    DateTime? lastChecked,
  }) {
    return ConnectivityState(
      isOnline: isOnline ?? this.isOnline,
      results: results ?? this.results,
      lastChecked: lastChecked ?? this.lastChecked,
    );
  }
}

/// Connectivity service for monitoring network status
class ConnectivityService {
  final Connectivity _connectivity;
  final Future<List<ConnectivityResult>> Function()? _checkConnectivity;
  final StreamController<ConnectivityState> _stateController =
      StreamController<ConnectivityState>.broadcast();

  Stream<ConnectivityState> get stateStream => _stateController.stream;
  ConnectivityState _currentState = const ConnectivityState();

  ConnectivityService({
    Connectivity? connectivity,
    Future<List<ConnectivityResult>> Function()? checkConnectivity,
  })  : _connectivity = connectivity ?? Connectivity(),
        _checkConnectivity = checkConnectivity;

  ConnectivityState get currentState => _currentState;

  /// Start monitoring connectivity changes
  Future<void> startMonitoring() async {
    // Check initial state
    final results = await _check();
    _updateState(results);

    // Listen for changes
    _connectivity.onConnectivityChanged.listen(_updateState);
  }

  /// Stop monitoring connectivity
  void stopMonitoring() {
    _stateController.close();
  }

  void _updateState(List<ConnectivityResult> results) {
    final isOnline = results.isNotEmpty &&
        !results.every((r) => r == ConnectivityResult.none);

    _currentState = ConnectivityState(
      isOnline: isOnline,
      results: results,
      lastChecked: DateTime.now(),
    );

    _stateController.add(_currentState);
  }

  /// Check current connectivity
  Future<ConnectivityState> checkConnectivity() async {
    final results = await _check();
    _updateState(results);
    return _currentState;
  }

  Future<List<ConnectivityResult>> _check() =>
      _checkConnectivity?.call() ?? _connectivity.checkConnectivity();
}

/// Provider for connectivity state
final connectivityServiceProvider =
    StateNotifierProvider<ConnectivityNotifier, ConnectivityState>((ref) {
  final service = ConnectivityService();
  service.startMonitoring();
  return ConnectivityNotifier(service);
});

class ConnectivityNotifier extends StateNotifier<ConnectivityState> {
  final ConnectivityService _service;
  StreamSubscription<ConnectivityState>? _subscription;

  ConnectivityNotifier(this._service) : super(const ConnectivityState()) {
    _subscription = _service.stateStream.listen((newState) {
      state = newState;
    });
  }

  @override
  void dispose() {
    _subscription?.cancel();
    _service.stopMonitoring();
    super.dispose();
  }

  Future<void> check() async {
    state = await _service.checkConnectivity();
  }
}
