import 'package:flutter_local_notifications/flutter_local_notifications.dart';

import '../../domain/models/notification_models.dart';

class MobileNotificationService {
  MobileNotificationService._();

  static final instance = MobileNotificationService._();

  final FlutterLocalNotificationsPlugin _plugin =
      FlutterLocalNotificationsPlugin();
  bool _initialized = false;

  Future<void> initialize() async {
    if (_initialized) {
      return;
    }

    const android = AndroidInitializationSettings('@mipmap/ic_launcher');
    const settings = InitializationSettings(android: android);
    await _plugin.initialize(settings: settings);

    final androidPlugin = _plugin
        .resolvePlatformSpecificImplementation<
          AndroidFlutterLocalNotificationsPlugin
        >();
    await androidPlugin?.requestNotificationsPermission();

    _initialized = true;
  }

  Future<void> showChatNotification(AppNotification notification) async {
    await initialize();
    final details = NotificationDetails(
      android: AndroidNotificationDetails(
        'deal_chat_notifications',
        'Уведомления чатов',
        channelDescription: 'Новые сообщения по сделкам',
        importance: Importance.high,
        priority: Priority.high,
        number: notification.unreadCount,
      ),
    );

    await _plugin.show(
      id: notification.id.hashCode,
      title: notification.title,
      body: notification.message,
      notificationDetails: details,
      payload: notification.dealId,
    );
  }
}
