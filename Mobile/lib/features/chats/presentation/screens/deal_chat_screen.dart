import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../../core/auth/app_role.dart';
import '../../../../core/errors/api_exception.dart';
import '../../data/api/deal_chat_signalr_client.dart';
import '../../data/repository/chat_repository.dart';
import '../../domain/models/chat_models.dart';

class DealChatScreen extends ConsumerStatefulWidget {
  const DealChatScreen({required this.dealId, required this.role, super.key});

  final String dealId;
  final AppRole role;

  @override
  ConsumerState<DealChatScreen> createState() => _DealChatScreenState();
}

class _DealChatScreenState extends ConsumerState<DealChatScreen> {
  final _messageController = TextEditingController();
  final _scrollController = ScrollController();

  late Future<DealChatDialog> _future;
  StreamSubscription<DealChatMessage>? _signalRSubscription;

  bool _isSending = false;
  bool _isSignalRConnected = false;
  String? _signalRError;

  @override
  void initState() {
    super.initState();
    _future = _loadDialog();
    _future.then((_) => _connectSignalR()).catchError((_) {});
  }

  @override
  void dispose() {
    _messageController.dispose();
    _scrollController.dispose();
    _signalRSubscription?.cancel();
    unawaited(
      ref.read(dealChatSignalRClientProvider).disconnect(widget.dealId),
    );
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<DealChatDialog>(
      future: _future,
      builder: (context, snapshot) {
        if (snapshot.connectionState == ConnectionState.waiting) {
          return const Center(child: CircularProgressIndicator());
        }

        if (snapshot.hasError) {
          return _LoadError(
            message: _messageFrom(snapshot.error!),
            onRetry: _reload,
          );
        }

        final dialog = snapshot.data!;
        _scrollToBottomSoon();

        return Column(
          children: [
            _ChatHeader(
              dialog: dialog,
              isSignalRConnected: _isSignalRConnected,
              signalRError: _signalRError,
              onBack: () => _goBack(context),
              onRefresh: _reload,
              onOpenDeal: () => context.push(_dealPath),
            ),
            const Divider(height: 1),
            Expanded(
              child: RefreshIndicator(
                onRefresh: () async => _reload(),
                child: dialog.messages.isEmpty
                    ? ListView(
                        physics: const AlwaysScrollableScrollPhysics(),
                        padding: const EdgeInsets.all(16),
                        children: const [_EmptyMessagesCard()],
                      )
                    : ListView.separated(
                        controller: _scrollController,
                        padding: const EdgeInsets.all(16),
                        physics: const AlwaysScrollableScrollPhysics(),
                        itemCount: dialog.messages.length,
                        separatorBuilder: (_, _) => const SizedBox(height: 8),
                        itemBuilder: (context, index) {
                          return _MessageBubble(
                            message: dialog.messages[index],
                            dialog: dialog,
                            role: widget.role,
                          );
                        },
                      ),
              ),
            ),
            _Composer(
              controller: _messageController,
              enabled: dialog.canWrite && !_isSending,
              isSending: _isSending,
              blockReason: dialog.canWrite
                  ? null
                  : dialog.blockReason ?? 'Отправка сообщений недоступна.',
              onSend: () => _send(dialog),
            ),
          ],
        );
      },
    );
  }

  String get _listPath {
    return widget.role == AppRole.admin ? '/admin/chats' : '/realtor/chats';
  }

  String get _dealPath {
    final prefix = widget.role == AppRole.admin ? '/admin' : '/realtor';
    return '$prefix/deals/${widget.dealId}';
  }

  void _goBack(BuildContext context) {
    if (context.canPop()) {
      context.pop();
      return;
    }

    context.go(_listPath);
  }

  Future<DealChatDialog> _loadDialog() {
    return ref.read(chatRepositoryProvider).getDialog(widget.dealId);
  }

  void _reload() {
    setState(() {
      _future = _loadDialog();
    });
  }

  Future<void> _connectSignalR() async {
    final client = ref.read(dealChatSignalRClientProvider);
    await _signalRSubscription?.cancel();
    _signalRSubscription = client.messages.listen((message) {
      if (message.dealId == widget.dealId && mounted) {
        _reload();
      }
    });

    try {
      await client.connect(widget.dealId);
      if (!mounted) {
        return;
      }

      setState(() {
        _isSignalRConnected = true;
        _signalRError = null;
      });
    } catch (_) {
      if (!mounted) {
        return;
      }

      setState(() {
        _isSignalRConnected = false;
        _signalRError =
            'Автообновление недоступно, история сообщений загружена.';
      });
    }
  }

  Future<void> _send(DealChatDialog dialog) async {
    final content = _messageController.text.trim();
    if (content.isEmpty || !dialog.canWrite || _isSending) {
      return;
    }

    setState(() {
      _isSending = true;
    });

    try {
      await ref
          .read(chatRepositoryProvider)
          .sendMessage(dealId: widget.dealId, content: content);
      _messageController.clear();
      _reload();
    } on ApiException catch (error) {
      _showSnack(error.message);
    } catch (_) {
      _showSnack('Не удалось отправить сообщение.');
    } finally {
      if (mounted) {
        setState(() {
          _isSending = false;
        });
      }
    }
  }

  void _scrollToBottomSoon() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!_scrollController.hasClients) {
        return;
      }

      _scrollController.jumpTo(_scrollController.position.maxScrollExtent);
    });
  }

  void _showSnack(String message) {
    if (!mounted) {
      return;
    }

    ScaffoldMessenger.of(
      context,
    ).showSnackBar(SnackBar(content: Text(message)));
  }

  String _messageFrom(Object error) {
    if (error is ApiException) {
      return error.message;
    }

    return 'Не удалось открыть чат.';
  }
}

