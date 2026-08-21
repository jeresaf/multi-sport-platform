import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../../core/services/api_client.dart';
import '../../../../core/services/notification_service.dart';
import '../../data/datasources/auth_local_datasource.dart';
import '../../data/datasources/auth_remote_datasource.dart';
import '../../data/repositories/auth_repository_impl.dart';
import '../../domain/repositories/auth_repository.dart';

/// Auth state
class AuthState {
  final bool isAuthenticated;
  final bool isLoading;
  final String? userId;
  final String? email;
  final String? displayName;
  final String? tenantId;
  final List<String> roles;
  final String? errorMessage;

  const AuthState({
    this.isAuthenticated = false,
    this.isLoading = false,
    this.userId,
    this.email,
    this.displayName,
    this.tenantId,
    this.roles = const [],
    this.errorMessage,
  });

  bool get canScore {
    final normalized = roles.map((role) => role.toLowerCase()).toSet();
    return normalized.any({
      'scorer',
      'admin',
      'tenant admin',
      'tenant administrator',
      'platform owner',
    }.contains);
  }

  AuthState copyWith({
    bool? isAuthenticated,
    bool? isLoading,
    String? userId,
    String? email,
    String? displayName,
    String? tenantId,
    List<String>? roles,
    String? errorMessage,
  }) {
    return AuthState(
      isAuthenticated: isAuthenticated ?? this.isAuthenticated,
      isLoading: isLoading ?? this.isLoading,
      userId: userId ?? this.userId,
      email: email ?? this.email,
      displayName: displayName ?? this.displayName,
      tenantId: tenantId ?? this.tenantId,
      roles: roles ?? this.roles,
      errorMessage: errorMessage,
    );
  }
}

/// Auth state notifier
class AuthNotifier extends StateNotifier<AuthState> {
  final AuthRepository _repository;
  final Future<void> Function()? _onAuthenticated;
  final Future<void> Function()? _beforeLogout;

  AuthNotifier(
    this._repository, {
    Future<void> Function()? onAuthenticated,
    Future<void> Function()? beforeLogout,
  })  : _onAuthenticated = onAuthenticated,
        _beforeLogout = beforeLogout,
        super(const AuthState()) {
    _loadAuthState();
  }

  Future<void> _loadAuthState() async {
    state = state.copyWith(isLoading: true);

    try {
      final isAuth = await _repository.isAuthenticated();
      if (isAuth) {
        final user = await _repository.getCurrentUser();
        state = AuthState(
          isAuthenticated: true,
          userId: user?.id,
          email: user?.email,
          displayName: user?.displayName,
          tenantId: user?.tenantId,
          roles: user?.roles ?? const [],
        );
        await _onAuthenticated?.call();
      } else {
        state = const AuthState();
      }
    } catch (e) {
      state = AuthState(errorMessage: _friendlyAuthError(e));
    }
  }

  Future<bool> login(String email, String password) async {
    state = state.copyWith(isLoading: true, errorMessage: null);

    try {
      final result = await _repository.login(email, password);
      if (result != null) {
        state = AuthState(
          isAuthenticated: true,
          userId: result.id,
          email: result.email,
          displayName: result.displayName,
          tenantId: result.tenantId,
          roles: result.roles,
        );
        await _onAuthenticated?.call();
        return true;
      } else {
        state = state.copyWith(
          isLoading: false,
          errorMessage: 'Invalid credentials',
        );
        return false;
      }
    } catch (e) {
      state = state.copyWith(
        isLoading: false,
        errorMessage: _friendlyAuthError(e),
      );
      return false;
    }
  }

  Future<bool> register(
      String email, String password, String displayName) async {
    state = state.copyWith(isLoading: true, errorMessage: null);
    try {
      final user = await _repository.register(email, password, displayName);
      if (user == null) {
        state = state.copyWith(
          isLoading: false,
          errorMessage: 'Registration failed',
        );
        return false;
      }
      state = AuthState(
        isAuthenticated: true,
        userId: user.id,
        email: user.email,
        displayName: user.displayName,
        tenantId: user.tenantId,
        roles: user.roles,
      );
      await _onAuthenticated?.call();
      return true;
    } catch (error) {
      state = state.copyWith(
        isLoading: false,
        errorMessage: _friendlyAuthError(error, action: 'Registration'),
      );
      return false;
    }
  }

  Future<void> logout() async {
    state = state.copyWith(isLoading: true);

    try {
      await _beforeLogout?.call();
      await _repository.logout();
      state = const AuthState();
    } catch (e) {
      state = state.copyWith(
        isLoading: false,
        errorMessage: e.toString(),
      );
    }
  }

  Future<void> refreshToken() async {
    try {
      await _repository.refreshToken();
    } catch (e) {
      // If refresh fails, logout
      await logout();
    }
  }

  Future<bool> sendPasswordReset(String email) async {
    state = state.copyWith(isLoading: true, errorMessage: null);
    try {
      await _repository.sendPasswordReset(email);
      state = state.copyWith(isLoading: false, errorMessage: null);
      return true;
    } catch (error) {
      state = state.copyWith(
        isLoading: false,
        errorMessage: _friendlyAuthError(error, action: 'Password reset'),
      );
      return false;
    }
  }
}

String _friendlyAuthError(Object error, {String action = 'Sign in'}) {
  final message = error.toString().toLowerCase();
  if (message.contains('badpadding') ||
      message.contains('bad_decrypt') ||
      message.contains('cipher')) {
    return 'Saved sign-in data could not be read and has been cleared. Please try again.';
  }
  if (message.contains('401') || message.contains('unauthorized')) {
    return 'The email or password is incorrect.';
  }
  if (message.contains('socket') ||
      message.contains('connection') ||
      message.contains('timed out') ||
      message.contains('timeout')) {
    return 'Could not reach the sports platform. Check Wi-Fi and try again.';
  }
  return '$action failed. Please try again.';
}

/// Provider for API client (shared)
final apiClientProvider = Provider<ApiClient>((ref) {
  return ApiClient();
});

/// Provider for auth local datasource
final authLocalDataSourceProvider = Provider<AuthLocalDataSource>((ref) {
  return AuthLocalDataSource();
});

/// Provider for auth remote datasource
final authRemoteDataSourceProvider = Provider<AuthRemoteDataSource>((ref) {
  final apiClient = ref.watch(apiClientProvider);
  return AuthRemoteDataSource(apiClient);
});

/// Provider for auth repository
final authRepositoryProvider = Provider<AuthRepository>((ref) {
  final localDataSource = ref.watch(authLocalDataSourceProvider);
  final remoteDataSource = ref.watch(authRemoteDataSourceProvider);
  return AuthRepositoryImpl(
    localDataSource: localDataSource,
    remoteDataSource: remoteDataSource,
  );
});

/// Provider for auth state
final authStateProvider = StateNotifierProvider<AuthNotifier, AuthState>((ref) {
  final repository = ref.watch(authRepositoryProvider);
  final apiClient = ref.watch(apiClientProvider);
  final notifications = NotificationService();
  return AuthNotifier(
    repository,
    onAuthenticated: () async {
      await notifications.synchronizeToken(apiClient);
    },
    beforeLogout: () => notifications.disableCurrentDevice(apiClient),
  );
});
