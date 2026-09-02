import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/api_exception.dart';
import '../../../../core/network/api_client.dart';
import '../../domain/models/efficiency_models.dart';
import '../dto/efficiency_dtos.dart';

final efficiencyRepositoryProvider = Provider<EfficiencyRepository>((ref) {
  return EfficiencyRepository(ref.watch(dioProvider));
});

class EfficiencyRepository {
  const EfficiencyRepository(this._dio);

  final Dio _dio;

  Future<RealtorScoreSnapshot> getMyLatest() async {
    return _getSnapshot('api/realtor-efficiency/me/scores/latest');
  }

  Future<List<RealtorScoreSnapshot>> getMyHistory() async {
    return _getHistory('api/realtor-efficiency/me/scores/history');
  }

  Future<RealtorScoreSnapshot> getLatestForRealtor(String realtorId) async {
    return _getSnapshot(
      'api/realtor-efficiency/realtors/$realtorId/scores/latest',
    );
  }

  Future<List<RealtorScoreSnapshot>> getHistoryForRealtor(String realtorId) {
    return _getHistory(
      'api/realtor-efficiency/realtors/$realtorId/scores/history',
    );
  }

  Future<List<RealtorOption>> getRealtors() async {
    try {
      final response = await _dio.get<List<dynamic>>(
        'api/realtors',
        queryParameters: {'limit': 500},
      );
      return [
        for (final item in response.data ?? const [])
          if (item is Map)
            RealtorOptionDto.fromJson(
              Map<String, dynamic>.from(item),
            ).toModel(),
      ];
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<EligibilitySettings> getEligibilitySettings() async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        'api/realtor-efficiency/eligibility-settings',
      );
      final data = response.data;
      if (data == null) {
        throw const ApiException('API вернул пустые настройки.');
      }
      return EligibilitySettingsDto.fromJson(data).settings;
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<EligibilitySettings> updateEligibilitySettings(
    EligibilitySettings settings,
  ) async {
    try {
      final response = await _dio.put<Map<String, dynamic>>(
        'api/realtor-efficiency/eligibility-settings',
        data: settings.toJson(),
      );
      final data = response.data;
      if (data == null) {
        throw const ApiException('API вернул пустые настройки.');
      }
      return EligibilitySettingsDto.fromJson(data).settings;
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<CommissionSettings> getCommissionSettings() async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        'api/realtor-efficiency/commission-settings',
      );
      final data = response.data;
      if (data == null) {
        throw const ApiException('Сервер вернул пустые настройки.');
      }
      return CommissionSettingsDto.fromJson(data).settings;
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<CommissionSettings> updateCommissionSettings(
    CommissionSettings settings,
  ) async {
    try {
      final response = await _dio.put<Map<String, dynamic>>(
        'api/realtor-efficiency/commission-settings',
        data: settings.toJson(),
      );
      final data = response.data;
      if (data == null) {
        throw const ApiException('Сервер вернул пустые настройки.');
      }
      return CommissionSettingsDto.fromJson(data).settings;
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<LevelSettings> getLevelSettings() async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        'api/realtor-efficiency/level-settings',
      );
      final data = response.data;
      if (data == null) {
        throw const ApiException('Сервер вернул пустые настройки.');
      }
      return LevelSettingsDto.fromJson(data).settings;
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<LevelSettings> updateLevelSettings(LevelSettings settings) async {
    try {
      final response = await _dio.put<Map<String, dynamic>>(
        'api/realtor-efficiency/level-settings',
        data: settings.toJson(),
      );
      final data = response.data;
      if (data == null) {
        throw const ApiException('Сервер вернул пустые настройки.');
      }
      return LevelSettingsDto.fromJson(data).settings;
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<RealtorScoreSnapshot> _getSnapshot(String path) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(path);
      final data = response.data;
      if (data == null) {
        throw const ApiException('API вернул пустой расчет эффективности.');
      }
      return RealtorScoreSnapshotDto.fromJson(data).toModel();
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<List<RealtorScoreSnapshot>> _getHistory(String path) async {
    try {
      final response = await _dio.get<List<dynamic>>(
        path,
        queryParameters: {'limit': 50},
      );
      return [
        for (final item in response.data ?? const [])
          if (item is Map)
            RealtorScoreSnapshotDto.fromJson(
              Map<String, dynamic>.from(item),
            ).toModel(),
      ]..sort((a, b) => b.createdDate.compareTo(a.createdDate));
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }
}
