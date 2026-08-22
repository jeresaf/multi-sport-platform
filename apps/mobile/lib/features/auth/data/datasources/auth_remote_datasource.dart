import '../../../../core/services/api_client.dart';
import 'auth_local_datasource.dart';
import '../../../../core/services/recovering_secure_storage.dart';

/// Remote data source for authentication API calls
class AuthRemoteDataSource {
  final ApiClient _apiClient;

  AuthRemoteDataSource(this._apiClient);

  /// Login with email and password
  Future<AuthResponse> login(
      String email, String password, String deviceId) async {
    final response = await _apiClient.post(
      '/api/auth/login',
      data: {
        'email': email,
        'password': password,
        'deviceId': deviceId,
        'deviceName': 'Sports Platform mobile',
        'deviceType': 'mobile',
      },
    );

    if (response.statusCode == 200) {
      final data = response.data as Map<String, dynamic>;
      return AuthResponse.fromJson(data);
    }

    throw Exception('Login failed: ${response.statusCode}');
  }

  /// Register a new user
  Future<AuthResponse> register({
    required String email,
    required String password,
    required String displayName,
    required String deviceId,
  }) async {
    final normalizedName = displayName.trim();
    final names = normalizedName.isEmpty
        ? const <String>['User']
        : normalizedName.split(RegExp(r'\s+'));
    final response = await _apiClient.post(
      '/api/auth/register',
      data: {
        'email': email,
        'password': password,
        'firstName': names.first,
        'lastName': names.length > 1 ? names.skip(1).join(' ') : 'User',
        'tenantId': await appSecureStorage.readRecovering(key: 'selected_tenant_id'),
        'deviceId': deviceId,
        'deviceName': 'Sports Platform mobile',
        'deviceType': 'mobile',
      },
    );

    if (response.statusCode == 200 || response.statusCode == 201) {
      final data = response.data as Map<String, dynamic>;
      return AuthResponse.fromJson(data);
    }

    throw Exception('Registration failed: ${response.statusCode}');
  }

  /// Refresh access token
  Future<TokenResponse> refreshToken(
      String refreshToken, String deviceId) async {
    final response = await _apiClient.post(
      '/api/auth/refresh',
      data: {
        'refreshToken': refreshToken,
        'deviceId': deviceId,
      },
    );

    if (response.statusCode == 200) {
      final data = response.data as Map<String, dynamic>;
      return TokenResponse.fromJson(data);
    }

    throw Exception('Token refresh failed: ${response.statusCode}');
  }

  /// Logout
  Future<void> logout(String refreshToken, String deviceId) async {
    try {
      await _apiClient.post('/api/auth/logout', data: {
        'refreshToken': refreshToken,
        'deviceId': deviceId,
      });
    } catch (e) {
      // Ignore errors on logout - we still want to clear local data
    }
  }

  /// Get current user profile
  Future<User> getCurrentUser() async {
    final response = await _apiClient.get('/api/auth/me');

    if (response.statusCode == 200) {
      final data = response.data as Map<String, dynamic>;
      return User.fromJson(data);
    }

    throw Exception('Failed to get user: ${response.statusCode}');
  }

  Future<User> updateProfile(String firstName, String lastName) async {
    final response = await _apiClient.put(
      '/api/auth/me',
      data: {'firstName': firstName, 'lastName': lastName},
    );
    if (response.statusCode == 200) {
      return User.fromJson(response.data as Map<String, dynamic>);
    }
    throw Exception('Failed to update profile: ${response.statusCode}');
  }

  /// Send password reset email
  Future<void> sendPasswordReset(String email) async {
    final response = await _apiClient.post(
      '/api/auth/forgot-password',
      data: {'email': email},
    );

    if (response.statusCode != 200) {
      throw Exception('Password reset failed: ${response.statusCode}');
    }
  }
}

/// Response from authentication endpoints
class AuthResponse {
  final String accessToken;
  final String refreshToken;
  final User user;
  final DateTime? expiresAt;

  AuthResponse({
    required this.accessToken,
    required this.refreshToken,
    required this.user,
    this.expiresAt,
  });

  factory AuthResponse.fromJson(Map<String, dynamic> json) {
    return AuthResponse(
      accessToken: json['accessToken'] ?? '',
      refreshToken: json['refreshToken'] ?? '',
      user: User.fromJson(json['user'] ?? {}),
      expiresAt: json['expiresAt'] != null
          ? DateTime.tryParse(json['expiresAt'])
          : null,
    );
  }
}

/// Response from token refresh
class TokenResponse {
  final String accessToken;
  final String? refreshToken;
  final DateTime? expiresAt;

  TokenResponse({
    required this.accessToken,
    this.refreshToken,
    this.expiresAt,
  });

  factory TokenResponse.fromJson(Map<String, dynamic> json) {
    return TokenResponse(
      accessToken: json['accessToken'] ?? '',
      refreshToken: json['refreshToken'],
      expiresAt: json['expiresAt'] != null
          ? DateTime.tryParse(json['expiresAt'])
          : null,
    );
  }
}
