class DealChatNotificationDto {
  const DealChatNotificationDto({
    required this.dealId,
    required this.unreadCount,
  });

  final String dealId;
  final int unreadCount;

  factory DealChatNotificationDto.fromJson(Map<String, dynamic> json) {
    String? read(String key) {
      final pascalKey = key[0].toUpperCase() + key.substring(1);
      return (json[key] ?? json[pascalKey])?.toString();
    }

    return DealChatNotificationDto(
      dealId: read('dealId') ?? '',
      unreadCount: int.tryParse(read('unreadCount') ?? '') ?? 0,
    );
  }
}
