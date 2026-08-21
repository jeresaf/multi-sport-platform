import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'dart:convert';
import 'package:uuid/uuid.dart';
import '../../../../core/services/recovering_secure_storage.dart';

/// User model for authentication
class User {
  final String id;
  final String email;
  final String? displayName;
  final String? avatarUrl;
  final List<String> roles;
  final String? tenantId;

  const User({
    required this.id,
    required this.email,
    this.displayName,
    this.avatarUrl,
    this.roles = const [],
    this.tenantId,
  });

  factory User.fromJson(Map<String, dynamic> json) {
    final firstName = json['firstName']?.toString().trim() ?? '';
    final lastName = json['lastName']?.toString().trim() ?? '';
    final fullName =
        [firstName, lastName].where((part) => part.isNotEmpty).join(' ');
    return User(
      id: json['id'] ?? '',
      email: json['email'] ?? '',
      displayName: json['displayName'] ?? (fullName.isEmpty ? null : fullName),
      avatarUrl: json['avatarUrl'],
      roles: json['roles'] != null ? List<String>.from(json['roles']) : [],
      tenantId: json['tenantId'],
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'email': email,
      if (displayName != null) 'displayName': displayName,
      if (avatarUrl != null) 'avatarUrl': avatarUrl,
      'roles': roles,
      if (tenantId != null) 'tenantId': tenantId,
    };
  }
}

/// Local data source for authentication
class AuthLocalDataSource {
  final FlutterSecureStorage _secureStorage;

  static const String _userKey = 'user_data';
  static const String _tokenKey = 'access_token';
  static const String _refreshTokenKey = 'refresh_token';
  static const String _tokenExpiryKey = 'token_expiry';
  static const String _deviceIdKey = 'identity_device_id';

  AuthLocalDataSource({FlutterSecureStorage? secureStorage})
      : _secureStorage = secureStorage ?? appSecureStorage;

  /// Save user data
  Future<void> saveUser(User user) async {
    await _secureStorage.writeRecovering(
        key: _userKey, value: jsonEncode(user.toJson()));
  }

  /// Get user data
  Future<User?> getUser() async {
    final data = await _secureStorage.readRecovering(key: _userKey);
    if (data == null) return null;
    return User.fromJson(jsonDecode(data));
  }

  /// Save access token
  Future<void> saveAccessToken(String token) async {
    await _secureStorage.writeRecovering(key: _tokenKey, value: token);
    // Set expiry to 1 hour from now
    final expiry =
        DateTime.now().add(const Duration(hours: 1)).toIso8601String();
    await _secureStorage.writeRecovering(key: _tokenExpiryKey, value: expiry);
  }

  /// Get access token
  Future<String?> getAccessToken() async {
    return _secureStorage.readRecovering(key: _tokenKey);
  }

  /// Save refresh token
  Future<void> saveRefreshToken(String token) async {
    await _secureStorage.writeRecovering(key: _refreshTokenKey, value: token);
  }

  /// Get refresh token
  Future<String?> getRefreshToken() async {
    return _secureStorage.readRecovering(key: _refreshTokenKey);
  }

  Future<String> getOrCreateDeviceId() async {
    final existing = await _secureStorage.readRecovering(key: _deviceIdKey);
    if (existing != null && existing.isNotEmpty) return existing;
    final created = const Uuid().v4();
    await _secureStorage.writeRecovering(key: _deviceIdKey, value: created);
    return created;
  }

  /// Check if token is valid (not expired)
  Future<bool> hasValidToken() async {
    final token = await getAccessToken();
    if (token == null || token.isEmpty) return false;

    final expiryStr = await _secureStorage.readRecovering(key: _tokenExpiryKey);
    if (expiryStr == null) return true; // No expiry set, assume valid

    final expiry = DateTime.tryParse(expiryStr);
    if (expiry == null) return true;

    return DateTime.now().isBefore(expiry);
  }

  /// Clear all auth data
  Future<void> clearAll() async {
    await _secureStorage.deleteRecovering(key: _userKey);
    await _secureStorage.deleteRecovering(key: _tokenKey);
    await _secureStorage.deleteRecovering(key: _refreshTokenKey);
    await _secureStorage.deleteRecovering(key: _tokenExpiryKey);
    // Keep the device identity stable across sign-in sessions.
  }

  /// Check if user is authenticated
  Future<bool> isAuthenticated() async {
    return hasValidToken();
  }
}
