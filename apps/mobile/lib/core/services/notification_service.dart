import 'dart:async';

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:timezone/data/latest.dart' as timezone_data;
import 'package:timezone/timezone.dart' as timezone;
import 'package:uuid/uuid.dart';

import '../config/app_config.dart';
import 'api_client.dart';
import 'recovering_secure_storage.dart';

@pragma('vm:entry-point')
Future<void> firebaseMessagingBackgroundHandler(RemoteMessage message) async {
  if (Firebase.apps.isEmpty) await Firebase.initializeApp();
}

enum PushPermissionStatus {
  unavailable,
  notDetermined,
  denied,
  authorized,
  provisional,
}

/// Owns FCM lifecycle and foreground local-notification presentation.
class NotificationService {
  static final NotificationService _instance = NotificationService._internal();
  factory NotificationService() => _instance;
  NotificationService._internal();

  static const _channel = AndroidNotificationChannel(
    'sports_platform_live_updates',
    'Live sports updates',
    description: 'Contest, result, and competition updates',
    importance: Importance.high,
  );
  static const _deviceIdKey = 'push_device_id';

  final FlutterLocalNotificationsPlugin _localNotifications =
      FlutterLocalNotificationsPlugin();
  final FlutterSecureStorage _secureStorage = appSecureStorage;
  final StreamController<NotificationData> _notificationController =
      StreamController<NotificationData>.broadcast();
  final StreamController<NotificationData> _interactionController =
      StreamController<NotificationData>.broadcast();
  final StreamController<PushPermissionStatus> _permissionController =
      StreamController<PushPermissionStatus>.broadcast();

  StreamSubscription<RemoteMessage>? _foregroundSubscription;
  StreamSubscription<RemoteMessage>? _openedSubscription;
  StreamSubscription<String>? _tokenSubscription;
  ApiClient? _registrationClient;
  final Set<String> _seenMessageIds = <String>{};
  bool _isInitialized = false;
  bool _localInitialized = false;
  String? _fcmToken;
  PushPermissionStatus _permissionStatus = AppConfig.useFCM
      ? PushPermissionStatus.notDetermined
      : PushPermissionStatus.unavailable;

  Stream<NotificationData> get notifications => _notificationController.stream;
  Stream<NotificationData> get interactions => _interactionController.stream;
  Stream<PushPermissionStatus> get permissionChanges =>
      _permissionController.stream;
  PushPermissionStatus get permissionStatus => _permissionStatus;
  String? get fcmToken => _fcmToken;
  bool get pushAvailable => AppConfig.useFirebase && AppConfig.useFCM;

  Future<void> initialize() async {
    if (_isInitialized) return;
    await _initializeLocalNotifications();
    if (!pushAvailable) {
      _setPermission(PushPermissionStatus.unavailable);
      _isInitialized = true;
      return;
    }

    FirebaseMessaging.onBackgroundMessage(firebaseMessagingBackgroundHandler);
    final messaging = FirebaseMessaging.instance;
    await messaging.setForegroundNotificationPresentationOptions(
      alert: false,
      badge: true,
      sound: true,
    );
    _foregroundSubscription = FirebaseMessaging.onMessage.listen(
      _handleForegroundMessage,
    );
    _openedSubscription = FirebaseMessaging.onMessageOpenedApp.listen(
      (message) => _interactionController.add(_fromRemoteMessage(message)),
    );
    _tokenSubscription = messaging.onTokenRefresh.listen((token) {
      _fcmToken = token;
      final client = _registrationClient;
      if (client != null) unawaited(synchronizeToken(client));
    });

    final settings = await messaging.getNotificationSettings();
    _setPermission(_mapPermission(settings.authorizationStatus));
    if (_permissionStatus == PushPermissionStatus.authorized ||
        _permissionStatus == PushPermissionStatus.provisional) {
      _fcmToken = await messaging.getToken();
    }
    final initialMessage = await messaging.getInitialMessage();
    if (initialMessage != null) {
      _interactionController.add(_fromRemoteMessage(initialMessage));
    }
    _isInitialized = true;
  }

  Future<void> _initializeLocalNotifications() async {
    if (_localInitialized) return;
    timezone_data.initializeTimeZones();
    const settings = InitializationSettings(
      android: AndroidInitializationSettings('@mipmap/ic_launcher'),
      iOS: DarwinInitializationSettings(
        requestAlertPermission: false,
        requestBadgePermission: false,
        requestSoundPermission: false,
      ),
    );
    await _localNotifications.initialize(
      settings,
      onDidReceiveNotificationResponse: (response) {
        final payload = response.payload;
        if (payload != null && payload.isNotEmpty) {
          _interactionController.add(
            NotificationData(title: '', body: '', payload: payload),
          );
        }
      },
    );
    await _localNotifications
        .resolvePlatformSpecificImplementation<
            AndroidFlutterLocalNotificationsPlugin>()
        ?.createNotificationChannel(_channel);
    _localInitialized = true;
  }

