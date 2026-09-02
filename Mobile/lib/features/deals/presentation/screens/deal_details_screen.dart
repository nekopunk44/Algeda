import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:open_filex/open_filex.dart';

import '../../../../core/auth/app_role.dart';
import '../../../../core/errors/api_exception.dart';
import '../../data/repository/deals_repository.dart';
import '../../domain/models/deal_document_models.dart';
import '../../domain/models/deal_models.dart';

class DealDetailsScreen extends ConsumerStatefulWidget {
  const DealDetailsScreen({
    required this.dealId,
    required this.role,
    super.key,
  });

  final String dealId;
  final AppRole role;

  @override
  ConsumerState<DealDetailsScreen> createState() => _DealDetailsScreenState();
}

class _DealDetailsScreenState extends ConsumerState<DealDetailsScreen> {
  late Future<DealWorkflow> _future;
  late Future<List<DealDocument>> _documentsFuture;
  bool _isBusy = false;
  bool _documentsBusy = false;

  bool get _isAdmin => widget.role == AppRole.admin;
  bool get _isRealtor => widget.role == AppRole.realtor;
  String get _prefix => _isAdmin ? '/admin' : '/realtor';

  @override
  void initState() {
    super.initState();
    _future = _load();
    _documentsFuture = _loadDocuments();
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<DealWorkflow>(
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

        final deal = snapshot.data!;
        return RefreshIndicator(
          onRefresh: () async => _reload(),
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(
                      'Заявка',
                      style: Theme.of(context).textTheme.headlineSmall,
                    ),
                  ),
                  if (deal.hasUnread)
                    Badge(
                      label: Text(deal.unreadCount.toString()),
                      child: const Icon(Icons.mark_chat_unread_outlined),
                    ),
                  IconButton(
                    onPressed: () => context.push('$_prefix/chats/${deal.id}'),
                    icon: const Icon(Icons.chat_bubble_outline),
                    tooltip: 'Открыть чат',
                  ),
                ],
              ),
              const SizedBox(height: 12),
              _DealInfoCard(deal: deal),
              const SizedBox(height: 12),
              _ClientCard(deal: deal),
              const SizedBox(height: 12),
              _PropertyCard(
                deal: deal,
                onOpenProperty: deal.propertyId?.isNotEmpty == true
                    ? () =>
                          context.push('$_prefix/properties/${deal.propertyId}')
                    : null,
                onEditProperty: deal.propertyId?.isNotEmpty == true
                    ? () => context.push(
                        '$_prefix/properties/${deal.propertyId}/edit',
                      )
                    : null,
              ),
              const SizedBox(height: 12),
              _ActionsCard(
                deal: deal,
                isAdmin: _isAdmin,
                isRealtor: _isRealtor,
                isBusy: _isBusy,
                onAccept: () =>
                    _run(() => _repo.accept(deal.id), 'Заявка принята.'),
                onReject: () => _confirmRun(
                  'Отклонить заявку?',
                  () => _repo.reject(deal.id),
                  'Заявка отклонена.',
                ),
                onRelease: () => _confirmRun(
                  'Вернуть заявку в общий пул?',
                  () => _repo.release(deal.id),
                  'Заявка возвращена в общий пул.',
                ),
                onCancel: () => _confirmRun(
                  deal.isSaleRequest ? 'Отменить заявку?' : 'Отменить сделку?',
                  () => _repo.cancel(deal.id),
                  'Статус изменен на «Отменена».',
                ),
                onComplete: deal.isSaleRequest
                    ? null
                    : () => _showCompleteDialog(deal),
                onAssign: _isAdmin ? () => _showAssignDialog(deal) : null,
              ),
              const SizedBox(height: 12),
              _DocumentsCard(
                future: _documentsFuture,
                isBusy: _documentsBusy,
                onAdd: (deal.isInProgress || _isAdmin)
                    ? () => _showDocumentDialog(deal)
                    : null,
                onOpen: (document) => _openDocument(deal, document),
                onDelete: (document) => _confirmRun(
                  'Удалить документ?',
                  () => _repo.deleteDocument(
                    dealId: deal.id,
                    documentId: document.id,
                  ),
                  'Документ удален.',
                  reloadDocuments: true,
                ),
                messageFrom: _messageFrom,
              ),
              const SizedBox(height: 12),
              _NotesCard(
                deal: deal,
                isBusy: _isBusy,
                onAdd: () => _showNoteDialog(deal),
                onEdit: (note) => _showNoteDialog(deal, note: note),
                onDelete: (note) => _confirmRun(
                  'Удалить заметку?',
                  () => _repo.deleteNote(id: deal.id, noteId: note.id),
                  'Заметка удалена.',
                ),
              ),
            ],
          ),
        );
      },
    );
  }

  DealsRepository get _repo => ref.read(dealsRepositoryProvider);

  Future<DealWorkflow> _load() {
    return _repo.getWorkflow(widget.dealId);
  }

  Future<List<DealDocument>> _loadDocuments() {
    return _repo.getDocuments(widget.dealId);
  }

  void _reload() {
    setState(() {
      _future = _load();
      _documentsFuture = _loadDocuments();
    });
  }

  void _reloadDocuments() {
    setState(() {
      _documentsFuture = _loadDocuments();
    });
  }

  Future<void> _run(Future<void> Function() action, String success) async {
    setState(() => _isBusy = true);
    try {
      await action();
      _showMessage(success);
      _reload();
    } catch (error) {
      _showMessage(_messageFrom(error), isError: true);
    } finally {
      if (mounted) {
        setState(() => _isBusy = false);
      }
    }
  }

  Future<void> _confirmRun(
    String question,
    Future<void> Function() action,
    String success, {
    bool reloadDocuments = false,
  }) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Подтверждение'),
        content: Text(question),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('Нет'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text('Да'),
          ),
        ],
      ),
    );

    if (confirmed == true) {
      await _run(action, success);
      if (reloadDocuments) {
        _reloadDocuments();
      }
    }
  }

  Future<void> _showCompleteDialog(DealWorkflow deal) async {
    final amountController = TextEditingController(text: '1000');
    final currencyController = TextEditingController(text: 'USD');
    final formKey = GlobalKey<FormState>();

    final result = await showDialog<({num amount, String currency})>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Завершить сделку'),
        content: Form(
          key: formKey,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextFormField(
                controller: amountController,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(labelText: 'Комиссия'),
                validator: (value) {
                  final amount = num.tryParse(
                    value?.replaceAll(',', '.') ?? '',
                  );
                  if (amount == null || amount <= 0) {
                    return 'Введите сумму больше нуля.';
                  }
                  return null;
                },
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: currencyController,
                textCapitalization: TextCapitalization.characters,
                decoration: const InputDecoration(labelText: 'Валюта'),
                validator: (value) {
                  if ((value ?? '').trim().length != 3) {
                    return 'Код валюты: 3 символа.';
                  }
                  return null;
                },
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(),
            child: const Text('Отмена'),
          ),
          FilledButton(
            onPressed: () {
              if (!formKey.currentState!.validate()) {
                return;
              }

              Navigator.of(context).pop((
                amount: num.parse(amountController.text.replaceAll(',', '.')),
                currency: currencyController.text.trim().toUpperCase(),
              ));
            },
            child: const Text('Завершить'),
          ),
        ],
      ),
    );

    amountController.dispose();
    currencyController.dispose();

    if (result == null) {
      return;
    }

    await _run(
      () => _repo.complete(
        id: deal.id,
        commissionAmount: result.amount,
        commissionCurrency: result.currency,
      ),
      'Сделка завершена.',
    );
  }

  Future<void> _showAssignDialog(DealWorkflow deal) async {
    final searchController = TextEditingController();
    var candidates = <RealtorCandidate>[];
    var isLoading = false;
    String? selectedId = deal.realtorId;

    Future<void> load(StateSetter setState) async {
      setState(() => isLoading = true);
      try {
        candidates = await _repo.searchRealtors(searchController.text);
      } catch (error) {
        _showMessage(_messageFrom(error), isError: true);
      } finally {
        setState(() => isLoading = false);
      }
    }

    final result = await showDialog<String>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setState) {
          return AlertDialog(
            title: const Text('Назначить риелтора'),
            content: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                TextField(
                  controller: searchController,
                  decoration: InputDecoration(
                    labelText: 'Поиск',
                    suffixIcon: IconButton(
                      onPressed: () => load(setState),
                      icon: const Icon(Icons.search),
                    ),
                  ),
                  onSubmitted: (_) => load(setState),
                ),
                const SizedBox(height: 12),
                if (isLoading)
                  const LinearProgressIndicator()
                else
                  DropdownButtonFormField<String>(
                    initialValue:
                        candidates.any((x) => x.realtorId == selectedId)
                        ? selectedId
                        : null,
                    decoration: const InputDecoration(labelText: 'Риелтор'),
                    items: [
                      for (final candidate in candidates)
                        DropdownMenuItem(
                          value: candidate.realtorId,
                          child: Text(candidate.display),
                        ),
                    ],
                    onChanged: (value) {
                      setState(() {
                        selectedId = value;
                      });
                    },
                  ),
                if (!isLoading && candidates.isEmpty)
                  const Padding(
                    padding: EdgeInsets.only(top: 8),
                    child: Text('Введите запрос и нажмите поиск.'),
                  ),
              ],
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(context).pop(),
                child: const Text('Отмена'),
              ),
              FilledButton(
                onPressed: selectedId == null
                    ? null
                    : () => Navigator.of(context).pop(selectedId),
                child: const Text('Назначить'),
              ),
            ],
          );
        },
      ),
    );

    searchController.dispose();
    if (result == null || result.isEmpty) {
      return;
    }

    await _run(
      () => _repo.assignRealtor(id: deal.id, realtorId: result),
      'Ответственный риелтор назначен.',
    );
  }

  Future<void> _showNoteDialog(DealWorkflow deal, {DealNote? note}) async {
    final controller = TextEditingController(text: note?.text ?? '');
    final formKey = GlobalKey<FormState>();

    final text = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(note == null ? 'Новая заметка' : 'Редактировать заметку'),
        content: Form(
          key: formKey,
          child: TextFormField(
            controller: controller,
            minLines: 3,
            maxLines: 5,
            decoration: const InputDecoration(labelText: 'Текст'),
            validator: (value) {
              if ((value ?? '').trim().isEmpty) {
                return 'Введите текст заметки.';
              }
              return null;
            },
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(),
            child: const Text('Отмена'),
          ),
          FilledButton(
            onPressed: () {
              if (formKey.currentState!.validate()) {
                Navigator.of(context).pop(controller.text.trim());
              }
            },
            child: const Text('Сохранить'),
          ),
        ],
      ),
    );

    controller.dispose();
    if (text == null) {
      return;
    }

    await _run(() async {
      if (note == null) {
        await _repo.addNote(id: deal.id, text: text);
      } else {
        await _repo.updateNote(id: deal.id, noteId: note.id, text: text);
      }
    }, note == null ? 'Заметка добавлена.' : 'Заметка обновлена.');
  }

  Future<void> _showDocumentDialog(DealWorkflow deal) async {
    final titleController = TextEditingController();
    final formKey = GlobalKey<FormState>();
    PlatformFile? selectedFile;

    final result = await showDialog<({String title, PlatformFile file})>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setState) {
          return AlertDialog(
            title: const Text('Добавить документ'),
            content: Form(
              key: formKey,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  TextFormField(
                    controller: titleController,
                    decoration: const InputDecoration(
                      labelText: 'Название документа',
                    ),
                    validator: (value) {
                      if ((value ?? '').trim().isEmpty) {
                        return 'Введите название документа.';
                      }
                      return null;
                    },
                  ),
                  const SizedBox(height: 12),
                  OutlinedButton.icon(
                    onPressed: () async {
                      final picked = await FilePicker.platform.pickFiles(
                        allowMultiple: false,
                        type: FileType.custom,
                        allowedExtensions: const [
                          'pdf',
                          'doc',
                          'docx',
                          'jpg',
                          'jpeg',
                          'png',
                          'webp',
                        ],
                      );
                      final file = picked?.files.single;
                      if (file == null) {
                        return;
                      }
                      if (file.size > 5 * 1024 * 1024) {
                        _showMessage(
                          'Размер документа не должен превышать 5 МБ.',
                          isError: true,
                        );
                        return;
                      }
                      if (file.path == null || file.path!.isEmpty) {
                        _showMessage(
                          'Не удалось получить выбранный файл.',
                          isError: true,
                        );
                        return;
                      }
                      setState(() {
                        selectedFile = file;
                      });
                    },
                    icon: const Icon(Icons.attach_file),
                    label: const Text('Выбрать файл'),
                  ),
                  if (selectedFile != null) ...[
                    const SizedBox(height: 8),
                    Text(
                      '${selectedFile!.name} · ${formatDealDocumentSize(selectedFile!.size)}',
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                  ],
                  const SizedBox(height: 8),
                  Text(
                    'Доступны PDF, Word и изображения. Максимальный размер 5 МБ.',
                    style: Theme.of(context).textTheme.bodySmall,
                  ),
                ],
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(context).pop(),
                child: const Text('Отмена'),
              ),
              FilledButton(
                onPressed: () {
                  if (!formKey.currentState!.validate()) {
                    return;
                  }
                  final file = selectedFile;
                  if (file == null) {
                    _showMessage('Выберите документ.', isError: true);
                    return;
                  }
                  Navigator.of(
                    context,
                  ).pop((title: titleController.text.trim(), file: file));
                },
                child: const Text('Добавить'),
              ),
            ],
          );
        },
      ),
    );

    titleController.dispose();
    if (result == null || result.file.path == null) {
      return;
    }

    setState(() => _documentsBusy = true);
    try {
      await _repo.uploadDocument(
        dealId: deal.id,
        title: result.title,
        filePath: result.file.path!,
        fileName: result.file.name,
      );
      _showMessage('Документ добавлен.');
      _reloadDocuments();
    } catch (error) {
      _showMessage(_messageFrom(error), isError: true);
    } finally {
      if (mounted) {
        setState(() => _documentsBusy = false);
      }
    }
  }

  Future<void> _openDocument(DealWorkflow deal, DealDocument document) async {
    setState(() => _documentsBusy = true);
    try {
      final path = await _repo.downloadDocument(
        dealId: deal.id,
        document: document,
      );
      final result = await OpenFilex.open(path);
      if (result.type != ResultType.done) {
        _showMessage('Не удалось открыть документ.', isError: true);
      }
      _reloadDocuments();
    } catch (error) {
      _showMessage(_messageFrom(error), isError: true);
    } finally {
      if (mounted) {
        setState(() => _documentsBusy = false);
      }
    }
  }

  String _messageFrom(Object error) {
    if (error is ApiException) {
      return error.message;
    }

    return 'Не удалось выполнить действие.';
  }

  void _showMessage(String message, {bool isError = false}) {
    if (!mounted) {
      return;
    }

    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: isError ? Theme.of(context).colorScheme.error : null,
      ),
    );
  }
}

