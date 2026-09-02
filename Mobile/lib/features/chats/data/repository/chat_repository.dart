import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/api_exception.dart';
import '../../../../core/network/api_client.dart';
import '../../../deals/data/repository/deals_repository.dart';
import '../../../deals/domain/models/deal_models.dart';
import '../../domain/models/chat_models.dart';
import '../dto/chat_dtos.dart';

final chatRepositoryProvider = Provider<ChatRepository>((ref) {
  return ChatRepository(
    dio: ref.watch(dioProvider),
    dealsRepository: ref.watch(dealsRepositoryProvider),
  );
});

class ChatRepository {
  const ChatRepository({
    required Dio dio,
    required DealsRepository dealsRepository,
  }) : _dio = dio,
       _dealsRepository = dealsRepository;

  final Dio _dio;
  final DealsRepository _dealsRepository;

  Future<List<DealChatSummary>> getRealtorChats() async {
    final deals = await _dealsRepository.getDeals(
      const DealFilters(scope: DealScope.mine),
    );
    final notifications = await getUnreadNotifications();
    final notificationMap = {
      for (final item in notifications) item.dealId: item,
    };

    final chats = deals
        .where(
          (deal) =>
              deal.realtorId != null &&
              deal.realtorId!.isNotEmpty &&
              !deal.isIncoming,
        )
        .map((deal) {
          final notification = notificationMap[deal.id];
          return DealChatSummary(
            dealId: deal.id,
            clientId: deal.clientId,
            clientName: deal.clientFullName,
            realtorId: deal.realtorId ?? '',
            realtorName: deal.realtorFullName,
            dealStatus: deal.status,
            lastMessageAtUtc: notification?.lastMessageAtUtc,
            lastMessagePreview: notification?.preview,
            messageCount: 0,
            unreadCount: notification?.unreadCount ?? 0,
          );
        })
        .toList();

    return _sortSummaries(chats);
  }

  Future<List<DealChatSummary>> getAdminChats({
    String? dealId,
    String? client,
    String? realtor,
  }) async {
    try {
      final query = <String, dynamic>{'limit': 200};
      if (_isGuid(dealId)) {
        query['dealId'] = dealId!.trim();
      }
      if (_isGuid(client)) {
        query['clientId'] = client!.trim();
      }
      if (_isGuid(realtor)) {
        query['realtorId'] = realtor!.trim();
      }

      final response = await _dio.get<List<dynamic>>(
        'api/deal-chats/admin',
        queryParameters: query,
      );
      final notifications = await getUnreadNotifications();
      final notificationMap = {
        for (final item in notifications) item.dealId: item,
      };

      var result =
          [
            for (final item in response.data ?? const [])
              if (item is Map)
                DealChatSummaryDto.fromJson(
                  Map<String, dynamic>.from(item),
                ).toModel(),
          ].map((summary) {
            final notification = notificationMap[summary.dealId];
            if (notification == null) {
              return summary;
            }

            return summary.withUnread(
              unreadCount: notification.unreadCount,
              preview: notification.preview,
              lastMessageAtUtc: notification.lastMessageAtUtc,
            );
          }).toList();

      result = _filterAdmin(
        result,
        dealId: dealId,
        client: client,
        realtor: realtor,
      );
      return _sortSummaries(result);
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<DealChatDialog> getDialog(String dealId, {int limit = 200}) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        'api/deal-chats/$dealId/messages',
        queryParameters: {'limit': limit},
      );

      final data = response.data;
      if (data == null) {
        throw const ApiException('API вернул пустой чат.');
      }

      return DealChatDialogDto.fromJson(data).toModel();
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<DealChatMessage> sendMessage({
    required String dealId,
    required String content,
  }) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(
        'api/deal-chats/$dealId/messages',
        data: {'content': content.trim()},
      );

      final data = response.data;
      if (data == null) {
        throw const ApiException('API вернул пустое сообщение.');
      }

      return DealChatMessageDto.fromJson(data).toModel();
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<List<DealChatNotification>> getUnreadNotifications() async {
    try {
      final response = await _dio.get<List<dynamic>>(
        'api/deal-chats/notifications/unread',
        queryParameters: {'limit': 200},
      );

      return [
        for (final item in response.data ?? const [])
          if (item is Map)
            DealChatNotificationDto.fromJson(
              Map<String, dynamic>.from(item),
            ).toModel(),
      ]..sort((a, b) => b.lastMessageAtUtc.compareTo(a.lastMessageAtUtc));
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<void> markNotificationsRead() async {
    try {
      await _dio.post<void>('api/deal-chats/notifications/mark-read');
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  List<DealChatSummary> _filterAdmin(
    List<DealChatSummary> items, {
    String? dealId,
    String? client,
    String? realtor,
  }) {
    var result = items;

    if (dealId != null && dealId.trim().isNotEmpty) {
      final needle = dealId.trim().toLowerCase();
      result = result
          .where((item) => item.dealId.toLowerCase().contains(needle))
          .toList();
    }

    if (client != null && client.trim().isNotEmpty) {
      final needle = client.trim().toLowerCase();
      result = result
          .where(
            (item) =>
                item.clientId.toLowerCase().contains(needle) ||
                (item.clientName?.toLowerCase().contains(needle) ?? false),
          )
          .toList();
    }

    if (realtor != null && realtor.trim().isNotEmpty) {
      final needle = realtor.trim().toLowerCase();
      result = result
          .where(
            (item) =>
                item.realtorId.toLowerCase().contains(needle) ||
                (item.realtorName?.toLowerCase().contains(needle) ?? false),
          )
          .toList();
    }

    return result;
  }

  List<DealChatSummary> _sortSummaries(List<DealChatSummary> source) {
    return source..sort((a, b) {
      final aDate =
          a.lastMessageAtUtc ?? DateTime.fromMillisecondsSinceEpoch(0);
      final bDate =
          b.lastMessageAtUtc ?? DateTime.fromMillisecondsSinceEpoch(0);
      return bDate.compareTo(aDate);
    });
  }

  bool _isGuid(String? value) {
    if (value == null) {
      return false;
    }

    return RegExp(
      r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$',
    ).hasMatch(value.trim());
  }
}
