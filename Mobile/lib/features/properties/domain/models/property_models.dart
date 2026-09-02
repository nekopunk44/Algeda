class PropertyItem {
  const PropertyItem({
    required this.id,
    required this.title,
    required this.address,
    required this.price,
    required this.originalPriceAmount,
    required this.originalPriceCurrency,
    required this.area,
    required this.roomsCount,
    required this.latitude,
    required this.longitude,
    required this.type,
    required this.status,
    required this.photoPaths,
    required this.createdDate,
    this.criteriaCount = 0,
    this.soldAtUtc,
    this.ownerFullName,
    this.ownerEmail,
    this.ownerPhoneNumber,
    this.responsibleRealtorId,
    this.mainPhotoPath,
  });

  final String id;
  final String title;
  final String address;
  final double price;
  final double originalPriceAmount;
  final String originalPriceCurrency;
  final double area;
  final int roomsCount;
  final double latitude;
  final double longitude;
  final String type;
  final String status;
  final DateTime? soldAtUtc;
  final String? ownerFullName;
  final String? ownerEmail;
  final String? ownerPhoneNumber;
  final String? responsibleRealtorId;
  final String? mainPhotoPath;
  final List<String> photoPaths;
  final int criteriaCount;
  final DateTime createdDate;

  PropertyFormData toFormData() {
    return PropertyFormData(
      title: title,
      address: address,
      price: originalPriceAmount,
      priceCurrency: originalPriceCurrency,
      area: area,
      roomsCount: roomsCount,
      latitude: latitude,
      longitude: longitude,
      type: type,
      photoPaths: photoPaths,
      ownerFullName: ownerFullName ?? '',
      ownerEmail: ownerEmail ?? '',
      ownerPhoneNumber: ownerPhoneNumber ?? '',
      responsibleRealtorId: responsibleRealtorId,
    );
  }
}

class PropertyFormData {
  const PropertyFormData({
    required this.title,
    required this.address,
    required this.price,
    required this.priceCurrency,
    required this.area,
    required this.roomsCount,
    required this.latitude,
    required this.longitude,
    required this.type,
    required this.photoPaths,
    required this.ownerFullName,
    required this.ownerEmail,
    required this.ownerPhoneNumber,
    this.responsibleRealtorId,
  });

  factory PropertyFormData.empty() {
    return const PropertyFormData(
      title: '',
      address: '',
      price: 100000,
      priceCurrency: 'USD',
      area: 60,
      roomsCount: 2,
      latitude: 47.0105,
      longitude: 28.8638,
      type: 'Apartment',
      photoPaths: [],
      ownerFullName: '',
      ownerEmail: '',
      ownerPhoneNumber: '',
    );
  }

  final String title;
  final String address;
  final double price;
  final String priceCurrency;
  final double area;
  final int roomsCount;
  final double latitude;
  final double longitude;
  final String type;
  final List<String> photoPaths;
  final String ownerFullName;
  final String ownerEmail;
  final String ownerPhoneNumber;
  final String? responsibleRealtorId;

  Map<String, dynamic> toJson({String? propertyId}) {
    return {
      ...(propertyId == null
          ? const <String, dynamic>{}
          : {'propertyId': propertyId}),
      'title': title.trim(),
      'address': address.trim(),
      'price': price,
      'priceCurrency': priceCurrency.trim().toUpperCase(),
      'area': area,
      'roomsCount': roomsCount,
      'latitude': latitude,
      'longitude': longitude,
      'type': type,
      'photoPaths': photoPaths,
      'ownerFullName': ownerFullName.trim(),
      'ownerEmail': ownerEmail.trim(),
      'ownerPhoneNumber': ownerPhoneNumber.trim(),
      if (responsibleRealtorId != null && responsibleRealtorId!.isNotEmpty)
        'responsibleRealtorId': responsibleRealtorId,
    };
  }
}

