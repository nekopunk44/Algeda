import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../../core/errors/api_exception.dart';
import '../../data/repository/deals_repository.dart';
import '../../domain/models/deal_document_models.dart';

class AdminDocumentAccessScreen extends ConsumerStatefulWidget {
  const AdminDocumentAccessScreen({super.key});

  @override
  ConsumerState<AdminDocumentAccessScreen> createState() =>
      _AdminDocumentAccessScreenState();
}

class _AdminDocumentAccessScreenState
    extends ConsumerState<AdminDocumentAccessScreen> {
  late Future<List<DealDocumentAccessLog>> _future;
  final _searchController = TextEditingController();
  String? _action;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  @override
  void dispose() {
    _searchController.dispose();
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
                  'Доступ к документам',
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
          Card(
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Column(
                children: [
                  TextField(
                    controller: _searchController,
                    decoration: const InputDecoration(
                      labelText: 'Поиск',
                      hintText: 'ФИО, email, телефон, ID или дата',
                      prefixIcon: Icon(Icons.search),
                    ),
                    onSubmitted: (_) => _reload(),
                  ),
                  const SizedBox(height: 8),
                  DropdownButtonFormField<String?>(
                    initialValue: _action,
                    isExpanded: true,
                    decoration: const InputDecoration(labelText: 'Действие'),
                    items: const [
                      DropdownMenuItem(value: null, child: Text('Все')),
                      DropdownMenuItem(
                        value: 'Added',
                        child: Text('Добавление'),
                      ),
                      DropdownMenuItem(
                        value: 'Opened',
                        child: Text('Открытие'),
                      ),
                      DropdownMenuItem(
                        value: 'Deleted',
                        child: Text('Удаление'),
                      ),
                    ],
                    onChanged: (value) {
                      setState(() {
                        _action = value;
                        _future = _load();
                      });
                    },
                  ),
                  const SizedBox(height: 8),
                  Align(
                    alignment: Alignment.centerRight,
                    child: FilledButton.icon(
                      onPressed: _reload,
                      icon: const Icon(Icons.search),
                      label: const Text('Применить'),
                    ),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),
          FutureBuilder<List<DealDocumentAccessLog>>(
            future: _future,
            builder: (context, snapshot) {
              if (snapshot.connectionState == ConnectionState.waiting) {
                return const Card(
                  child: Padding(
                    padding: EdgeInsets.all(16),
                    child: LinearProgressIndicator(),
                  ),
                );
              }

              if (snapshot.hasError) {
                return _MessageCard(text: _messageFrom(snapshot.error!));
              }

              final logs = snapshot.data ?? const [];
              if (logs.isEmpty) {
                return const _MessageCard(text: 'Записи журнала не найдены.');
              }

              return Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  for (final item in logs) ...[
                    _DocumentLogCard(item: item),
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

  Future<List<DealDocumentAccessLog>> _load() {
    return ref
        .read(dealsRepositoryProvider)
        .getDocumentAccessLog(action: _action, search: _searchController.text);
  }

  void _reload() {
    setState(() {
      _future = _load();
    });
  }

  String _messageFrom(Object error) {
    if (error is ApiException) {
      return error.message;
    }
    return 'Не удалось загрузить журнал доступа.';
  }
}

class _DocumentLogCard extends StatelessWidget {
  const _DocumentLogCard({required this.item});

  final DealDocumentAccessLog item;

  @override
  Widget build(BuildContext context) {
    final color = Theme.of(context).colorScheme;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Icon(_iconFor(item.action), color: color.primary),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        dealDocumentActionLabel(item.action),
                        style: Theme.of(context).textTheme.titleSmall,
                      ),
                      const SizedBox(height: 2),
                      Text(
                        _formatDate(item.createdDate),
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 10),
            _Line(label: 'Документ', value: item.documentTitle),
            _Line(label: 'Файл', value: item.documentFileName),
            _Line(label: 'Пользователь', value: item.actorText),
            if (item.actorRole.isNotEmpty)
              _Line(label: 'Роль', value: item.actorRole),
            if (item.ipAddress?.isNotEmpty == true)
              _Line(label: 'IP', value: item.ipAddress!),
            const SizedBox(height: 10),
            OutlinedButton.icon(
              onPressed: () => context.push('/admin/deals/${item.dealId}'),
              icon: const Icon(Icons.open_in_new),
              label: const Text('Открыть сделку'),
            ),
            if (item.clientSummary.isNotEmpty)
              _Line(label: 'Клиент', value: item.clientSummary),
          ],
        ),
      ),
    );
  }

  IconData _iconFor(String action) {
    return switch (action.toLowerCase()) {
      'added' => Icons.add_circle_outline,
      'opened' => Icons.visibility_outlined,
      'deleted' => Icons.delete_outline,
      _ => Icons.description_outlined,
    };
  }
}

class _Line extends StatelessWidget {
  const _Line({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: 6),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: Theme.of(context).textTheme.bodySmall),
          SelectableText(value),
        ],
      ),
    );
  }
}

class _MessageCard extends StatelessWidget {
  const _MessageCard({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(padding: const EdgeInsets.all(16), child: Text(text)),
    );
  }
}

String _formatDate(DateTime date) {
  return DateFormat('dd.MM.yyyy HH:mm').format(date.toLocal());
}
