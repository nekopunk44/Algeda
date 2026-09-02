import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:real_estate_mobile/features/auth/data/repository/auth_repository.dart';

void main() {
  test('репозиторий авторизации отправляет вход и читает роли', () async {
    late RequestOptions captured;
    final dio = Dio(BaseOptions(baseUrl: 'http://localhost/'))
      ..httpClientAdapter = _JsonAdapter((options) {
        captured = options;
        return {
          'accessToken': 'jwt-token',
          'expiresAtUtc': '2026-05-17T12:00:00Z',
          'tokenType': 'Bearer',
          'roles': ['Realtor'],
        };
      });

    final response = await AuthRepository(
      dio,
    ).login(email: ' realtor@realestate.local ', password: 'secret');

    expect(captured.path, 'api/auth/login');
    expect(captured.method, 'POST');
    expect(captured.data['email'], 'realtor@realestate.local');
    expect(response.accessToken, 'jwt-token');
    expect(response.roles, ['Realtor']);
  });

  test(
    'репозиторий авторизации проверяет восстановленную сессию через API',
    () async {
      late RequestOptions captured;
      final dio = Dio(BaseOptions(baseUrl: 'http://localhost/'))
        ..httpClientAdapter = _JsonAdapter((options) {
          captured = options;
          return {'isFrozen': false};
        });

      await AuthRepository(dio).checkSessionStatus();

      expect(captured.path, 'api/auth/session-status');
      expect(captured.method, 'GET');
    },
  );
}

typedef _JsonHandler = Object? Function(RequestOptions options);

class _JsonAdapter implements HttpClientAdapter {
  const _JsonAdapter(this.handler);

  final _JsonHandler handler;

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    final data = handler(options);
    return ResponseBody.fromString(
      jsonEncode(data),
      200,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}
