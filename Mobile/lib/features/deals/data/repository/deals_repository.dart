import 'dart:io';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:path_provider/path_provider.dart';

import '../../../../core/errors/api_exception.dart';
import '../../../../core/network/api_client.dart';
import '../../domain/models/deal_document_models.dart';
import '../../domain/models/deal_models.dart';
import '../dto/deal_chat_notification_dto.dart';
import '../dto/deal_workflow_dto.dart';
import '../dto/realtor_candidate_dto.dart';

final dealsRepositoryProvider = Provider<DealsRepository>((ref) {
  return DealsRepository(ref.watch(dioProvider));
});

class DealsRepository {
  const DealsRepository(this._dio);

  final Dio _dio;

  Future<List<DealWorkflow>> getDeals(DealFilters filters) async {
    try {
      final path = switch (filters.scope) {
        DealScope.incoming => 'api/deals/incoming',
        DealScope.mine => 'api/deals/mine',
        DealScope.all => 'api/deals/all',
      };

      final query = <String, dynamic>{
        'limit': 500,
        if (filters.search.trim().isNotEmpty) 'search': filters.search.trim(),
        if (filters.source.apiValue != null) 'source': filters.source.apiValue,
        if (filters.scope != DealScope.incoming &&
            filters.status.apiValue != null)
          'status': filters.status.apiValue,
      };

      final response = await _dio.get<List<dynamic>>(
        path,
        queryParameters: query,
      );

      var deals = [
        for (final item in response.data ?? const [])
          if (item is Map<String, dynamic>)
            DealWorkflowDto.fromJson(item).toModel(),
      ];

      if (filters.source == DealSourceFilter.purchase) {
        deals = deals.where((deal) => !deal.isSaleRequest).toList();
      }

      if (filters.scope == DealScope.incoming &&
          filters.status != DealStatusFilter.all) {
        final status = filters.status.apiValue?.toLowerCase();
        deals = deals
            .where((deal) => deal.status.toLowerCase() == status)
            .toList();
      }

      final unread = await getUnreadCounts();
      return deals
          .map((deal) => deal.withUnreadCount(unread[deal.id] ?? 0))
          .toList()
        ..sort((a, b) => b.createdDate.compareTo(a.createdDate));
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<DealWorkflow> getWorkflow(String id) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        'api/deals/$id/workflow',
      );

      final data = response.data;
      if (data == null) {
        throw const ApiException('API вернул пустую карточку заявки.');
      }

      final unread = await getUnreadCounts();
      final deal = DealWorkflowDto.fromJson(data).toModel();
      return deal.withUnreadCount(unread[deal.id] ?? 0);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<Map<String, int>> getUnreadCounts() async {
    try {
      final response = await _dio.get<List<dynamic>>(
        'api/deal-chats/notifications/unread',
        queryParameters: {'limit': 200},
      );

      final result = <String, int>{};
      for (final item in response.data ?? const []) {
        if (item is Map<String, dynamic>) {
          final notification = DealChatNotificationDto.fromJson(item);
          if (notification.dealId.isNotEmpty && notification.unreadCount > 0) {
            result.update(
              notification.dealId,
              (value) => value + notification.unreadCount,
              ifAbsent: () => notification.unreadCount,
            );
          }
        }
      }

      return result;
    } on DioException {
      return {};
    }
  }

  Future<List<RealtorCandidate>> searchRealtors(String search) async {
    try {
      final response = await _dio.get<List<dynamic>>(
        'api/deals/realtors/search',
        queryParameters: {
          'limit': 20,
          if (search.trim().isNotEmpty) 'search': search.trim(),
        },
      );

      return [
        for (final item in response.data ?? const [])
          if (item is Map<String, dynamic>)
            RealtorCandidateDto.fromJson(item).toModel(),
      ];
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<void> accept(String id) => _patch('api/deals/$id/accept');
  Future<void> reject(String id) => _patch('api/deals/$id/reject');
  Future<void> release(String id) => _patch('api/deals/$id/release');
  Future<void> cancel(String id) => _patch('api/deals/$id/cancel');

  Future<void> complete({
    required String id,
    required num commissionAmount,
    required String commissionCurrency,
  }) {
    return _patch(
      'api/deals/$id/complete',
      data: {
        'dealId': id,
        'commissionAmount': commissionAmount,
        'commissionCurrency': commissionCurrency.trim().toUpperCase(),
      },
    );
  }

  Future<void> assignRealtor({required String id, required String realtorId}) {
    return _patch('api/deals/$id/assign', data: {'realtorId': realtorId});
  }

  Future<DealNote> addNote({required String id, required String text}) async {
    return _sendNote(
      method: 'POST',
      path: 'api/deals/$id/notes',
      data: {'text': text.trim()},
    );
  }

  Future<DealNote> updateNote({
    required String id,
    required String noteId,
    required String text,
  }) async {
    return _sendNote(
      method: 'PUT',
      path: 'api/deals/$id/notes/$noteId',
      data: {'text': text.trim()},
    );
  }

  Future<void> deleteNote({required String id, required String noteId}) {
    return _delete('api/deals/$id/notes/$noteId');
  }

  Future<List<DealDocument>> getDocuments(String dealId) async {
    try {
      final response = await _dio.get<List<dynamic>>(
        'api/deals/$dealId/documents',
      );

      return [
        for (final item in response.data ?? const [])
          if (item is Map) _documentFromJson(Map<String, dynamic>.from(item)),
      ]..sort((a, b) => b.createdDate.compareTo(a.createdDate));
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<DealDocument> uploadDocument({
    required String dealId,
    required String title,
    required String filePath,
    required String fileName,
  }) async {
    try {
      final data = FormData.fromMap({
        'Title': title.trim(),
        'File': await MultipartFile.fromFile(filePath, filename: fileName),
      });

      final response = await _dio.post<Map<String, dynamic>>(
        'api/deals/$dealId/documents',
        data: data,
      );

      final payload = response.data;
      if (payload == null) {
        throw const ApiException('Сервер вернул пустой ответ документа.');
      }

      return _documentFromJson(payload);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<String> downloadDocument({
    required String dealId,
    required DealDocument document,
  }) async {
    try {
      final response = await _dio.get<List<int>>(
        'api/deals/$dealId/documents/${document.id}/download',
        options: Options(responseType: ResponseType.bytes),
      );
      final bytes = response.data ?? const <int>[];
      if (bytes.isEmpty) {
        throw const ApiException('Не удалось получить файл документа.');
      }

      final directory = await getTemporaryDirectory();
      final safeName = _safeFileName(document.originalFileName);
      final file = File('${directory.path}/$safeName');
      await file.writeAsBytes(bytes, flush: true);
      return file.path;
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<void> deleteDocument({
    required String dealId,
    required String documentId,
  }) {
    return _delete('api/deals/$dealId/documents/$documentId');
  }

  Future<List<DealDocumentAccessLog>> getDocumentAccessLog({
    String? action,
    String? search,
    int limit = 200,
  }) async {
    try {
      final response = await _dio.get<List<dynamic>>(
        'api/deal-documents/access-log',
        queryParameters: {
          'limit': limit.clamp(1, 500),
          if (action != null && action.trim().isNotEmpty) 'action': action,
          if (search != null && search.trim().isNotEmpty)
            'search': search.trim(),
        },
      );

      return [
        for (final item in response.data ?? const [])
          if (item is Map)
            _documentLogFromJson(Map<String, dynamic>.from(item)),
      ]..sort((a, b) => b.createdDate.compareTo(a.createdDate));
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<void> _patch(String path, {Object? data}) async {
    try {
      await _dio.patch<void>(path, data: data);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<void> _delete(String path) async {
    try {
      await _dio.delete<void>(path);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<DealNote> _sendNote({
    required String method,
    required String path,
    required Object data,
  }) async {
    try {
      final response = await _dio.request<Map<String, dynamic>>(
        path,
        data: data,
        options: Options(method: method),
      );

      final payload = response.data;
      if (payload == null) {
        throw const ApiException('API вернул пустой ответ заметки.');
      }

      return DealNoteDto.fromJson(payload).toModel();
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }
}

DealDocument _documentFromJson(Map<String, dynamic> json) {
  return DealDocument(
    id: json['id']?.toString() ?? '',
    dealId: json['dealId']?.toString() ?? '',
    title: json['title']?.toString() ?? '',
    originalFileName: json['originalFileName']?.toString() ?? '',
    contentType: json['contentType']?.toString() ?? '',
    fileSizeBytes: _readInt(json['fileSizeBytes']),
    uploadedByDisplayName:
        json['uploadedByDisplayName']?.toString() ?? 'Пользователь',
    uploadedByEmail: json['uploadedByEmail']?.toString(),
    createdDate: _readDate(json['createdDate']),
  );
}

DealDocumentAccessLog _documentLogFromJson(Map<String, dynamic> json) {
  return DealDocumentAccessLog(
    id: json['id']?.toString() ?? '',
    dealId: json['dealId']?.toString() ?? '',
    documentId: json['documentId']?.toString() ?? '',
    documentTitle: json['documentTitle']?.toString() ?? '',
    documentFileName: json['documentFileName']?.toString() ?? '',
    action: json['action']?.toString() ?? '',
    actorDisplayName: json['actorDisplayName']?.toString() ?? 'Пользователь',
    actorEmail: json['actorEmail']?.toString(),
    actorRole: json['actorRole']?.toString() ?? '',
    ipAddress: json['ipAddress']?.toString(),
    dealSummary: json['dealSummary']?.toString() ?? '',
    clientSummary: json['clientSummary']?.toString() ?? '',
    realtorSummary: json['realtorSummary']?.toString() ?? '',
    createdDate: _readDate(json['createdDate']),
  );
}

DateTime _readDate(Object? value) {
  return DateTime.tryParse(value?.toString() ?? '') ?? DateTime.now();
}

int _readInt(Object? value) {
  if (value is int) {
    return value;
  }
  return int.tryParse(value?.toString() ?? '') ?? 0;
}

String _safeFileName(String value) {
  final cleaned = value
      .trim()
      .replaceAll(RegExp(r'[\\/:*?"<>|]'), '_')
      .replaceAll(RegExp(r'\s+'), ' ');
  if (cleaned.isEmpty) {
    return 'document';
  }
  return cleaned;
}
