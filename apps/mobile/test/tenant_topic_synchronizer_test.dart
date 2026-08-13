import 'package:flutter_test/flutter_test.dart';
import 'package:sports_platform_mobile/core/services/tenant_topic_synchronizer.dart';

void main() {
  test('failed subscription remains retryable', () async {
    var attempts = 0;
    final subscribed = <String>[];
    final synchronizer = TenantTopicSynchronizer(
      subscribe: (topic) async {
        attempts++;
        if (attempts == 1) throw StateError('FCM not ready');
        subscribed.add(topic);
      },
      unsubscribe: (_) async {},
    );

    await expectLater(
      synchronizer.synchronize(tenantSlug: 'uganda-sports-demo', enabled: true),
      throwsStateError,
    );
    expect(synchronizer.subscribedTopic, isNull);

    await synchronizer.synchronize(
      tenantSlug: 'uganda-sports-demo',
      enabled: true,
    );

    expect(attempts, 2);
    expect(subscribed, ['tenant-uganda-sports-demo']);
    expect(synchronizer.subscribedTopic, 'tenant-uganda-sports-demo');
  });

  test('tenant changes unsubscribe the old topic before subscribing the new',
      () async {
    final operations = <String>[];
    final synchronizer = TenantTopicSynchronizer(
      subscribe: (topic) async => operations.add('subscribe:$topic'),
      unsubscribe: (topic) async => operations.add('unsubscribe:$topic'),
    );

    await synchronizer.synchronize(tenantSlug: 'uganda', enabled: true);
    await synchronizer.synchronize(tenantSlug: 'east-africa', enabled: true);

    expect(operations, [
      'subscribe:tenant-uganda',
      'unsubscribe:tenant-uganda',
      'subscribe:tenant-east-africa',
    ]);
  });

  test('disabling notifications removes the active tenant topic', () async {
    final unsubscribed = <String>[];
    final synchronizer = TenantTopicSynchronizer(
      subscribe: (_) async {},
      unsubscribe: (topic) async => unsubscribed.add(topic),
    );

    await synchronizer.synchronize(tenantSlug: 'uganda', enabled: true);
    await synchronizer.synchronize(tenantSlug: 'uganda', enabled: false);

    expect(unsubscribed, ['tenant-uganda']);
    expect(synchronizer.subscribedTopic, isNull);
  });
}
