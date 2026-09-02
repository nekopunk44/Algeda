import '../../domain/models/deal_models.dart';

class RealtorCandidateDto {
  const RealtorCandidateDto({
    required this.realtorId,
    required this.fullName,
    required this.phoneNumber,
    this.email,
  });

  final String realtorId;
  final String fullName;
  final String phoneNumber;
  final String? email;

  factory RealtorCandidateDto.fromJson(Map<String, dynamic> json) {
    String? read(String key) {
      final pascalKey = key[0].toUpperCase() + key.substring(1);
      return (json[key] ?? json[pascalKey])?.toString();
    }

    return RealtorCandidateDto(
      realtorId: read('realtorId') ?? '',
      fullName: read('fullName') ?? '',
      phoneNumber: read('phoneNumber') ?? '',
      email: read('email'),
    );
  }

  RealtorCandidate toModel() {
    return RealtorCandidate(
      realtorId: realtorId,
      fullName: fullName,
      phoneNumber: phoneNumber,
      email: email,
    );
  }
}
