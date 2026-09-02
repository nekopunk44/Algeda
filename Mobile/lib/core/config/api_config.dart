import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../storage/secure_token_storage.dart';

final apiSettingsControllerProvider =
    NotifierProvider<ApiSettingsController, ApiSettings>(
      ApiSettingsController.new,
    );

class ApiConfig {
  const ApiConfig._();

  static const defaultBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'https://api.algeda.invalid',
  );

  static const defaultWebBaseUrl = String.fromEnvironment(
    'WEB_BASE_URL',
    defaultValue: defaultBaseUrl,
  );

  static const _legacyDefaultBaseUrls = {
    'http://192.168.1.2:5142',
    'http://10.0.2.2:5142',
  };

  static const chatHubPath = '/hubs/chat';

  static String normalizeBaseUrl(String value) {
    var result = value.trim();
    if (result.isEmpty) {
      return defaultBaseUrl;
    }

    if (!result.startsWith('http://') && !result.startsWith('https://')) {
      result = 'http://$result';
    }

    while (result.endsWith('/')) {
      result = result.substring(0, result.length - 1);
    }

    return result;
  }

  static bool isLegacyDefaultBaseUrl(String value) {
    return _legacyDefaultBaseUrls.contains(normalizeBaseUrl(value));
  }
}

class ApiSettings {
  const ApiSettings({required this.baseUrl, required this.webBaseUrl});

  final String baseUrl;
  final String webBaseUrl;

  Uri get chatHubUri => Uri.parse('$baseUrl${ApiConfig.chatHubPath}');

  Uri analyticsPdfUri(Map<String, String> queryParameters) {
    return Uri.parse(
      '$webBaseUrl/Analytics/ExportPdf',
    ).replace(queryParameters: queryParameters);
  }

  String? resolveFileUrl(String? path) {
    if (path == null || path.trim().isEmpty) {
      return null;
    }

    final value = path.trim();
    final uri = Uri.tryParse(value);
    if (uri != null && uri.hasScheme) {
      return value;
    }

    final base = Uri.parse(baseUrl);
    if (value.startsWith('/uploads/')) {
      return Uri.parse(
        '${base.origin}/api/property-photos/by-path',
      ).replace(queryParameters: {'path': value}).toString();
    }

    if (value.startsWith('/')) {
      return '${base.origin}$value';
    }

    return '${base.origin}/$value';
  }
}

class ApiSettingsController extends Notifier<ApiSettings> {
  @override
  ApiSettings build() {
    Future<void>.microtask(_restore);
    return const ApiSettings(
      baseUrl: ApiConfig.defaultBaseUrl,
      webBaseUrl: ApiConfig.defaultWebBaseUrl,
    );
  }

  Future<void> save({required String baseUrl, String? webBaseUrl}) async {
    final normalizedApi = ApiConfig.normalizeBaseUrl(baseUrl);
    final normalizedWeb = ApiConfig.normalizeBaseUrl(
      webBaseUrl == null || webBaseUrl.trim().isEmpty
          ? normalizedApi
          : webBaseUrl,
    );

    await ref
        .read(secureTokenStorageProvider)
        .saveBaseUrls(apiBaseUrl: normalizedApi, webBaseUrl: normalizedWeb);
    state = ApiSettings(baseUrl: normalizedApi, webBaseUrl: normalizedWeb);
  }

  Future<void> _restore() async {
    final storage = ref.read(secureTokenStorageProvider);
    final apiBaseUrl = await storage.readApiBaseUrl();
    final webBaseUrl = await storage.readWebBaseUrl();
    if (apiBaseUrl == null || apiBaseUrl.trim().isEmpty) {
      return;
    }

    if (ApiConfig.isLegacyDefaultBaseUrl(apiBaseUrl)) {
      await storage.saveBaseUrls(
        apiBaseUrl: ApiConfig.defaultBaseUrl,
        webBaseUrl: ApiConfig.defaultWebBaseUrl,
      );
      state = const ApiSettings(
        baseUrl: ApiConfig.defaultBaseUrl,
        webBaseUrl: ApiConfig.defaultWebBaseUrl,
      );
      return;
    }

    state = ApiSettings(
      baseUrl: ApiConfig.normalizeBaseUrl(apiBaseUrl),
      webBaseUrl: ApiConfig.normalizeBaseUrl(
        webBaseUrl == null || webBaseUrl.trim().isEmpty
            ? apiBaseUrl
            : webBaseUrl,
      ),
    );
  }
}
