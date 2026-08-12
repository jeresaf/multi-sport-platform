import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/config/app_config.dart';
import '../../../../core/services/api_client.dart';
import '../../../tenant/presentation/providers/tenant_provider.dart';
import '../../domain/tenant_branding.dart';

final brandingApiClientProvider = Provider<ApiClient>((ref) => ApiClient());

final tenantBrandingProvider = FutureProvider<TenantBranding>((ref) async {
  final selected = ref.watch(tenantStateProvider).selectedTenant;
  final slug = selected?.slug ?? Environment.tenantSlug;
  final response =
      await ref.watch(brandingApiClientProvider).get<Map<String, dynamic>>(
            '/api/public/tenants/$slug',
          );
  return TenantBranding.fromPublicTenant(response.data!);
});