class _DealInfoCard extends StatelessWidget {
  const _DealInfoCard({required this.deal});

  final DealWorkflow deal;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    'Информация',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                ),
                Chip(label: Text(dealStatusLabel(deal.status))),
              ],
            ),
            _Line(label: 'Источник', value: dealSourceLabel(deal.source)),
            _Line(label: 'Создана', value: _formatDate(deal.createdDate)),
            _Line(
              label: 'Ответственный',
              value: deal.realtorFullName?.isNotEmpty == true
                  ? deal.realtorFullName!
                  : 'Не назначен',
            ),
            if (deal.requestMessage?.isNotEmpty == true)
              _Line(label: 'Комментарий', value: deal.requestMessage!),
            _Line(label: 'ID', value: deal.id),
          ],
        ),
      ),
    );
  }
}

class _ClientCard extends StatelessWidget {
  const _ClientCard({required this.deal});

  final DealWorkflow deal;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Клиент', style: Theme.of(context).textTheme.titleMedium),
            _Line(label: 'ФИО', value: deal.clientFullName),
            _Line(label: 'Телефон', value: deal.clientPhoneNumber),
            _Line(label: 'Email', value: deal.clientEmail ?? '-'),
          ],
        ),
      ),
    );
  }
}

class _PropertyCard extends StatelessWidget {
  const _PropertyCard({
    required this.deal,
    required this.onOpenProperty,
    required this.onEditProperty,
  });

