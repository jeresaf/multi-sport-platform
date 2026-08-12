import 'package:flutter_test/flutter_test.dart';
import 'package:sports_platform_mobile/core/config/app_config.dart';
import 'package:sports_platform_mobile/features/auth/presentation/providers/auth_provider.dart';
import 'package:sports_platform_mobile/features/tenant/domain/models/tenant.dart';

void main() {
  test('tenant API identity survives cache serialization', () {
    final tenant = Tenant.fromJson({
      'id': 'tenant-id',
      'name': 'Six Nations',
      'slug': 'six-nations',
      'description': 'Rugby',
    });

    expect(tenant.slug, 'six-nations');
    expect(Tenant.fromJson(tenant.toJson()).slug, 'six-nations');
  });

  test('selecting a tenant updates runtime public API scope', () {
    Environment.selectTenant('  SIX-NATIONS  ');
    expect(Environment.tenantSlug, 'six-nations');
  });

  test('scoring navigation requires a scorer-capable role', () {
    expect(
      const AuthState(isAuthenticated: true, roles: ['Supporter']).canScore,
      isFalse,
    );
    expect(
      const AuthState(isAuthenticated: true, roles: ['Scorer']).canScore,
      isTrue,
    );
    expect(
      const AuthState(isAuthenticated: true, roles: ['Tenant Admin']).canScore,
      isTrue,
    );
  });
}