class PropertyCriterionDefinition {
  const PropertyCriterionDefinition({
    required this.id,
    required this.code,
    required this.displayName,
    required this.valueType,
    required this.isHidden,
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
  final bool isHidden;
  final List<PropertyCriterionOption> options;

  bool get isBoolean => valueType.toLowerCase() == 'boolean';
  bool get isNumber => valueType.toLowerCase() == 'number';
  bool get isText => valueType.toLowerCase() == 'text';
  bool get isSingleSelect => valueType.toLowerCase() == 'singleselect';
  bool get isMultiSelect => valueType.toLowerCase() == 'multiselect';
}

class PropertyCriterionOption {
  const PropertyCriterionOption({
    required this.value,
    required this.label,
    required this.sortOrder,
  });

  final String value;
  final String label;
  final int sortOrder;
}

class PropertyCriterionValue {
  const PropertyCriterionValue({
    required this.id,
    required this.criterionDefinitionId,
    required this.criterionCode,
    required this.criterionDisplayName,
    required this.criterionValueType,
    required this.isCriterionHidden,
    required this.value,
    required this.displayValue,
    required this.createdDate,
    this.category,
    this.criterionDescription,
  });

  final String id;
  final String criterionDefinitionId;
  final String criterionCode;
  final String criterionDisplayName;
  final String criterionValueType;
  final String? category;
  final String? criterionDescription;
  final bool isCriterionHidden;
  final String value;
  final String displayValue;
  final DateTime createdDate;

  String get effectiveValue {
    final display = displayValue.trim();
    return display.isEmpty
        ? propertyCriterionValueLabel(value, criterionValueType)
        : display;
  }
}

class PropertyCriterionDraft {
  const PropertyCriterionDraft({
    required this.definition,
    this.value,
    this.values = const [],
    this.enabled = false,
  });

  final PropertyCriterionDefinition definition;
  final String? value;
  final List<String> values;
  final bool enabled;

  PropertyCriterionDraft copyWith({
    String? value,
    List<String>? values,
    bool? enabled,
  }) {
    return PropertyCriterionDraft(
      definition: definition,
      value: value ?? this.value,
      values: values ?? this.values,
      enabled: enabled ?? this.enabled,
    );
  }

  bool get hasValue {
    if (!enabled) {
      return false;
    }
    if (definition.isMultiSelect) {
      return values.isNotEmpty;
    }
    return value != null && value!.trim().isNotEmpty;
  }

  Map<String, dynamic> toJson() {
    return {
      'criterionDefinitionId': definition.id,
      'value': definition.isMultiSelect ? null : value?.trim(),
      'values': definition.isMultiSelect ? values : null,
    };
  }
}

const propertyTypes = ['Apartment', 'House', 'Commercial', 'Land'];
const propertyStatuses = ['Available', 'Reserved', 'Sold', 'Hidden'];

String propertyTypeLabel(String value) {
  return switch (value) {
    'Apartment' => 'Квартира',
    'House' => 'Дом',
    'Commercial' => 'Коммерция',
    'Land' => 'Участок',
    _ => 'Не указан',
  };
}

String propertyStatusLabel(String value) {
  return switch (value) {
    'Available' => 'Доступен',
    'Reserved' => 'Зарезервирован',
    'Sold' => 'Продан',
    'Hidden' => 'Скрыт',
    _ => 'Не указан',
  };
}

String propertyCriterionValueTypeLabel(String value) {
  return switch (value) {
    'Boolean' => 'Логический',
    'Number' => 'Число',
    'Text' => 'Текст',
    'SingleSelect' => 'Одиночный выбор',
    'MultiSelect' => 'Множественный выбор',
    _ => value,
  };
}

String propertyCriterionValueLabel(String value, String valueType) {
  if (valueType.toLowerCase() == 'boolean') {
    return value.toLowerCase() == 'true' ? 'Да' : 'Нет';
  }
  return value;
}

String formatPropertyPrice(num amount, String currency) {
  final rounded = amount.round().toString();
  final buffer = StringBuffer();
  for (var i = 0; i < rounded.length; i++) {
    final left = rounded.length - i;
    buffer.write(rounded[i]);
    if (left > 1 && left % 3 == 1) {
      buffer.write(' ');
    }
  }

  return '${buffer.toString()} ${currency.toUpperCase()}';
}
