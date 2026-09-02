class AppNotification {
  const AppNotification({
    required this.id,
    required this.dealId,
    required this.title,
    required this.message,
    required this.unreadCount,
    required this.occurredAtUtc,
  });

  final String id;
  final String dealId;
  final String title;
  final String message;
  final int unreadCount;
  final DateTime occurredAtUtc;

  bool get isUnread => unreadCount > 0;
}
