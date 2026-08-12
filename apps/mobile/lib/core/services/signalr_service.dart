import 'dart:async';

import 'package:signalr_netcore/signalr_client.dart' as signalr;

import '../config/app_config.dart';
import 'api_client.dart';

enum SignalRConnectionStatus {
  disconnected,
  connecting,
  connected,
  reconnecting,
}

typedef SignalRMessageHandler = void Function(List<Object?>? arguments);

/// Small adapter around the third-party client so reconnect behaviour can be
/// tested without opening a socket.
abstract class SignalRConnectionAdapter {
  bool get isConnected;

  void on(String methodName, SignalRMessageHandler handler);
  void onClose(void Function(Exception? error) callback);
  void onReconnecting(void Function(Exception? error) callback);
  void onReconnected(void Function(String? connectionId) callback);
  Future<void> start();
  Future<void> stop();
  Future<Object?> invoke(String methodName, List<Object> arguments);
}

typedef SignalRConnectionFactory = SignalRConnectionAdapter Function(
  String url,
  Future<String> Function() accessTokenFactory,
);

class _NetCoreSignalRConnection implements SignalRConnectionAdapter {
  final signalr.HubConnection _connection;

  _NetCoreSignalRConnection(
    String url,
    Future<String> Function() accessTokenFactory,
  ) : _connection = signalr.HubConnectionBuilder()
            .withUrl(
          url,
          options: signalr.HttpConnectionOptions(
            accessTokenFactory: accessTokenFactory,
            requestTimeout: AppConfig.apiTimeout * 1000,
          ),
        )
            .withAutomaticReconnect(
          retryDelays: const [0, 2000, 5000, 10000, 20000, 30000],
        ).build();

  @override
  bool get isConnected =>
      _connection.state == signalr.HubConnectionState.Connected;

  @override
  void on(String methodName, SignalRMessageHandler handler) =>
      _connection.on(methodName, handler);

  @override
  void onClose(void Function(Exception? error) callback) =>
      _connection.onclose(({error}) => callback(error));

  @override
  void onReconnecting(void Function(Exception? error) callback) =>
      _connection.onreconnecting(({error}) => callback(error));

  @override
  void onReconnected(void Function(String? connectionId) callback) =>
      _connection.onreconnected(
        ({connectionId}) => callback(connectionId),
      );

  @override
  Future<void> start() async {
    await _connection.start();
  }

  @override
  Future<void> stop() => _connection.stop();

  @override
  Future<Object?> invoke(String methodName, List<Object> arguments) =>
      _connection.invoke(methodName, args: arguments);
}

/// ASP.NET Core SignalR client with automatic reconnect and group recovery.
class SignalRService {
  final ApiClient _apiClient;
  final SignalRConnectionFactory _connectionFactory;
  final Future<bool> Function() _isAuthenticated;
  final String Function() _tenantSlug;

  SignalRConnectionAdapter? _connection;
  Future<void>? _connectOperation;
  Timer? _restartTimer;
  int _restartAttempt = 0;
  bool _manuallyStopped = false;
  bool _disposed = false;

  final Set<String> _contestSubscriptions = <String>{};
  final Set<String> _tenantSubscriptions = <String>{};

  final StreamController<ContestUpdate> _contestUpdateController =
      StreamController<ContestUpdate>.broadcast();
  final StreamController<ScoreUpdate> _scoreUpdateController =
      StreamController<ScoreUpdate>.broadcast();
  final StreamController<ContestEvent> _contestEventController =
      StreamController<ContestEvent>.broadcast();
  final StreamController<SignalRConnectionStatus> _statusController =
      StreamController<SignalRConnectionStatus>.broadcast();

  SignalRConnectionStatus _status = SignalRConnectionStatus.disconnected;

  SignalRService(
    ApiClient apiClient, {
    SignalRConnectionFactory? connectionFactory,
    Future<bool> Function()? isAuthenticated,
    String Function()? tenantSlug,
  })  : _apiClient = apiClient,
        _connectionFactory = connectionFactory ?? _createConnection,
        _isAuthenticated = isAuthenticated ?? apiClient.isAuthenticated,
        _tenantSlug = tenantSlug ?? (() => Environment.tenantSlug);

  Stream<ContestUpdate> get contestUpdates => _contestUpdateController.stream;
  Stream<ScoreUpdate> get scoreUpdates => _scoreUpdateController.stream;
  Stream<ContestEvent> get contestEvents => _contestEventController.stream;
  Stream<SignalRConnectionStatus> get connectionStatuses =>
      _statusController.stream;

  SignalRConnectionStatus get status => _status;
  bool get isConnected => _status == SignalRConnectionStatus.connected;

  static SignalRConnectionAdapter _createConnection(
    String url,
    Future<String> Function() accessTokenFactory,
  ) =>
      _NetCoreSignalRConnection(url, accessTokenFactory);

  Future<String> _accessToken() async =>
      (await _apiClient.getAccessToken()) ?? '';

