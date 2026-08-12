import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../services/connectivity_service.dart';
import '../services/offline_sync_service.dart';
import '../services/service_locator.dart';

/// Provider for sync status
final syncStatusProvider = StreamProvider<SyncStatus>((ref) {
  final syncService = ServiceLocator.get<OfflineSyncService>();
  return syncService.syncStatus;
});

/// Offline indicator widget that shows connectivity status and pending sync count
class OfflineIndicator extends ConsumerWidget {
  final Widget child;

  const OfflineIndicator({
    super.key,
    required this.child,
  });

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final connectivityState = ref.watch(connectivityServiceProvider);
    final syncStatusAsync = ref.watch(syncStatusProvider);

    return Column(
      children: [
        // Offline banner
        if (!connectivityState.isOnline) _OfflineBanner(),

        // Sync status indicator (when online but has pending items)
        if (connectivityState.isOnline)
          syncStatusAsync.when(
            data: (status) => status.pendingCount > 0
                ? _SyncStatusBar(
                    pendingCount: status.pendingCount,
                    isSyncing: status.isSyncing,
                    lastSyncTime: status.lastSyncTime,
                  )
                : const SizedBox.shrink(),
            loading: () => const SizedBox.shrink(),
            error: (_, __) => const SizedBox.shrink(),
          ),

        // Main content
        Expanded(child: child),
      ],
    );
  }
}

class _OfflineBanner extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 16),
      color: Colors.orange.shade700,
      child: const SafeArea(
        bottom: false,
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(
              Icons.cloud_off,
              color: Colors.white,
              size: 18,
            ),
            SizedBox(width: 8),
            Text(
              'You are offline. Changes will sync when connected.',
              style: TextStyle(
                color: Colors.white,
                fontSize: 13,
                fontWeight: FontWeight.w500,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _SyncStatusBar extends StatelessWidget {
  final int pendingCount;
  final bool isSyncing;
  final DateTime? lastSyncTime;

  const _SyncStatusBar({
    required this.pendingCount,
    required this.isSyncing,
    this.lastSyncTime,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(vertical: 6, horizontal: 16),
      color: Colors.blue.shade50,
      child: SafeArea(
        bottom: false,
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            if (isSyncing)
              const SizedBox(
                width: 14,
                height: 14,
                child: CircularProgressIndicator(
                  strokeWidth: 2,
                ),
              )
            else
              Icon(
                Icons.sync,
                color: Colors.blue.shade700,
                size: 16,
              ),
            const SizedBox(width: 8),
            Text(
              isSyncing
                  ? 'Syncing $pendingCount items...'
                  : '$pendingCount pending ${pendingCount == 1 ? 'item' : 'items'} to sync',
              style: TextStyle(
                color: Colors.blue.shade700,
                fontSize: 12,
                fontWeight: FontWeight.w500,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Compact offline indicator for use in app bar or status area
class CompactOfflineIndicator extends ConsumerWidget {
  const CompactOfflineIndicator({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final connectivityState = ref.watch(connectivityServiceProvider);
    final syncStatusAsync = ref.watch(syncStatusProvider);

    if (connectivityState.isOnline) {
      return syncStatusAsync.when(
        data: (status) => status.pendingCount > 0
            ? Tooltip(
                message: '${status.pendingCount} items pending sync',
                child: Badge(
                  label: Text('${status.pendingCount}'),
                  child: Icon(
                    status.isSyncing ? Icons.sync : Icons.sync_disabled,
                    size: 20,
                    color: Colors.blue,
                  ),
                ),
              )
            : const SizedBox.shrink(),
        loading: () => const SizedBox.shrink(),
        error: (_, __) => const SizedBox.shrink(),
      );
    }

    return const Tooltip(
      message: 'Offline mode',
      child: Icon(
        Icons.cloud_off,
        size: 20,
        color: Colors.orange,
      ),
    );
  }
}

/// Floating offline indicator button
class FloatingOfflineIndicator extends ConsumerWidget {
  const FloatingOfflineIndicator({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final connectivityState = ref.watch(connectivityServiceProvider);
    final syncStatusAsync = ref.watch(syncStatusProvider);

    // Only show when offline or has pending items
    if (connectivityState.isOnline) {
      final syncStatus = syncStatusAsync.valueOrNull;
      if (syncStatus == null || syncStatus.pendingCount == 0) {
        return const SizedBox.shrink();
      }
    }

    return Positioned(
      bottom: 16,
      right: 16,
      child: syncStatusAsync.when(
        data: (status) => FloatingActionButton.small(
          onPressed: () {
            // Trigger manual sync
            final syncService = ServiceLocator.get<OfflineSyncService>();
            syncService.syncPendingEvents();
          },
          backgroundColor:
              connectivityState.isOnline ? Colors.blue : Colors.orange,
          child: status.isSyncing
              ? const SizedBox(
                  width: 20,
                  height: 20,
                  child: CircularProgressIndicator(
                    strokeWidth: 2,
                    color: Colors.white,
                  ),
                )
              : Icon(
                  connectivityState.isOnline ? Icons.sync : Icons.cloud_off,
                  color: Colors.white,
                ),
        ),
        loading: () => const FloatingActionButton.small(
          onPressed: null,
          backgroundColor: Colors.grey,
          child: SizedBox(
            width: 20,
            height: 20,
            child: CircularProgressIndicator(
              strokeWidth: 2,
              color: Colors.white,
            ),
          ),
        ),
        error: (_, __) => const SizedBox.shrink(),
      ),
    );
  }
}
