import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:signalr_netcore/signalr_client.dart';

import '../../../../core/config/api_config.dart';
import '../../../../core/storage/secure_token_storage.dart';
import '../dto/chat_dtos.dart';
import '../../domain/models/chat_models.dart';

final dealChatSignalRClientProvider = Provider<DealChatSignalRClient>((ref) {
  return DealChatSignalRClient(
    ref.watch(secureTokenStorageProvider),
    ref.watch(apiSettingsControllerProvider).chatHubUri,
  );
});

class DealChatSignalRClient {
  DealChatSignalRClient(this._tokenStorage, this._chatHubUri);

  final SecureTokenStorage _tokenStorage;
  final Uri _chatHubUri;
  HubConnection? _connection;
  final _messagesController = StreamController<DealChatMessage>.broadcast();

  Stream<DealChatMessage> get messages => _messagesController.stream;

  Future<void> connect(String dealId) async {
    await _connection?.stop();
    _connection = null;

    final token = await _tokenStorage.readAccessToken();
    if (token == null || token.isEmpty) {
      return;
    }

    final connection = HubConnectionBuilder()
        .withUrl(
          _chatHubUri.toString(),
          options: HttpConnectionOptions(accessTokenFactory: () async => token),
        )
        .withAutomaticReconnect()
        .build();

    connection.on('ReceiveDealMessage', (arguments) {
      if (arguments == null || arguments.isEmpty) {
        return;
      }

      final payload = arguments.first;
      if (payload is Map<String, dynamic>) {
        _messagesController.add(DealChatMessageDto.fromJson(payload).toModel());
      } else if (payload is Map) {
        _messagesController.add(
          DealChatMessageDto.fromJson(
            Map<String, dynamic>.from(payload),
          ).toModel(),
        );
      }
    });

    await connection.start();
    await connection.invoke('JoinDealChat', args: <Object>[dealId]);
    _connection = connection;
  }

  Future<void> disconnect(String dealId) async {
    final connection = _connection;
    _connection = null;
    if (connection == null) {
      return;
    }

    try {
      await connection.invoke('LeaveDealChat', args: <Object>[dealId]);
    } catch (_) {
      // Connection may already be closed.
    }

    await connection.stop();
  }

  Future<void> dispose() async {
    await _connection?.stop();
    await _messagesController.close();
  }
}
