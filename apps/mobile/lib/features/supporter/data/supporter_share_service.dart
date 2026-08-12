import 'package:share_plus/share_plus.dart';

import '../../../core/config/app_config.dart';

class SupporterShareService {
  const SupporterShareService();

  Future<void> share({
    required String title,
    required String path,
    String? description,
  }) async {
    final target = buildUri(path);
    await Share.share(
      [if (description != null) description, title, target].join('\n'),
      subject: title,
    );
  }

  Uri buildUri(String path) {
    final base = Uri.parse(AppConfig.publicWebBaseUrl);
    return base.resolve(path).replace(
      queryParameters: {
        ...base.resolve(path).queryParameters,
        'tenant': Environment.tenantSlug,
      },
    );
  }
}

const supporterShareService = SupporterShareService();