  Future<bool> requestPermissions() async {
    if (!pushAvailable) {
      _setPermission(PushPermissionStatus.unavailable);
      return false;
    }
    final settings = await FirebaseMessaging.instance.requestPermission(
      alert: true,
      badge: true,
      sound: true,
      provisional: false,
    );
    _setPermission(_mapPermission(settings.authorizationStatus));
    final granted = _permissionStatus == PushPermissionStatus.authorized ||
        _permissionStatus == PushPermissionStatus.provisional;
    if (granted) _fcmToken = await FirebaseMessaging.instance.getToken();
    final client = _registrationClient;
    if (granted && client != null) await synchronizeToken(client);
    return granted;
  }

  Future<bool> synchronizeToken(ApiClient apiClient) async {
    _registrationClient = apiClient;
    if (!pushAvailable || !await apiClient.isAuthenticated()) return false;
    final token = _fcmToken ?? await FirebaseMessaging.instance.getToken();
    if (token == null || token.isEmpty) return false;
    _fcmToken = token;
    try {
      await apiClient.put(
        '/api/notifications/devices/current',
        data: {
          'token': token,
          'platform': _platformName(),
          'deviceId': await _deviceId(),
          'tenantSlug': Environment.tenantSlug,
        },
      );
      return true;
    } catch (_) {
      return false;
    }
  }

  Future<void> disableCurrentDevice(ApiClient apiClient) async {
    if (!pushAvailable || !await apiClient.isAuthenticated()) return;
    try {
      await apiClient.delete(
        '/api/notifications/devices/current',
        data: {
          if (_fcmToken != null) 'token': _fcmToken,
          'deviceId': await _deviceId(),
          'tenantSlug': Environment.tenantSlug,
        },
      );
    } catch (_) {
      // Logout must still complete while offline.
    }
  }

  Future<void> disableRegisteredDevice() async {
    final client = _registrationClient;
    if (client != null) await disableCurrentDevice(client);
  }

  Future<void> subscribeToTopic(String topic) async {
    if (!pushAvailable) return;
    final safeTopic = _safeTopic(topic);
    await FirebaseMessaging.instance.subscribeToTopic(safeTopic);
    await _synchronizeServerTopic(safeTopic, true);
  }

  Future<void> unsubscribeFromTopic(String topic) async {
    if (!pushAvailable) return;
    final safeTopic = _safeTopic(topic);
    await FirebaseMessaging.instance.unsubscribeFromTopic(safeTopic);
    await _synchronizeServerTopic(safeTopic, false);
  }

  Future<void> _synchronizeServerTopic(String topic, bool enabled) async {
    final client = _registrationClient;
    if (client == null || !await client.isAuthenticated()) {
      throw StateError('Authenticated push registration is not ready.');
    }
    final token = _fcmToken ?? await FirebaseMessaging.instance.getToken();
    if (token == null || token.isEmpty) {
      throw StateError('The Firebase device token is not ready.');
    }
    _fcmToken = token;
    await client.put(
      '/api/notifications/devices/current/topics',
      data: {'token': token, 'topic': topic, 'enabled': enabled},
    );
  }

  void handleNotificationTap(NotificationData notification) {
    _interactionController.add(notification);
  }

  Future<void> showLocalNotification({
    required String title,
    required String body,
    String? payload,
  }) async {
    await _initializeLocalNotifications();
    await _localNotifications.show(
      DateTime.now().millisecondsSinceEpoch.remainder(2147483647),
      title,
      body,
      const NotificationDetails(
        android: AndroidNotificationDetails(
          'sports_platform_live_updates',
          'Live sports updates',
          channelDescription: 'Contest, result, and competition updates',
          importance: Importance.high,
          priority: Priority.high,
        ),
        iOS: DarwinNotificationDetails(),
      ),
      payload: payload,
    );
  }

  Future<void> scheduleNotification({
    required String title,
    required String body,
    required DateTime scheduledTime,
    String? payload,
  }) async {
    await _initializeLocalNotifications();
    await _localNotifications.zonedSchedule(
      scheduledTime.millisecondsSinceEpoch.remainder(2147483647),
      title,
      body,
      timezone.TZDateTime.from(scheduledTime, timezone.local),
      const NotificationDetails(
        android: AndroidNotificationDetails(
          'sports_platform_live_updates',
          'Live sports updates',
          channelDescription: 'Contest, result, and competition updates',
          importance: Importance.high,
          priority: Priority.high,
        ),
        iOS: DarwinNotificationDetails(),
      ),
      androidScheduleMode: AndroidScheduleMode.inexactAllowWhileIdle,
      uiLocalNotificationDateInterpretation:
          UILocalNotificationDateInterpretation.absoluteTime,
      payload: payload,
    );
  }

  Future<void> cancelNotification(int id) => _localNotifications.cancel(id);
  Future<void> cancelAllNotifications() => _localNotifications.cancelAll();

