import 'dart:io';

import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../config/api_config.dart';
import '../storage/secure_token_storage.dart';

final dioProvider = Provider<Dio>((ref) {
  final apiSettings = ref.watch(apiSettingsControllerProvider);
  final dio = Dio(
    BaseOptions(
      baseUrl: '${apiSettings.baseUrl}/',
      connectTimeout: const Duration(seconds: 30),
      receiveTimeout: const Duration(seconds: 30),
      sendTimeout: const Duration(seconds: 30),
      headers: {
        'Accept': 'application/json',
        'X-Client-Device': _clientDeviceName(),
      },
    ),
  );

  dio.interceptors.add(
    QueuedInterceptorsWrapper(
      onRequest: (options, handler) async {
        final token = await ref
            .read(secureTokenStorageProvider)
            .readAccessToken();
        if (token != null && token.isNotEmpty) {
          options.headers['Authorization'] = 'Bearer $token';
        }

        handler.next(options);
      },
    ),
  );

  return dio;
});

String _clientDeviceName() {
  if (Platform.isAndroid) {
    return 'mobile;android';
  }

  if (Platform.isIOS) {
    return 'mobile;ios';
  }

  return 'mobile;${Platform.operatingSystem}';
}
