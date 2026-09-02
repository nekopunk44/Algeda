import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/api_exception.dart';
import '../../../../core/network/api_client.dart';
import '../../domain/models/complaint_models.dart';
import '../dto/complaint_dtos.dart';

final complaintsRepositoryProvider = Provider<ComplaintsRepository>((ref) {
  return ComplaintsRepository(ref.watch(dioProvider));
});

class ComplaintsRepository {
  const ComplaintsRepository(this._dio);

  final Dio _dio;

  Future<List<ComplaintItem>> getMyRealtorComplaints() async {
    try {
      final response = await _dio.get<List<dynamic>>(
        'api/complaints/my-realtor',
        queryParameters: {'limit': 200},
      );
      return _enrichComplaints(_readList(response.data));
    } on DioException catch (error) {
      if (error.response?.statusCode == 404) {
        return const [];
      }
      throw ApiException.fromDio(error);
    }
  }

  Future<List<ComplaintItem>> getAdminComplaints({
    String? status,
    String? category,
    bool? dealLinked,
  }) async {
    try {
      final response = await _dio.get<List<dynamic>>(
        'api/complaints',
        queryParameters: {
          'limit': 500,
          if (status != null && status.isNotEmpty) 'status': status,
          if (category != null && category.isNotEmpty) 'category': category,
          ...(dealLinked == null
              ? const <String, dynamic>{}
              : {'dealLinked': dealLinked}),
        },
      );
      return _enrichComplaints(_readList(response.data));
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<ComplaintItem> getById(String id) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        'api/complaints/$id',
      );
      return _enrichComplaint(_readOne(response.data));
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<void> markInProgress(String id) {
    return _patchNoContent('api/complaints/$id/in-progress');
  }

  Future<void> markOpened(String id) {
    return _patchNoContent('api/complaints/$id/opened');
  }

  Future<ComplaintItem> resolve({
    required String id,
    required String verdict,
    required String resolution,
  }) async {
    try {
      final response = await _dio.patch<Map<String, dynamic>>(
        'api/complaints/$id/resolve',
        data: {
          'complaintId': id,
          'verdict': verdict,
          'resolution': resolution.trim(),
        },
      );
      return _enrichComplaint(_readOne(response.data));
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<void> _patchNoContent(String path) async {
    try {
      await _dio.patch<void>(path);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  List<ComplaintItem> _readList(List<dynamic>? data) {
    return [
      for (final item in data ?? const [])
        if (item is Map)
          ComplaintDto.fromJson(Map<String, dynamic>.from(item)).toModel(),
    ]..sort((a, b) => b.createdDate.compareTo(a.createdDate));
  }

  ComplaintItem _readOne(Map<String, dynamic>? data) {
    if (data == null) {
      throw const ApiException('API вернул пустую жалобу.');
    }

    return ComplaintDto.fromJson(data).toModel();
  }

  Future<ComplaintItem> _enrichComplaint(ComplaintItem item) async {
    final items = await _enrichComplaints([item]);
    return items.first;
  }

  Future<List<ComplaintItem>> _enrichComplaints(
    List<ComplaintItem> items,
  ) async {
    if (items.isEmpty) {
      return items;
    }

    final clientsFuture = _loadClients();
    final realtorsFuture = _loadRealtors();
    final realtorEmailsFuture = _loadApprovedRealtorEmailsByPhone();

    final clients = await clientsFuture;
    final realtors = await realtorsFuture;
    final realtorEmails = await realtorEmailsFuture;

    return [
      for (final item in items)
        _enrichComplaintItem(
          item,
          clients: clients,
          realtors: realtors,
          realtorEmails: realtorEmails,
        ),
    ]..sort((a, b) => b.createdDate.compareTo(a.createdDate));
  }

  ComplaintItem _enrichComplaintItem(
    ComplaintItem item, {
    required Map<String, _ClientInfo> clients,
    required Map<String, _RealtorInfo> realtors,
    required Map<String, String> realtorEmails,
  }) {
    final client = clients[item.clientId];
    final realtor = item.targetRealtorId == null
        ? null
        : realtors[item.targetRealtorId!];
    final realtorPhone = realtor?.phoneNumber?.trim();

    return item.copyWith(
      clientFullName: client?.fullName,
      clientPhoneNumber: client?.phoneNumber,
      clientEmail: client?.email,
      realtorFullName: realtor?.fullName,
      realtorPhoneNumber: realtor?.phoneNumber,
      realtorEmail: realtor?.email ?? realtorEmails[realtorPhone],
    );
  }

  Future<Map<String, _ClientInfo>> _loadClients() async {
    try {
      final response = await _dio.get<List<dynamic>>(
        'api/clients',
        queryParameters: {'limit': 500},
      );
      return {
        for (final item in response.data ?? const [])
          if (item is Map)
            _readString(Map<String, dynamic>.from(item), 'id') ?? '':
                _ClientInfo.fromJson(Map<String, dynamic>.from(item)),
      }..remove('');
    } on DioException {
      return const {};
    }
  }

  Future<Map<String, _RealtorInfo>> _loadRealtors() async {
    try {
      final response = await _dio.get<List<dynamic>>(
        'api/realtors',
        queryParameters: {'limit': 500},
      );
      return {
        for (final item in response.data ?? const [])
          if (item is Map)
            _readString(Map<String, dynamic>.from(item), 'id') ?? '':
                _RealtorInfo.fromJson(Map<String, dynamic>.from(item)),
      }..remove('');
    } on DioException {
      return const {};
    }
  }

  Future<Map<String, String>> _loadApprovedRealtorEmailsByPhone() async {
    try {
      final response = await _dio.get<List<dynamic>>(
        'api/realtor-registration-requests',
        queryParameters: {'status': 'Approved', 'limit': 500},
      );
      final result = <String, String>{};
      final rows =
          [
            for (final item in response.data ?? const [])
              if (item is Map) Map<String, dynamic>.from(item),
          ]..sort((a, b) {
            final left =
                _readDate(a, 'reviewedAt') ??
                _readDate(a, 'createdDate') ??
                DateTime.fromMillisecondsSinceEpoch(0);
            final right =
                _readDate(b, 'reviewedAt') ??
                _readDate(b, 'createdDate') ??
                DateTime.fromMillisecondsSinceEpoch(0);
            return right.compareTo(left);
          });

      for (final row in rows) {
        final phone = _readString(row, 'phoneNumber')?.trim();
        final email = _readString(row, 'email')?.trim();
        if (phone != null &&
            phone.isNotEmpty &&
            email != null &&
            email.isNotEmpty) {
          result.putIfAbsent(phone, () => email);
        }
      }

      return result;
    } on DioException {
      return const {};
    }
  }
}

class _ClientInfo {
  const _ClientInfo({
    required this.fullName,
    required this.phoneNumber,
    required this.email,
  });

  final String fullName;
  final String? phoneNumber;
  final String? email;

  factory _ClientInfo.fromJson(Map<String, dynamic> json) {
    return _ClientInfo(
      fullName: _fullName(json),
      phoneNumber: _readString(json, 'phoneNumber'),
      email: _readString(json, 'email'),
    );
  }
}

class _RealtorInfo {
  const _RealtorInfo({
    required this.fullName,
    required this.phoneNumber,
    required this.email,
  });

  final String fullName;
  final String? phoneNumber;
  final String? email;

  factory _RealtorInfo.fromJson(Map<String, dynamic> json) {
    return _RealtorInfo(
      fullName: _fullName(json),
      phoneNumber: _readString(json, 'phoneNumber'),
      email: _readString(json, 'email'),
    );
  }
}

String _fullName(Map<String, dynamic> json) {
  final fullName = _readString(json, 'fullName')?.trim();
  if (fullName != null && fullName.isNotEmpty) {
    return fullName;
  }

  return [
    _readString(json, 'lastName'),
    _readString(json, 'firstName'),
    _readString(json, 'middleName'),
  ].where((part) => part != null && part.trim().isNotEmpty).join(' ');
}

Object? _readAny(Map<String, dynamic> json, String key) {
  final pascalKey = key[0].toUpperCase() + key.substring(1);
  return json[key] ?? json[pascalKey];
}

String? _readString(Map<String, dynamic> json, String key) {
  return _readAny(json, key)?.toString();
}

DateTime? _readDate(Map<String, dynamic> json, String key) {
  final value = _readString(json, key);
  if (value == null || value.isEmpty) {
    return null;
  }

  return DateTime.tryParse(value);
}
