import 'dart:async';
import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../../core/services/service_locator.dart';
import '../../../core/services/notification_service.dart';

class SupporterFavourite {
  final String type;
  final String id;
  final String title;
  final String? subtitle;
  final String route;

  const SupporterFavourite({
    required this.type,
    required this.id,
    required this.title,
    this.subtitle,
    required this.route,
  });

  String get key => '$type:$id';

  Map<String, dynamic> toJson() => {
        'type': type,
        'id': id,
        'title': title,
        if (subtitle != null) 'subtitle': subtitle,
        'route': route,
      };

  factory SupporterFavourite.fromJson(Map<String, dynamic> json) =>
      SupporterFavourite(
        type: json['type']?.toString() ?? 'item',
        id: json['id']?.toString() ?? '',
        title: json['title']?.toString() ?? 'Favourite',
        subtitle: json['subtitle']?.toString(),
        route: json['route']?.toString() ?? '/',
      );
}

class SupporterPreferencesState {
  final Map<String, SupporterFavourite> favourites;
  final bool lowDataMode;
  final bool notificationsEnabled;

  const SupporterPreferencesState({
    this.favourites = const {},
    this.lowDataMode = false,
    this.notificationsEnabled = false,
  });

  bool isFavourite(String type, String id) =>
      favourites.containsKey('$type:$id');
}

class SupporterPreferencesNotifier
    extends StateNotifier<SupporterPreferencesState> {
  static const _favouritesKey = 'supporter_favourites_v1';
  static const _lowDataKey = 'supporter_low_data_mode';
  static const _notificationsKey = 'supporter_notifications_enabled';
  final SharedPreferences _preferences;
  final NotificationService _notifications;

  SupporterPreferencesNotifier(
    this._preferences, [
    NotificationService? notifications,
  ])  : _notifications = notifications ?? NotificationService(),
        super(_load(_preferences)) {
    if (state.notificationsEnabled) unawaited(_synchronizeTopics());
  }

  static SupporterPreferencesState _load(SharedPreferences preferences) {
    final favourites = <String, SupporterFavourite>{};
    for (final encoded
        in preferences.getStringList(_favouritesKey) ?? const <String>[]) {
      try {
        final value = jsonDecode(encoded);
        if (value is Map) {
          final item = SupporterFavourite.fromJson(
            Map<String, dynamic>.from(value),
          );
          if (item.id.isNotEmpty) favourites[item.key] = item;
        }
      } catch (_) {
        // Ignore a corrupt legacy favourite and preserve the remaining items.
      }
    }
    return SupporterPreferencesState(
      favourites: favourites,
      lowDataMode: preferences.getBool(_lowDataKey) ?? false,
      notificationsEnabled: preferences.getBool(_notificationsKey) ?? false,
    );
  }

  Future<void> toggleFavourite(SupporterFavourite item) async {
    final next = Map<String, SupporterFavourite>.from(state.favourites);
    if (next.containsKey(item.key)) {
      next.remove(item.key);
      if (state.notificationsEnabled) {
        unawaited(_notifications.unsubscribeFromTopic(_topic(item)));
      }
    } else {
      next[item.key] = item;
      if (state.notificationsEnabled) {
        unawaited(_notifications.subscribeToTopic(_topic(item)));
      }
    }
    state = SupporterPreferencesState(
      favourites: next,
      lowDataMode: state.lowDataMode,
      notificationsEnabled: state.notificationsEnabled,
    );
    await _preferences.setStringList(
      _favouritesKey,
      next.values.map((value) => jsonEncode(value.toJson())).toList(),
    );
  }

  Future<void> setLowDataMode(bool enabled) async {
    state = SupporterPreferencesState(
      favourites: state.favourites,
      lowDataMode: enabled,
      notificationsEnabled: state.notificationsEnabled,
    );
    await _preferences.setBool(_lowDataKey, enabled);
  }

  Future<bool> setNotificationsEnabled(bool enabled) async {
    final granted = !enabled || await _notifications.requestPermissions();
    final value = enabled && granted;
    state = SupporterPreferencesState(
      favourites: state.favourites,
      lowDataMode: state.lowDataMode,
      notificationsEnabled: value,
    );
    await _preferences.setBool(_notificationsKey, value);
    if (value) {
      await _synchronizeTopics();
    } else {
      await _notifications.disableRegisteredDevice();
      for (final favourite in state.favourites.values) {
        await _notifications.unsubscribeFromTopic(_topic(favourite));
      }
    }
    return value;
  }

  Future<void> _synchronizeTopics() async {
    for (final favourite in state.favourites.values) {
      await _notifications.subscribeToTopic(_topic(favourite));
    }
  }

  String _topic(SupporterFavourite favourite) =>
      'favourite-${favourite.type}-${favourite.id}';
}

final supporterSharedPreferencesProvider = Provider<SharedPreferences>((ref) {
  return ServiceLocator.get<SharedPreferences>();
});

final supporterPreferencesProvider = StateNotifierProvider<
    SupporterPreferencesNotifier, SupporterPreferencesState>((ref) {
  return SupporterPreferencesNotifier(
    ref.watch(supporterSharedPreferencesProvider),
    NotificationService(),
  );
});
