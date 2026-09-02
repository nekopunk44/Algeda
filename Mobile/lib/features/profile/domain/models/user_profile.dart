class UserProfile {
  const UserProfile({
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

  String get fullName {
    return [
      lastName,
      firstName,
      if (middleName != null && middleName!.trim().isNotEmpty) middleName,
    ].whereType<String>().join(' ');
  }
}
