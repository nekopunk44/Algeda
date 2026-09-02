import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

final secureTokenStorageProvider = Provider<SecureTokenStorage>((ref) {
  return SecureTokenStorage(const FlutterSecureStorage());
});

class SecureTokenStorage {
  const SecureTokenStorage(this._storage);

  static const _accessTokenKey = 'access_token';
  static const _apiBaseUrlKey = 'api_base_url';
  static const _webBaseUrlKey = 'web_base_url';

  final FlutterSecureStorage _storage;

  Future<String?> readAccessToken() {
    return _storage.read(key: _accessTokenKey);
  }

  Future<void> saveAccessToken(String token) {
    return _storage.write(key: _accessTokenKey, value: token);
  }

  Future<void> clear() {
    return _storage.delete(key: _accessTokenKey);
  }

  Future<String?> readApiBaseUrl() {
    return _storage.read(key: _apiBaseUrlKey);
  }

  Future<String?> readWebBaseUrl() {
    return _storage.read(key: _webBaseUrlKey);
  }

  Future<void> saveBaseUrls({
    required String apiBaseUrl,
    required String webBaseUrl,
  }) async {
    await _storage.write(key: _apiBaseUrlKey, value: apiBaseUrl);
    await _storage.write(key: _webBaseUrlKey, value: webBaseUrl);
  }
}
