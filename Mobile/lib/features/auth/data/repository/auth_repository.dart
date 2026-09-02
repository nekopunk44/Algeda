import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/api_exception.dart';
import '../../../../core/network/api_client.dart';
import '../dto/login_response_dto.dart';

final authRepositoryProvider = Provider<AuthRepository>((ref) {
  return AuthRepository(ref.watch(dioProvider));
});

class AuthRepository {
  const AuthRepository(this._dio);

  final Dio _dio;

  Future<LoginResponseDto> login({
    required String email,
    required String password,
  }) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        'api/auth/login',
        data: {'email': email.trim(), 'password': password},
      );

      final data = response.data;
      final token = data?['accessToken'] ?? data?['AccessToken'];
      if (data == null || (token?.toString().isEmpty ?? true)) {
        throw const ApiException('Сервер вернул пустой ответ авторизации.');
      }

      return LoginResponseDto.fromJson(data);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<void> checkSessionStatus() async {
    try {
      await _dio.get<Map<String, dynamic>>('api/auth/session-status');
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<String> registerRealtorRequest({
    required String firstName,
    required String lastName,
    required String? middleName,
    required String email,
    required String phoneNumber,
    required String password,
    required String confirmPassword,
  }) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        'api/auth/register/realtor-request',
        data: {
          'firstName': firstName.trim(),
          'lastName': lastName.trim(),
          'middleName': _nullable(middleName),
          'email': email.trim(),
          'phoneNumber': phoneNumber.trim(),
          'password': password,
          'confirmPassword': confirmPassword,
        },
      );
      return _operationMessage(
        response.data,
        'Заявка риелтора отправлена. Мы отправили код подтверждения email.',
      );
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<String> confirmEmail({
    required String email,
    required String code,
  }) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        'api/auth/confirm-email',
        data: {'email': email.trim(), 'code': _normalizeCode(code)},
      );
      return _operationMessage(response.data, 'Email успешно подтвержден.');
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<String> resendConfirmation(String email) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        'api/auth/resend-confirmation',
        data: {'email': email.trim()},
      );
      return _operationMessage(
        response.data,
        'Если учетная запись существует, письмо отправлено.',
      );
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<String> forgotPassword(String email) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        'api/auth/forgot-password',
        data: {'email': email.trim()},
      );
      return _operationMessage(
        response.data,
        'Если учетная запись существует, письмо отправлено.',
      );
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<String> resetPassword({
    required String email,
    required String code,
    required String newPassword,
    required String confirmPassword,
  }) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        'api/auth/reset-password',
        data: {
          'email': email.trim(),
          'code': _normalizeCode(code),
          'newPassword': newPassword,
          'confirmPassword': confirmPassword,
        },
      );
      return _operationMessage(response.data, 'Пароль успешно изменен.');
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  String _operationMessage(Map<String, dynamic>? data, String fallback) {
    final message = data?['message'] ?? data?['Message'];
    return message?.toString().trim().isNotEmpty == true
        ? message.toString()
        : fallback;
  }
}

String _normalizeCode(String value) {
  return value.replaceAll(RegExp(r'\D'), '');
}

String? _nullable(String? value) {
  final trimmed = value?.trim();
  return trimmed == null || trimmed.isEmpty ? null : trimmed;
}
