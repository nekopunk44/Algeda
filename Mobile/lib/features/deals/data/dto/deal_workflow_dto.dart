import '../../domain/models/deal_models.dart';

class DealWorkflowDto {
  const DealWorkflowDto({
    required this.id,
    required this.clientId,
    required this.clientFullName,
    required this.clientPhoneNumber,
    required this.source,
    required this.status,
    required this.isIncoming,
    required this.notes,
    required this.createdDate,
    this.clientEmail,
    this.propertyId,
    this.propertyTitle,
    this.clientRequirementId,
    this.realtorId,
    this.realtorFullName,
    this.realtorPhoneNumber,
    this.realtorEmail,
    this.requestMessage,
    this.acceptedAtUtc,
    this.rejectedAtUtc,
    this.priorityRealtorId,
    this.priorityUntilUtc,
    this.completedAtUtc,
  });

  final String id;
  final String clientId;
  final String clientFullName;
  final String clientPhoneNumber;
  final String? clientEmail;
  final String? propertyId;
  final String? propertyTitle;
  final String? clientRequirementId;
  final String source;
  final String status;
  final bool isIncoming;
  final String? realtorId;
  final String? realtorFullName;
  final String? realtorPhoneNumber;
  final String? realtorEmail;
  final String? requestMessage;
  final DateTime? acceptedAtUtc;
  final DateTime? rejectedAtUtc;
  final String? priorityRealtorId;
  final DateTime? priorityUntilUtc;
  final DateTime? completedAtUtc;
  final List<DealNoteDto> notes;
  final DateTime createdDate;

  factory DealWorkflowDto.fromJson(Map<String, dynamic> json) {
    return DealWorkflowDto(
      id: _read(json, 'id') ?? '',
      clientId: _read(json, 'clientId') ?? '',
      clientFullName: _read(json, 'clientFullName') ?? '',
      clientPhoneNumber: _read(json, 'clientPhoneNumber') ?? '',
      clientEmail: _read(json, 'clientEmail'),
      propertyId: _read(json, 'propertyId'),
      propertyTitle: _read(json, 'propertyTitle'),
      clientRequirementId: _read(json, 'clientRequirementId'),
      source: _read(json, 'source') ?? 'Undefined',
      status: _read(json, 'status') ?? 'Undefined',
      isIncoming: _readBool(json, 'isIncoming'),
      realtorId: _read(json, 'realtorId'),
      realtorFullName: _read(json, 'realtorFullName'),
      realtorPhoneNumber: _read(json, 'realtorPhoneNumber'),
      realtorEmail: _read(json, 'realtorEmail'),
      requestMessage: _read(json, 'requestMessage'),
      acceptedAtUtc: _readDate(json, 'acceptedAtUtc'),
      rejectedAtUtc: _readDate(json, 'rejectedAtUtc'),
      priorityRealtorId: _read(json, 'priorityRealtorId'),
      priorityUntilUtc: _readDate(json, 'priorityUntilUtc'),
      completedAtUtc: _readDate(json, 'completedAtUtc'),
      notes: [
        for (final note in (_readAny(json, 'notes') as List? ?? const []))
          if (note is Map<String, dynamic>) DealNoteDto.fromJson(note),
      ],
      createdDate: _readDate(json, 'createdDate') ?? DateTime.now(),
    );
  }

  DealWorkflow toModel() {
    return DealWorkflow(
      id: id,
      clientId: clientId,
      clientFullName: clientFullName,
      clientPhoneNumber: clientPhoneNumber,
      clientEmail: clientEmail,
      propertyId: propertyId,
      propertyTitle: propertyTitle,
      clientRequirementId: clientRequirementId,
      source: source,
      status: status,
      isIncoming: isIncoming,
      realtorId: realtorId,
      realtorFullName: realtorFullName,
      realtorPhoneNumber: realtorPhoneNumber,
      realtorEmail: realtorEmail,
      requestMessage: requestMessage,
      acceptedAtUtc: acceptedAtUtc,
      rejectedAtUtc: rejectedAtUtc,
      priorityRealtorId: priorityRealtorId,
      priorityUntilUtc: priorityUntilUtc,
      completedAtUtc: completedAtUtc,
      notes: notes.map((note) => note.toModel()).toList(),
      createdDate: createdDate,
    );
  }

  static Object? _readAny(Map<String, dynamic> json, String key) {
    final pascalKey = key[0].toUpperCase() + key.substring(1);
    return json[key] ?? json[pascalKey];
  }

  static String? _read(Map<String, dynamic> json, String key) {
    return _readAny(json, key)?.toString();
  }

  static bool _readBool(Map<String, dynamic> json, String key) {
    return _readAny(json, key) == true;
  }

  static DateTime? _readDate(Map<String, dynamic> json, String key) {
    final value = _read(json, key);
    if (value == null || value.isEmpty) {
      return null;
    }

    return DateTime.tryParse(value);
  }
}

class DealNoteDto {
  const DealNoteDto({
    required this.id,
    required this.text,
    required this.createdDate,
    this.authorRealtorId,
    this.updatedAtUtc,
  });

  final String id;
  final String? authorRealtorId;
  final String text;
  final DateTime? updatedAtUtc;
  final DateTime createdDate;

  factory DealNoteDto.fromJson(Map<String, dynamic> json) {
    return DealNoteDto(
      id: DealWorkflowDto._read(json, 'id') ?? '',
      authorRealtorId: DealWorkflowDto._read(json, 'authorRealtorId'),
      text: DealWorkflowDto._read(json, 'text') ?? '',
      updatedAtUtc: DealWorkflowDto._readDate(json, 'updatedAtUtc'),
      createdDate:
          DealWorkflowDto._readDate(json, 'createdDate') ?? DateTime.now(),
    );
  }

  DealNote toModel() {
    return DealNote(
      id: id,
      authorRealtorId: authorRealtorId,
      text: text,
      updatedAtUtc: updatedAtUtc,
      createdDate: createdDate,
    );
  }
}
