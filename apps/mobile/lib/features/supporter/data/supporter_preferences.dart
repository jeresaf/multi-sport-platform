import 'dart:async';
import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../../core/services/notification_service.dart';
import '../../../core/services/service_locator.dart';

class SupporterFavourite {
  final String type;
  final String id;
  final String title;
  final String? subtitle;
  final String route;

  const SupporterFavourite(
      {required this.type,
      required this.id,
      required this.title,
      this.subtitle,
      required this.route});

  String get key => '$type:$id';
  Map<String, dynamic> toJson() => {
        'type': type,
        'id': id,
        'title': title,
        if (subtitle != null) 'subtitle': subtitle,
        'route': route
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
  final Map<String, Set<String>> alertPreferences;
  final bool lowDataMode;
  final bool notificationsEnabled;
  final String themeMode;

  const SupporterPreferencesState({
    this.favourites = const {},
    this.alertPreferences = const {},
    this.lowDataMode = false,
    this.notificationsEnabled = false,
    this.themeMode = 'system',
  });

  bool isFavourite(String type, String id) =>
      favourites.containsKey('$type:$id');
  Set<String> alertsFor(String type, String id) =>
      alertPreferences['$type:$id'] ?? const {};

  SupporterPreferencesState copyWith(
          {Map<String, SupporterFavourite>? favourites,
          Map<String, Set<String>>? alertPreferences,
          bool? lowDataMode,
          bool? notificationsEnabled,
          String? themeMode}) =>
      SupporterPreferencesState(
        favourites: favourites ?? this.favourites,
        alertPreferences: alertPreferences ?? this.alertPreferences,
        lowDataMode: lowDataMode ?? this.lowDataMode,
        notificationsEnabled: notificationsEnabled ?? this.notificationsEnabled,
        themeMode: themeMode ?? this.themeMode,
      );
}

class SupporterPreferencesNotifier
    extends StateNotifier<SupporterPreferencesState> {
  static const _favouritesKey = 'supporter_favourites_v1';
  static const _alertsKey = 'supporter_alert_preferences_v1';
  static const _lowDataKey = 'supporter_low_data_mode';
  static const _notificationsKey = 'supporter_notifications_enabled';
  static const _themeModeKey = 'supporter_theme_mode';
  final SharedPreferences _preferences;
  final NotificationService _notifications;

  SupporterPreferencesNotifier(this._preferences,
      [NotificationService? notifications])
      : _notifications = notifications ?? NotificationService(),
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
          final item =
              SupporterFavourite.fromJson(Map<String, dynamic>.from(value));
          if (item.id.isNotEmpty) favourites[item.key] = item;
        }
      } catch (_) {/* Preserve remaining valid favourites. */}
    }
    final alerts = <String, Set<String>>{};
    try {
      final decoded = jsonDecode(preferences.getString(_alertsKey) ?? '{}');
      if (decoded is Map) {
        for (final entry in decoded.entries) {
          if (entry.value is List) {
            alerts[entry.key.toString()] =
                (entry.value as List).map((item) => item.toString()).toSet();
          }
        }
      }
    } catch (_) {/* Ignore corrupt alert preferences. */}
    return SupporterPreferencesState(
      favourites: favourites,
      alertPreferences: alerts,
      lowDataMode: preferences.getBool(_lowDataKey) ?? false,
      notificationsEnabled: preferences.getBool(_notificationsKey) ?? false,
      themeMode: preferences.getString(_themeModeKey) ?? 'system',
    );
  }

  Future<void> toggleFavourite(SupporterFavourite item) async {
    final favourites = Map<String, SupporterFavourite>.from(state.favourites);
    final alerts = _copyAlerts(state.alertPreferences);
    if (favourites.remove(item.key) != null) {
      final removed = alerts.remove(item.key) ?? const <String>{};
      if (state.notificationsEnabled) {
        for (final kind in removed) {
          unawaited(
              _notifications.unsubscribeFromTopic(_alertTopic(item, kind)));
        }
      }
    } else {
      favourites[item.key] = item;
    }
    state = state.copyWith(favourites: favourites, alertPreferences: alerts);
    await _preferences.setStringList(_favouritesKey,
        favourites.values.map((value) => jsonEncode(value.toJson())).toList());
    await _persistAlerts(alerts);
  }

  Future<void> setAlertPreferences(
      SupporterFavourite item, Set<String> kinds) async {
    if (!state.isFavourite(item.type, item.id)) return;
    final alerts = _copyAlerts(state.alertPreferences);
    final previous = alerts[item.key] ?? const <String>{};
    if (kinds.isEmpty) {
      alerts.remove(item.key);
    } else {
      alerts[item.key] = Set<String>.from(kinds);
    }
    if (state.notificationsEnabled) {
      for (final kind in previous.difference(kinds)) {
        await _notifications.unsubscribeFromTopic(_alertTopic(item, kind));
      }
      for (final kind in kinds.difference(previous)) {
        await _notifications.subscribeToTopic(_alertTopic(item, kind));
      }
    }
    state = state.copyWith(alertPreferences: alerts);
    await _persistAlerts(alerts);
  }

  Future<void> setLowDataMode(bool enabled) async {
    state = state.copyWith(lowDataMode: enabled);
    await _preferences.setBool(_lowDataKey, enabled);
  }

  Future<bool> setNotificationsEnabled(bool enabled) async {
    final granted = !enabled || await _notifications.requestPermissions();
    final value = enabled && granted;
    state = state.copyWith(notificationsEnabled: value);
    await _preferences.setBool(_notificationsKey, value);
    if (value) {
      await _synchronizeTopics();
    } else {
      await _notifications.disableRegisteredDevice();
      for (final favourite in state.favourites.values) {
        for (final kind in state.alertsFor(favourite.type, favourite.id)) {
          await _notifications
              .unsubscribeFromTopic(_alertTopic(favourite, kind));
        }
      }
    }
    return value;
  }

  Future<void> setThemeMode(String value) async {
    state = state.copyWith(themeMode: value);
    await _preferences.setString(_themeModeKey, value);
  }

  Future<void> _synchronizeTopics() async {
    for (final favourite in state.favourites.values) {
      for (final kind in state.alertsFor(favourite.type, favourite.id)) {
        await _notifications.subscribeToTopic(_alertTopic(favourite, kind));
      }
    }
  }

  Future<void> _persistAlerts(Map<String, Set<String>> alerts) =>
      _preferences.setString(
        _alertsKey,
        jsonEncode(alerts.map((key, value) => MapEntry(key, value.toList()))),
      );
  String _alertTopic(SupporterFavourite favourite, String kind) =>
      'favourite-${favourite.type}-${favourite.id}-${alertNotificationType(kind)}';
  static Map<String, Set<String>> _copyAlerts(
          Map<String, Set<String>> source) =>
      {
        for (final entry in source.entries)
          entry.key: Set<String>.from(entry.value)
      };
}

const supporterBasicAlerts = <String, String>{
  'start': 'Start',
  'score': 'Score changes',
  'periodEnd': 'Period end',
  'final': 'Final result',
  'postponement': 'Postponement',
};
String alertNotificationType(String kind) => switch (kind) {
      'start' => 'contest_started',
      'score' => 'score_changed',
      'periodEnd' => 'period_ended',
      'final' => 'final_result',
      'postponement' => 'postponement',
      'goal' => 'goal',
      'wicket' => 'wicket',
      'roundResult' => 'round_result',
      'teeTime' => 'tee_time',
      'fightResult' => 'fight_result',
      _ => kind,
    };
Map<String, String> supporterAlertOptions(String _) => {
      ...supporterBasicAlerts,
    };

final supporterSharedPreferencesProvider = Provider<SharedPreferences>(
    (ref) => ServiceLocator.get<SharedPreferences>());
final supporterPreferencesProvider = StateNotifierProvider<
        SupporterPreferencesNotifier, SupporterPreferencesState>(
    (ref) => SupporterPreferencesNotifier(
        ref.watch(supporterSharedPreferencesProvider), NotificationService()));
