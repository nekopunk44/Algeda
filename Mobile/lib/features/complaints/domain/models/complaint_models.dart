class ComplaintItem {
  const ComplaintItem({
    required this.id,
    required this.clientId,
    required this.category,
    required this.subject,
    required this.description,
    required this.status,
    required this.moderationVerdict,
    required this.createdDate,
    this.targetRealtorId,
    this.dealId,
    this.propertyId,
    this.resolvedAt,
    this.adminResolution,
    this.clientFullName,
    this.clientPhoneNumber,
    this.clientEmail,
    this.realtorFullName,
    this.realtorPhoneNumber,
    this.realtorEmail,
  });

  final String id;
  final String clientId;
  final String? targetRealtorId;
  final String? dealId;
  final String? propertyId;
  final String category;
  final String subject;
  final String description;
  final String status;
  final String moderationVerdict;
  final DateTime? resolvedAt;
  final String? adminResolution;
  final DateTime createdDate;
  final String? clientFullName;
  final String? clientPhoneNumber;
  final String? clientEmail;
  final String? realtorFullName;
  final String? realtorPhoneNumber;
  final String? realtorEmail;

  ComplaintItem copyWith({
    String? clientFullName,
    String? clientPhoneNumber,
    String? clientEmail,
    String? realtorFullName,
    String? realtorPhoneNumber,
    String? realtorEmail,
  }) {
    return ComplaintItem(
      id: id,
      clientId: clientId,
      targetRealtorId: targetRealtorId,
      dealId: dealId,
      propertyId: propertyId,
      category: category,
      subject: subject,
      description: description,
      status: status,
      moderationVerdict: moderationVerdict,
      resolvedAt: resolvedAt,
      adminResolution: adminResolution,
      createdDate: createdDate,
      clientFullName: clientFullName ?? this.clientFullName,
      clientPhoneNumber: clientPhoneNumber ?? this.clientPhoneNumber,
      clientEmail: clientEmail ?? this.clientEmail,
      realtorFullName: realtorFullName ?? this.realtorFullName,
      realtorPhoneNumber: realtorPhoneNumber ?? this.realtorPhoneNumber,
      realtorEmail: realtorEmail ?? this.realtorEmail,
    );
  }
}

const complaintStatuses = ['Opened', 'InProgress', 'Resolved'];
const complaintCategories = [
  'Realtor',
  'PropertyDescriptionMismatch',
  'PoorPropertyMatching',
  'Other',
];
const complaintVerdicts = ['Confirmed', 'PartiallyConfirmed', 'NotConfirmed'];

String complaintStatusLabel(String value) {
  return switch (value) {
    'Opened' => 'Открыта',
    'InProgress' => 'В работе',
    'Resolved' => 'Решена',
    _ => 'Не указан',
  };
}

String complaintCategoryLabel(String value) {
  return switch (value) {
    'Realtor' => 'Риелтор',
    'PropertyDescriptionMismatch' => 'Несоответствие описания',
    'PoorPropertyMatching' => 'Плохой подбор',
    'Other' => 'Другое',
    _ => 'Не указана',
  };
}

String complaintVerdictLabel(String value) {
  return switch (value) {
    'Confirmed' => 'Подтверждена',
    'PartiallyConfirmed' => 'Частично подтверждена',
    'NotConfirmed' => 'Не подтверждена',
    _ => 'Не указан',
  };
}