class _ChatHeader extends StatelessWidget {
  const _ChatHeader({
    required this.dialog,
    required this.isSignalRConnected,
    required this.onBack,
    required this.onRefresh,
    required this.onOpenDeal,
    this.signalRError,
  });

  final DealChatDialog dialog;
  final bool isSignalRConnected;
  final String? signalRError;
  final VoidCallback onBack;
  final VoidCallback onRefresh;
  final VoidCallback onOpenDeal;

  @override
  Widget build(BuildContext context) {
    final title =
        '${dialog.clientName ?? 'Клиент'} / ${dialog.realtorName ?? 'Риелтор'}';

    return Padding(
      padding: const EdgeInsets.fromLTRB(8, 8, 8, 10),
      child: Row(
        children: [
          IconButton(
            onPressed: onBack,
            icon: const Icon(Icons.arrow_back),
            tooltip: 'К списку чатов',
          ),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 2),
                Row(
                  children: [
                    Icon(
                      isSignalRConnected ? Icons.wifi_tethering : Icons.sync,
                      size: 16,
                      color: isSignalRConnected
                          ? Theme.of(context).colorScheme.primary
                          : Theme.of(context).colorScheme.outline,
                    ),
                    const SizedBox(width: 4),
                    Expanded(
                      child: Text(
                        signalRError ??
                            (isSignalRConnected
                                ? 'Live-сообщения подключены'
                                : 'История сообщений загружена'),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
          IconButton(
            onPressed: onOpenDeal,
            icon: const Icon(Icons.assignment_outlined),
            tooltip: 'Открыть заявку',
          ),
          IconButton(
            onPressed: onRefresh,
            icon: const Icon(Icons.refresh),
            tooltip: 'Обновить',
          ),
        ],
      ),
    );
  }
}

class _MessageBubble extends StatelessWidget {
  const _MessageBubble({
    required this.message,
    required this.dialog,
    required this.role,
  });

  final DealChatMessage message;
  final DealChatDialog dialog;
  final AppRole role;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final isClientMessage =
        message.senderId.toLowerCase() == dialog.clientId.toLowerCase();
    final alignRight = role == AppRole.admin
        ? !isClientMessage
        : message.isOutgoing;
    final author = isClientMessage ? 'Клиент' : 'Риелтор';
    final date = DateFormat(
      'dd.MM.yyyy HH:mm',
    ).format(message.createdDate.toLocal());

    return Align(
      alignment: alignRight ? Alignment.centerRight : Alignment.centerLeft,
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 320),
        child: DecoratedBox(
          decoration: BoxDecoration(
            color: alignRight
                ? colors.primaryContainer
                : colors.surfaceContainerHighest,
            borderRadius: BorderRadius.only(
              topLeft: const Radius.circular(8),
              topRight: const Radius.circular(8),
              bottomLeft: Radius.circular(alignRight ? 8 : 2),
              bottomRight: Radius.circular(alignRight ? 2 : 8),
            ),
          ),
          child: Padding(
            padding: const EdgeInsets.all(10),
            child: Column(
              crossAxisAlignment: alignRight
                  ? CrossAxisAlignment.end
                  : CrossAxisAlignment.start,
              children: [
                if (role == AppRole.admin) ...[
                  Text(
                    author,
                    style: theme.textTheme.bodySmall?.copyWith(
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 4),
                ],
                Text(message.content),
                const SizedBox(height: 4),
                Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(date, style: theme.textTheme.bodySmall),
                    if (message.isOutgoing) ...[
                      const SizedBox(width: 4),
                      Icon(
                        message.isRead ? Icons.done_all : Icons.check,
                        size: 16,
                        color: message.isRead ? colors.primary : colors.outline,
                      ),
                    ],
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _Composer extends StatelessWidget {
  const _Composer({
    required this.controller,
    required this.enabled,
    required this.isSending,
    required this.onSend,
    this.blockReason,
  });

  final TextEditingController controller;
  final bool enabled;
  final bool isSending;
  final String? blockReason;
  final VoidCallback onSend;

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      top: false,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(12, 8, 12, 12),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (blockReason != null) ...[
              Text(blockReason!, style: Theme.of(context).textTheme.bodySmall),
              const SizedBox(height: 6),
            ],
            Row(
              children: [
                Expanded(
                  child: TextField(
                    controller: controller,
                    enabled: enabled,
                    minLines: 1,
                    maxLines: 4,
                    textInputAction: TextInputAction.newline,
                    decoration: const InputDecoration(
                      hintText: 'Сообщение',
                      prefixIcon: Icon(Icons.message_outlined),
                    ),
                  ),
                ),
                const SizedBox(width: 8),
                IconButton.filled(
                  onPressed: enabled ? onSend : null,
                  icon: isSending
                      ? const SizedBox.square(
                          dimension: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.send),
                  tooltip: 'Отправить',
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _EmptyMessagesCard extends StatelessWidget {
  const _EmptyMessagesCard();

  @override
  Widget build(BuildContext context) {
    return const Card(
      child: Padding(
        padding: EdgeInsets.all(16),
        child: Text('В этом чате пока нет сообщений.'),
      ),
    );
  }
}

class _LoadError extends StatelessWidget {
  const _LoadError({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Card(
        margin: const EdgeInsets.all(16),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            mainAxisSize: MainAxisSize.min,
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
      ),
    );
  }
}
