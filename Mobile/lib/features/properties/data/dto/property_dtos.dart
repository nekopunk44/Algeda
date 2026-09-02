import '../../domain/models/property_models.dart';

class PropertyDto {
  const PropertyDto({
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
    required this.criteriaCount,
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

  factory PropertyDto.fromJson(Map<String, dynamic> json) {
    final photoPaths = [
      for (final item in (_readAny(json, 'photoPaths') as List? ?? const []))
        if (item != null) item.toString(),
    ];
    final criteria = _readAny(json, 'criteria') as List? ?? const [];

    return PropertyDto(
      id: _read(json, 'id') ?? '',
      title: _read(json, 'title') ?? '',
      address: _read(json, 'address') ?? '',
      price: _readDouble(json, 'price'),
      originalPriceAmount: _readDouble(json, 'originalPriceAmount'),
      originalPriceCurrency: _read(json, 'originalPriceCurrency') ?? 'USD',
      area: _readDouble(json, 'area'),
      roomsCount: _readInt(json, 'roomsCount'),
      latitude: _readDouble(json, 'latitude'),
      longitude: _readDouble(json, 'longitude'),
      type: _read(json, 'type') ?? 'Undefined',
      status: _read(json, 'status') ?? 'Undefined',
      soldAtUtc: _readDate(json, 'soldAtUtc'),
      ownerFullName: _read(json, 'ownerFullName'),
      ownerEmail: _read(json, 'ownerEmail'),
      ownerPhoneNumber: _read(json, 'ownerPhoneNumber'),
      responsibleRealtorId: _read(json, 'responsibleRealtorId'),
      mainPhotoPath:
          _read(json, 'mainPhotoPath') ??
          (photoPaths.isEmpty ? null : photoPaths.first),
      photoPaths: photoPaths,
      criteriaCount: criteria.length,
      createdDate: _readDate(json, 'createdDate') ?? DateTime.now().toUtc(),
    );
  }

  PropertyItem toModel() {
    return PropertyItem(
      id: id,
      title: title,
      address: address,
      price: price,
      originalPriceAmount: originalPriceAmount,
      originalPriceCurrency: originalPriceCurrency,
      area: area,
      roomsCount: roomsCount,
      latitude: latitude,
      longitude: longitude,
      type: type,
      status: status,
      soldAtUtc: soldAtUtc,
      ownerFullName: ownerFullName,
      ownerEmail: ownerEmail,
      ownerPhoneNumber: ownerPhoneNumber,
      responsibleRealtorId: responsibleRealtorId,
      mainPhotoPath: mainPhotoPath,
      photoPaths: photoPaths,
      criteriaCount: criteriaCount,
      createdDate: createdDate,
    );
  }
}

class PropertyPhotoUploadDto {
  const PropertyPhotoUploadDto({required this.path});

  final String path;

  factory PropertyPhotoUploadDto.fromJson(Map<String, dynamic> json) {
    return PropertyPhotoUploadDto(path: _read(json, 'path') ?? '');
  }
}

class PropertyCriterionDefinitionDto {
  const PropertyCriterionDefinitionDto({
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
  final List<PropertyCriterionOptionDto> options;

  factory PropertyCriterionDefinitionDto.fromJson(Map<String, dynamic> json) {
    final options =
        [
          for (final item in (_readAny(json, 'options') as List? ?? const []))
            if (item is Map)
              PropertyCriterionOptionDto.fromJson(
                Map<String, dynamic>.from(item),
              ),
        ]..sort((a, b) {
          final sort = a.sortOrder.compareTo(b.sortOrder);
          return sort == 0 ? a.value.compareTo(b.value) : sort;
        });

    return PropertyCriterionDefinitionDto(
      id: _read(json, 'id') ?? '',
      code: _read(json, 'code') ?? '',
      displayName: _read(json, 'displayName') ?? '',
      valueType: _read(json, 'valueType') ?? 'Text',
      category: _read(json, 'category'),
      description: _read(json, 'description'),
      isHidden: _readBool(json, 'isHidden'),
      options: options,
    );
  }

  PropertyCriterionDefinition toModel() {
    return PropertyCriterionDefinition(
      id: id,
      code: code,
      displayName: displayName,
      valueType: valueType,
      category: category,
      description: description,
      isHidden: isHidden,
      options: options.map((item) => item.toModel()).toList(),
    );
  }
}

class PropertyCriterionOptionDto {
  const PropertyCriterionOptionDto({
    required this.value,
    required this.label,
    required this.sortOrder,
  });

  final String value;
  final String label;
  final int sortOrder;

  factory PropertyCriterionOptionDto.fromJson(Map<String, dynamic> json) {
    return PropertyCriterionOptionDto(
      value: _read(json, 'value') ?? '',
      label: _read(json, 'label') ?? '',
      sortOrder: _readInt(json, 'sortOrder'),
    );
  }

  PropertyCriterionOption toModel() {
    return PropertyCriterionOption(
      value: value,
      label: label,
      sortOrder: sortOrder,
    );
  }
}

class PropertyCriterionValueDto {
  const PropertyCriterionValueDto({
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

  factory PropertyCriterionValueDto.fromJson(Map<String, dynamic> json) {
    return PropertyCriterionValueDto(
      id: _read(json, 'id') ?? '',
      criterionDefinitionId: _read(json, 'criterionDefinitionId') ?? '',
      criterionCode: _read(json, 'criterionCode') ?? '',
      criterionDisplayName: _read(json, 'criterionDisplayName') ?? '',
      criterionValueType: _read(json, 'criterionValueType') ?? 'Text',
      category: _read(json, 'category'),
      criterionDescription: _read(json, 'criterionDescription'),
      isCriterionHidden: _readBool(json, 'isCriterionHidden'),
      value: _read(json, 'value') ?? '',
      displayValue: _read(json, 'displayValue') ?? '',
      createdDate: _readDate(json, 'createdDate') ?? DateTime.now().toUtc(),
    );
  }

  PropertyCriterionValue toModel() {
    return PropertyCriterionValue(
      id: id,
      criterionDefinitionId: criterionDefinitionId,
      criterionCode: criterionCode,
      criterionDisplayName: criterionDisplayName,
      criterionValueType: criterionValueType,
      category: category,
      criterionDescription: criterionDescription,
      isCriterionHidden: isCriterionHidden,
      value: value,
      displayValue: displayValue,
      createdDate: createdDate,
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

int _readInt(Map<String, dynamic> json, String key) {
  return int.tryParse(_read(json, key) ?? '') ?? 0;
}

double _readDouble(Map<String, dynamic> json, String key) {
  final raw = _readAny(json, key);
  if (raw is num) {
    return raw.toDouble();
  }

  return double.tryParse(raw?.toString() ?? '') ?? 0;
}

bool _readBool(Map<String, dynamic> json, String key) {
  final raw = _readAny(json, key);
  return raw == true || raw?.toString().toLowerCase() == 'true';
}

DateTime? _readDate(Map<String, dynamic> json, String key) {
  final value = _read(json, key);
  if (value == null || value.isEmpty) {
    return null;
  }

  return DateTime.tryParse(value);
}
