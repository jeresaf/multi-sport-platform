import '../../../../core/services/api_client.dart';
import '../../../../core/services/notification_service.dart';

/// Remote data source for notifications
class NotificationRemoteDataSource {
  final ApiClient _apiClient;

  NotificationRemoteDataSource(this._apiClient);

  /// Get notifications for the current user
  Future<List<NotificationData>> getNotifications({
    int page = 1,
    int pageSize = 20,
    bool unreadOnly = false,
  }) async {
    try {
      final response = await _apiClient.get<Map<String, dynamic>>(
        '/api/notifications',
        queryParameters: {
          'page': page,
          'pageSize': pageSize,
          if (unreadOnly) 'unreadOnly': true,
        },
      );

      final data = response.data?['data'] as List<dynamic>? ?? [];
      return data
          .map((json) => NotificationData.fromMap(json as Map<String, dynamic>))
          .toList();
    } catch (e) {
      // Return empty list on error - notifications are non-critical
      return [];
    }
  }

  /// Mark a notification as read
  Future<bool> markAsRead(String notificationId) async {
    try {
      await _apiClient.put('/api/notifications/$notificationId/read');
      return true;
    } catch (e) {
      return false;
    }
  }

  /// Mark all notifications as read
  Future<bool> markAllAsRead() async {
    try {
      await _apiClient.put('/api/notifications/read-all');
      return true;
    } catch (e) {
      return false;
    }
  }

  /// Delete a notification
  Future<bool> deleteNotification(String notificationId) async {
    try {
      await _apiClient.delete('/api/notifications/$notificationId');
      return true;
    } catch (e) {
      return false;
    }
  }

  /// Get unread notification count
  Future<int> getUnreadCount() async {
    try {
      final response = await _apiClient
          .get<Map<String, dynamic>>('/api/notifications/unread-count');
      return response.data?['count'] ?? 0;
    } catch (e) {
      return 0;
    }
  }
}
