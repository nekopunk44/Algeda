import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:real_estate_mobile/features/complaints/data/repository/complaints_repository.dart';

void main() {
  test('репозиторий жалоб дополняет жалобы данными как в веб-панели', () async {
    final dio = Dio(BaseOptions(baseUrl: 'http://localhost/'))
      ..httpClientAdapter = _JsonAdapter((options) {
        return switch (options.path) {
          'api/complaints' => [
            {
              'id': 'complaint-1',
              'clientId': 'client-1',
              'targetRealtorId': 'realtor-1',
              'category': 'Realtor',
              'subject': 'Проверка',
              'description': 'Описание',
              'status': 'Opened',
              'moderationVerdict': 'Undefined',
              'createdDate': '2026-05-19T10:00:00Z',
            },
          ],
          'api/clients' => [
            {
              'id': 'client-1',
              'firstName': 'Иван',
              'lastName': 'Иванов',
              'middleName': 'Иванович',
              'phoneNumber': '+37360000001',
              'email': 'client@example.com',
            },
          ],
          'api/realtors' => [
            {
              'id': 'realtor-1',
              'firstName': 'Анна',
              'lastName': 'Петрова',
              'middleName': null,
              'phoneNumber': '+37360000002',
            },
          ],
          'api/realtor-registration-requests' => [
            {
              'id': 'request-1',
              'phoneNumber': '+37360000002',
              'email': 'realtor@example.com',
              'reviewedAt': '2026-05-18T10:00:00Z',
              'createdDate': '2026-05-17T10:00:00Z',
            },
          ],
          _ => fail('Unexpected request: ${options.path}'),
        };
      });

    final complaints = await ComplaintsRepository(dio).getAdminComplaints();

    expect(complaints, hasLength(1));
    final complaint = complaints.single;
    expect(complaint.clientFullName, 'Иванов Иван Иванович');
    expect(complaint.clientPhoneNumber, '+37360000001');
    expect(complaint.clientEmail, 'client@example.com');
    expect(complaint.realtorFullName, 'Петрова Анна');
    expect(complaint.realtorPhoneNumber, '+37360000002');
    expect(complaint.realtorEmail, 'realtor@example.com');
  });
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
