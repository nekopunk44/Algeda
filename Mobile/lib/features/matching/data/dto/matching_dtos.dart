import '../../domain/models/matching_models.dart';

class PropertyMatchDto {
  const PropertyMatchDto({
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

  factory PropertyMatchDto.fromJson(Map<String, dynamic> json) {
    final breakdown = _readMap(json, 'breakdown');

    return PropertyMatchDto(
      propertyId: _read(json, 'propertyId') ?? '',
      title: _read(json, 'title') ?? '',
      address: _read(json, 'address') ?? '',
      price: _readDouble(json, 'price'),
      originalPriceAmount: _readDouble(json, 'originalPriceAmount'),
      originalPriceCurrency: _read(json, 'originalPriceCurrency') ?? 'USD',
      area: _readDouble(json, 'area'),
      roomsCount: _readInt(json, 'roomsCount'),
      latitude: _readDouble(json, 'latitude'),
      longitude: _readDouble(json, 'longitude'),
      distanceMeters: _readNullableDouble(json, 'distanceMeters'),
      matchScore: _normalizeScore(_readDouble(json, 'matchScore')),
      baseScore: _normalizeScore(_readDouble(breakdown, 'baseScore')),
      criteriaScore: _normalizeScore(_readDouble(breakdown, 'criteriaScore')),
      passedMustHave: _readBool(breakdown, 'passedMustHave'),
    );
  }

  PropertyMatch toModel() {
    return PropertyMatch(
      propertyId: propertyId,
      title: title,
      address: address,
      price: price,
      originalPriceAmount: originalPriceAmount,
      originalPriceCurrency: originalPriceCurrency,
      area: area,
      roomsCount: roomsCount,
      latitude: latitude,
      longitude: longitude,
      distanceMeters: distanceMeters,
      matchScore: matchScore,
      baseScore: baseScore,
      criteriaScore: criteriaScore,
      passedMustHave: passedMustHave,
    );
  }
}

Map<String, dynamic> _readMap(Map<String, dynamic> json, String key) {
  final value = _readAny(json, key);
  if (value is Map) {
    return Map<String, dynamic>.from(value);
  }

  return const {};
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

double? _readNullableDouble(Map<String, dynamic> json, String key) {
  final raw = _readAny(json, key);
  if (raw == null) {
    return null;
  }
  if (raw is num) {
    return raw.toDouble();
  }

  return double.tryParse(raw.toString());
}

bool _readBool(Map<String, dynamic> json, String key) {
  final raw = _readAny(json, key);
  if (raw is bool) {
    return raw;
  }

  return raw?.toString().toLowerCase() == 'true';
}

double _normalizeScore(double value) {
  if (value > 1) {
    return value / 100;
  }

  return value;
}
