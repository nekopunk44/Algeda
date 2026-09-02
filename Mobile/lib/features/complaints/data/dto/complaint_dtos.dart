import '../../domain/models/complaint_models.dart';

class ComplaintDto {
  const ComplaintDto({
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

  factory ComplaintDto.fromJson(Map<String, dynamic> json) {
    return ComplaintDto(
      id: _read(json, 'id') ?? '',
      clientId: _read(json, 'clientId') ?? '',
      targetRealtorId: _read(json, 'targetRealtorId'),
      dealId: _read(json, 'dealId'),
      propertyId: _read(json, 'propertyId'),
      category: _read(json, 'category') ?? 'Undefined',
      subject: _read(json, 'subject') ?? '',
      description: _read(json, 'description') ?? '',
      status: _read(json, 'status') ?? 'Undefined',
      moderationVerdict: _read(json, 'moderationVerdict') ?? 'Undefined',
      resolvedAt: _readDate(json, 'resolvedAt'),
      adminResolution: _read(json, 'adminResolution'),
      createdDate: _readDate(json, 'createdDate') ?? DateTime.now().toUtc(),
      clientFullName: _read(json, 'clientFullName'),
      clientPhoneNumber: _read(json, 'clientPhoneNumber'),
      clientEmail: _read(json, 'clientEmail'),
      realtorFullName: _read(json, 'realtorFullName'),
      realtorPhoneNumber: _read(json, 'realtorPhoneNumber'),
      realtorEmail: _read(json, 'realtorEmail'),
    );
  }

  ComplaintItem toModel() {
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
      clientFullName: clientFullName,
      clientPhoneNumber: clientPhoneNumber,
      clientEmail: clientEmail,
      realtorFullName: realtorFullName,
      realtorPhoneNumber: realtorPhoneNumber,
      realtorEmail: realtorEmail,
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

DateTime? _readDate(Map<String, dynamic> json, String key) {
  final value = _read(json, key);
  if (value == null || value.isEmpty) {
    return null;
  }

  return DateTime.tryParse(value);
}
