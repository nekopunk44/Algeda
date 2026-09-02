import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../../core/errors/api_exception.dart';
import '../../data/repository/complaints_repository.dart';
import '../../domain/models/complaint_models.dart';

class AdminComplaintDetailsScreen extends ConsumerStatefulWidget {
  const AdminComplaintDetailsScreen({required this.complaintId, super.key});

  final String complaintId;

  @override
  ConsumerState<AdminComplaintDetailsScreen> createState() =>
      _AdminComplaintDetailsScreenState();
}

class _AdminComplaintDetailsScreenState
    extends ConsumerState<AdminComplaintDetailsScreen> {
  final _resolutionController = TextEditingController();
  late Future<ComplaintItem> _future;
  String _verdict = 'Confirmed';
  bool _isBusy = false;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  @override
  void dispose() {
    _resolutionController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<ComplaintItem>(
      future: _future,
      builder: (context, snapshot) {
        if (snapshot.connectionState == ConnectionState.waiting) {
          return const Center(child: CircularProgressIndicator());
        }
        if (snapshot.hasError) {
          return _ErrorView(
            message: _messageFrom(snapshot.error!),
            onRetry: _reload,
          );
        }

        final item = snapshot.data!;
        return ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Row(
              children: [
                IconButton(
                  onPressed: () => _goBack(context),
                  icon: const Icon(Icons.arrow_back),
                  tooltip: 'К списку',
                ),
                Expanded(
                  child: Text(
                    'Жалоба',
                    style: Theme.of(context).textTheme.headlineSmall,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            _DetailsCard(item: item),
            if (item.status != 'Resolved') ...[
              const SizedBox(height: 12),
              _WorkflowCard(
                item: item,
                isBusy: _isBusy,
                verdict: _verdict,
                resolutionController: _resolutionController,
                onVerdictChanged: (value) {
                  if (value != null) {
                    setState(() => _verdict = value);
                  }
                },
                onOpened: () => _runNoContent(() => _repo.markOpened(item.id)),
                onInProgress: () =>
                    _runNoContent(() => _repo.markInProgress(item.id)),
                onResolve: () => _resolve(item.id),
              ),
            ],
          ],
        );
      },
    );
  }

  ComplaintsRepository get _repo => ref.read(complaintsRepositoryProvider);

  Future<ComplaintItem> _load() => _repo.getById(widget.complaintId);

  void _reload() {
    setState(() => _future = _load());
  }

  Future<void> _runNoContent(Future<void> Function() action) async {
    setState(() => _isBusy = true);
    try {
      await action();
      _reload();
    } on ApiException catch (error) {
      _showSnack(error.message);
    } finally {
      if (mounted) {
        setState(() => _isBusy = false);
      }
    }
  }

  Future<void> _resolve(String id) async {
    final text = _resolutionController.text.trim();
    if (text.isEmpty) {
      _showSnack('Укажите решение администратора.');
      return;
    }

    setState(() => _isBusy = true);
    try {
      final resolved = await _repo.resolve(
        id: id,
        verdict: _verdict,
        resolution: text,
      );
      setState(() => _future = Future.value(resolved));
    } on ApiException catch (error) {
      _showSnack(error.message);
    } finally {
      if (mounted) {
        setState(() => _isBusy = false);
      }
    }
  }

  void _showSnack(String message) {
    if (!mounted) {
      return;
    }
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(SnackBar(content: Text(message)));
  }

  void _goBack(BuildContext context) {
    if (context.canPop()) {
      context.pop();
      return;
    }

    context.go('/admin/complaints');
  }

  String _messageFrom(Object error) {
    if (error is ApiException) {
      return error.message;
    }
    return 'Не удалось открыть жалобу.';
  }
}

class _DetailsCard extends StatelessWidget {
  const _DetailsCard({required this.item});

  final ComplaintItem item;

  @override
  Widget build(BuildContext context) {
    final created = DateFormat(
      'dd.MM.yyyy HH:mm',
    ).format(item.createdDate.toLocal());

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(item.subject, style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            _row('Категория', complaintCategoryLabel(item.category)),
            _row('Статус', complaintStatusLabel(item.status)),
            _row('Создана', created),
            _personBlock(
              context,
              label: 'Клиент',
              name: item.clientFullName,
              phone: item.clientPhoneNumber,
              email: item.clientEmail,
              fallback: 'Клиент не найден',
            ),
            if (item.targetRealtorId != null ||
                item.realtorFullName?.trim().isNotEmpty == true)
              _personBlock(
                context,
                label: 'Риелтор',
                name: item.realtorFullName,
                phone: item.realtorPhoneNumber,
                email: item.realtorEmail,
                fallback: 'Риелтор не найден',
              ),
            if (item.dealId != null) _row('Сделка', item.dealId!),
            if (item.propertyId != null) _row('Объект', item.propertyId!),
            const Divider(),
            Text(item.description),
            if (item.adminResolution?.isNotEmpty ?? false) ...[
              const Divider(),
              _row('Вердикт', complaintVerdictLabel(item.moderationVerdict)),
              Text(item.adminResolution!),
            ],
          ],
        ),
      ),
    );
  }

  Widget _row(String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(width: 90, child: Text(label)),
          Expanded(child: SelectableText(value)),
        ],
      ),
    );
  }

  Widget _personBlock(
    BuildContext context, {
    required String label,
    required String? name,
    required String? phone,
    required String? email,
    required String fallback,
  }) {
    final rows = [
      if (name?.trim().isNotEmpty == true) name!.trim(),
      if (phone?.trim().isNotEmpty == true) phone!.trim(),
      if (email?.trim().isNotEmpty == true) email!.trim(),
    ];

    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: Theme.of(context).textTheme.bodySmall),
          const SizedBox(height: 2),
          if (rows.isEmpty)
            Text(fallback)
          else
            for (final row in rows)
              SelectableText(
                row,
                style: row == rows.first
                    ? Theme.of(context).textTheme.bodyMedium?.copyWith(
                        fontWeight: FontWeight.w600,
                      )
                    : Theme.of(context).textTheme.bodySmall,
              ),
        ],
      ),
    );
  }
}

