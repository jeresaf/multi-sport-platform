import 'package:flutter_test/flutter_test.dart';
import 'package:sports_platform_mobile/core/config/app_config.dart';

void main() {
  test('environment initializes with a usable API URL', () async {
    await Environment.initialize();

    expect(AppConfig.appName, 'Sports Platform');
    expect(Uri.parse(AppConfig.apiBaseUrl).hasScheme, isTrue);
  });
}
