import '../../../properties/domain/models/property_models.dart';

class MatchingSearchRequest {
  const MatchingSearchRequest({
    required this.desiredTypes,
    required this.ignoreArea,
    required this.latitude,
    required this.longitude,
    required this.searchRadiusKm,
    required this.minMatchPercent,
    required this.limit,
    required this.minPrice,
    required this.maxPrice,
    required this.minArea,
    this.maxArea,
    this.addressQuery,
    this.criteria = const [],
    this.sort = MatchingSort.score,
  });

  final List<String> desiredTypes;
  final bool ignoreArea;
  final double latitude;
  final double longitude;
  final double searchRadiusKm;
  final double minPrice;
  final double maxPrice;
  final double minArea;
  final double? maxArea;
  final String? addressQuery;
  final double minMatchPercent;
  final int limit;
  final List<MatchingCriterionValue> criteria;
  final MatchingSort sort;

  Map<String, dynamic> toJson() {
    final normalizedTypes = desiredTypes.isEmpty ? propertyTypes : desiredTypes;

    return {
      'desiredType': normalizedTypes.first,
      'desiredTypes': normalizedTypes,
      'latitude': latitude,
      'longitude': longitude,
      'searchRadiusMeters': searchRadiusKm * 1000,
      'ignoreArea': ignoreArea,
      'minPrice': minPrice,
      'maxPrice': maxPrice,
      'minArea': minArea,
      'maxArea': maxArea,
      if (addressQuery != null && addressQuery!.trim().isNotEmpty)
        'addressQuery': addressQuery!.trim(),
      'minMatchPercentage': minMatchPercent / 100,
      'criteria': criteria.map((item) => item.toJson()).toList(),
      'limit': limit,
    };
  }
}

enum MatchingSort {
  score('Лучшее совпадение'),
  distance('Ближе к точке'),
  price('Сначала дешевле');

  const MatchingSort(this.label);

  final String label;
}

enum MatchingCriterionPriority {
  mustHave('MustHave', 'Обязательное'),
  important('Important', 'Важное'),
  niceToHave('NiceToHave', 'Желательное');

  const MatchingCriterionPriority(this.apiValue, this.label);

  final String apiValue;
  final String label;
}

class MatchingCurrency {
  const MatchingCurrency({
    required this.code,
    required this.name,
    required this.symbol,
    required this.rateToBase,
  });

  final String code;
  final String name;
  final String symbol;
  final double rateToBase;

  String get label => '$code · $name';
}

class MatchingCriterionDefinition {
  const MatchingCriterionDefinition({
    required this.id,
    required this.code,
    required this.displayName,
    required this.valueType,
    required this.options,
    this.category,
    this.description,
  });

  final String id;
  final String code;
  final String displayName;
  final String valueType;
  final String? category;
  final String? description;
  final List<MatchingCriterionOption> options;

  bool get isBoolean => valueType.toLowerCase() == 'boolean';
  bool get isNumber => valueType.toLowerCase() == 'number';
  bool get isSingleSelect => valueType.toLowerCase() == 'singleselect';
  bool get isMultiSelect => valueType.toLowerCase() == 'multiselect';
}

class MatchingCriterionOption {
  const MatchingCriterionOption({required this.value, required this.label});

  final String value;
  final String label;
}

class MatchingCriterionValue {
  const MatchingCriterionValue({
    required this.definition,
    required this.priority,
    this.value,
    this.values = const [],
  });

  final MatchingCriterionDefinition definition;
  final MatchingCriterionPriority priority;
  final String? value;
  final List<String> values;

  MatchingCriterionValue copyWith({
    MatchingCriterionPriority? priority,
    String? value,
    List<String>? values,
  }) {
    return MatchingCriterionValue(
      definition: definition,
      priority: priority ?? this.priority,
      value: value ?? this.value,
      values: values ?? this.values,
    );
  }

  bool get hasValue {
    if (definition.isMultiSelect) {
      return values.isNotEmpty;
    }
    return value != null && value!.trim().isNotEmpty;
  }

  Map<String, dynamic> toJson() {
    return {
      'criterionDefinitionId': definition.id,
      'priority': priority.apiValue,
      'value': definition.isMultiSelect ? null : value?.trim(),
      'values': definition.isMultiSelect ? values : null,
    };
  }
}

class PropertyMatch {
  const PropertyMatch({
    required this.propertyId,
    required this.title,
    required this.address,
    required this.price,
    required this.originalPriceAmount,
    required this.originalPriceCurrency,
    required this.area,
    required this.roomsCount,
    required this.latitude,
    required this.longitude,
    required this.distanceMeters,
    required this.matchScore,
    required this.baseScore,
    required this.criteriaScore,
    required this.passedMustHave,
  });

  final String propertyId;
  final String title;
  final String address;
  final double price;
  final double originalPriceAmount;
  final String originalPriceCurrency;
  final double area;
  final int roomsCount;
  final double latitude;
  final double longitude;
  final double? distanceMeters;
  final double matchScore;
  final double baseScore;
  final double criteriaScore;
  final bool passedMustHave;

  String get priceText {
    final amount = originalPriceAmount > 0 ? originalPriceAmount : price;
    return formatPropertyPrice(amount, originalPriceCurrency);
  }

  String get matchPercentText => '${(matchScore * 100).round()}%';

  String get distanceText {
    final value = distanceMeters;
    if (value == null || value <= 0) {
      return 'Без учета расстояния';
    }

    return '${(value / 1000).toStringAsFixed(1)} км';
  }
}
