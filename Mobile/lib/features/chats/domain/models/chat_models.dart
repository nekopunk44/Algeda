class DealChatSummary {
  const DealChatSummary({
    required this.dealId,
    required this.clientId,
    required this.realtorId,
    required this.dealStatus,
    required this.messageCount,
    this.clientName,
    this.realtorName,
    this.lastMessageAtUtc,
    this.lastMessagePreview,
    this.unreadCount = 0,
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
  final int unreadCount;

  DealChatSummary withUnread({
    required int unreadCount,
    String? preview,
    DateTime? lastMessageAtUtc,
  }) {
    return DealChatSummary(
      dealId: dealId,
      clientId: clientId,
      clientName: clientName,
      realtorId: realtorId,
      realtorName: realtorName,
      dealStatus: dealStatus,
      lastMessageAtUtc: lastMessageAtUtc ?? this.lastMessageAtUtc,
      lastMessagePreview: preview ?? lastMessagePreview,
      messageCount: messageCount,
      unreadCount: unreadCount,
    );
  }
}

class DealChatNotification {
  const DealChatNotification({
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
}

class DealChatDialog {
  const DealChatDialog({
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
  final List<DealChatMessage> messages;

  DealChatDialog copyWithMessages(List<DealChatMessage> value) {
    return DealChatDialog(
      dealId: dealId,
      clientId: clientId,
      realtorId: realtorId,
      currentActorId: currentActorId,
      clientName: clientName,
      realtorName: realtorName,
      canWrite: canWrite,
      blockReason: blockReason,
      messages: value,
    );
  }
}

class DealChatMessage {
  const DealChatMessage({
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
}
