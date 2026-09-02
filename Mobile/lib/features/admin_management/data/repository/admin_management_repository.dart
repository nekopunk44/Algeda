import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/api_exception.dart';
import '../../../../core/network/api_client.dart';
import '../../domain/models/admin_management_models.dart';

final adminManagementRepositoryProvider = Provider<AdminManagementRepository>((
  ref,
) {
  return AdminManagementRepository(ref.watch(dioProvider));
});

class AdminManagementRepository {
  const AdminManagementRepository(this._dio);

  final Dio _dio;

  Future<List<AdminClient>> getClients({int limit = 500}) async {
    try {
      final response = await _dio.get<List<dynamic>>(
        'api/clients',
        queryParameters: {'limit': limit.clamp(1, 500)},
      );
      return [
        for (final item in response.data ?? const [])
          if (item is Map) _clientFromJson(Map<String, dynamic>.from(item)),
      ]..sort((a, b) => b.createdDate.compareTo(a.createdDate));
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<void> createClient({
    required String firstName,
    required String lastName,
    required String? middleName,
    required String phoneNumber,
    required String? email,
  }) async {
    await _send(
      () => _dio.post<void>(
        'api/clients',
        data: {
          'firstName': firstName.trim(),
          'lastName': lastName.trim(),
          'middleName': _nullable(middleName),
          'phoneNumber': phoneNumber.trim(),
          'email': _nullable(email),
        },
      ),
    );
  }

  Future<void> updateClientPhone({
    required String clientId,
    required String phoneNumber,
  }) async {
    await _send(
      () => _dio.put<void>(
        'api/clients/$clientId',
        data: {'id': clientId, 'phoneNumber': phoneNumber.trim()},
      ),
    );
  }

  Future<void> deleteClient(String clientId) async {
    await _send(() => _dio.delete<void>('api/clients/$clientId'));
  }

  Future<List<AdminCurrency>> getCurrencies({
    bool includeInactive = true,
    int limit = 500,
  }) async {
    try {
      final response = await _dio.get<List<dynamic>>(
        'api/currencies',
        queryParameters: {
          'limit': limit.clamp(1, 500),
          'includeInactive': includeInactive,
        },
      );
      return [
        for (final item in response.data ?? const [])
          if (item is Map) _currencyFromJson(Map<String, dynamic>.from(item)),
      ]..sort((a, b) => a.code.compareTo(b.code));
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<void> createCurrency({
    required String code,
    required String name,
    required String symbol,
    required double rateToBase,
    required bool isActive,
  }) async {
    await _send(
      () => _dio.post<void>(
        'api/currencies',
        data: {
          'code': code.trim().toUpperCase(),
          'name': name.trim(),
          'symbol': symbol.trim(),
          'rateToBase': rateToBase,
          'isActive': isActive,
        },
      ),
    );
  }

  Future<void> updateCurrency({
    required String currencyId,
    required String code,
    required String name,
    required String symbol,
    required double rateToBase,
    required bool isActive,
  }) async {
    await _send(
      () => _dio.put<void>(
        'api/currencies/$currencyId',
        data: {
          'code': code.trim().toUpperCase(),
          'name': name.trim(),
          'symbol': symbol.trim(),
          'rateToBase': rateToBase,
          'isActive': isActive,
        },
      ),
    );
  }

  Future<void> setCurrencyActive({
    required String currencyId,
    required bool isActive,
  }) async {
    await _send(
      () => _dio.patch<void>(
        'api/currencies/$currencyId/active',
        data: {'isActive': isActive},
      ),
    );
  }

  Future<void> deleteCurrency(String currencyId) async {
    await _send(() => _dio.delete<void>('api/currencies/$currencyId'));
  }

  Future<AdminCriterionPage> getCriteriaPage({
    required int page,
    required int pageSize,
    required bool includeHidden,
    String? search,
    String sort = 'name_asc',
  }) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        'api/property-criteria/paged',
        queryParameters: {
          'page': page < 1 ? 1 : page,
          'pageSize': pageSize.clamp(1, 100),
          'includeHidden': includeHidden,
          if (search != null && search.trim().isNotEmpty)
            'search': search.trim(),
          'sort': sort,
        },
      );
      final data = response.data;
      if (data == null) {
        throw const ApiException('Сервер вернул пустую страницу критериев.');
      }
      return _criteriaPageFromJson(data);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<List<AdminCriterion>> getCriteria({
    int limit = 500,
    bool includeHidden = true,
  }) async {
    try {
      final response = await _dio.get<List<dynamic>>(
        'api/property-criteria',
        queryParameters: {
          'limit': limit.clamp(1, 500),
          'includeHidden': includeHidden,
        },
      );
      return [
        for (final item in response.data ?? const [])
          if (item is Map) _criterionFromJson(Map<String, dynamic>.from(item)),
      ];
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<void> createCriterion(AdminCriterionFormData data) async {
    await _send(
      () => _dio.post<void>('api/property-criteria', data: data.toJson()),
    );
  }

  Future<void> updateCriterion({
    required String criterionId,
    required AdminCriterionFormData data,
  }) async {
    await _send(
      () => _dio.put<void>(
        'api/property-criteria/$criterionId',
        data: data.toJson(),
      ),
    );
  }

  Future<void> setCriterionHidden({
    required String criterionId,
    required bool isHidden,
  }) async {
    await _send(
      () => _dio.patch<void>(
        'api/property-criteria/$criterionId/hidden',
        data: {'isHidden': isHidden},
      ),
    );
  }

  Future<void> deleteCriterion(String criterionId) async {
    await _send(() => _dio.delete<void>('api/property-criteria/$criterionId'));
  }

  Future<List<AdminAccessUser>> getAccessUsers({
    String? search,
    String? role,
    int limit = 500,
    bool excludeClients = false,
  }) async {
    try {
      final response = await _dio.get<List<dynamic>>(
        'api/access-management/users',
        queryParameters: {
          'limit': limit.clamp(1, 500),
          'excludeClients': excludeClients,
          if (search != null && search.trim().isNotEmpty)
            'search': search.trim(),
          if (role != null && role.trim().isNotEmpty) 'role': role.trim(),
        },
      );
      return [
        for (final item in response.data ?? const [])
          if (item is Map) _accessUserFromJson(Map<String, dynamic>.from(item)),
      ];
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<void> assignRole({
    required String userId,
    required String role,
  }) async {
    await _send(
      () => _dio.post<void>(
        'api/access-management/users/$userId/roles/${Uri.encodeComponent(role)}',
      ),
    );
  }

  Future<void> removeRole({
    required String userId,
    required String role,
  }) async {
    await _send(
      () => _dio.delete<void>(
        'api/access-management/users/$userId/roles/${Uri.encodeComponent(role)}',
      ),
    );
  }

  Future<void> freezeAccount(String userId) async {
    await _send(
      () => _dio.post<void>('api/access-management/users/$userId/freeze'),
    );
  }

  Future<void> unfreezeAccount(String userId) async {
    await _send(
      () => _dio.delete<void>('api/access-management/users/$userId/freeze'),
    );
  }

  Future<void> transferSuperAdmin({
    required String userId,
    required String password,
  }) async {
    await _send(
      () => _dio.post<void>(
        'api/access-management/users/$userId/transfer-super-admin',
        data: {'targetUserId': userId, 'password': password, 'confirmed': true},
      ),
    );
  }

  Future<void> _send(Future<Response<void>> Function() action) async {
    try {
      await action();
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  AdminClient _clientFromJson(Map<String, dynamic> json) {
    return AdminClient(
      id: _read(json, 'id') ?? '',
      firstName: _read(json, 'firstName') ?? '',
      lastName: _read(json, 'lastName') ?? '',
      middleName: _read(json, 'middleName'),
      phoneNumber: _read(json, 'phoneNumber') ?? '',
      email: _read(json, 'email'),
      createdDate: _readDate(json, 'createdDate') ?? DateTime.now().toUtc(),
    );
  }

  AdminCurrency _currencyFromJson(Map<String, dynamic> json) {
    return AdminCurrency(
      id: _read(json, 'id') ?? '',
      code: (_read(json, 'code') ?? 'USD').toUpperCase(),
      name: _read(json, 'name') ?? '',
      symbol: _read(json, 'symbol') ?? '',
      rateToBase: _readDouble(json, 'rateToBase'),
      isActive: _readBool(json, 'isActive'),
      updatedAtUtc: _readDate(json, 'updatedAtUtc') ?? DateTime.now().toUtc(),
      createdDate: _readDate(json, 'createdDate') ?? DateTime.now().toUtc(),
    );
  }

  AdminCriterionPage _criteriaPageFromJson(Map<String, dynamic> json) {
    return AdminCriterionPage(
      page: _readInt(json, 'page', fallback: 1),
      pageSize: _readInt(json, 'pageSize', fallback: 20),
      totalCount: _readInt(json, 'totalCount'),
      items: [
        for (final item in (_readAny(json, 'items') as List? ?? const []))
          if (item is Map) _criterionFromJson(Map<String, dynamic>.from(item)),
      ],
    );
  }

  AdminCriterion _criterionFromJson(Map<String, dynamic> json) {
    final options =
        [
          for (final item in (_readAny(json, 'options') as List? ?? const []))
            if (item is Map)
              AdminCriterionOption(
                value: _read(Map<String, dynamic>.from(item), 'value') ?? '',
                label: _read(Map<String, dynamic>.from(item), 'label') ?? '',
                sortOrder: _readInt(
                  Map<String, dynamic>.from(item),
                  'sortOrder',
                ),
              ),
        ]..sort((a, b) {
          final sort = a.sortOrder.compareTo(b.sortOrder);
          return sort == 0 ? a.value.compareTo(b.value) : sort;
        });

    return AdminCriterion(
      id: _read(json, 'id') ?? '',
      code: _read(json, 'code') ?? '',
      displayName: _read(json, 'displayName') ?? '',
      valueType: _read(json, 'valueType') ?? 'Text',
      category: _read(json, 'category'),
      description: _read(json, 'description'),
      isHidden: _readBool(json, 'isHidden'),
      options: options,
      createdDate: _readDate(json, 'createdDate') ?? DateTime.now().toUtc(),
    );
  }

  AdminAccessUser _accessUserFromJson(Map<String, dynamic> json) {
    return AdminAccessUser(
      userId: _read(json, 'userId') ?? '',
      email: _read(json, 'email') ?? '',
      displayName: _read(json, 'displayName'),
      emailConfirmed: _readBool(json, 'emailConfirmed'),
      isFrozen: _readBool(json, 'isFrozen'),
      roles: [
        for (final item in (_readAny(json, 'roles') as List? ?? const []))
          item.toString(),
      ]..sort(),
    );
  }

  Object? _readAny(Map<String, dynamic> json, String key) {
    final pascal = key[0].toUpperCase() + key.substring(1);
    return json[key] ?? json[pascal];
  }

  String? _read(Map<String, dynamic> json, String key) {
    return _readAny(json, key)?.toString();
  }

  int _readInt(Map<String, dynamic> json, String key, {int fallback = 0}) {
    final raw = _readAny(json, key);
    if (raw is num) {
      return raw.toInt();
    }
    return int.tryParse(raw?.toString() ?? '') ?? fallback;
  }

  double _readDouble(Map<String, dynamic> json, String key) {
    final raw = _readAny(json, key);
    if (raw is num) {
      return raw.toDouble();
    }
    return double.tryParse(raw?.toString() ?? '') ?? 0;
  }

  bool _readBool(Map<String, dynamic> json, String key) {
    final raw = _readAny(json, key);
    return raw == true || raw?.toString().toLowerCase() == 'true';
  }

  DateTime? _readDate(Map<String, dynamic> json, String key) {
    final raw = _read(json, key);
    if (raw == null || raw.isEmpty) {
      return null;
    }
    return DateTime.tryParse(raw);
  }
}

String? _nullable(String? value) {
  final trimmed = value?.trim();
  return trimmed == null || trimmed.isEmpty ? null : trimmed;
}
