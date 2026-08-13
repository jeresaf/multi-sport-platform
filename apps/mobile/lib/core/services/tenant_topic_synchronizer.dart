typedef TopicOperation = Future<void> Function(String topic);

/// Serializes tenant-topic changes and records a subscription only after the
/// messaging provider confirms it. Failed operations therefore remain
/// retryable on the next state synchronization.
class TenantTopicSynchronizer {
  final TopicOperation _subscribe;
  final TopicOperation _unsubscribe;

  String? _subscribedTopic;
  String? _desiredTopic;
  Future<void>? _inFlight;

  TenantTopicSynchronizer({
    required TopicOperation subscribe,
    required TopicOperation unsubscribe,
  })  : _subscribe = subscribe,
        _unsubscribe = unsubscribe;

  String? get subscribedTopic => _subscribedTopic;

  Future<void> synchronize({
    required String? tenantSlug,
    required bool enabled,
  }) {
    final slug = tenantSlug?.trim();
    _desiredTopic =
        enabled && slug != null && slug.isNotEmpty ? 'tenant-$slug' : null;

    final active = _inFlight;
    if (active != null) {
      return active.then((_) => _ensureSynchronized());
    }
    return _ensureSynchronized();
  }

  Future<void> _ensureSynchronized() {
    if (_subscribedTopic == _desiredTopic) return Future<void>.value();
    final operation = _drain();
    _inFlight = operation;
    return operation.whenComplete(() {
      if (identical(_inFlight, operation)) _inFlight = null;
    });
  }

  Future<void> _drain() async {
    while (_subscribedTopic != _desiredTopic) {
      final previous = _subscribedTopic;
      if (previous != null) {
        await _unsubscribe(previous);
        _subscribedTopic = null;
        continue;
      }

      final target = _desiredTopic;
      if (target != null) {
        await _subscribe(target);
        _subscribedTopic = target;
      }
    }
  }
}
