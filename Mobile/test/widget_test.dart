import 'dart:ui';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:real_estate_mobile/app/app.dart';

void main() {
  testWidgets('приложение открывается с экрана входа', (tester) async {
    FlutterSecureStorage.setMockInitialValues({});

    await tester.pumpWidget(const ProviderScope(child: RealEstateMobileApp()));
    await tester.pump(const Duration(seconds: 2));
    await tester.pump(const Duration(milliseconds: 500));

    expect(find.text('Вход'), findsOneWidget);
    expect(find.text('Мобильный кабинет сотрудников'), findsOneWidget);
  });

  testWidgets('экран входа помещается на маленьком Android-экране', (
    tester,
  ) async {
    FlutterSecureStorage.setMockInitialValues({});

    tester.view.physicalSize = const Size(320, 640);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(const ProviderScope(child: RealEstateMobileApp()));
    await tester.pump(const Duration(seconds: 2));
    await tester.pump(const Duration(milliseconds: 500));

    expect(find.text('Email'), findsOneWidget);
    expect(find.text('Пароль'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}
