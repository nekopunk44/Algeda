import 'package:flutter_test/flutter_test.dart';
import 'package:real_estate_mobile/features/properties/domain/models/property_models.dart';

void main() {
  test('формат цены объекта использует пробелы в разрядах и код валюты', () {
    expect(formatPropertyPrice(36000, 'usd'), '36 000 USD');
    expect(formatPropertyPrice(1250000, 'EUR'), '1 250 000 EUR');
  });
}
