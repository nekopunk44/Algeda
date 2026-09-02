import '../../domain/models/efficiency_models.dart';

class RealtorScoreSnapshotDto {
  const RealtorScoreSnapshotDto({
    required this.snapshotId,
    required this.realtorId,
    required this.clientTrustScore,
    required this.adminPerformanceScore,
    required this.clientTrustBreakdown,
    required this.adminPerformanceBreakdown,
    required this.createdDate,
    this.calculationVersion,
  });

  final String snapshotId;
  final String realtorId;
  final double clientTrustScore;
  final double adminPerformanceScore;
  final ClientTrustBreakdown clientTrustBreakdown;
  final AdminPerformanceBreakdown adminPerformanceBreakdown;
  final String? calculationVersion;
  final DateTime createdDate;

  factory RealtorScoreSnapshotDto.fromJson(Map<String, dynamic> json) {
    final cts = _map(json, 'clientTrustBreakdown');
    final aps = _map(json, 'adminPerformanceBreakdown');
    return RealtorScoreSnapshotDto(
      snapshotId: _read(json, 'snapshotId') ?? '',
      realtorId: _read(json, 'realtorId') ?? '',
      clientTrustScore: _double(json, 'clientTrustScore'),
      adminPerformanceScore: _double(json, 'adminPerformanceScore'),
      clientTrustBreakdown: ClientTrustBreakdown(
        clientServiceScoreComponent: _double(
          cts,
          'clientServiceScoreComponent',
        ),
        propertyAccuracyScoreComponent: _double(
          cts,
          'propertyAccuracyScoreComponent',
        ),
        complaintPenaltyComponent: _double(cts, 'complaintPenaltyComponent'),
      ),
      adminPerformanceBreakdown: AdminPerformanceBreakdown(
        propertyDataQualityComponent: _double(
          aps,
          'propertyDataQualityComponent',
        ),
        workflowDisciplineComponent: _double(
          aps,
          'workflowDisciplineComponent',
        ),
        businessResultComponent: _double(aps, 'businessResultComponent'),
        reputationRiskComponent: _double(aps, 'reputationRiskComponent'),
      ),
      calculationVersion: _read(json, 'calculationVersion'),
      createdDate: _date(json, 'createdDate') ?? DateTime.now().toUtc(),
    );
  }

  RealtorScoreSnapshot toModel() {
    return RealtorScoreSnapshot(
      snapshotId: snapshotId,
      realtorId: realtorId,
      clientTrustScore: clientTrustScore,
      adminPerformanceScore: adminPerformanceScore,
      clientTrustBreakdown: clientTrustBreakdown,
      adminPerformanceBreakdown: adminPerformanceBreakdown,
      calculationVersion: calculationVersion,
      createdDate: createdDate,
    );
  }
}

class RealtorOptionDto {
  const RealtorOptionDto({
    required this.id,
    required this.fullName,
    required this.phoneNumber,
    required this.level,
  });

  final String id;
  final String fullName;
  final String phoneNumber;
  final String level;

  factory RealtorOptionDto.fromJson(Map<String, dynamic> json) {
    final last = _read(json, 'lastName') ?? '';
    final first = _read(json, 'firstName') ?? '';
    final middle = _read(json, 'middleName') ?? '';
    return RealtorOptionDto(
      id: _read(json, 'id') ?? '',
      fullName: [last, first, middle].where((x) => x.isNotEmpty).join(' '),
      phoneNumber: _read(json, 'phoneNumber') ?? '',
      level: _read(json, 'level') ?? 'Undefined',
    );
  }

  RealtorOption toModel() {
    return RealtorOption(
      id: id,
      fullName: fullName.isEmpty ? id : fullName,
      phoneNumber: phoneNumber,
      level: level,
    );
  }
}

class EligibilitySettingsDto {
  const EligibilitySettingsDto({required this.settings});

  final EligibilitySettings settings;

