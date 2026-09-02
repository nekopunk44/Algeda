import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/api_exception.dart';
import '../../../../core/network/api_client.dart';
import '../../domain/models/account_session.dart';
import '../../domain/models/user_profile.dart';
import '../dto/account_session_dto.dart';
import '../dto/user_profile_dto.dart';

final profileRepositoryProvider = Provider<ProfileRepository>((ref) {
  return ProfileRepository(ref.watch(dioProvider));
});

final currentProfileProvider = FutureProvider.autoDispose<UserProfile>((ref) {
  return ref.watch(profileRepositoryProvider).getMe();
});

final accountSessionsProvider =
    FutureProvider.autoDispose<List<AccountSession>>((ref) {
      return ref.watch(profileRepositoryProvider).getSessions();
    });

class ProfileRepository {
  const ProfileRepository(this._dio);

  final Dio _dio;

  Future<UserProfile> getMe() async {
    try {
      final response = await _dio.get<Map<String, dynamic>>('api/account/me');
      final data = response.data;
      if (data == null) {
        throw const ApiException('API вернул пустой аккаунт.');
      }

      return UserProfileDto.fromJson(data).toModel();
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<UserProfile> updateMe({
    required String email,
    required String firstName,
    required String lastName,
    required String? middleName,
    required String phoneNumber,
  }) async {
    try {
      final response = await _dio.put<Map<String, dynamic>>(
        'api/account/me',
        data: {
          'email': email.trim(),
          'firstName': firstName.trim(),
          'lastName': lastName.trim(),
          'middleName': middleName?.trim(),
          'phoneNumber': phoneNumber.trim(),
        },
      );

      final data = response.data;
      if (data == null) {
        throw const ApiException('API вернул пустой аккаунт.');
      }

      return UserProfileDto.fromJson(data).toModel();
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
    required String confirmPassword,
  }) async {
    try {
      await _dio.post<void>(
        'api/account/change-password',
        data: {
          'currentPassword': currentPassword,
          'newPassword': newPassword,
          'confirmPassword': confirmPassword,
        },
      );
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<void> requestEmailChange({required String newEmail}) async {
    try {
      await _dio.post<void>(
        'api/account/request-email-change',
        data: {'newEmail': newEmail.trim()},
      );
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<UserProfile> confirmEmailChange({
    required String newEmail,
    required String code,
  }) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        'api/account/confirm-email-change',
        data: {'newEmail': newEmail.trim(), 'code': code.trim()},
      );

      final data = response.data;
      if (data == null) {
        throw const ApiException('API вернул пустой аккаунт.');
      }

      return UserProfileDto.fromJson(data).toModel();
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<String> uploadAvatar(String filePath, String fileName) async {
    try {
      final form = FormData.fromMap({
        'avatar': await MultipartFile.fromFile(filePath, filename: fileName),
      });

      final response = await _dio.post<Map<String, dynamic>>(
        'api/account/avatar',
        data: form,
      );

      final path = (response.data?['path'] ?? response.data?['Path'])
          ?.toString();
      if (path == null || path.isEmpty) {
        throw const ApiException('API не вернул путь к аватару.');
      }

      return path;
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<void> deleteAvatar() async {
    try {
      await _dio.delete<void>('api/account/avatar');
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<List<AccountSession>> getSessions() async {
    try {
      final response = await _dio.get<List<dynamic>>('api/account/sessions');
      final data = response.data ?? const [];
      return data
          .whereType<Map<String, dynamic>>()
          .map((item) => AccountSessionDto.fromJson(item).toModel())
          .toList();
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<void> revokeSession(String sessionId) async {
    try {
      await _dio.delete<void>('api/account/sessions/$sessionId');
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }
}
