import '../../data/datasources/auth_local_datasource.dart';

/// Repository interface for authentication
abstract class AuthRepository {
  /// Login with email and password
  Future<User?> login(String email, String password);

  /// Logout
  Future<void> logout();

  /// Check if user is authenticated
  Future<bool> isAuthenticated();

  /// Get current user
  Future<User?> getCurrentUser();

  /// Refresh token
  Future<void> refreshToken();

  /// Register new user
  Future<User?> register(String email, String password, String displayName);

  /// Send password reset email
  Future<void> sendPasswordReset(String email);
}
