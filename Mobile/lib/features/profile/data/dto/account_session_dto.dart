import '../../domain/models/account_session.dart';

class AccountSessionDto {
  const AccountSessionDto({
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

  factory AccountSessionDto.fromJson(Map<String, dynamic> json) {
    return AccountSessionDto(
      id: (json['id'] ?? json['Id']).toString(),
      deviceName: (json['deviceName'] ?? json['DeviceName'] ?? 'Устройство')
          .toString(),
      ipAddress: (json['ipAddress'] ?? json['IpAddress'])?.toString(),
      createdAtUtc: _parseDate(json['createdAtUtc'] ?? json['CreatedAtUtc']),
      lastSeenAtUtc: _parseDate(json['lastSeenAtUtc'] ?? json['LastSeenAtUtc']),
      expiresAtUtc: _parseDate(json['expiresAtUtc'] ?? json['ExpiresAtUtc']),
      isCurrent: json['isCurrent'] == true || json['IsCurrent'] == true,
    );
  }

  AccountSession toModel() {
    return AccountSession(
      id: id,
      deviceName: deviceName,
      ipAddress: ipAddress,
      createdAtUtc: createdAtUtc,
      lastSeenAtUtc: lastSeenAtUtc,
      expiresAtUtc: expiresAtUtc,
      isCurrent: isCurrent,
    );
  }

  static DateTime _parseDate(Object? value) {
    if (value == null) {
      return DateTime.fromMillisecondsSinceEpoch(0, isUtc: true);
    }

    return DateTime.parse(value.toString()).toUtc();
  }
}
