import '../../../complaints/domain/models/complaint_models.dart';
import '../../../deals/domain/models/deal_models.dart';
import '../../../properties/domain/models/property_models.dart';
import '../../../realtor_efficiency/domain/models/efficiency_models.dart';

class AnalyticsFilters {
  const AnalyticsFilters({
    this.dateFrom,
    this.dateTo,
    this.dealType = 'all',
    this.source = 'all',
  });

  final DateTime? dateFrom;
  final DateTime? dateTo;
  final String dealType;
  final String source;

  AnalyticsFilters copyWith({
    DateTime? dateFrom,
    DateTime? dateTo,
    String? dealType,
    String? source,
    bool clearDateFrom = false,
    bool clearDateTo = false,
  }) {
    return AnalyticsFilters(
      dateFrom: clearDateFrom ? null : dateFrom ?? this.dateFrom,
      dateTo: clearDateTo ? null : dateTo ?? this.dateTo,
      dealType: dealType ?? this.dealType,
      source: source ?? this.source,
    );
  }
}

class AdminAnalyticsPayload {
  const AdminAnalyticsPayload({
    required this.deals,
    required this.properties,
    required this.complaints,
    required this.realtors,
    required this.realtorScores,
    required this.financialDeals,
    required this.availableSources,
  });

  final List<DealWorkflow> deals;
  final List<PropertyItem> properties;
  final List<ComplaintItem> complaints;
  final List<RealtorOption> realtors;
  final Map<String, RealtorScoreSnapshot> realtorScores;
  final List<FinancialDeal> financialDeals;
  final List<String> availableSources;

  int get totalDeals => deals.length;
  int get completedDeals => deals.where((item) => item.isCompleted).length;
  int get cancelledDeals => deals.where((item) => item.isCancelled).length;
  int get activeDeals => deals.where((item) => item.isInProgress).length;

  double get conversionRate {
    if (totalDeals == 0) {
      return 0;
    }
    return completedDeals / totalDeals * 100;
  }

  Set<String> get completedPropertyIds => deals
      .where((item) => item.isCompleted && item.propertyId != null)
      .map((item) => item.propertyId!)
      .toSet();

  List<PropertyItem> get soldProperties {
    final ids = completedPropertyIds;
    return properties.where((item) => ids.contains(item.id)).toList();
  }

  List<FinancialDeal> get completedFinancialDeals {
    final ids = deals
        .where((item) => item.isCompleted)
        .map((item) => item.id)
        .toSet();
    return financialDeals.where((item) => ids.contains(item.id)).toList();
  }

  int get soldObjects => soldProperties.length;

  double get averageSoldPrice {
    final values = soldProperties.map((item) => item.price).toList();
    if (values.isEmpty) {
      return 0;
    }
    return values.reduce((a, b) => a + b) / values.length;
  }

  double get medianSoldPrice {
    final values = soldProperties.map((item) => item.price).toList()..sort();
    if (values.isEmpty) {
      return 0;
    }
    final middle = values.length ~/ 2;
    if (values.length.isOdd) {
      return values[middle];
    }
    return (values[middle - 1] + values[middle]) / 2;
  }

  double get totalCommissionUsd {
    return completedFinancialDeals.fold(
      0,
      (sum, item) => sum + item.commissionAmount,
    );
  }

  double get realtorPayoutUsd {
    return completedFinancialDeals.fold(
      0,
      (sum, item) => sum + item.realtorPayoutAmount,
    );
  }

  double get agencyNetCommissionUsd {
    return completedFinancialDeals.fold(
      0,
      (sum, item) => sum + item.agencyNetCommissionAmount,
    );
  }

  double get averageDealDurationDays {
    final values = completedFinancialDeals
        .where((item) => item.completedAt != null)
        .map(
          (item) => item.completedAt!.difference(item.createdDate).inHours / 24,
        )
        .where((value) => value > 0)
        .toList();
    if (values.isEmpty) {
      return 0;
    }
    return values.reduce((a, b) => a + b) / values.length;
  }

  double get averageSaleDurationDays {
    final completedByPropertyId = <String, DateTime>{};
    for (final deal in deals.where(
      (item) => item.isCompleted && item.propertyId != null,
    )) {
      final completedAt = deal.completedAtUtc;
      if (completedAt != null) {
        final current = completedByPropertyId[deal.propertyId!];
        if (current == null || completedAt.isAfter(current)) {
          completedByPropertyId[deal.propertyId!] = completedAt;
        }
      }
    }

    final durations = soldProperties
        .map((property) {
          final soldAt =
              property.soldAtUtc ?? completedByPropertyId[property.id];
          if (soldAt == null) {
            return null;
          }
          final days = soldAt
              .toLocal()
              .difference(property.createdDate.toLocal())
              .inDays;
          return days < 1 ? 1.0 : days.toDouble();
        })
        .whereType<double>()
        .toList();

    if (durations.isEmpty) {
      return 0;
    }
    return durations.reduce((a, b) => a + b) / durations.length;
  }

