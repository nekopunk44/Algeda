import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/api_exception.dart';
import '../../../../core/network/api_client.dart';
import '../../domain/models/notification_models.dart';

final notificationsRepositoryProvider = Provider<NotificationsRepository>((
  ref,
) {
  return NotificationsRepository(ref.watch(dioProvider));
});

class NotificationsRepository {
  const NotificationsRepository(this._dio);

  final Dio _dio;

  Future<List<AppNotification>> getUnread({int limit = 20}) async {
    try {
      final response = await _dio.get<List<dynamic>>(
        'api/deal-chats/notifications/unread',
        queryParameters: {'limit': limit.clamp(1, 200)},
      );

      return [
        for (final item in response.data ?? const [])
          if (item is Map)
            _notificationFromJson(Map<String, dynamic>.from(item)),
      ]..sort((a, b) => b.occurredAtUtc.compareTo(a.occurredAtUtc));
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  Future<void> markAllRead() async {
    try {
      await _dio.post<void>('api/deal-chats/notifications/mark-read');
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  AppNotification _notificationFromJson(Map<String, dynamic> json) {
    final dealId = _read(json, 'dealId') ?? '';
    final counterparty = _read(json, 'counterpartyName');
    final preview = _read(json, 'preview');
    final occurredAt =
        _readDate(json, 'lastMessageAtUtc') ?? DateTime.now().toUtc();
    final unreadCount = _readInt(json, 'unreadCount');

    return AppNotification(
      id: 'chat:$dealId',
      dealId: dealId,
      title: counterparty == null || counterparty.trim().isEmpty
          ? 'Новое сообщение'
          : 'Новое сообщение: $counterparty',
      message: preview == null || preview.trim().isEmpty
          ? 'Откройте чат, чтобы прочитать сообщение.'
          : preview,
      unreadCount: unreadCount,
      occurredAtUtc: occurredAt,
    );
  }

  Object? _readAny(Map<String, dynamic> json, String key) {
    final pascal = key[0].toUpperCase() + key.substring(1);
    return json[key] ?? json[pascal];
  }

  String? _read(Map<String, dynamic> json, String key) {
    return _readAny(json, key)?.toString();
  }

  int _readInt(Map<String, dynamic> json, String key) {
    final raw = _readAny(json, key);
    if (raw is num) {
      return raw.toInt();
    }
    return int.tryParse(raw?.toString() ?? '') ?? 0;
  }

  DateTime? _readDate(Map<String, dynamic> json, String key) {
    final raw = _read(json, key);
    if (raw == null || raw.isEmpty) {
      return null;
    }
    return DateTime.tryParse(raw);
  }
}
