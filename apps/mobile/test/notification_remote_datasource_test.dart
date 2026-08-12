import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sports_platform_mobile/core/services/api_client.dart';
import 'package:sports_platform_mobile/features/notifications/data/datasources/notification_remote_datasource.dart';

void main() {
  test('notification inbox parses API payload and preserves navigation data',
      () async {
    final api = _NotificationApiClient();
    final dataSource = NotificationRemoteDataSource(api);

    final notifications = await dataSource.getNotifications(unreadOnly: true);

    expect(api.lastPath, '/api/notifications');
    expect(api.lastQuery?['unreadOnly'], isTrue);
    expect(notifications, hasLength(1));
    expect(notifications.single.id, 'notification-1');
    expect(notifications.single.payload, 'contest:contest-1');
    expect(notifications.single.data?['isRead'], isFalse);
  });

  test('notification mutations use the server contract', () async {
    final api = _NotificationApiClient();
    final dataSource = NotificationRemoteDataSource(api);

    expect(await dataSource.markAsRead('notification-1'), isTrue);
    expect(api.lastPath, '/api/notifications/notification-1/read');
    expect(await dataSource.markAllAsRead(), isTrue);
    expect(api.lastPath, '/api/notifications/read-all');
    expect(await dataSource.deleteNotification('notification-1'), isTrue);
    expect(api.lastPath, '/api/notifications/notification-1');
  });
}

class _NotificationApiClient extends ApiClient {
  String? lastPath;
  Map<String, dynamic>? lastQuery;

  @override
  Future<Response<T>> get<T>(
    String path, {
    Map<String, dynamic>? queryParameters,
    Options? options,
  }) async {
    lastPath = path;
    lastQuery = queryParameters;
    final Object payload = path.endsWith('unread-count')
        ? <String, dynamic>{'count': 1}
        : <String, dynamic>{
            'data': <Object>[
              <String, dynamic>{
                'id': 'notification-1',
                'title': 'Match started',
                'body': 'The contest is live.',
                'payload': 'contest:contest-1',
                'data': <String, dynamic>{
                  'type': 'contest_live',
                  'isRead': false,
                },
                'timestamp': '2026-08-04T12:00:00Z',
              },
            ],
          };
    return Response<T>(
      requestOptions: RequestOptions(path: path),
      data: payload as T,
      statusCode: 200,
    );
  }

  @override
  Future<Response<T>> put<T>(
    String path, {
    dynamic data,
    Map<String, dynamic>? queryParameters,
    Options? options,
  }) async {
    lastPath = path;
    return Response<T>(
      requestOptions: RequestOptions(path: path),
      statusCode: 204,
    );
  }

  @override
  Future<Response<T>> delete<T>(
    String path, {
    dynamic data,
    Map<String, dynamic>? queryParameters,
    Options? options,
  }) async {
    lastPath = path;
    return Response<T>(
      requestOptions: RequestOptions(path: path),
      statusCode: 204,
    );
  }
}
