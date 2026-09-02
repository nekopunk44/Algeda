import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:real_estate_mobile/features/deals/data/repository/deals_repository.dart';
import 'package:real_estate_mobile/features/deals/domain/models/deal_models.dart';

void main() {
  test(
    'репозиторий заявок фильтрует покупки и добавляет счетчики непрочитанных',
    () async {
      final dio = Dio(BaseOptions(baseUrl: 'http://localhost/'))
        ..httpClientAdapter = _JsonAdapter((options) {
          if (options.path == 'api/deal-chats/notifications/unread') {
            return [
              {'dealId': 'purchase-1', 'unreadCount': 3},
            ];
          }

          expect(options.path, 'api/deals/all');
          return [
            _deal(id: 'sale-1', source: 'Sale'),
            _deal(id: 'purchase-1', source: 'Manual'),
          ];
        });

      final deals = await DealsRepository(dio).getDeals(
        const DealFilters(
          scope: DealScope.all,
          source: DealSourceFilter.purchase,
        ),
      );

      expect(deals, hasLength(1));
      expect(deals.single.id, 'purchase-1');
      expect(deals.single.unreadCount, 3);
    },
  );
}

Map<String, Object?> _deal({required String id, required String source}) {
  return {
    'id': id,
    'clientId': 'client-$id',
    'clientFullName': 'Client $id',
    'clientPhoneNumber': '+37300000000',
    'source': source,
    'status': 'InProgress',
    'isIncoming': false,
    'notes': const [],
    'createdDate': '2026-05-17T12:00:00Z',
  };
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
