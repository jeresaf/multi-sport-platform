import 'package:flutter_test/flutter_test.dart';
import 'package:sports_platform_mobile/core/services/api_client.dart';
import 'package:sports_platform_mobile/core/services/signalr_service.dart';

void main() {
  test('public contest subscription is restored after reconnect', () async {
    final connection = _FakeSignalRConnection();
    final service = SignalRService(
      ApiClient(),
      connectionFactory: (_, __) => connection,
      isAuthenticated: () async => false,
      tenantSlug: () => 'rugby-club',
    );
    addTearDown(service.dispose);

    await service.connect(contestId: 'contest-1');

    expect(service.status, SignalRConnectionStatus.connected);
    expect(
      connection.invocations,
      contains(
        const _Invocation(
          'JoinPublicContestGroup',
          <Object>['rugby-club', 'contest-1'],
        ),
      ),
    );

    connection.simulateReconnecting();
    expect(service.status, SignalRConnectionStatus.reconnecting);

    connection.simulateReconnected();
    await Future<void>.delayed(Duration.zero);

    expect(service.status, SignalRConnectionStatus.connected);
    expect(
      connection.invocations
          .where((call) => call.method == 'JoinPublicContestGroup'),
      hasLength(2),
    );

    final updateFuture = service.contestUpdates.first;
    final patchFuture = service.livePatches.first;
    connection.emit('ContestSnapshot', <String, Object?>{
      'contestId': 'contest-1',
      'status': 'InProgress',
      'lastUpdated': '2026-08-04T10:00:00Z',
      'recentScores': <Object>[],
      'participantScores': <Object>[
        <String, Object?>{
          'participantId': 'team-1',
          'score': '12',
          'scoreDisplay': '12',
        },
      ],
    });
    final update = await updateFuture;

    expect(update.contestId, 'contest-1');
    expect(update.status, 'InProgress');
    final patch = await patchFuture;
    expect(patch.participants.single.participantId, 'team-1');
    expect(patch.participants.single.score, '12');
  });

  test('authoritative live patch carries version, totals, and summary',
      () async {
    final connection = _FakeSignalRConnection();
    final service = SignalRService(ApiClient(),
        connectionFactory: (_, __) => connection,
        isAuthenticated: () async => false);
    addTearDown(service.dispose);
    await service.connect(contestId: 'contest-3');

    final future = service.livePatches.first;
    connection.emit('ContestLiveUpdated', <String, Object?>{
      'contestId': 'contest-3',
      'version': 42,
      'status': 'InProgress',
      'summary': 'Second half',
      'participants': <Object>[
        <String, Object?>{'participantId': 'team-1', 'score': '19'}
      ],
    });
    final patch = await future;
    expect(patch.version, 42);
    expect(patch.status, 'InProgress');
    expect(patch.summary, 'Second half');
    expect(patch.participants.single.score, '19');
  });

  test('authenticated subscription uses protected join and leave methods',
      () async {
    final connection = _FakeSignalRConnection();
    final service = SignalRService(
      ApiClient(),
      connectionFactory: (_, __) => connection,
      isAuthenticated: () async => true,
    );
    addTearDown(service.dispose);

    await service.subscribeToContest('contest-2');
    await service.unsubscribeFromContest('contest-2');

    expect(connection.invocations.first.method, 'JoinContestGroup');
    expect(connection.invocations.last.method, 'LeaveContestGroup');
  });
}

class _FakeSignalRConnection implements SignalRConnectionAdapter {
  final Map<String, SignalRMessageHandler> _handlers = {};
  final List<_Invocation> invocations = [];
  void Function(Exception? error)? _reconnecting;
  void Function(String? connectionId)? _reconnected;
  bool _connected = false;

  @override
  bool get isConnected => _connected;

  @override
  Future<Object?> invoke(String methodName, List<Object> arguments) async {
    invocations.add(_Invocation(methodName, arguments));
    return null;
  }

  @override
  void on(String methodName, SignalRMessageHandler handler) {
    _handlers[methodName] = handler;
  }

  @override
  void onClose(void Function(Exception? error) callback) {}

  @override
  void onReconnected(void Function(String? connectionId) callback) =>
      _reconnected = callback;

  @override
  void onReconnecting(void Function(Exception? error) callback) =>
      _reconnecting = callback;

  @override
  Future<void> start() async {
    _connected = true;
  }

  @override
  Future<void> stop() async {
    _connected = false;
  }

  void emit(String methodName, Map<String, Object?> payload) {
    _handlers[methodName]?.call(<Object?>[payload]);
  }

  void simulateReconnecting() {
    _connected = false;
    _reconnecting?.call(Exception('network interrupted'));
  }

  void simulateReconnected() {
    _connected = true;
    _reconnected?.call('new-connection');
  }
}

class _Invocation {
  final String method;
  final List<Object> arguments;

  const _Invocation(this.method, this.arguments);

  @override
  bool operator ==(Object other) =>
      other is _Invocation &&
      method == other.method &&
      _listEquals(arguments, other.arguments);

  @override
  int get hashCode => Object.hash(method, Object.hashAll(arguments));
}

bool _listEquals(List<Object> left, List<Object> right) {
  if (left.length != right.length) return false;
  for (var index = 0; index < left.length; index++) {
    if (left[index] != right[index]) return false;
  }
  return true;
}