  final DealWorkflow deal;
  final VoidCallback? onOpenProperty;
  final VoidCallback? onEditProperty;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              deal.isSaleRequest ? 'Заявка на продажу' : 'Объект',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            _Line(
              label: 'Название',
              value: deal.propertyTitle?.isNotEmpty == true
                  ? deal.propertyTitle!
                  : 'Не указан',
            ),
            if (deal.clientRequirementId?.isNotEmpty == true)
              _Line(label: 'Требование', value: deal.clientRequirementId!),
            if (onOpenProperty != null || onEditProperty != null) ...[
              const SizedBox(height: 12),
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  if (onOpenProperty != null)
                    FilledButton.tonalIcon(
                      onPressed: onOpenProperty,
                      icon: const Icon(Icons.home_work_outlined),
                      label: const Text('Открыть объект'),
                    ),
                  if (onEditProperty != null)
                    OutlinedButton.icon(
                      onPressed: onEditProperty,
                      icon: const Icon(Icons.edit_outlined),
                      label: const Text('Редактировать'),
                    ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _ActionsCard extends StatelessWidget {
  const _ActionsCard({
    required this.deal,
    required this.isAdmin,
    required this.isRealtor,
    required this.isBusy,
    required this.onAccept,
    required this.onReject,
    required this.onRelease,
    required this.onCancel,
    required this.onComplete,
    required this.onAssign,
  });

  final DealWorkflow deal;
  final bool isAdmin;
  final bool isRealtor;
  final bool isBusy;
  final VoidCallback onAccept;
  final VoidCallback onReject;
  final VoidCallback onRelease;
  final VoidCallback onCancel;
  final VoidCallback? onComplete;
  final VoidCallback? onAssign;

  @override
  Widget build(BuildContext context) {
    final buttons = <Widget>[];

    if (isRealtor && deal.isIncoming && deal.isCreated) {
      buttons.addAll([
        FilledButton.icon(
          onPressed: isBusy ? null : onAccept,
          icon: const Icon(Icons.check),
          label: const Text('Принять'),
        ),
        OutlinedButton.icon(
          onPressed: isBusy ? null : onReject,
          icon: const Icon(Icons.close),
          label: const Text('Отклонить'),
        ),
      ]);
    }

    if (deal.isInProgress) {
      if (onComplete != null) {
        buttons.add(
          FilledButton.tonalIcon(
            onPressed: isBusy ? null : onComplete,
            icon: const Icon(Icons.task_alt),
            label: const Text('Завершить'),
          ),
        );
      }

      buttons.add(
        OutlinedButton.icon(
          onPressed: isBusy ? null : onCancel,
          icon: const Icon(Icons.cancel_outlined),
          label: Text(deal.isSaleRequest ? 'Отменить заявку' : 'Отменить'),
        ),
      );

      if (isRealtor) {
        buttons.add(
          OutlinedButton.icon(
            onPressed: isBusy ? null : onRelease,
            icon: const Icon(Icons.undo),
            label: const Text('Освободить'),
          ),
        );
      }
    }

    if (isAdmin) {
      buttons.add(
        OutlinedButton.icon(
          onPressed: isBusy ? null : onAssign,
          icon: const Icon(Icons.assignment_ind_outlined),
          label: const Text('Назначить риелтора'),
        ),
      );
    }

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Действия', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 12),
            if (isBusy) const LinearProgressIndicator(),
            if (isBusy) const SizedBox(height: 12),
            if (buttons.isEmpty)
              const Text('Для текущего статуса нет доступных действий.')
            else
              for (final button in buttons) ...[
                button,
                const SizedBox(height: 8),
              ],
          ],
        ),
      ),
    );
  }
}

