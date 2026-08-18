import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/config/app_config.dart';
import '../../../core/services/api_client.dart';
import '../../tenant/presentation/providers/tenant_provider.dart';

typedef PublicSearchQuery = ({String term, String type});

final publicSearchApiClientProvider = Provider<ApiClient>((_) => ApiClient());

final publicSearchProvider = FutureProvider.autoDispose
    .family<PublicSearchResponse, PublicSearchQuery>((ref, query) async {
  ref.watch(tenantStateProvider.select((state) => state.selectedTenant?.slug));
  final response = await ref.watch(publicSearchApiClientProvider).get(
    '/api/public/tenants/${Environment.tenantSlug}/search',
    queryParameters: {
      'q': query.term,
      'type': query.type,
      'pageSize': 50,
    },
  );
  return PublicSearchResponse.fromJson(
    Map<String, dynamic>.from(response.data as Map),
  );
});

class PublicSearchResponse {
  const PublicSearchResponse({required this.items, required this.total});

  final List<PublicSearchItem> items;
  final int total;

  factory PublicSearchResponse.fromJson(Map<String, dynamic> json) =>
      PublicSearchResponse(
        total: (json['total'] as num?)?.toInt() ?? 0,
        items: json['items'] is List
            ? (json['items'] as List)
                .whereType<Map>()
                .map((item) => PublicSearchItem.fromJson(
                      Map<String, dynamic>.from(item),
                    ))
                .toList()
            : const [],
      );
}

class PublicSearchItem {
  const PublicSearchItem({
    required this.id,
    required this.type,
    required this.title,
    this.subtitle,
    this.status,
    this.href,
  });

  final String id;
  final String type;
  final String title;
  final String? subtitle;
  final String? status;
  final String? href;

  factory PublicSearchItem.fromJson(Map<String, dynamic> json) =>
      PublicSearchItem(
        id: json['id']?.toString() ?? '',
        type: json['type']?.toString() ?? 'Result',
        title: json['title']?.toString() ?? 'Public result',
        subtitle: json['subtitle']?.toString(),
        status: json['status']?.toString(),
        href: json['href']?.toString(),
      );
}
