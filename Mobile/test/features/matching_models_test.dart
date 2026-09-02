import 'package:flutter_test/flutter_test.dart';
import 'package:real_estate_mobile/features/matching/domain/models/matching_models.dart';

void main() {
  test('запрос подбора формирует preview payload без подписок', () {
    final request = MatchingSearchRequest(
      desiredTypes: const ['Apartment'],
      ignoreArea: false,
      latitude: 47.0105,
      longitude: 28.8638,
      searchRadiusKm: 7.5,
      minPrice: 30000,
      maxPrice: 90000,
      minArea: 30,
      minMatchPercent: 40,
      limit: 20,
    );

    final json = request.toJson();

    expect(json['desiredType'], 'Apartment');
    expect(json['desiredTypes'], ['Apartment']);
    expect(json['searchRadiusMeters'], 7500);
    expect(json['minMatchPercentage'], 0.4);
    expect(json.containsKey('requirementId'), isFalse);
  });

  test('запрос подбора сохраняет координаты при отключенном учете площади', () {
    const request = MatchingSearchRequest(
      desiredTypes: ['Apartment'],
      ignoreArea: true,
      latitude: 47.0105,
      longitude: 28.8638,
      searchRadiusKm: 5,
      minPrice: 0,
      maxPrice: 200000,
      minArea: 30,
      minMatchPercent: 70,
      limit: 20,
    );

    final json = request.toJson();

    expect(json['ignoreArea'], isTrue);
    expect(json['latitude'], 47.0105);
    expect(json['longitude'], 28.8638);
    expect(json['searchRadiusMeters'], 5000);
  });
}
