import 'package:flutter/material.dart';

class TenantBranding {
  final String tenantId;
  final String tenantName;
  final String shortName;
  final String? logoUrl;
  final Color? primaryColor;
  final Color? secondaryColor;
  final Color? accentColor;
  final String? publicSlug;
  final String? contactEmail;
  final String? contactPhone;
  final String? website;

  const TenantBranding({
    required this.tenantId,
    required this.tenantName,
    required this.shortName,
    this.logoUrl,
    this.primaryColor,
    this.secondaryColor,
    this.accentColor,
    this.publicSlug,
    this.contactEmail,
    this.contactPhone,
    this.website,
  });

  factory TenantBranding.fromPublicTenant(Map<String, dynamic> json) {
    final branding = json['branding'] is Map
        ? Map<String, dynamic>.from(json['branding'] as Map)
        : const <String, dynamic>{};
    return TenantBranding(
      tenantId: json['id']?.toString() ?? '',
      tenantName: json['name']?.toString() ?? 'Sports Platform',
      shortName: json['shortName']?.toString() ?? '',
      logoUrl: branding['logoUrl']?.toString(),
      primaryColor: parseBrandColor(branding['primaryColor']?.toString()),
      secondaryColor: parseBrandColor(branding['secondaryColor']?.toString()),
      accentColor: parseBrandColor(branding['accentColor']?.toString()),
      publicSlug: branding['publicSlug']?.toString(),
      contactEmail: branding['contactEmail']?.toString(),
      contactPhone: branding['contactPhone']?.toString(),
      website: branding['website']?.toString(),
    );
  }
}

Color? parseBrandColor(String? value) {
  if (value == null) return null;
  final normalized = value.trim().replaceFirst('#', '');
  if (!RegExp(r'^[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$').hasMatch(normalized)) {
    return null;
  }
  final argb = normalized.length == 6 ? 'FF$normalized' : normalized;
  return Color(int.parse(argb, radix: 16));
}