class _DocumentsCard extends StatelessWidget {
  const _DocumentsCard({
    required this.future,
    required this.isBusy,
    required this.onAdd,
    required this.onOpen,
    required this.onDelete,
    required this.messageFrom,
  });

  final Future<List<DealDocument>> future;
  final bool isBusy;
  final VoidCallback? onAdd;
  final ValueChanged<DealDocument> onOpen;
  final ValueChanged<DealDocument> onDelete;
  final String Function(Object error) messageFrom;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    'Документы',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                ),
                IconButton(
                  onPressed: isBusy ? null : onAdd,
                  icon: const Icon(Icons.add),
                  tooltip: 'Добавить документ',
                ),
              ],
            ),
            if (isBusy) ...[
              const LinearProgressIndicator(),
              const SizedBox(height: 10),
            ],
            FutureBuilder<List<DealDocument>>(
              future: future,
              builder: (context, snapshot) {
                if (snapshot.connectionState == ConnectionState.waiting) {
                  return const Padding(
                    padding: EdgeInsets.symmetric(vertical: 10),
                    child: LinearProgressIndicator(),
                  );
                }

                if (snapshot.hasError) {
                  return Text(messageFrom(snapshot.error!));
                }

                final documents = snapshot.data ?? const [];
                if (documents.isEmpty) {
                  return const Text('По заявке пока нет документов.');
                }

                return Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    for (final document in documents) ...[
                      const Divider(),
                      Text(
                        document.title,
                        style: Theme.of(context).textTheme.titleSmall,
                      ),
                      const SizedBox(height: 4),
                      Text(
                        '${document.originalFileName} · ${formatDealDocumentSize(document.fileSizeBytes)}',
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                      const SizedBox(height: 4),
                      Text(
                        '${document.uploadedByText} · ${_formatDate(document.createdDate)}',
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                      const SizedBox(height: 8),
                      Row(
                        mainAxisAlignment: MainAxisAlignment.end,
                        children: [
                          TextButton.icon(
                            onPressed: isBusy ? null : () => onOpen(document),
                            icon: const Icon(Icons.open_in_new),
                            label: const Text('Открыть'),
                          ),
                          IconButton(
                            onPressed: isBusy ? null : () => onDelete(document),
                            icon: const Icon(Icons.delete_outline),
                            color: Theme.of(context).colorScheme.error,
                            tooltip: 'Удалить',
                          ),
                        ],
                      ),
                    ],
                  ],
                );
              },
            ),
          ],
        ),
      ),
    );
  }
}