  Future<void> _handleForegroundMessage(RemoteMessage message) async {
    if (!_acceptMessage(message)) return;
    final notification = _fromRemoteMessage(message);
    _notificationController.add(notification);
    await showLocalNotification(
      title: notification.title,
      body: notification.body,
      payload: notification.payload,
    );
  }

  bool _acceptMessage(RemoteMessage message) {
    final id = message.messageId ??
        message.data['notificationId']?.toString() ??
        '${message.sentTime?.millisecondsSinceEpoch}:${message.data}';
    if (!_seenMessageIds.add(id)) return false;
    if (_seenMessageIds.length > 200) {
      _seenMessageIds.remove(_seenMessageIds.first);
    }
    return true;
  }

  NotificationData _fromRemoteMessage(RemoteMessage message) {
    final data = Map<String, dynamic>.from(message.data);
    final title =
        message.notification?.title ?? data['title']?.toString() ?? '';
    final body = message.notification?.body ?? data['body']?.toString() ?? '';
    return NotificationData(
      id: message.messageId,
      title: title,
      body: body,
      payload: data['payload']?.toString() ?? _payloadFromData(data),
      data: data,
      timestamp: message.sentTime ?? DateTime.now().toUtc(),
    );
  }

  Future<String> _deviceId() async {
    final existing = await _secureStorage.readRecovering(key: _deviceIdKey);
    if (existing != null && existing.isNotEmpty) return existing;
    final generated = const Uuid().v4();
    await _secureStorage.writeRecovering(key: _deviceIdKey, value: generated);
    return generated;
  }

  String _platformName() => switch (defaultTargetPlatform) {
        TargetPlatform.android => 'android',
        TargetPlatform.iOS => 'ios',
        _ => 'web',
      };

  String _safeTopic(String value) {
    final normalized = value.toLowerCase().replaceAll(
          RegExp(r'[^a-z0-9-_.~%]'),
          '-',
        );
    return normalized.length <= 900 ? normalized : normalized.substring(0, 900);
  }

  String? _payloadFromData(Map<String, dynamic> data) {
    final contestId = data['contestId']?.toString();
    if (contestId != null && contestId.isNotEmpty) return 'contest:$contestId';
    final competitionId = data['competitionId']?.toString();
    if (competitionId != null && competitionId.isNotEmpty) {
      return 'competition:$competitionId';
    }
    return null;
  }

  PushPermissionStatus _mapPermission(AuthorizationStatus status) =>
      switch (status) {
        AuthorizationStatus.authorized => PushPermissionStatus.authorized,
        AuthorizationStatus.provisional => PushPermissionStatus.provisional,
        AuthorizationStatus.denied => PushPermissionStatus.denied,
        AuthorizationStatus.notDetermined => PushPermissionStatus.notDetermined,
      };

  void _setPermission(PushPermissionStatus value) {
    if (_permissionStatus == value) return;
    _permissionStatus = value;
    _permissionController.add(value);
  }

  void dispose() {
    _foregroundSubscription?.cancel();
    _openedSubscription?.cancel();
    _tokenSubscription?.cancel();
    _notificationController.close();
    _interactionController.close();
    _permissionController.close();
  }
}

class NotificationData {
  final String? id;
  final String title;
  final String body;
  final String? payload;
  final Map<String, dynamic>? data;
  final DateTime? timestamp;

  const NotificationData({
    this.id,
    required this.title,
    required this.body,
    this.payload,
    this.data,
    this.timestamp,
  });

  factory NotificationData.fromMap(Map<String, dynamic> map) {
    final rawData = map['data'];
    return NotificationData(
      id: map['id']?.toString(),
      title: map['title']?.toString() ?? '',
      body: map['body']?.toString() ?? '',
      payload: map['payload']?.toString(),
      data: rawData is Map ? Map<String, dynamic>.from(rawData) : null,
      timestamp: map['timestamp'] != null
          ? DateTime.tryParse(map['timestamp'].toString())
          : null,
    );
  }

  Map<String, dynamic> toMap() => {
        if (id != null) 'id': id,
        'title': title,
        'body': body,
        if (payload != null) 'payload': payload,
        if (data != null) 'data': data,
        if (timestamp != null) 'timestamp': timestamp!.toIso8601String(),
      };
}

class NotificationType {
  static const String fixtureReminder = 'fixture_reminder';
  static const String contestStarted = 'contest_started';
  static const String scoreChanged = 'score_changed';
  static const String periodEnded = 'period_ended';
  static const String finalResult = 'final_result';
  static const String postponement = 'postponement';
  static const String qualification = 'qualification';
  static const String announcement = 'announcement';
  // Backwards-compatible values accepted from older server deployments.
  static const String contestLive = 'contest_live';
  static const String contestEnded = 'contest_ended';
  static const String scoreUpdate = 'score_update';
  static const String registrationOpen = 'registration_open';
  static const String registrationClosed = 'registration_closed';
  static const String resultApproved = 'result_approved';
  static const String correction = 'correction';
  static const String general = 'general';
}
