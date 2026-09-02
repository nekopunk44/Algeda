class RealtorScoreSnapshot {
  const RealtorScoreSnapshot({
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
}

class ClientTrustBreakdown {
  const ClientTrustBreakdown({
    required this.clientServiceScoreComponent,
    required this.propertyAccuracyScoreComponent,
    required this.complaintPenaltyComponent,
  });

  final double clientServiceScoreComponent;
  final double propertyAccuracyScoreComponent;
  final double complaintPenaltyComponent;
}

class AdminPerformanceBreakdown {
  const AdminPerformanceBreakdown({
    required this.propertyDataQualityComponent,
    required this.workflowDisciplineComponent,
    required this.businessResultComponent,
    required this.reputationRiskComponent,
  });

  final double propertyDataQualityComponent;
  final double workflowDisciplineComponent;
  final double businessResultComponent;
  final double reputationRiskComponent;
}

class RealtorOption {
  const RealtorOption({
    required this.id,
    required this.fullName,
    required this.phoneNumber,
    required this.level,
  });

  final String id;
  final String fullName;
  final String phoneNumber;
  final String level;
}

class EligibilitySettings {
  const EligibilitySettings({
    required this.restrictionsEnabled,
    required this.minConfirmedHistoryDeals,
    required this.criticalComplaintLookbackDays,
    required this.scoreSnapshotMaxAgeHours,
    required this.blockOnCriticalComplaints,
    required this.priceTiers,
  });

  final bool restrictionsEnabled;
  final int minConfirmedHistoryDeals;
  final int criticalComplaintLookbackDays;
  final int scoreSnapshotMaxAgeHours;
  final bool blockOnCriticalComplaints;
  final List<EligibilityTier> priceTiers;

  Map<String, dynamic> toJson() {
    return {
      'restrictionsEnabled': restrictionsEnabled,
      'minConfirmedHistoryDeals': minConfirmedHistoryDeals,
      'criticalComplaintLookbackDays': criticalComplaintLookbackDays,
      'scoreSnapshotMaxAgeHours': scoreSnapshotMaxAgeHours,
      'blockOnCriticalComplaints': blockOnCriticalComplaints,
      'priceTiers': priceTiers.map((item) => item.toJson()).toList(),
    };
  }
}

class EligibilityTier {
  const EligibilityTier({
    required this.name,
    required this.minPrice,
    required this.minClientTrustScore,
    required this.minAdminPerformanceScore,
    required this.sortOrder,
    this.maxPrice,
  });

  final String name;
  final double minPrice;
  final double? maxPrice;
  final double minClientTrustScore;
  final double minAdminPerformanceScore;
  final int sortOrder;

  Map<String, dynamic> toJson() {
    return {
      'name': name,
      'minPrice': minPrice,
      'maxPrice': maxPrice,
      'minClientTrustScore': minClientTrustScore,
      'minAdminPerformanceScore': minAdminPerformanceScore,
      'sortOrder': sortOrder,
    };
  }
}

class CommissionSettings {
  const CommissionSettings({required this.items});

  final List<CommissionLevel> items;

  Map<String, dynamic> toJson() {
    return {'items': items.map((item) => item.toJson()).toList()};
  }
}

class CommissionLevel {
  const CommissionLevel({required this.level, required this.percent});

  final String level;
  final double percent;

  Map<String, dynamic> toJson() {
    return {'level': level, 'percent': percent};
  }
}

class LevelSettings {
  const LevelSettings({required this.demotionBuffer, required this.rules});

  final double demotionBuffer;
  final List<LevelRule> rules;

  Map<String, dynamic> toJson() {
    return {
      'demotionBuffer': demotionBuffer,
      'rules': rules.map((item) => item.toJson()).toList(),
    };
  }
}

class LevelRule {
  const LevelRule({
    required this.level,
    required this.minCompletedDeals,
    required this.minClientTrustScore,
    required this.minAdminPerformanceScore,
    required this.sortOrder,
  });

  final String level;
  final int minCompletedDeals;
  final double minClientTrustScore;
  final double minAdminPerformanceScore;
  final int sortOrder;

  Map<String, dynamic> toJson() {
    return {
      'level': level,
      'minCompletedDeals': minCompletedDeals,
      'minClientTrustScore': minClientTrustScore,
      'minAdminPerformanceScore': minAdminPerformanceScore,
      'sortOrder': sortOrder,
    };
  }
}

double normalizeScore(double value) {
  if (value.isNaN || value.isInfinite) {
    return 0;
  }

  final normalized = value > 5 && value <= 100 ? value / 20 : value;
  return normalized.clamp(0, 5).toDouble();
}

String scoreText(double value) => normalizeScore(value).toStringAsFixed(2);