class _NotesCard extends StatelessWidget {
  const _NotesCard({
    required this.deal,
    required this.isBusy,
    required this.onAdd,
    required this.onEdit,
    required this.onDelete,
  });

  final DealWorkflow deal;
  final bool isBusy;
  final VoidCallback onAdd;
  final ValueChanged<DealNote> onEdit;
  final ValueChanged<DealNote> onDelete;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    'Заметки',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                ),
                IconButton(
                  onPressed: isBusy ? null : onAdd,
                  icon: const Icon(Icons.add),
                  tooltip: 'Добавить заметку',
                ),
              ],
            ),
            if (deal.notes.isEmpty)
              const Text('Пока нет заметок по заявке.')
            else
              for (final note in deal.notes) ...[
                const Divider(),
                Text(note.text),
                const SizedBox(height: 6),
                Text(
                  _formatDate(note.createdDate),
                  style: Theme.of(context).textTheme.bodySmall,
                ),
                Row(
                  mainAxisAlignment: MainAxisAlignment.end,
                  children: [
                    IconButton(
                      onPressed: isBusy ? null : () => onEdit(note),
                      icon: const Icon(Icons.edit_outlined),
                      tooltip: 'Редактировать',
                    ),
                    IconButton(
                      onPressed: isBusy ? null : () => onDelete(note),
                      icon: const Icon(Icons.delete_outline),
                      tooltip: 'Удалить',
                    ),
                  ],
                ),
              ],
          ],
        ),
      ),
    );
  }
}

class _Line extends StatelessWidget {
  const _Line({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: 8),
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

class _LoadError extends StatelessWidget {
  const _LoadError({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.error_outline, size: 42),
            const SizedBox(height: 12),
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 12),
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

String _formatDate(DateTime date) {
  return DateFormat('dd.MM.yyyy HH:mm').format(date.toLocal());
}
