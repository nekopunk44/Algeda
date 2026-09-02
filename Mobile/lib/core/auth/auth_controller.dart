import 'dart:convert';
import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../features/auth/data/repository/auth_repository.dart';
import '../errors/api_exception.dart';
import '../storage/secure_token_storage.dart';
import 'app_role.dart';

final authControllerProvider = NotifierProvider<AuthController, AuthState>(
  AuthController.new,
);

class AuthState {
  const AuthState({
    this.role,
    this.isAuthenticated = false,
    this.isLoading = true,
    this.errorMessage,
  });

  final AppRole? role;
  final bool isAuthenticated;
  final bool isLoading;
  final String? errorMessage;

  AuthState copyWith({
    AppRole? role,
    bool? isAuthenticated,
    bool? isLoading,
    String? errorMessage,
    bool clearError = false,
  }) {
    return AuthState(
      role: role ?? this.role,
      isAuthenticated: isAuthenticated ?? this.isAuthenticated,
      isLoading: isLoading ?? this.isLoading,
      errorMessage: clearError ? null : errorMessage ?? this.errorMessage,
    );
  }
}

class AuthController extends Notifier<AuthState> {
  @override
  AuthState build() {
    Future<void>.microtask(_restoreSession);
    return const AuthState();
  }

  Future<void> login({required String email, required String password}) async {
    state = state.copyWith(isLoading: true, clearError: true);

    try {
      final response = await ref
          .read(authRepositoryProvider)
          .login(email: email, password: password);

      if (!_isMobileRole(response.roles)) {
        throw const ApiException(
          'Мобильное приложение доступно только риелторам и администраторам.',
        );
      }

      final role = AppRole.fromBackendRoles(response.roles);
      await ref
          .read(secureTokenStorageProvider)
          .saveAccessToken(response.accessToken);

      state = AuthState(role: role, isAuthenticated: true, isLoading: false);
    } catch (error) {
      await ref.read(secureTokenStorageProvider).clear();
      state = AuthState(isLoading: false, errorMessage: _messageFrom(error));
    }
  }

  Future<void> signOut() async {
    await ref.read(secureTokenStorageProvider).clear();
    state = const AuthState(isLoading: false);
  }

  Future<void> _restoreSession() async {
    final String? token;
    try {
      token = await ref
          .read(secureTokenStorageProvider)
          .readAccessToken()
          .timeout(const Duration(milliseconds: 1500));
    } catch (_) {
      state = const AuthState(isLoading: false);
      return;
    }

    if (token == null || token.isEmpty) {
      state = const AuthState(isLoading: false);
      return;
    }

    try {
      final role = _roleFromToken(token);
      if (role == null) {
        await ref.read(secureTokenStorageProvider).clear();
        state = const AuthState(isLoading: false);
        return;
      }

      state = AuthState(role: role, isAuthenticated: true, isLoading: false);
      unawaited(_validateRestoredSession(token));
    } catch (_) {
      await ref.read(secureTokenStorageProvider).clear();
      state = const AuthState(isLoading: false);
    }
  }

  Future<void> _validateRestoredSession(String restoredToken) async {
    try {
      await ref.read(authRepositoryProvider).checkSessionStatus();
    } on ApiException catch (error) {
      if (!_shouldClearSession(error)) {
        return;
      }

      final currentToken = await ref
          .read(secureTokenStorageProvider)
          .readAccessToken();
      if (currentToken != restoredToken) {
        return;
      }

      await ref.read(secureTokenStorageProvider).clear();
      state = const AuthState(isLoading: false);
    } catch (_) {
      return;
    }
  }

  bool _shouldClearSession(ApiException error) {
    return error.statusCode == 401 ||
        error.statusCode == 403 ||
        error.statusCode == 423;
  }

  String _messageFrom(Object error) {
    if (error is ApiException) {
      return error.message;
    }

    return 'Произошла ошибка. Попробуйте еще раз.';
  }

  bool _isMobileRole(Iterable<String> roles) {
    final normalized = roles.map((role) => role.toLowerCase()).toSet();
    return normalized.contains('realtor') ||
        normalized.contains('admin') ||
        normalized.contains('superadmin');
  }

  AppRole? _roleFromToken(String token) {
    final parts = token.split('.');
    if (parts.length < 2) {
      return null;
    }

    try {
      final payload = utf8.decode(
        base64Url.decode(base64Url.normalize(parts[1])),
      );
      final json = jsonDecode(payload);
      if (json is! Map<String, dynamic>) {
        return null;
      }

      final exp = json['exp'];
      if (exp is num) {
        final expiresAt = DateTime.fromMillisecondsSinceEpoch(
          exp.toInt() * 1000,
          isUtc: true,
        );
        if (DateTime.now().toUtc().isAfter(expiresAt)) {
          return null;
        }
      }

      final roles = <String>[];
      void addClaim(Object? value) {
        if (value is Iterable) {
          roles.addAll(value.map((item) => item.toString()));
        } else if (value != null) {
          roles.add(value.toString());
        }
      }

      addClaim(json['role']);
      addClaim(
        json['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'],
      );

      if (!_isMobileRole(roles)) {
        return null;
      }

      return AppRole.fromBackendRoles(roles);
    } catch (_) {
      return null;
    }
  }
}