class _WorkflowCard extends StatelessWidget {
  const _WorkflowCard({
    required this.item,
    required this.isBusy,
    required this.verdict,
    required this.resolutionController,
    required this.onVerdictChanged,
    required this.onOpened,
    required this.onInProgress,
    required this.onResolve,
  });

  final ComplaintItem item;
  final bool isBusy;
  final String verdict;
  final TextEditingController resolutionController;
  final ValueChanged<String?> onVerdictChanged;
  final VoidCallback onOpened;
  final VoidCallback onInProgress;
  final VoidCallback onResolve;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    'Текущий статус: ${complaintStatusLabel(item.status)}',
                    style: Theme.of(context).textTheme.bodySmall,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            if (item.status == 'InProgress')
              OutlinedButton.icon(
                onPressed: isBusy ? null : onOpened,
                icon: const Icon(Icons.undo),
                label: const Text('Убрать из работы'),
              )
            else
              FilledButton.tonalIcon(
                onPressed: isBusy ? null : onInProgress,
                icon: const Icon(Icons.work_outline),
                label: const Text('В работу'),
              ),
            const SizedBox(height: 12),
            DropdownButtonFormField<String>(
              initialValue: verdict,
              decoration: const InputDecoration(labelText: 'Вердикт'),
              items: [
                for (final item in complaintVerdicts)
                  DropdownMenuItem(
                    value: item,
                    child: Text(complaintVerdictLabel(item)),
                  ),
              ],
              onChanged: isBusy ? null : onVerdictChanged,
            ),
            const SizedBox(height: 8),
            TextField(
              controller: resolutionController,
              minLines: 2,
              maxLines: 5,
              decoration: const InputDecoration(labelText: 'Решение'),
            ),
            const SizedBox(height: 12),
            FilledButton.icon(
              onPressed: isBusy ? null : onResolve,
              icon: const Icon(Icons.check_circle_outline),
              label: const Text('Решить'),
            ),
          ],
        ),
      ),
    );
  }
}

class _ErrorView extends StatelessWidget {
  const _ErrorView({required this.message, required this.onRetry});

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
