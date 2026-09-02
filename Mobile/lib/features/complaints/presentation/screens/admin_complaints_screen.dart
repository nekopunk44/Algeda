import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/errors/api_exception.dart';
import '../../data/repository/complaints_repository.dart';
import '../../domain/models/complaint_models.dart';
import 'realtor_complaints_screen.dart';

class AdminComplaintsScreen extends ConsumerStatefulWidget {
  const AdminComplaintsScreen({super.key});

  @override
  ConsumerState<AdminComplaintsScreen> createState() =>
      _AdminComplaintsScreenState();
}

class _AdminComplaintsScreenState extends ConsumerState<AdminComplaintsScreen> {
  final _searchController = TextEditingController();
  late Future<List<ComplaintItem>> _future;
  String _status = '';
  String _category = '';
  String _link = '';

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
                  'Жалобы',
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
          _filters(),
          const SizedBox(height: 12),
          FutureBuilder<List<ComplaintItem>>(
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

              final items = _applySearch(snapshot.data ?? const []);
              if (items.isEmpty) {
                return const _EmptyCard();
              }

              return Column(
                children: [
                  for (final item in items) ...[
                    ComplaintCard(
                      item: item,
                      onTap: () => context.push('/admin/complaints/${item.id}'),
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

  Widget _filters() {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          children: [
            DropdownButtonFormField<String>(
              initialValue: _status,
              decoration: const InputDecoration(labelText: 'Статус'),
              items: [
                const DropdownMenuItem(value: '', child: Text('Все')),
                for (final item in complaintStatuses)
                  DropdownMenuItem(
                    value: item,
                    child: Text(complaintStatusLabel(item)),
                  ),
              ],
              onChanged: (value) => _changeFilter(() => _status = value ?? ''),
            ),
            const SizedBox(height: 8),
            DropdownButtonFormField<String>(
              initialValue: _category,
              decoration: const InputDecoration(labelText: 'Категория'),
              items: [
                const DropdownMenuItem(value: '', child: Text('Все')),
                for (final item in complaintCategories)
                  DropdownMenuItem(
                    value: item,
                    child: Text(complaintCategoryLabel(item)),
                  ),
              ],
              onChanged: (value) =>
                  _changeFilter(() => _category = value ?? ''),
            ),
            const SizedBox(height: 8),
            DropdownButtonFormField<String>(
              initialValue: _link,
              decoration: const InputDecoration(labelText: 'Привязка'),
              items: const [
                DropdownMenuItem(value: '', child: Text('Все')),
                DropdownMenuItem(value: 'linked', child: Text('По сделке')),
                DropdownMenuItem(value: 'general', child: Text('Общая')),
              ],
              onChanged: (value) => _changeFilter(() => _link = value ?? ''),
            ),
            const SizedBox(height: 8),
            TextField(
              controller: _searchController,
              decoration: const InputDecoration(
                labelText: 'Поиск',
                hintText: 'ID, тема, описание',
                prefixIcon: Icon(Icons.search),
              ),
              onChanged: (_) => setState(() {}),
            ),
          ],
        ),
      ),
    );
  }

  Future<List<ComplaintItem>> _load() {
    return ref
        .read(complaintsRepositoryProvider)
        .getAdminComplaints(
          status: _status,
          category: _category,
          dealLinked: switch (_link) {
            'linked' => true,
            'general' => false,
            _ => null,
          },
        );
  }

  void _changeFilter(VoidCallback change) {
    setState(() {
      change();
      _future = _load();
    });
  }

  void _reload() {
    setState(() => _future = _load());
  }

  List<ComplaintItem> _applySearch(List<ComplaintItem> source) {
    final query = _searchController.text.trim().toLowerCase();
    if (query.isEmpty) {
      return source;
    }

    return source.where((item) {
      return item.id.toLowerCase().contains(query) ||
          item.clientId.toLowerCase().contains(query) ||
          item.subject.toLowerCase().contains(query) ||
          item.description.toLowerCase().contains(query) ||
          (item.dealId?.toLowerCase().contains(query) ?? false) ||
          (item.propertyId?.toLowerCase().contains(query) ?? false) ||
          (item.targetRealtorId?.toLowerCase().contains(query) ?? false) ||
          (item.clientFullName?.toLowerCase().contains(query) ?? false) ||
          (item.clientPhoneNumber?.toLowerCase().contains(query) ?? false) ||
          (item.clientEmail?.toLowerCase().contains(query) ?? false) ||
          (item.realtorFullName?.toLowerCase().contains(query) ?? false) ||
          (item.realtorPhoneNumber?.toLowerCase().contains(query) ?? false) ||
          (item.realtorEmail?.toLowerCase().contains(query) ?? false);
    }).toList();
  }

  String _messageFrom(Object error) {
    if (error is ApiException) {
      return error.message;
    }
    return 'Не удалось загрузить жалобы.';
  }
}

class _EmptyCard extends StatelessWidget {
  const _EmptyCard();

  @override
  Widget build(BuildContext context) {
    return const Card(
      child: Padding(
        padding: EdgeInsets.all(16),
        child: Text('Жалобы не найдены.'),
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
