import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:real_estate_mobile/core/errors/api_exception.dart';

void main() {
  group('Ошибки API', () {
    test('берет текст из problem details, если API его вернул', () {
      final error = DioException(
        requestOptions: RequestOptions(path: 'api/auth/login'),
        response: Response(
          requestOptions: RequestOptions(path: 'api/auth/login'),
          statusCode: 400,
          data: {'detail': 'Неверные учетные данные.'},
        ),
      );

      expect(ApiException.fromDio(error).message, 'Неверные учетные данные.');
    });

    test('показывает понятный текст для запрета доступа', () {
      final error = DioException(
        requestOptions: RequestOptions(path: 'api/deals/all'),
        response: Response(
          requestOptions: RequestOptions(path: 'api/deals/all'),
          statusCode: 403,
        ),
      );

      expect(
        ApiException.fromDio(error).message,
        'У этой роли нет доступа к действию.',
      );
    });

    test('показывает ошибки валидации раньше общего заголовка', () {
      final error = DioException(
        requestOptions: RequestOptions(path: 'api/property-matching/preview'),
        response: Response(
          requestOptions: RequestOptions(path: 'api/property-matching/preview'),
          statusCode: 400,
          data: {
            'title': 'One or more validation errors occurred.',
            'errors': {
              'MaxPrice': ['Проверьте диапазон цены.'],
            },
          },
        ),
      );

      expect(ApiException.fromDio(error).message, 'Проверьте диапазон цены.');
    });

    test('переводит общий заголовок ошибки валидации', () {
      final error = DioException(
        requestOptions: RequestOptions(path: 'api/property-matching/preview'),
        response: Response(
          requestOptions: RequestOptions(path: 'api/property-matching/preview'),
          statusCode: 400,
          data: {'title': 'One or more validation errors occurred.'},
        ),
      );

      expect(
        ApiException.fromDio(error).message,
        'Проверьте заполнение формы.',
      );
    });

    test('сохраняет код статуса для обработки отсутствующих данных', () {
      final error = DioException(
        requestOptions: RequestOptions(path: 'api/profile/me'),
        response: Response(
          requestOptions: RequestOptions(path: 'api/profile/me'),
          statusCode: 404,
        ),
      );

      final exception = ApiException.fromDio(error);

      expect(exception.statusCode, 404);
      expect(exception.isNotFound, isTrue);
    });
  });
}