  factory EligibilitySettingsDto.fromJson(Map<String, dynamic> json) {
    return EligibilitySettingsDto(
      settings: EligibilitySettings(
        restrictionsEnabled: _bool(json, 'restrictionsEnabled'),
        minConfirmedHistoryDeals: _int(json, 'minConfirmedHistoryDeals'),
        criticalComplaintLookbackDays: _int(
          json,
          'criticalComplaintLookbackDays',
        ),
        scoreSnapshotMaxAgeHours: _int(json, 'scoreSnapshotMaxAgeHours'),
        blockOnCriticalComplaints: _bool(json, 'blockOnCriticalComplaints'),
        priceTiers: [
          for (final item in (_any(json, 'priceTiers') as List? ?? const []))
            if (item is Map)
              EligibilityTier(
                name: _read(Map<String, dynamic>.from(item), 'name') ?? '',
                minPrice: _double(Map<String, dynamic>.from(item), 'minPrice'),
                maxPrice: _nullableDouble(
                  Map<String, dynamic>.from(item),
                  'maxPrice',
                ),
                minClientTrustScore: _double(
                  Map<String, dynamic>.from(item),
                  'minClientTrustScore',
                ),
                minAdminPerformanceScore: _double(
                  Map<String, dynamic>.from(item),
                  'minAdminPerformanceScore',
                ),
                sortOrder: _int(Map<String, dynamic>.from(item), 'sortOrder'),
              ),
        ]..sort((a, b) => a.sortOrder.compareTo(b.sortOrder)),
      ),
    );
  }
}

class CommissionSettingsDto {
  const CommissionSettingsDto({required this.settings});

  final CommissionSettings settings;

  factory CommissionSettingsDto.fromJson(Map<String, dynamic> json) {
    return CommissionSettingsDto(
      settings: CommissionSettings(
        items: [
          for (final item in (_any(json, 'items') as List? ?? const []))
            if (item is Map)
              CommissionLevel(
                level: _read(Map<String, dynamic>.from(item), 'level') ?? '',
                percent: _double(Map<String, dynamic>.from(item), 'percent'),
              ),
        ],
      ),
    );
  }
}

class LevelSettingsDto {
  const LevelSettingsDto({required this.settings});

  final LevelSettings settings;

  factory LevelSettingsDto.fromJson(Map<String, dynamic> json) {
    return LevelSettingsDto(
      settings: LevelSettings(
        demotionBuffer: _double(json, 'demotionBuffer'),
        rules: [
          for (final item in (_any(json, 'rules') as List? ?? const []))
            if (item is Map)
              LevelRule(
                level: _read(Map<String, dynamic>.from(item), 'level') ?? '',
                minCompletedDeals: _int(
                  Map<String, dynamic>.from(item),
                  'minCompletedDeals',
                ),
                minClientTrustScore: _double(
                  Map<String, dynamic>.from(item),
                  'minClientTrustScore',
                ),
                minAdminPerformanceScore: _double(
                  Map<String, dynamic>.from(item),
                  'minAdminPerformanceScore',
                ),
                sortOrder: _int(Map<String, dynamic>.from(item), 'sortOrder'),
              ),
        ]..sort((a, b) => a.sortOrder.compareTo(b.sortOrder)),
      ),
    );
  }
}

Map<String, dynamic> _map(Map<String, dynamic> json, String key) {
  final value = _any(json, key);
  return value is Map ? Map<String, dynamic>.from(value) : <String, dynamic>{};
}

Object? _any(Map<String, dynamic> json, String key) {
  final pascalKey = key[0].toUpperCase() + key.substring(1);
  return json[key] ?? json[pascalKey];
}

String? _read(Map<String, dynamic> json, String key) =>
    _any(json, key)?.toString();

double _double(Map<String, dynamic> json, String key) {
  final value = _any(json, key);
  return value is num ? value.toDouble() : double.tryParse('$value') ?? 0;
}

double? _nullableDouble(Map<String, dynamic> json, String key) {
  final value = _any(json, key);
  if (value == null) {
    return null;
  }
  return value is num ? value.toDouble() : double.tryParse('$value');
}

int _int(Map<String, dynamic> json, String key) {
  final value = _any(json, key);
  return value is num ? value.toInt() : int.tryParse('$value') ?? 0;
}

bool _bool(Map<String, dynamic> json, String key) => _any(json, key) == true;

DateTime? _date(Map<String, dynamic> json, String key) {
  final value = _read(json, key);
  if (value == null || value.isEmpty) {
    return null;
  }
  return DateTime.tryParse(value);
}
