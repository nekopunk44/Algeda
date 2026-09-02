import '../../domain/models/user_profile.dart';

class UserProfileDto {
  const UserProfileDto({
    required this.email,
    required this.firstName,
    required this.lastName,
    required this.phoneNumber,
    required this.isRealtor,
    required this.isClient,
    required this.isAdmin,
    required this.realtorPayoutCurrency,
    this.middleName,
    this.avatarPath,
    this.realtorLevel,
    this.isLevelManuallyAssigned,
    this.realtorCommissionPercent,
    this.realtorPayoutThisMonth,
    this.realtorPayoutTotal,
  });

  final String email;
  final String firstName;
  final String lastName;
  final String? middleName;
  final String phoneNumber;
  final bool isRealtor;
  final bool isClient;
  final bool isAdmin;
  final String? avatarPath;
  final String? realtorLevel;
  final bool? isLevelManuallyAssigned;
  final num? realtorCommissionPercent;
  final num? realtorPayoutThisMonth;
  final num? realtorPayoutTotal;
  final String realtorPayoutCurrency;

  factory UserProfileDto.fromJson(Map<String, dynamic> json) {
    return UserProfileDto(
      email: _read(json, 'email') ?? '',
      firstName: _read(json, 'firstName') ?? '',
      lastName: _read(json, 'lastName') ?? '',
      middleName: _read(json, 'middleName'),
      phoneNumber: _read(json, 'phoneNumber') ?? '',
      isRealtor: _readBool(json, 'isRealtor'),
      isClient: _readBool(json, 'isClient'),
      isAdmin: _readBool(json, 'isAdmin'),
      avatarPath: _read(json, 'avatarPath'),
      realtorLevel: _read(json, 'realtorLevel'),
      isLevelManuallyAssigned: _readBoolNullable(
        json,
        'isLevelManuallyAssigned',
      ),
      realtorCommissionPercent: _readNum(json, 'realtorCommissionPercent'),
      realtorPayoutThisMonth: _readNum(json, 'realtorPayoutThisMonth'),
      realtorPayoutTotal: _readNum(json, 'realtorPayoutTotal'),
      realtorPayoutCurrency: _read(json, 'realtorPayoutCurrency') ?? 'USD',
    );
  }

  UserProfile toModel() {
    return UserProfile(
      email: email,
      firstName: firstName,
      lastName: lastName,
      middleName: middleName,
      phoneNumber: phoneNumber,
      isRealtor: isRealtor,
      isClient: isClient,
      isAdmin: isAdmin,
      avatarPath: avatarPath,
      realtorLevel: realtorLevel,
      isLevelManuallyAssigned: isLevelManuallyAssigned,
      realtorCommissionPercent: realtorCommissionPercent,
      realtorPayoutThisMonth: realtorPayoutThisMonth,
      realtorPayoutTotal: realtorPayoutTotal,
      realtorPayoutCurrency: realtorPayoutCurrency,
    );
  }

  static String? _read(Map<String, dynamic> json, String key) {
    final pascalKey = key[0].toUpperCase() + key.substring(1);
    return (json[key] ?? json[pascalKey])?.toString();
  }

  static bool _readBool(Map<String, dynamic> json, String key) {
    final pascalKey = key[0].toUpperCase() + key.substring(1);
    return (json[key] ?? json[pascalKey]) == true;
  }

  static bool? _readBoolNullable(Map<String, dynamic> json, String key) {
    final pascalKey = key[0].toUpperCase() + key.substring(1);
    final value = json[key] ?? json[pascalKey];
    return value is bool ? value : null;
  }

  static num? _readNum(Map<String, dynamic> json, String key) {
    final pascalKey = key[0].toUpperCase() + key.substring(1);
    final value = json[key] ?? json[pascalKey];
    if (value is num) {
      return value;
    }

    return num.tryParse(value?.toString() ?? '');
  }
}
