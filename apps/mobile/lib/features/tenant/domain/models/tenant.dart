/// Tenant model
class Tenant {
  final String id;
  final String name;
  final String slug;
  final String? description;
  final String? logoUrl;
  final String? primaryColor;
  final String? secondaryColor;
  final String? domain;
  final bool isActive;
  final DateTime createdAt;

  const Tenant({
    required this.id,
    required this.name,
    required this.slug,
    this.description,
    this.logoUrl,
    this.primaryColor,
    this.secondaryColor,
    this.domain,
    this.isActive = true,
    required this.createdAt,
  });

  factory Tenant.fromJson(Map<String, dynamic> json) {
    return Tenant(
      id: json['id'] as String,
      name: json['name'] as String,
      slug: (json['slug'] ?? json['domain'] ?? '').toString(),
      description: json['description'] as String?,
      logoUrl: json['logoUrl'] as String?,
      primaryColor: json['primaryColor'] as String?,
      secondaryColor: json['secondaryColor'] as String?,
      domain: json['domain'] as String?,
      isActive: json['isActive'] as bool? ?? true,
      createdAt: DateTime.tryParse(json['createdAt']?.toString() ?? '') ??
          DateTime.fromMillisecondsSinceEpoch(0, isUtc: true),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'name': name,
      'slug': slug,
      'description': description,
      'logoUrl': logoUrl,
      'primaryColor': primaryColor,
      'secondaryColor': secondaryColor,
      'domain': domain,
      'isActive': isActive,
      'createdAt': createdAt.toIso8601String(),
    };
  }

  Tenant copyWith({
    String? id,
    String? name,
    String? slug,
    String? description,
    String? logoUrl,
    String? primaryColor,
    String? secondaryColor,
    String? domain,
    bool? isActive,
    DateTime? createdAt,
  }) {
    return Tenant(
      id: id ?? this.id,
      name: name ?? this.name,
      slug: slug ?? this.slug,
      description: description ?? this.description,
      logoUrl: logoUrl ?? this.logoUrl,
      primaryColor: primaryColor ?? this.primaryColor,
      secondaryColor: secondaryColor ?? this.secondaryColor,
      domain: domain ?? this.domain,
      isActive: isActive ?? this.isActive,
      createdAt: createdAt ?? this.createdAt,
    );
  }
}
