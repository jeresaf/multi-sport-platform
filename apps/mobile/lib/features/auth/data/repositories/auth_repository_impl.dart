import '../../domain/repositories/auth_repository.dart';
import '../datasources/auth_local_datasource.dart';
import '../datasources/auth_remote_datasource.dart';

/// Implementation of auth repository with API integration
class AuthRepositoryImpl implements AuthRepository {
  final AuthLocalDataSource _localDataSource;
  final AuthRemoteDataSource _remoteDataSource;

  AuthRepositoryImpl({
    required AuthLocalDataSource localDataSource,
    required AuthRemoteDataSource remoteDataSource,
  })  : _localDataSource = localDataSource,
        _remoteDataSource = remoteDataSource;

  @override
  Future<User?> login(String email, String password) async {
    final response = await _remoteDataSource.login(
      email,
      password,
      await _localDataSource.getOrCreateDeviceId(),
    );

    await _localDataSource.saveAccessToken(response.accessToken);
    await _localDataSource.saveRefreshToken(response.refreshToken);
    await _localDataSource.saveUser(response.user);

    return response.user;
  }

  @override
  Future<void> logout() async {
    try {
      // Call the API to invalidate token
      final refreshToken = await _localDataSource.getRefreshToken();
      if (refreshToken != null) {
        await _remoteDataSource.logout(
          refreshToken,
          await _localDataSource.getOrCreateDeviceId(),
        );
      }
    } catch (e) {
      // Ignore API errors, still clear local data
    }

    // Clear local data
    await _localDataSource.clearAll();
  }

  @override
  Future<bool> isAuthenticated() async {
    var hasToken = await _localDataSource.hasValidToken();
    if (!hasToken && await _localDataSource.getRefreshToken() != null) {
      try {
        await refreshToken();
        hasToken = true;
      } catch (_) {
        return false;
      }
    }
    if (!hasToken) return false;

    // Try to get current user from API
    try {
      final user = await _remoteDataSource.getCurrentUser();
      await _localDataSource.saveUser(user);
      return true;
    } catch (e) {
      // If API fails, check if we have a local user
      final localUser = await _localDataSource.getUser();
      return localUser != null;
    }
  }

  @override
  Future<User?> getCurrentUser() async {
    // First try to get from local storage
    final localUser = await _localDataSource.getUser();

    // If we have a valid token, try to refresh from API
    if (await _localDataSource.hasValidToken()) {
      try {
        final user = await _remoteDataSource.getCurrentUser();
        await _localDataSource.saveUser(user);
        return user;
      } catch (e) {
        // Return local user if API fails
        return localUser;
      }
    }

    return localUser;
  }

  @override
  Future<User> updateProfile(String firstName, String lastName) async {
    final user = await _remoteDataSource.updateProfile(firstName, lastName);
    await _localDataSource.saveUser(user);
    return user;
  }

  @override
  Future<void> refreshToken() async {
    final refreshToken = await _localDataSource.getRefreshToken();
    if (refreshToken == null) {
      throw Exception('No refresh token available');
    }

    try {
      final response = await _remoteDataSource.refreshToken(
        refreshToken,
        await _localDataSource.getOrCreateDeviceId(),
      );
      await _localDataSource.saveAccessToken(response.accessToken);
      if (response.refreshToken != null) {
        await _localDataSource.saveRefreshToken(response.refreshToken!);
      }
    } catch (e) {
      // If refresh fails, clear tokens
      await _localDataSource.clearAll();
      rethrow;
    }
  }

  @override
  Future<User?> register(
      String email, String password, String displayName) async {
    final response = await _remoteDataSource.register(
      email: email,
      password: password,
      displayName: displayName,
      deviceId: await _localDataSource.getOrCreateDeviceId(),
    );

    await _localDataSource.saveAccessToken(response.accessToken);
    await _localDataSource.saveRefreshToken(response.refreshToken);
    await _localDataSource.saveUser(response.user);

    return response.user;
  }

  @override
  Future<void> sendPasswordReset(String email) async {
    await _remoteDataSource.sendPasswordReset(email);
  }
}
