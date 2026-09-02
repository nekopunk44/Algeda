import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../core/errors/api_exception.dart';
import '../../data/repository/complaints_repository.dart';
import '../../domain/models/complaint_models.dart';

class RealtorComplaintsScreen extends ConsumerStatefulWidget {
  const RealtorComplaintsScreen({super.key});

  @override
  ConsumerState<RealtorComplaintsScreen> createState() =>
      _RealtorComplaintsScreenState();
}

class _RealtorComplaintsScreenState
    extends ConsumerState<RealtorComplaintsScreen> {
  late Future<List<ComplaintItem>> _future;

  @override
  void initState() {
    super.initState();
    _future = _load();
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
                  'Жалобы по мне',
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
                return _ErrorCard(message: _messageFrom(snapshot.error!));
              }

              final items = snapshot.data ?? const [];
              if (items.isEmpty) {
                return const _EmptyCard();
              }

              return Column(
                children: [
                  for (final item in items) ...[
                    ComplaintCard(item: item, showIds: false),
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

  Future<List<ComplaintItem>> _load() {
    return ref.read(complaintsRepositoryProvider).getMyRealtorComplaints();
  }

  void _reload() {
    setState(() => _future = _load());
  }

  String _messageFrom(Object error) {
    if (error is ApiException) {
      return error.message;
    }
    return 'Не удалось загрузить жалобы.';
  }
}

class ComplaintCard extends StatelessWidget {
  const ComplaintCard({
    required this.item,
    this.showIds = true,
    this.onTap,
    super.key,
  });

  final ComplaintItem item;
  final bool showIds;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final date = DateFormat(
      'dd.MM.yyyy HH:mm',
    ).format(item.createdDate.toLocal());

    return Card(
      child: ListTile(
        leading: const Icon(Icons.report_problem_outlined),
        title: Text(item.subject),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(complaintCategoryLabel(item.category)),
            Text('${complaintStatusLabel(item.status)} · $date'),
            if (item.moderationVerdict != 'Undefined')
              Text('Вердикт: ${complaintVerdictLabel(item.moderationVerdict)}'),
            if (item.adminResolution?.isNotEmpty ?? false)
              Text('Решение: ${item.adminResolution}'),
            if (showIds && item.clientFullName?.trim().isNotEmpty == true)
              Text('Клиент: ${item.clientFullName}'),
            if (showIds && item.realtorFullName?.trim().isNotEmpty == true)
              Text('Риелтор: ${item.realtorFullName}'),
            if (showIds && item.dealId != null) Text('Сделка: ${item.dealId}'),
            if (showIds && item.propertyId != null)
              Text('Объект: ${item.propertyId}'),
          ],
        ),
        trailing: onTap == null ? null : const Icon(Icons.chevron_right),
        onTap: onTap,
      ),
    );
  }
}

class _EmptyCard extends StatelessWidget {
  const _EmptyCard();

  @override
  Widget build(BuildContext context) {
    return const Card(
      child: Padding(
        padding: EdgeInsets.all(16),
        child: Text('Жалоб по вам нет.'),
      ),
    );
  }
}

class _ErrorCard extends StatelessWidget {
  const _ErrorCard({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Text(message, textAlign: TextAlign.center),
      ),
    );
  }
}
