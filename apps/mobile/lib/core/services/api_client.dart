import 'dart:developer' as developer;

import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:uuid/uuid.dart';
import '../config/app_config.dart';

/// API client for making HTTP requests
class ApiClient {
  late final Dio _dio;
  final FlutterSecureStorage _secureStorage = const FlutterSecureStorage();

  static const String _accessTokenKey = 'access_token';
  static const String _refreshTokenKey = 'refresh_token';
  static const String _deviceIdKey = 'identity_device_id';

  ApiClient() {
    _dio = Dio(BaseOptions(
      baseUrl: AppConfig.apiBaseUrl,
      connectTimeout: const Duration(seconds: AppConfig.apiTimeout),
      receiveTimeout: const Duration(seconds: AppConfig.apiTimeout),
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
      },
    ));

    _setupInterceptors();
  }

  void _setupInterceptors() {
    _dio.interceptors.add(InterceptorsWrapper(
      onRequest: (options, handler) async {
        // Add auth token if available
        final token = await getAccessToken();
        if (token != null) {
          options.headers['Authorization'] = 'Bearer $token';
        }

        // Add tenant header if available
        final tenantId = await _secureStorage.read(key: 'selected_tenant_id');
        if (tenantId != null) {
          options.headers['X-Tenant-Id'] = tenantId;
        }

        if (AppConfig.enableDebugLogging) {
          developer.log(
            'API Request: ${options.method} ${options.path}',
            name: 'ApiClient',
          );
        }

        handler.next(options);
      },
      onResponse: (response, handler) {
        if (AppConfig.enableDebugLogging) {
          developer.log(
            'API Response: ${response.statusCode} ${response.requestOptions.path}',
            name: 'ApiClient',
          );
        }
        handler.next(response);
      },
      onError: (error, handler) async {
        if (AppConfig.enableDebugLogging) {
          developer.log(
            'API Error: ${error.message}',
            name: 'ApiClient',
            error: error,
          );
        }

        // Handle 401 - try to refresh token
        if (error.response?.statusCode == 401 &&
            error.requestOptions.path != '/api/auth/refresh') {
          final refreshed = await _refreshToken();
          if (refreshed) {
            // Retry the request
            final opts = error.requestOptions;
            final token = await getAccessToken();
            opts.headers['Authorization'] = 'Bearer $token';

            try {
              final response = await _dio.fetch(opts);
              handler.resolve(response);
              return;
            } catch (e) {
              handler.next(error);
              return;
            }
          }
        }

        handler.next(error);
      },
    ));
  }

  /// GET request
  Future<Response<T>> get<T>(
    String path, {
    Map<String, dynamic>? queryParameters,
    Options? options,
  }) {
    return _dio.get<T>(
      path,
      queryParameters: queryParameters,
      options: options,
    );
  }

  /// POST request
  Future<Response<T>> post<T>(
    String path, {
    dynamic data,
    Map<String, dynamic>? queryParameters,
    Options? options,
  }) {
    return _dio.post<T>(
      path,
      data: data,
      queryParameters: queryParameters,
      options: options,
    );
  }

  /// PUT request
  Future<Response<T>> put<T>(
    String path, {
    dynamic data,
    Map<String, dynamic>? queryParameters,
    Options? options,
  }) {
    return _dio.put<T>(
      path,
      data: data,
      queryParameters: queryParameters,
      options: options,
    );
  }

  /// PATCH request
  Future<Response<T>> patch<T>(
    String path, {
    dynamic data,
    Map<String, dynamic>? queryParameters,
    Options? options,
  }) {
    return _dio.patch<T>(
      path,
      data: data,
      queryParameters: queryParameters,
      options: options,
    );
  }

  /// DELETE request
  Future<Response<T>> delete<T>(
    String path, {
    dynamic data,
    Map<String, dynamic>? queryParameters,
    Options? options,
  }) {
    return _dio.delete<T>(
      path,
      data: data,
      queryParameters: queryParameters,
      options: options,
    );
  }

  /// Request using a method selected at runtime (used by offline replay).
  Future<Response<T>> request<T>({
    required String endpoint,
    required String method,
    dynamic data,
  }) {
    return _dio.request<T>(
      endpoint,
      data: data,
      options: Options(method: method),
    );
  }

  /// Get access token
  Future<String?> getAccessToken() async {
    return _secureStorage.read(key: _accessTokenKey);
  }

  /// Set access token
  Future<void> setAccessToken(String token) async {
    await _secureStorage.write(key: _accessTokenKey, value: token);
  }

  /// Get refresh token
  Future<String?> getRefreshToken() async {
    return _secureStorage.read(key: _refreshTokenKey);
  }

  /// Set refresh token
  Future<void> setRefreshToken(String token) async {
    await _secureStorage.write(key: _refreshTokenKey, value: token);
  }

  /// Clear tokens
  Future<void> clearTokens() async {
    await _secureStorage.delete(key: _accessTokenKey);
    await _secureStorage.delete(key: _refreshTokenKey);
  }

  /// Refresh access token
  Future<bool> _refreshToken() async {
    try {
      final refreshToken = await getRefreshToken();
      if (refreshToken == null) return false;

      final response = await _dio.post(
        '/api/auth/refresh',
        data: {
          'refreshToken': refreshToken,
          'deviceId': await _getOrCreateDeviceId(),
        },
      );

      if (response.statusCode == 200) {
        final data = response.data as Map<String, dynamic>;
        await setAccessToken(data['accessToken']);
        if (data['refreshToken'] != null) {
          await setRefreshToken(data['refreshToken']);
        }
        return true;
      }
    } catch (e) {
      developer.log('Token refresh failed', name: 'ApiClient', error: e);
    }
    return false;
  }

  Future<String> _getOrCreateDeviceId() async {
    final existing = await _secureStorage.read(key: _deviceIdKey);
    if (existing != null && existing.isNotEmpty) return existing;
    final created = const Uuid().v4();
    await _secureStorage.write(key: _deviceIdKey, value: created);
    return created;
  }

  /// Check if authenticated
  Future<bool> isAuthenticated() async {
    final token = await getAccessToken();
    return token != null;
  }
}
