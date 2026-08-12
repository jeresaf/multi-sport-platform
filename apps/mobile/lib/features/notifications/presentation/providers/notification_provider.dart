import 'dart:async';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/services/api_client.dart';
import '../../../../core/services/notification_service.dart';
import '../../data/datasources/notification_remote_datasource.dart';

// Re-export NotificationData for convenience
export '../../../../core/services/notification_service.dart'
    show NotificationData;

/// Provider for notification remote data source
final notificationRemoteDataSourceProvider =
    Provider<NotificationRemoteDataSource>((ref) {
  final apiClient = ref.watch(apiClientProvider);
  return NotificationRemoteDataSource(apiClient);
});

/// Provider for API client (shared)
final apiClientProvider = Provider<ApiClient>((ref) {
  return ApiClient();
});

/// State for notifications
class NotificationsState {
  final List<NotificationData> notifications;
  final int unreadCount;
  final bool isLoading;
  final bool isLoadingMore;
  final String? error;
  final int currentPage;
  final bool hasMore;

  const NotificationsState({
    this.notifications = const [],
    this.unreadCount = 0,
    this.isLoading = false,
    this.isLoadingMore = false,
    this.error,
    this.currentPage = 1,
    this.hasMore = true,
  });

  NotificationsState copyWith({
    List<NotificationData>? notifications,
    int? unreadCount,
    bool? isLoading,
    bool? isLoadingMore,
    String? error,
    int? currentPage,
    bool? hasMore,
  }) {
    return NotificationsState(
      notifications: notifications ?? this.notifications,
      unreadCount: unreadCount ?? this.unreadCount,
      isLoading: isLoading ?? this.isLoading,
      isLoadingMore: isLoadingMore ?? this.isLoadingMore,
      error: error,
      currentPage: currentPage ?? this.currentPage,
      hasMore: hasMore ?? this.hasMore,
    );
  }
}

/// Notifier for notifications
class NotificationsNotifier extends StateNotifier<NotificationsState> {
  final NotificationRemoteDataSource _dataSource;
  final NotificationService _notificationService;
  StreamSubscription? _pushNotificationSubscription;

  NotificationsNotifier(this._dataSource, this._notificationService)
      : super(const NotificationsState()) {
    _loadNotifications();
    _loadUnreadCount();
    _subscribeToPushNotifications();
  }

  void _subscribeToPushNotifications() {
    _pushNotificationSubscription =
        _notificationService.notifications.listen((notification) {
      if (notification.id != null &&
          state.notifications.any((item) => item.id == notification.id)) {
        return;
      }
      // Add new push notification to the list
      state = state.copyWith(
        notifications: [notification, ...state.notifications],
        unreadCount: state.unreadCount + 1,
      );
    });
  }

  Future<void> _loadNotifications() async {
    if (state.isLoading) return;

    state = state.copyWith(isLoading: true, error: null);

    try {
      final notifications = await _dataSource.getNotifications(page: 1);
      state = state.copyWith(
        notifications: notifications,
        isLoading: false,
        currentPage: 1,
        hasMore: notifications.length >= 20,
      );
    } catch (e) {
      state = state.copyWith(
        isLoading: false,
        error: e.toString(),
      );
    }
  }

  Future<void> _loadUnreadCount() async {
    try {
      final count = await _dataSource.getUnreadCount();
      state = state.copyWith(unreadCount: count);
    } catch (e) {
      // Silently fail - unread count is non-critical
    }
  }

  Future<void> loadMore() async {
    if (state.isLoadingMore || !state.hasMore) return;

    state = state.copyWith(isLoadingMore: true);

    try {
      final nextPage = state.currentPage + 1;
      final moreNotifications =
          await _dataSource.getNotifications(page: nextPage);

      state = state.copyWith(
        notifications: [...state.notifications, ...moreNotifications],
        isLoadingMore: false,
        currentPage: nextPage,
        hasMore: moreNotifications.length >= 20,
      );
    } catch (e) {
      state = state.copyWith(
        isLoadingMore: false,
        error: e.toString(),
      );
    }
  }

  Future<void> refresh() async {
    await _loadNotifications();
    await _loadUnreadCount();
  }

  Future<void> markAsRead(String notificationId) async {
    final success = await _dataSource.markAsRead(notificationId);
    if (success) {
      state = state.copyWith(
        notifications: state.notifications.map((n) {
          if (n.id == notificationId) {
            return NotificationData(
              id: n.id,
              title: n.title,
              body: n.body,
              payload: n.payload,
              data: {...?n.data, 'isRead': true},
              timestamp: n.timestamp,
            );
          }
          return n;
        }).toList(),
        unreadCount: state.unreadCount > 0 ? state.unreadCount - 1 : 0,
      );
    }
  }

  Future<void> markAllAsRead() async {
    final success = await _dataSource.markAllAsRead();
    if (success) {
      state = state.copyWith(
        notifications: state.notifications.map((n) {
          return NotificationData(
            id: n.id,
            title: n.title,
            body: n.body,
            payload: n.payload,
            data: {...?n.data, 'isRead': true},
            timestamp: n.timestamp,
          );
        }).toList(),
        unreadCount: 0,
      );
    }
  }

  Future<void> deleteNotification(String notificationId) async {
    final success = await _dataSource.deleteNotification(notificationId);
    if (success) {
      final notification = state.notifications.firstWhere(
        (n) => n.id == notificationId,
        orElse: () => const NotificationData(title: '', body: ''),
      );
      final wasUnread = notification.data?['isRead'] != true;

      state = state.copyWith(
        notifications:
            state.notifications.where((n) => n.id != notificationId).toList(),
        unreadCount: wasUnread && state.unreadCount > 0
            ? state.unreadCount - 1
            : state.unreadCount,
      );
    }
  }

  @override
  void dispose() {
    _pushNotificationSubscription?.cancel();
    super.dispose();
  }
}

/// Provider for notifications
final notificationsProvider =
    StateNotifierProvider<NotificationsNotifier, NotificationsState>((ref) {
  final dataSource = ref.watch(notificationRemoteDataSourceProvider);
  final notificationService = NotificationService();
  return NotificationsNotifier(dataSource, notificationService);
});

/// Provider for unread notification count (simplified)
final unreadNotificationCountProvider = Provider<int>((ref) {
  return ref.watch(notificationsProvider).unreadCount;
});
