import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/api_exception.dart';
import '../../../../core/network/api_client.dart';
import '../../../complaints/data/repository/complaints_repository.dart';
import '../../../complaints/domain/models/complaint_models.dart';
import '../../../deals/data/repository/deals_repository.dart';
import '../../../deals/domain/models/deal_models.dart';
import '../../../properties/data/repository/properties_repository.dart';
import '../../../realtor_efficiency/data/repository/efficiency_repository.dart';
import '../../../realtor_efficiency/domain/models/efficiency_models.dart';
import '../../domain/models/analytics_models.dart';

final analyticsRepositoryProvider = Provider<AnalyticsRepository>((ref) {
  return AnalyticsRepository(
    dealsRepository: ref.watch(dealsRepositoryProvider),
    propertiesRepository: ref.watch(propertiesRepositoryProvider),
    complaintsRepository: ref.watch(complaintsRepositoryProvider),
    efficiencyRepository: ref.watch(efficiencyRepositoryProvider),
    dio: ref.watch(dioProvider),
  );
});

class AnalyticsRepository {
  const AnalyticsRepository({
    required DealsRepository dealsRepository,
    required PropertiesRepository propertiesRepository,
    required ComplaintsRepository complaintsRepository,
    required EfficiencyRepository efficiencyRepository,
    required Dio dio,
  }) : _dealsRepository = dealsRepository,
       _propertiesRepository = propertiesRepository,
       _complaintsRepository = complaintsRepository,
       _efficiencyRepository = efficiencyRepository,
       _dio = dio;

  final DealsRepository _dealsRepository;
  final PropertiesRepository _propertiesRepository;
  final ComplaintsRepository _complaintsRepository;
  final EfficiencyRepository _efficiencyRepository;
  final Dio _dio;

  Future<AdminAnalyticsPayload> loadAdminAnalytics(
    AnalyticsFilters filters,
  ) async {
    final source = switch (filters.dealType) {
      'sale' => DealSourceFilter.sale,
      'purchase' => DealSourceFilter.purchase,
      _ => DealSourceFilter.all,
    };

    final dealsFuture = _dealsRepository.getDeals(
      DealFilters(scope: DealScope.all, source: source),
    );
    final propertiesFuture = _propertiesRepository.getProperties();
    final complaintsFuture = _complaintsRepository.getAdminComplaints();
    final realtorsFuture = _efficiencyRepository.getRealtors();
    final financialDealsFuture = _getFinancialDeals();

    final allWorkflowDeals = await dealsFuture;
    final deals = _filterDeals(allWorkflowDeals, filters);
    final dealIds = deals.map((item) => item.id).toSet();
    final properties = await propertiesFuture;
    final complaints = _filterComplaints(
      await complaintsFuture,
      filters,
      dealIds,
    );
    final realtors = await realtorsFuture;

    final scoreEntries = await Future.wait(
      realtors.take(20).map((realtor) async {
        try {
          final score = await _efficiencyRepository.getLatestForRealtor(
            realtor.id,
          );
          return MapEntry(realtor.id, score);
        } catch (_) {
          return MapEntry<String, RealtorScoreSnapshot?>(realtor.id, null);
        }
      }),
    );

    return AdminAnalyticsPayload(
      deals: deals,
      properties: properties,
      complaints: complaints,
      realtors: realtors,
      realtorScores: {
        for (final entry in scoreEntries)
          if (entry.value != null) entry.key: entry.value!,
      },
      financialDeals: _filterFinancialDeals(
        await financialDealsFuture,
        filters,
      ),
      availableSources: _availableSources(allWorkflowDeals),
    );
  }

  List<DealWorkflow> _filterDeals(
    List<DealWorkflow> source,
    AnalyticsFilters filters,
  ) {
    return source.where((item) {
      if (!_isInPeriod(item.createdDate, filters)) {
        return false;
      }
      if (filters.source != 'all' &&
          item.source.toLowerCase() != filters.source.toLowerCase()) {
        return false;
      }
      return true;
    }).toList();
  }

  List<FinancialDeal> _filterFinancialDeals(
    List<FinancialDeal> source,
    AnalyticsFilters filters,
  ) {
    return source
        .where((item) => _isInPeriod(item.createdDate, filters))
        .toList();
  }

  List<ComplaintItem> _filterComplaints(
    List<ComplaintItem> source,
    AnalyticsFilters filters,
    Set<String> dealIds,
  ) {
    return source.where((item) {
      if (!_isInPeriod(item.createdDate, filters)) {
        return false;
      }
      if (filters.dealType == 'all') {
        return true;
      }
      if (item.dealId == null) {
        return false;
      }
      return dealIds.contains(item.dealId);
    }).toList();
  }

  bool _isInPeriod(DateTime dateTime, AnalyticsFilters filters) {
    final converted = dateTime.toLocal();
    final local = DateTime(converted.year, converted.month, converted.day);
    final from = filters.dateFrom;
    final to = filters.dateTo;
    if (from != null &&
        local.isBefore(DateTime(from.year, from.month, from.day))) {
      return false;
    }
    if (to != null && local.isAfter(DateTime(to.year, to.month, to.day))) {
      return false;
    }
    return true;
  }

  List<String> _availableSources(List<DealWorkflow> deals) {
    final values =
        deals
            .map((item) => item.source)
            .where((item) => item.trim().isNotEmpty)
            .toSet()
            .toList()
          ..sort();
    return values;
  }

  Future<List<FinancialDeal>> _getFinancialDeals() async {
    try {
      final response = await _dio.get<List<dynamic>>(
        'api/deals',
        queryParameters: {'limit': 500},
      );

      return [
        for (final item in response.data ?? const [])
          if (item is Map<String, dynamic>) _financialDealFromJson(item),
      ];
    } on DioException catch (error) {
      throw ApiException.fromDio(error);
    }
  }

  FinancialDeal _financialDealFromJson(Map<String, dynamic> json) {
    return FinancialDeal(
      id: _read(json, 'id') ?? '',
      propertyId: _read(json, 'propertyId') ?? '',
      status: _read(json, 'status') ?? '',
      commissionAmount: _readDouble(json, 'commissionAmount'),
      realtorPayoutAmount: _readDouble(json, 'realtorPayoutAmount'),
      agencyNetCommissionAmount: _readDouble(json, 'agencyNetCommissionAmount'),
      completedAt: _readDate(json, 'completedAt'),
      createdDate: _readDate(json, 'createdDate') ?? DateTime.now().toUtc(),
    );
  }

  String? _read(Map<String, dynamic> json, String key) {
    final pascal = key[0].toUpperCase() + key.substring(1);
    return (json[key] ?? json[pascal])?.toString();
  }

  double _readDouble(Map<String, dynamic> json, String key) {
    final pascal = key[0].toUpperCase() + key.substring(1);
    final value = json[key] ?? json[pascal];
    if (value is num) {
      return value.toDouble();
    }
    return double.tryParse(value?.toString() ?? '') ?? 0;
  }

  DateTime? _readDate(Map<String, dynamic> json, String key) {
    final value = _read(json, key);
    if (value == null || value.isEmpty) {
      return null;
    }
    return DateTime.tryParse(value);
  }
}