  Future<void> connect({String? contestId}) async {
    if (_disposed) return;
    if (contestId != null) _contestSubscriptions.add(contestId);
    _manuallyStopped = false;

    if (_connection?.isConnected == true) {
      if (contestId != null) await _joinContest(contestId);
      return;
    }

    final pending = _connectOperation;
    if (pending != null) return pending;

    final operation = _startConnection();
    _connectOperation = operation;
    try {
      await operation;
    } finally {
      _connectOperation = null;
    }
  }

  Future<void> _startConnection() async {
    _restartTimer?.cancel();
    _setStatus(SignalRConnectionStatus.connecting);
    final connection = _connection ??= _buildConnection();
    try {
      await connection.start();
      _restartAttempt = 0;
      _setStatus(SignalRConnectionStatus.connected);
      await _restoreSubscriptions();
    } catch (_) {
      _setStatus(SignalRConnectionStatus.disconnected);
      _scheduleRestart();
    }
  }

  SignalRConnectionAdapter _buildConnection() {
    final connection = _connectionFactory(
      AppConfig.signalRHubUrl,
      _accessToken,
    );
    connection.on('ContestSnapshot', _handleContestSnapshot);
    connection.on('LiveScoreUpdated', _handleLiveScoreUpdated);
    connection.on('ContestStatusChanged', _handleContestStatusChanged);
    connection.on('ContestResultFinalized', _handleContestChanged);
    connection.on('SegmentStarted', _handleContestChanged);
    connection.on('SegmentEnded', _handleContestChanged);
    connection.on('ParticipantStatusChanged', _handleContestChanged);
    connection.on('EventRecorded', _handleContestEvent);
    connection.onReconnecting((_) {
      if (!_manuallyStopped) {
        _setStatus(SignalRConnectionStatus.reconnecting);
      }
    });
    connection.onReconnected((_) {
      if (!_manuallyStopped) unawaited(_recoverAfterReconnect());
    });
    connection.onClose((_) {
      if (_manuallyStopped || _disposed) return;
      _setStatus(SignalRConnectionStatus.disconnected);
      _scheduleRestart();
    });
    return connection;
  }

  Future<void> _recoverAfterReconnect() async {
    _setStatus(SignalRConnectionStatus.connected);
    try {
      await _restoreSubscriptions();
      _restartAttempt = 0;
    } catch (_) {
      _setStatus(SignalRConnectionStatus.reconnecting);
    }
  }

  Future<void> _restoreSubscriptions() async {
    for (final contestId in _contestSubscriptions.toList()) {
      await _joinContest(contestId);
    }
    for (final tenantId in _tenantSubscriptions.toList()) {
      await _connection!.invoke('JoinTenantGroup', <Object>[tenantId]);
    }
  }

  void _scheduleRestart() {
    if (_manuallyStopped || _disposed || _restartTimer != null) return;
    if (_contestSubscriptions.isEmpty && _tenantSubscriptions.isEmpty) return;
    const delays = <Duration>[
      Duration(seconds: 2),
      Duration(seconds: 5),
      Duration(seconds: 10),
      Duration(seconds: 20),
      Duration(seconds: 30),
    ];
    final delay = delays[_restartAttempt.clamp(0, delays.length - 1)];
    _restartAttempt++;
    _restartTimer = Timer(delay, () {
      _restartTimer = null;
      if (!_manuallyStopped && !_disposed) unawaited(connect());
    });
  }

  Future<void> subscribeToContest(String contestId) async {
    final added = _contestSubscriptions.add(contestId);
    if (_connection?.isConnected != true) {
      await connect();
    } else if (added) {
      await _joinContest(contestId);
    }
  }

  Future<void> _joinContest(String contestId) async {
    if (await _isAuthenticated()) {
      await _connection!.invoke('JoinContestGroup', <Object>[contestId]);
    } else {
      await _connection!.invoke(
        'JoinPublicContestGroup',
        <Object>[_tenantSlug(), contestId],
      );
    }
  }

  Future<void> unsubscribeFromContest(String contestId) async {
    if (!_contestSubscriptions.remove(contestId)) return;
    if (_connection?.isConnected != true) return;
    final method = await _isAuthenticated()
        ? 'LeaveContestGroup'
        : 'LeavePublicContestGroup';
    await _connection!.invoke(method, <Object>[contestId]);
  }

  Future<void> subscribeToTenant(String tenantId) async {
    final added = _tenantSubscriptions.add(tenantId);
    if (_connection?.isConnected != true) {
      await connect();
    } else if (added) {
      await _connection!.invoke('JoinTenantGroup', <Object>[tenantId]);
    }
  }

  Future<void> unsubscribeFromTenant(String tenantId) async {
    if (!_tenantSubscriptions.remove(tenantId)) return;
    if (_connection?.isConnected == true) {
      await _connection!.invoke('LeaveTenantGroup', <Object>[tenantId]);
    }
  }

  void _handleContestSnapshot(List<Object?>? arguments) {
    final payload = _payload(arguments);
    if (payload == null) return;
    _contestUpdateController.add(ContestUpdate.fromJson(payload));
    final scores = payload['recentScores'];
    if (scores is List) {
      for (final score in scores.whereType<Map>()) {
        _scoreUpdateController.add(
          ScoreUpdate.fromJson(Map<String, dynamic>.from(score)),
        );
      }
    }
  }

