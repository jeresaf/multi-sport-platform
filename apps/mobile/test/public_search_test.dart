import 'package:flutter_test/flutter_test.dart';
import 'package:sports_platform_mobile/features/search/data/public_search.dart';

void main() {
  test('public search parses all canonical discovery fields', () {
    final response = PublicSearchResponse.fromJson({
      'total': 1,
      'items': [
        {
          'id': 'football-id',
          'type': 'Sport',
          'title': 'Football',
          'subtitle': 'Association football',
          'status': 'Active',
          'href': '/sport/football',
        },
      ],
    });

    expect(response.total, 1);
    expect(response.items, hasLength(1));
    expect(response.items.single.type, 'Sport');
    expect(response.items.single.title, 'Football');
    expect(response.items.single.subtitle, 'Association football');
    expect(response.items.single.status, 'Active');
    expect(response.items.single.href, '/sport/football');
  });

  test('public search tolerates absent and malformed item collections', () {
    expect(PublicSearchResponse.fromJson({}).items, isEmpty);
    expect(PublicSearchResponse.fromJson({'items': 'invalid'}).items, isEmpty);
  });
}
