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

  /// Update the signed-in user's profile.
  Future<User> updateProfile(String firstName, String lastName) =>
      throw UnimplementedError('Profile updates are not available.');

  /// Refresh token
  Future<void> refreshToken();

  /// Register new user
  Future<User?> register(String email, String password, String displayName);

  /// Send password reset email
  Future<void> sendPasswordReset(String email);
}