  void _handleLiveScoreUpdated(List<Object?>? arguments) {
    final payload = _payload(arguments);
    if (payload == null) return;
    final score = ScoreUpdate.fromJson(payload);
    _scoreUpdateController.add(score);
    _contestUpdateController.add(
      ContestUpdate(
        contestId: score.contestId,
        status: 'Live',
        timestamp: score.timestamp,
        data: payload,
      ),
    );
  }

  void _handleContestStatusChanged(List<Object?>? arguments) {
    final payload = _payload(arguments);
    if (payload != null) {
      _contestUpdateController.add(ContestUpdate.fromJson(payload));
    }
  }

  void _handleContestChanged(List<Object?>? arguments) {
    final payload = _payload(arguments);
    if (payload == null) return;
    final contestId = payload['contestId']?.toString();
    if (contestId != null && contestId.isNotEmpty) {
      _contestUpdateController.add(ContestUpdate.fromJson(payload));
      return;
    }
    for (final subscribedId in _contestSubscriptions) {
      _contestUpdateController.add(
        ContestUpdate(
          contestId: subscribedId,
          status: '',
          timestamp: DateTime.now().toUtc(),
          data: payload,
        ),
      );
    }
  }

  void _handleContestEvent(List<Object?>? arguments) {
    final payload = _payload(arguments);
    if (payload != null) {
      _contestEventController.add(ContestEvent.fromJson(payload));
    }
  }

  Map<String, dynamic>? _payload(List<Object?>? arguments) {
    if (arguments == null || arguments.isEmpty || arguments.first is! Map) {
      return null;
    }
    return Map<String, dynamic>.from(arguments.first! as Map);
  }

  void _setStatus(SignalRConnectionStatus value) {
    if (_status == value || _disposed) return;
    _status = value;
    _statusController.add(value);
  }

  Future<void> disconnect() async {
    _manuallyStopped = true;
    _restartTimer?.cancel();
    _restartTimer = null;
    _contestSubscriptions.clear();
    _tenantSubscriptions.clear();
    final connection = _connection;
    _connection = null;
    if (connection != null) await connection.stop();
    _setStatus(SignalRConnectionStatus.disconnected);
  }

  void dispose() {
    if (_disposed) return;
    unawaited(disconnect());
    _disposed = true;
    _contestUpdateController.close();
    _scoreUpdateController.close();
    _contestEventController.close();
    _statusController.close();
  }
}

class ContestUpdate {
  final String contestId;
  final String status;
  final DateTime timestamp;
  final Map<String, dynamic>? data;

  const ContestUpdate({
    required this.contestId,
    required this.status,
    required this.timestamp,
    this.data,
  });

  factory ContestUpdate.fromJson(Map<String, dynamic> json) => ContestUpdate(
        contestId: json['contestId']?.toString() ?? '',
        status: json['status']?.toString() ?? '',
        timestamp: _dateTime(json['timestamp'] ?? json['lastUpdated']),
        data: json,
      );
}

class ScoreUpdate {
  final String contestId;
  final String participantId;
  final double score;
  final int? segmentIndex;
  final DateTime timestamp;

  const ScoreUpdate({
    required this.contestId,
    required this.participantId,
    required this.score,
    this.segmentIndex,
    required this.timestamp,
  });

  factory ScoreUpdate.fromJson(Map<String, dynamic> json) => ScoreUpdate(
        contestId: json['contestId']?.toString() ?? '',
        participantId: json['participantId']?.toString() ?? '',
        score: (json['value'] ?? json['score'] as num? ?? 0).toDouble(),
        segmentIndex: json['segmentIndex'] as int?,
        timestamp: _dateTime(json['scoredAt'] ?? json['timestamp']),
      );
}

class ContestEvent {
  final String eventId;
  final String contestId;
  final String eventType;
  final String? participantId;
  final String? secondaryParticipantId;
  final Map<String, dynamic>? metrics;
  final DateTime timestamp;
  final String? clientId;

  const ContestEvent({
    required this.eventId,
    required this.contestId,
    required this.eventType,
    this.participantId,
    this.secondaryParticipantId,
    this.metrics,
    required this.timestamp,
    this.clientId,
  });

  factory ContestEvent.fromJson(Map<String, dynamic> json) => ContestEvent(
        eventId: json['eventId']?.toString() ?? '',
        contestId: json['contestId']?.toString() ?? '',
        eventType: json['eventType']?.toString() ?? '',
        participantId: json['participantId']?.toString(),
        secondaryParticipantId: json['secondaryParticipantId']?.toString(),
        metrics: json['metrics'] is Map
            ? Map<String, dynamic>.from(json['metrics'] as Map)
            : null,
        timestamp: _dateTime(json['timestamp'] ?? json['scoredAt']),
        clientId: json['clientId']?.toString(),
      );
}

DateTime _dateTime(Object? value) {
  if (value is String) {
    return DateTime.tryParse(value) ?? DateTime.now().toUtc();
  }
  return DateTime.now().toUtc();
}
