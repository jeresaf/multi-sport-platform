import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sports_platform_mobile/core/theme/app_theme.dart';
import 'package:sports_platform_mobile/features/branding/domain/tenant_branding.dart';

void main() {
  test('public tenant branding parses API colors and identity', () {
    final branding = TenantBranding.fromPublicTenant({
      'id': 'tenant-1',
      'name': 'Nairobi Sports',
      'shortName': 'NS',
      'branding': {
        'logoUrl': 'https://cdn.example.test/logo.png',
        'primaryColor': '#0F766E',
        'secondaryColor': '#7C3AED',
        'accentColor': '#F59E0B',
        'publicSlug': 'nairobi-sports',
        'contactEmail': 'hello@nairobi.example',
        'contactPhone': '+254700000000',
        'website': 'https://nairobi.example/',
      },
    });

    expect(branding.tenantName, 'Nairobi Sports');
    expect(branding.logoUrl, 'https://cdn.example.test/logo.png');
    expect(branding.primaryColor, const Color(0xFF0F766E));
    expect(branding.secondaryColor, const Color(0xFF7C3AED));
    expect(branding.accentColor, const Color(0xFFF59E0B));
    expect(branding.publicSlug, 'nairobi-sports');
    expect(branding.contactEmail, 'hello@nairobi.example');
    expect(branding.contactPhone, '+254700000000');
    expect(branding.website, 'https://nairobi.example/');
  });

  test('invalid colors fall back without breaking theme construction', () {
    expect(parseBrandColor('not-a-color'), isNull);
    final theme = AppTheme.lightThemeFor(primary: const Color(0xFF0F766E));
    expect(theme.colorScheme.primary, const Color(0xFF0F766E));
  });
}
