import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../../../../core/services/notification_service.dart';
import '../providers/notification_provider.dart';

/// Notifications screen - displays all notifications
class NotificationsScreen extends ConsumerWidget {
  const NotificationsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final notificationsState = ref.watch(notificationsProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Notifications'),
        actions: [
          if (notificationsState.unreadCount > 0)
            TextButton(
              onPressed: () {
                ref.read(notificationsProvider.notifier).markAllAsRead();
              },
              child: const Text('Mark all read'),
            ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          await ref.read(notificationsProvider.notifier).refresh();
        },
        child: _buildContent(context, ref, notificationsState),
      ),
    );
  }

  Widget _buildContent(
    BuildContext context,
    WidgetRef ref,
    NotificationsState state,
  ) {
    if (state.isLoading && state.notifications.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }

    if (state.error != null && state.notifications.isEmpty) {
      return Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(Icons.error_outline, size: 64, color: Colors.red.shade300),
            const SizedBox(height: 16),
            Text(
              'Failed to load notifications',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            ElevatedButton(
              onPressed: () {
                ref.read(notificationsProvider.notifier).refresh();
              },
              child: const Text('Retry'),
            ),
          ],
        ),
      );
    }

    if (state.notifications.isEmpty) {
      return Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(Icons.notifications_none,
                size: 64, color: Colors.grey.shade400),
            const SizedBox(height: 16),
            Text(
              'No notifications yet',
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: Colors.grey,
                  ),
            ),
            const SizedBox(height: 8),
            Text(
              'You\'ll see updates about contests and competitions here',
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: Colors.grey,
                  ),
              textAlign: TextAlign.center,
            ),
          ],
        ),
      );
    }

    return ListView.builder(
      padding: const EdgeInsets.symmetric(vertical: 8),
      itemCount: state.notifications.length + (state.isLoadingMore ? 1 : 0),
      itemBuilder: (context, index) {
        if (index == state.notifications.length) {
          // Loading indicator at the bottom
          return const Padding(
            padding: EdgeInsets.all(16),
            child: Center(child: CircularProgressIndicator()),
          );
        }

        final notification = state.notifications[index];
        final isRead = notification.data?['isRead'] == true;

        return _NotificationTile(
          notification: notification,
          isRead: isRead,
          onTap: () => _handleNotificationTap(context, ref, notification),
          onDismissed: () {
            if (notification.id != null) {
              ref
                  .read(notificationsProvider.notifier)
                  .deleteNotification(notification.id!);
            }
          },
        );
      },
    );
  }

  void _handleNotificationTap(
    BuildContext context,
    WidgetRef ref,
    NotificationData notification,
  ) {
    // Mark as read
    if (notification.id != null) {
      ref.read(notificationsProvider.notifier).markAsRead(notification.id!);
    }

    // Navigate based on payload
    final payload = notification.payload;
    if (payload != null) {
      if (payload.startsWith('contest:')) {
        final contestId = payload.replaceFirst('contest:', '');
        context.push('/contest/$contestId');
      } else if (payload.startsWith('competition:')) {
        final competitionId = payload.replaceFirst('competition:', '');
        context.push('/competitions/$competitionId');
      }
    }
  }
}

class _NotificationTile extends StatelessWidget {
  final NotificationData notification;
  final bool isRead;
  final VoidCallback onTap;
  final VoidCallback onDismissed;

  const _NotificationTile({
    required this.notification,
    required this.isRead,
    required this.onTap,
    required this.onDismissed,
  });

  @override
  Widget build(BuildContext context) {
    return Dismissible(
      key: Key(notification.id ?? notification.hashCode.toString()),
      direction: DismissDirection.endToStart,
      onDismissed: (_) => onDismissed(),
      background: Container(
        alignment: Alignment.centerRight,
        padding: const EdgeInsets.only(right: 16),
        color: Colors.red,
        child: const Icon(Icons.delete, color: Colors.white),
      ),
      child: ListTile(
        leading: CircleAvatar(
          backgroundColor:
              _getNotificationColor(notification).withValues(alpha: 0.1),
          child: Icon(
            _getNotificationIcon(notification),
            color: _getNotificationColor(notification),
          ),
        ),
        title: Text(
          notification.title,
          style: TextStyle(
            fontWeight: isRead ? FontWeight.normal : FontWeight.bold,
          ),
        ),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              notification.body,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
            ),
            if (notification.timestamp != null)
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Text(
                  _formatTimestamp(notification.timestamp!),
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: Colors.grey,
                      ),
                ),
              ),
          ],
        ),
        trailing: !isRead
            ? Container(
                width: 10,
                height: 10,
                decoration: const BoxDecoration(
                  color: Colors.blue,
                  shape: BoxShape.circle,
                ),
              )
            : null,
        onTap: onTap,
      ),
    );
  }

  IconData _getNotificationIcon(NotificationData notification) {
    final type = notification.data?['type'] as String?;
    switch (type) {
      case NotificationType.fixtureReminder:
        return Icons.event;
      case NotificationType.contestStarted:
      case NotificationType.contestLive:
        return Icons.play_circle;
      case NotificationType.finalResult:
      case NotificationType.contestEnded:
        return Icons.flag;
      case NotificationType.scoreChanged:
      case NotificationType.scoreUpdate:
        return Icons.score;
      case NotificationType.periodEnded:
        return Icons.timer_off;
      case NotificationType.postponement:
        return Icons.event_busy;
      case NotificationType.qualification:
        return Icons.emoji_events;
      case NotificationType.announcement:
        return Icons.campaign;
      case NotificationType.registrationOpen:
        return Icons.event_available;
      case NotificationType.registrationClosed:
        return Icons.event_busy;
      case NotificationType.resultApproved:
        return Icons.check_circle;
      case NotificationType.correction:
        return Icons.edit;
      default:
        return Icons.notifications;
    }
  }

  Color _getNotificationColor(NotificationData notification) {
    final type = notification.data?['type'] as String?;
    switch (type) {
      case NotificationType.contestStarted:
      case NotificationType.contestLive:
        return Colors.red;
      case NotificationType.finalResult:
      case NotificationType.contestEnded:
        return Colors.green;
      case NotificationType.scoreChanged:
      case NotificationType.scoreUpdate:
        return Colors.blue;
      case NotificationType.fixtureReminder:
        return Colors.indigo;
      case NotificationType.periodEnded:
        return Colors.deepOrange;
      case NotificationType.postponement:
        return Colors.grey;
      case NotificationType.qualification:
        return Colors.purple;
      case NotificationType.announcement:
        return Colors.teal;
      case NotificationType.registrationOpen:
        return Colors.orange;
      case NotificationType.registrationClosed:
        return Colors.grey;
      case NotificationType.resultApproved:
        return Colors.green;
      case NotificationType.correction:
        return Colors.amber;
      default:
        return Colors.blue;
    }
  }

  String _formatTimestamp(DateTime timestamp) {
    final now = DateTime.now();
    final difference = now.difference(timestamp);

    if (difference.inMinutes < 1) {
      return 'Just now';
    } else if (difference.inMinutes < 60) {
      return '${difference.inMinutes}m ago';
    } else if (difference.inHours < 24) {
      return '${difference.inHours}h ago';
    } else if (difference.inDays < 7) {
      return '${difference.inDays}d ago';
    } else {
      return '${timestamp.day}/${timestamp.month}/${timestamp.year}';
    }
  }
}
