import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../../core/auth/app_role.dart';
import '../../../../core/errors/api_exception.dart';
import '../../data/repository/chat_repository.dart';
import '../../domain/models/chat_models.dart';

class ChatsScreen extends ConsumerStatefulWidget {
  const ChatsScreen({required this.role, super.key});

  final AppRole role;

  @override
  ConsumerState<ChatsScreen> createState() => _ChatsScreenState();
}

class _ChatsScreenState extends ConsumerState<ChatsScreen> {
  final _dealController = TextEditingController();
  final _clientController = TextEditingController();
  final _realtorController = TextEditingController();

  late Future<_ChatsPayload> _future;

  bool get _isAdmin => widget.role == AppRole.admin;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  @override
  void dispose() {
    _dealController.dispose();
    _clientController.dispose();
    _realtorController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return RefreshIndicator(
      onRefresh: () async => _reload(),
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  'Чаты',
                  style: Theme.of(context).textTheme.headlineSmall,
                ),
              ),
              IconButton(
                onPressed: _reload,
                icon: const Icon(Icons.refresh),
                tooltip: 'Обновить',
              ),
            ],
          ),
          const SizedBox(height: 12),
          if (_isAdmin) _adminFilters(),
          FutureBuilder<_ChatsPayload>(
            future: _future,
            builder: (context, snapshot) {
              if (snapshot.connectionState == ConnectionState.waiting) {
                return const Padding(
                  padding: EdgeInsets.only(top: 48),
                  child: Center(child: CircularProgressIndicator()),
                );
              }

              if (snapshot.hasError) {
                return _ErrorCard(
                  message: _messageFrom(snapshot.error!),
                  onRetry: _reload,
                );
              }

              final payload = snapshot.data ?? const _ChatsPayload();
              return Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (payload.notifications.isNotEmpty) ...[
                    Text(
                      'Непрочитанные',
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                    const SizedBox(height: 8),
                    for (final notification in payload.notifications) ...[
                      _NotificationCard(
                        notification: notification,
                        onTap: () => _openChat(notification.dealId),
                      ),
                      const SizedBox(height: 8),
                    ],
                    const SizedBox(height: 8),
                  ],
                  Text(
                    _isAdmin ? 'Все чаты' : 'Мои чаты',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  const SizedBox(height: 8),
                  if (payload.chats.isEmpty)
                    const _EmptyCard()
                  else
                    for (final chat in payload.chats) ...[
                      _ChatSummaryCard(
                        summary: chat,
                        onTap: () => _openChat(chat.dealId),
                      ),
                      const SizedBox(height: 8),
                    ],
                ],
              );
            },
          ),
        ],
      ),
    );
  }

  Widget _adminFilters() {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          children: [
            TextField(
              controller: _dealController,
              decoration: const InputDecoration(
                labelText: 'Сделка',
                hintText: 'ID сделки',
                prefixIcon: Icon(Icons.tag),
              ),
              onSubmitted: (_) => _reload(),
            ),
            const SizedBox(height: 8),
            TextField(
              controller: _clientController,
              decoration: const InputDecoration(
                labelText: 'Клиент',
                hintText: 'Имя или ID',
                prefixIcon: Icon(Icons.person_outline),
              ),
              onSubmitted: (_) => _reload(),
            ),
            const SizedBox(height: 8),
            TextField(
              controller: _realtorController,
              decoration: const InputDecoration(
                labelText: 'Риелтор',
                hintText: 'Имя или ID',
                prefixIcon: Icon(Icons.badge_outlined),
              ),
              onSubmitted: (_) => _reload(),
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: _reload,
                    icon: const Icon(Icons.search),
                    label: const Text('Найти'),
                  ),
                ),
                const SizedBox(width: 8),
                IconButton.outlined(
                  onPressed: () {
                    _dealController.clear();
                    _clientController.clear();
                    _realtorController.clear();
                    _reload();
                  },
                  icon: const Icon(Icons.restart_alt),
                  tooltip: 'Сбросить',
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Future<_ChatsPayload> _load() async {
    final repo = ref.read(chatRepositoryProvider);
    final notifications = await repo.getUnreadNotifications();
    final chats = _isAdmin
        ? await repo.getAdminChats(
            dealId: _dealController.text,
            client: _clientController.text,
            realtor: _realtorController.text,
          )
        : await repo.getRealtorChats();

    return _ChatsPayload(chats: chats, notifications: notifications);
  }

  void _reload() {
    setState(() {
      _future = _load();
    });
  }

  void _openChat(String dealId) {
    final prefix = _isAdmin ? '/admin' : '/realtor';
    context.push('$prefix/chats/$dealId');
  }

  String _messageFrom(Object error) {
    if (error is ApiException) {
      return error.message;
    }

    return 'Не удалось загрузить чаты.';
  }
}

class _ChatsPayload {
  const _ChatsPayload({this.chats = const [], this.notifications = const []});

  final List<DealChatSummary> chats;
  final List<DealChatNotification> notifications;
}

class _NotificationCard extends StatelessWidget {
  const _NotificationCard({required this.notification, required this.onTap});

  final DealChatNotification notification;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: ListTile(
        leading: Badge(
          label: Text(notification.unreadCount.toString()),
          child: const Icon(Icons.mark_chat_unread_outlined),
        ),
        title: Text(notification.counterpartyName ?? 'Новое сообщение'),
        subtitle: Text(notification.preview),
        trailing: const Icon(Icons.chevron_right),
        onTap: onTap,
      ),
    );
  }
}

class _ChatSummaryCard extends StatelessWidget {
  const _ChatSummaryCard({required this.summary, required this.onTap});

  final DealChatSummary summary;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final date = summary.lastMessageAtUtc;

    return Card(
      child: ListTile(
        leading: summary.unreadCount > 0
            ? Badge(
                label: Text(summary.unreadCount.toString()),
                child: const Icon(Icons.forum_outlined),
              )
            : const Icon(Icons.forum_outlined),
        title: Text(_title),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(summary.lastMessagePreview ?? 'История сообщений'),
            Text(
              date == null
                  ? 'Сделка ${summary.dealStatus}'
                  : DateFormat('dd.MM.yyyy HH:mm').format(date.toLocal()),
              style: Theme.of(context).textTheme.bodySmall,
            ),
          ],
        ),
        trailing: const Icon(Icons.chevron_right),
        onTap: onTap,
      ),
    );
  }

  String get _title {
    final client = summary.clientName ?? 'Клиент';
    final realtor = summary.realtorName ?? 'Риелтор';
    return '$client / $realtor';
  }
}

class _EmptyCard extends StatelessWidget {
  const _EmptyCard();

  @override
  Widget build(BuildContext context) {
    return const Card(
      child: Padding(
        padding: EdgeInsets.all(16),
        child: Text('Чаты не найдены.'),
      ),
    );
  }
}

class _ErrorCard extends StatelessWidget {
  const _ErrorCard({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          children: [
            const Icon(Icons.error_outline),
            const SizedBox(height: 8),
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 8),
            OutlinedButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('Повторить'),
            ),
          ],
        ),
      ),
    );
  }
}