  double get averageCardQuality {
    if (properties.isEmpty) {
      return 0;
    }
    final total = properties.map(_cardQuality).reduce((a, b) => a + b);
    return total / properties.length;
  }

  int get openComplaints {
    return complaints.where((item) => item.status == 'Opened').length;
  }

  int get inProgressComplaints {
    return complaints.where((item) => item.status == 'InProgress').length;
  }

  int get resolvedComplaints {
    return complaints.where((item) => item.status == 'Resolved').length;
  }

  int get confirmedComplaints {
    return complaints
        .where((item) => item.moderationVerdict == 'Confirmed')
        .length;
  }

  int get partiallyConfirmedComplaints {
    return complaints
        .where((item) => item.moderationVerdict == 'PartiallyConfirmed')
        .length;
  }

  int get notConfirmedComplaints {
    return complaints
        .where((item) => item.moderationVerdict == 'NotConfirmed')
        .length;
  }

  List<NamedCount> get monthlyCompletedDeals {
    final map = <String, int>{};
    final completed =
        completedFinancialDeals
            .where((item) => item.completedAt != null)
            .toList()
          ..sort((a, b) => a.completedAt!.compareTo(b.completedAt!));

    for (final item in completed) {
      final date = item.completedAt!.toLocal();
      final key = '${date.month.toString().padLeft(2, '0')}.${date.year}';
      map.update(key, (value) => value + 1, ifAbsent: () => 1);
    }

    return map.entries
        .map((entry) => NamedCount(entry.key, entry.value))
        .toList()
        .takeLast(6);
  }

  List<NamedCount> get soldByType {
    final map = <String, int>{};
    for (final item in soldProperties) {
      map.update(
        propertyTypeLabel(item.type),
        (value) => value + 1,
        ifAbsent: () => 1,
      );
    }
    return _toNamedCounts(map);
  }

  List<NamedCount> get complaintsByCategory {
    final map = <String, int>{};
    for (final item in complaints) {
      map.update(
        complaintCategoryLabel(item.category),
        (value) => value + 1,
        ifAbsent: () => 1,
      );
    }
    return _toNamedCounts(map);
  }

  List<RealtorScoreRow> get realtorRows {
    return [
      for (final realtor in realtors)
        RealtorScoreRow(realtor: realtor, score: realtorScores[realtor.id]),
    ];
  }

  double _cardQuality(PropertyItem property) {
    final ownerFields = [
      property.ownerFullName?.isNotEmpty ?? false,
      property.ownerEmail?.isNotEmpty ?? false,
      property.ownerPhoneNumber?.isNotEmpty ?? false,
    ].where((value) => value).length;
    final ownerScore = ownerFields / 3 * 5;
    final photoScore = (property.photoPaths.length / 8 * 5)
        .clamp(0, 5)
        .toDouble();
    final criteriaScore = (property.criteriaCount / 6 * 5)
        .clamp(0, 5)
        .toDouble();
    return (ownerScore + photoScore + criteriaScore) / 3;
  }

  List<NamedCount> _toNamedCounts(Map<String, int> map) {
    return [for (final entry in map.entries) NamedCount(entry.key, entry.value)]
      ..sort((a, b) => b.count.compareTo(a.count));
  }
}

class FinancialDeal {
  const FinancialDeal({
    required this.id,
    required this.propertyId,
    required this.status,
    required this.commissionAmount,
    required this.realtorPayoutAmount,
    required this.agencyNetCommissionAmount,
    required this.createdDate,
    this.completedAt,
  });

  final String id;
  final String propertyId;
  final String status;
  final double commissionAmount;
  final double realtorPayoutAmount;
  final double agencyNetCommissionAmount;
  final DateTime createdDate;
  final DateTime? completedAt;
}

class NamedCount {
  const NamedCount(this.name, this.count);

  final String name;
  final int count;
}

class RealtorScoreRow {
  const RealtorScoreRow({required this.realtor, required this.score});

  final RealtorOption realtor;
  final RealtorScoreSnapshot? score;
}

extension _TakeLast<T> on List<T> {
  List<T> takeLast(int count) {
    if (length <= count) {
      return this;
    }
    return sublist(length - count);
  }
}
