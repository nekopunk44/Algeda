import 'package:flutter_test/flutter_test.dart';
import 'package:real_estate_mobile/core/auth/app_role.dart';

void main() {
  group('Роли приложения', () {
    test('преобразует роли backend в роли мобильного приложения', () {
      expect(AppRole.fromBackendRoles(['Realtor']), AppRole.realtor);
      expect(AppRole.fromBackendRoles(['Admin']), AppRole.admin);
      expect(AppRole.fromBackendRoles(['SuperAdmin']), AppRole.admin);
    });

    test('проверяет доступ к маршрутам по роли', () {
      expect(AppRole.realtor.ownsPath('/realtor/deals'), isTrue);
      expect(AppRole.realtor.ownsPath('/admin/deals'), isFalse);
      expect(AppRole.admin.ownsPath('/admin/analytics'), isTrue);
      expect(AppRole.admin.ownsPath('/realtor/chats'), isFalse);
    });
  });
}
