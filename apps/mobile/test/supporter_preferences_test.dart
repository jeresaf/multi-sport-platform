import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:sports_platform_mobile/core/config/app_config.dart';
import 'package:sports_platform_mobile/features/supporter/data/supporter_preferences.dart';
import 'package:sports_platform_mobile/features/supporter/data/supporter_share_service.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() async {
    SharedPreferences.setMockInitialValues({});
    await Environment.initialize();
    Environment.selectTenant('six-nations');
  });

  test('favourites and low-data mode persist across notifier recreation',
      () async {
    final preferences = await SharedPreferences.getInstance();
    final first = SupporterPreferencesNotifier(preferences);
    const favourite = SupporterFavourite(
      type: 'contest',
      id: 'fixture-1',
      title: 'Lions vs Leopards',
      subtitle: 'Six Nations',
      route: '/contest/fixture-1',
    );

    await first.toggleFavourite(favourite);
    await first.setLowDataMode(true);

    final restored = SupporterPreferencesNotifier(preferences);
    expect(restored.state.lowDataMode, isTrue);
    expect(restored.state.isFavourite('contest', 'fixture-1'), isTrue);
    expect(restored.state.favourites.values.single.route, '/contest/fixture-1');

    await restored.toggleFavourite(favourite);
    expect(restored.state.favourites, isEmpty);
  });

  test('shared deep links retain tenant context', () {
    final uri = supporterShareService.buildUri('/contest/fixture-1');

    expect(uri.path, '/contest/fixture-1');
    expect(uri.queryParameters['tenant'], 'six-nations');
  });

  test('push preference stays disabled in builds without Firebase', () async {
    final preferences = await SharedPreferences.getInstance();
    final notifier = SupporterPreferencesNotifier(preferences);

    final enabled = await notifier.setNotificationsEnabled(true);

    expect(enabled, isFalse);
    expect(notifier.state.notificationsEnabled, isFalse);
    expect(preferences.getBool('supporter_notifications_enabled'), isFalse);
  });
}
