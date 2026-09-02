import 'package:dio/dio.dart';

class ApiException implements Exception {
  const ApiException(this.message, {this.statusCode});

  final String message;
  final int? statusCode;

  bool get isNotFound => statusCode == 404;

  @override
  String toString() => message;

  factory ApiException.fromDio(DioException error) {
    final response = error.response;
    if (response != null) {
      final detail = _extractDetail(response.data);
      if (detail != null && detail.isNotEmpty) {
        return ApiException(detail, statusCode: response.statusCode);
      }

      return ApiException(
        _messageForStatus(response.statusCode),
        statusCode: response.statusCode,
      );
    }

    return switch (error.type) {
      DioExceptionType.connectionTimeout ||
      DioExceptionType.receiveTimeout ||
      DioExceptionType.sendTimeout => const ApiException(
        'Сервер отвечает слишком долго. Попробуйте еще раз.',
      ),
      DioExceptionType.connectionError => const ApiException(
        'Не удалось связаться с сервером. Проверьте подключение и попробуйте еще раз.',
      ),
      DioExceptionType.badCertificate => const ApiException(
        'Не удалось установить защищенное соединение с сервером.',
      ),
      DioExceptionType.cancel => const ApiException('Запрос был отменен.'),
      _ => const ApiException(
        'Не удалось выполнить запрос. Попробуйте еще раз.',
      ),
    };
  }

  static String _messageForStatus(int? statusCode) {
    final status = statusCode ?? 0;
    return switch (status) {
      400 => 'Проверьте заполнение формы.',
      401 => 'Неверный email или пароль, либо сессия истекла.',
      403 => 'У этой роли нет доступа к действию.',
      404 => 'Запрошенные данные не найдены.',
      423 => 'Аккаунт заморожен. Обратитесь к администратору.',
      503 => 'Сервер временно недоступен. Попробуйте позже.',
      504 => 'Сервер отвечает слишком долго. Попробуйте еще раз.',
      >= 500 => 'Ошибка сервера. Попробуйте позже.',
      _ => 'Не удалось выполнить запрос. Попробуйте еще раз.',
    };
  }

  static String? _extractDetail(Object? data) {
    if (data is Map) {
      final detail = (data['detail'] ?? data['Detail'])?.toString();
      if (detail != null && detail.isNotEmpty) {
        return detail;
      }

      final errors = data['errors'] ?? data['Errors'];
      if (errors is Map && errors.isNotEmpty) {
        final messages = <String>[];
        for (final value in errors.values) {
          if (value is Iterable) {
            messages.addAll(value.map((item) => item.toString()));
          } else if (value != null) {
            messages.add(value.toString());
          }
        }

        if (messages.isNotEmpty) {
          return messages.join('\n');
        }
      }

      final title = (data['title'] ?? data['Title'])?.toString();
      if (title != null && title.isNotEmpty) {
        if (title.toLowerCase().contains('validation errors')) {
          return 'Проверьте заполнение формы.';
        }
        return title;
      }
    }

    if (data is String && data.isNotEmpty) {
      return data;
    }

    return null;
  }
}
