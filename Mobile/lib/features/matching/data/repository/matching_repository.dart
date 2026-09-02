import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/api_exception.dart';
import '../../../../core/network/api_client.dart';
import '../../domain/models/matching_models.dart';
import '../dto/matching_dtos.dart';

final matchingRepositoryProvider = Provider<MatchingRepository>((ref) {
  return MatchingRepository(ref.watch(dioProvider));
});

class MatchingRepository {
  const MatchingRepository(this._dio);

  final Dio _dio;

  Future<List<PropertyMatch>> preview(MatchingSearchRequest request) async {
    try {
      final response = await _dio.post<List<dynamic>>(
        'api/property-matching/preview',
        data: request.toJson(),
      );

      return [
        for (final item in response.data ?? const [])
          if (item is Map)
            PropertyMatchDto.fromJson(
              Map<String, dynamic>.from(item),
            ).toModel(),
      ]..sort((a, b) => b.matchScore.compareTo(a.matchScore));
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<List<MatchingCurrency>> getCurrencies() async {
    try {
      final response = await _dio.get<List<dynamic>>(
        'api/currencies',
        queryParameters: {'limit': 200, 'includeInactive': false},
      );

      return [
        for (final item in response.data ?? const [])
          if (item is Map) _currencyFromJson(Map<String, dynamic>.from(item)),
      ];
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<List<MatchingCriterionDefinition>> getCriteriaDefinitions() async {
    try {
      final response = await _dio.get<List<dynamic>>(
        'api/property-criteria',
        queryParameters: {'limit': 300, 'includeHidden': false},
      );

      return [
        for (final item in response.data ?? const [])
          if (item is Map) _criterionFromJson(Map<String, dynamic>.from(item)),
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

  Future<MatchingAddressPoint?> searchAddress(String query) async {
    final trimmed = query.trim();
    if (trimmed.isEmpty) {
      return null;
    }

    try {
      final response = await _dio.get<List<dynamic>>(
        'https://nominatim.openstreetmap.org/search',
        queryParameters: {'format': 'jsonv2', 'limit': 1, 'q': trimmed},
        options: Options(headers: {'User-Agent': 'AlgedaMobile/1.0'}),
      );
      Map? item;
      for (final candidate in response.data ?? const []) {
        if (candidate is Map) {
          item = candidate;
          break;
        }
      }
      if (item == null) {
        return null;
      }

      final json = Map<String, dynamic>.from(item);
      final lat = double.tryParse('${json['lat'] ?? ''}');
      final lon = double.tryParse('${json['lon'] ?? ''}');
      if (lat == null || lon == null) {
        return null;
      }

      return MatchingAddressPoint(
        latitude: lat,
        longitude: lon,
        title: '${json['display_name'] ?? trimmed}',
      );
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  static MatchingCurrency _currencyFromJson(Map<String, dynamic> json) {
    return MatchingCurrency(
      code: '${json['code'] ?? 'USD'}'.toUpperCase(),
      name: '${json['name'] ?? ''}',
      symbol: '${json['symbol'] ?? ''}',
      rateToBase: _readDouble(json['rateToBase']) ?? 1,
    );
  }

  static MatchingCriterionDefinition _criterionFromJson(
    Map<String, dynamic> json,
  ) {
    return MatchingCriterionDefinition(
      id: '${json['id'] ?? ''}',
      code: '${json['code'] ?? ''}',
      displayName: '${json['displayName'] ?? json['code'] ?? ''}',
      valueType: '${json['valueType'] ?? 'Text'}',
      category: json['category']?.toString(),
      description: json['description']?.toString(),
      options: [
        for (final item in (json['options'] as List<dynamic>? ?? const []))
          if (item is Map)
            MatchingCriterionOption(
              value: '${item['value'] ?? ''}',
              label: '${item['label'] ?? item['value'] ?? ''}',
            ),
      ],
    );
  }

  static double? _readDouble(Object? value) {
    if (value is num) {
      return value.toDouble();
    }
    return double.tryParse('${value ?? ''}');
  }
}

class MatchingAddressPoint {
  const MatchingAddressPoint({
    required this.latitude,
    required this.longitude,
    required this.title,
  });

  final double latitude;
  final double longitude;
  final String title;
}
