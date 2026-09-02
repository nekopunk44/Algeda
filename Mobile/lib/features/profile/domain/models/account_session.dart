class AccountSession {
  const AccountSession({
    required this.id,
    required this.deviceName,
    required this.createdAtUtc,
    required this.lastSeenAtUtc,
    required this.expiresAtUtc,
    required this.isCurrent,
    this.ipAddress,
  });

  final String id;
  final String deviceName;
  final String? ipAddress;
  final DateTime createdAtUtc;
  final DateTime lastSeenAtUtc;
  final DateTime expiresAtUtc;
  final bool isCurrent;
}
