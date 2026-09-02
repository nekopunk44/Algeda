import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';

import '../../../../core/errors/api_exception.dart';
import '../../../../core/network/api_client.dart';
import '../../domain/models/property_models.dart';
import '../dto/property_dtos.dart';

final propertiesRepositoryProvider = Provider<PropertiesRepository>((ref) {
  return PropertiesRepository(ref.watch(dioProvider));
});

class PropertiesRepository {
  const PropertiesRepository(this._dio);

  final Dio _dio;

  Future<List<PropertyItem>> getProperties({int limit = 500}) async {
    try {
      final response = await _dio.get<List<dynamic>>(
        'api/properties',
        queryParameters: {'limit': limit},
      );

      final items = [
        for (final item in response.data ?? const [])
          if (item is Map)
            PropertyDto.fromJson(Map<String, dynamic>.from(item)).toModel(),
      ]..sort((a, b) => b.createdDate.compareTo(a.createdDate));

      return items;
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<PropertyItem> getProperty(String id) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        'api/properties/$id',
      );

      return _readProperty(response.data);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<PropertyItem> getPropertyForManagement(String id) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        'api/properties/$id/management',
      );

      return _readProperty(response.data);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<PropertyItem> createProperty(PropertyFormData data) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        'api/properties',
        data: data.toJson(),
      );

      return _readProperty(response.data);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<PropertyItem> updateProperty({
    required String id,
    required PropertyFormData data,
  }) async {
    try {
      final response = await _dio.put<Map<String, dynamic>>(
        'api/properties/$id',
        data: data.toJson(propertyId: id),
      );

      return _readProperty(response.data);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<PropertyItem> hide(String id) {
    return _patchStatus('api/properties/$id/hide');
  }

  Future<PropertyItem> show(String id) {
    return _patchStatus('api/properties/$id/show');
  }

  Future<PropertyItem> markAsSold(String id) {
    return _patchStatus('api/properties/$id/mark-as-sold');
  }

  Future<List<PropertyCriterionDefinition>> getCriterionDefinitions() async {
    try {
      final response = await _dio.get<List<dynamic>>(
        'api/property-criteria',
        queryParameters: {'limit': 500, 'includeHidden': false},
      );

      return [
        for (final item in response.data ?? const [])
          if (item is Map)
            PropertyCriterionDefinitionDto.fromJson(
              Map<String, dynamic>.from(item),
            ).toModel(),
      ]..sort((a, b) {
        final category = (a.category ?? '').compareTo(b.category ?? '');
        return category == 0
            ? a.displayName.compareTo(b.displayName)
            : category;
      });
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<List<PropertyCriterionValue>> getCriteria(String propertyId) async {
    try {
      final response = await _dio.get<List<dynamic>>(
        'api/properties/$propertyId/criteria',
      );

      return [
        for (final item in response.data ?? const [])
          if (item is Map)
            PropertyCriterionValueDto.fromJson(
              Map<String, dynamic>.from(item),
            ).toModel(),
      ]..sort((a, b) {
        final category = (a.category ?? '').compareTo(b.category ?? '');
        return category == 0
            ? a.criterionDisplayName.compareTo(b.criterionDisplayName)
            : category;
      });
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<void> upsertCriterion({
    required String propertyId,
    required PropertyCriterionDraft draft,
  }) async {
    try {
      await _dio.put<Map<String, dynamic>>(
        'api/properties/$propertyId/criteria',
        data: draft.toJson(),
      );
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<void> removeCriterion({
    required String propertyId,
    required String criterionDefinitionId,
  }) async {
    try {
      await _dio.delete<void>(
        'api/properties/$propertyId/criteria/$criterionDefinitionId',
      );
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<List<String>> uploadPhotos(List<XFile> files) async {
    if (files.isEmpty) {
      return const [];
    }

    try {
      final form = FormData();
      for (final file in files) {
        form.files.add(
          MapEntry(
            'files',
            await MultipartFile.fromFile(file.path, filename: file.name),
          ),
        );
      }

      final response = await _dio.post<List<dynamic>>(
        'api/property-photos/upload',
        data: form,
      );

      return [
        for (final item in response.data ?? const [])
          if (item is Map)
            PropertyPhotoUploadDto.fromJson(
              Map<String, dynamic>.from(item),
            ).path,
      ].where((path) => path.isNotEmpty).toList();
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<PropertyItem> _patchStatus(String path) async {
    try {
      final response = await _dio.patch<Map<String, dynamic>>(path);
      return _readProperty(response.data);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  PropertyItem _readProperty(Map<String, dynamic>? data) {
    if (data == null) {
      throw const ApiException('API вернул пустой объект недвижимости.');
    }

    return PropertyDto.fromJson(data).toModel();
  }
}
