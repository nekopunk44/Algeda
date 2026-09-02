import '../../domain/models/chat_models.dart';

class DealChatSummaryDto {
  const DealChatSummaryDto({
    required this.dealId,
    required this.clientId,
    required this.realtorId,
    required this.dealStatus,
    required this.messageCount,
    this.clientName,
    this.realtorName,
    this.lastMessageAtUtc,
    this.lastMessagePreview,
  });

  final String dealId;
  final String clientId;
  final String? clientName;
  final String realtorId;
  final String? realtorName;
  final String dealStatus;
  final DateTime? lastMessageAtUtc;
  final String? lastMessagePreview;
  final int messageCount;

  factory DealChatSummaryDto.fromJson(Map<String, dynamic> json) {
    return DealChatSummaryDto(
      dealId: _read(json, 'dealId') ?? '',
      clientId: _read(json, 'clientId') ?? '',
      clientName: _read(json, 'clientName'),
      realtorId: _read(json, 'realtorId') ?? '',
      realtorName: _read(json, 'realtorName'),
      dealStatus: _read(json, 'dealStatus') ?? 'Undefined',
      lastMessageAtUtc: _readDate(json, 'lastMessageAtUtc'),
      lastMessagePreview: _read(json, 'lastMessagePreview'),
      messageCount: _readInt(json, 'messageCount'),
    );
  }

  DealChatSummary toModel() {
    return DealChatSummary(
      dealId: dealId,
      clientId: clientId,
      clientName: clientName,
      realtorId: realtorId,
      realtorName: realtorName,
      dealStatus: dealStatus,
      lastMessageAtUtc: lastMessageAtUtc,
      lastMessagePreview: lastMessagePreview,
      messageCount: messageCount,
    );
  }
}

class DealChatNotificationDto {
  const DealChatNotificationDto({
    required this.dealId,
    required this.preview,
    required this.unreadCount,
    required this.lastMessageAtUtc,
    this.counterpartyName,
  });

  final String dealId;
  final String? counterpartyName;
  final String preview;
  final int unreadCount;
  final DateTime lastMessageAtUtc;

  factory DealChatNotificationDto.fromJson(Map<String, dynamic> json) {
    return DealChatNotificationDto(
      dealId: _read(json, 'dealId') ?? '',
      counterpartyName: _read(json, 'counterpartyName'),
      preview: _read(json, 'preview') ?? '',
      unreadCount: _readInt(json, 'unreadCount'),
      lastMessageAtUtc:
          _readDate(json, 'lastMessageAtUtc') ?? DateTime.now().toUtc(),
    );
  }

  DealChatNotification toModel() {
    return DealChatNotification(
      dealId: dealId,
      counterpartyName: counterpartyName,
      preview: preview,
      unreadCount: unreadCount,
      lastMessageAtUtc: lastMessageAtUtc,
    );
  }
}

class DealChatDialogDto {
  const DealChatDialogDto({
    required this.dealId,
    required this.clientId,
    required this.realtorId,
    required this.currentActorId,
    required this.canWrite,
    required this.messages,
    this.clientName,
    this.realtorName,
    this.blockReason,
  });

  final String dealId;
  final String clientId;
  final String realtorId;
  final String currentActorId;
  final String? clientName;
  final String? realtorName;
  final bool canWrite;
  final String? blockReason;
  final List<DealChatMessageDto> messages;

  factory DealChatDialogDto.fromJson(Map<String, dynamic> json) {
    return DealChatDialogDto(
      dealId: _read(json, 'dealId') ?? '',
      clientId: _read(json, 'clientId') ?? '',
      realtorId: _read(json, 'realtorId') ?? '',
      currentActorId: _read(json, 'currentActorId') ?? '',
      clientName: _read(json, 'clientName'),
      realtorName: _read(json, 'realtorName'),
      canWrite: _readBool(json, 'canWrite'),
      blockReason: _read(json, 'blockReason'),
      messages: [
        for (final item in (_readAny(json, 'messages') as List? ?? const []))
          if (item is Map)
            DealChatMessageDto.fromJson(Map<String, dynamic>.from(item)),
      ],
    );
  }

  DealChatDialog toModel() {
    return DealChatDialog(
      dealId: dealId,
      clientId: clientId,
      realtorId: realtorId,
      currentActorId: currentActorId,
      clientName: clientName,
      realtorName: realtorName,
      canWrite: canWrite,
      blockReason: blockReason,
      messages: messages.map((item) => item.toModel()).toList()
        ..sort((a, b) => a.createdDate.compareTo(b.createdDate)),
    );
  }
}

class DealChatMessageDto {
  const DealChatMessageDto({
    required this.id,
    required this.dealId,
    required this.senderId,
    required this.receiverId,
    required this.content,
    required this.isRead,
    required this.createdDate,
    required this.isOutgoing,
  });

  final String id;
  final String dealId;
  final String senderId;
  final String receiverId;
  final String content;
  final bool isRead;
  final DateTime createdDate;
  final bool isOutgoing;

  factory DealChatMessageDto.fromJson(Map<String, dynamic> json) {
    return DealChatMessageDto(
      id: _read(json, 'id') ?? '',
      dealId: _read(json, 'dealId') ?? '',
      senderId: _read(json, 'senderId') ?? '',
      receiverId: _read(json, 'receiverId') ?? '',
      content: _read(json, 'content') ?? '',
      isRead: _readBool(json, 'isRead'),
      createdDate: _readDate(json, 'createdDate') ?? DateTime.now().toUtc(),
      isOutgoing: _readBool(json, 'isOutgoing'),
    );
  }

  DealChatMessage toModel() {
    return DealChatMessage(
      id: id,
      dealId: dealId,
      senderId: senderId,
      receiverId: receiverId,
      content: content,
      isRead: isRead,
      createdDate: createdDate,
      isOutgoing: isOutgoing,
    );
  }
}

Object? _readAny(Map<String, dynamic> json, String key) {
  final pascalKey = key[0].toUpperCase() + key.substring(1);
  return json[key] ?? json[pascalKey];
}

String? _read(Map<String, dynamic> json, String key) {
  return _readAny(json, key)?.toString();
}

bool _readBool(Map<String, dynamic> json, String key) {
  return _readAny(json, key) == true;
}

int _readInt(Map<String, dynamic> json, String key) {
  return int.tryParse(_read(json, key) ?? '') ?? 0;
}

DateTime? _readDate(Map<String, dynamic> json, String key) {
  final value = _read(json, key);
  if (value == null || value.isEmpty) {
    return null;
  }

  return DateTime.tryParse(value);
}
