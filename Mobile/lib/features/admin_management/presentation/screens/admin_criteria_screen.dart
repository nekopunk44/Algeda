import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/api_exception.dart';
import '../../data/repository/admin_management_repository.dart';
import '../../domain/models/admin_management_models.dart';

class AdminCriteriaScreen extends ConsumerStatefulWidget {
  const AdminCriteriaScreen({super.key});

  @override
  ConsumerState<AdminCriteriaScreen> createState() =>
      _AdminCriteriaScreenState();
}

class _AdminCriteriaScreenState extends ConsumerState<AdminCriteriaScreen> {
  late Future<_CriteriaPayload> _future;
  final _searchController = TextEditingController();
  int _page = 1;
  int _pageSize = 20;
  bool _includeHidden = true;
  String _sort = 'name_asc';

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
                  'Критерии недвижимости',
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
          const SizedBox(height: 8),
          Align(
            alignment: Alignment.centerLeft,
            child: FilledButton.icon(
              onPressed: () => _openCriterionSheet(),
              icon: const Icon(Icons.add),
              label: const Text('Создать'),
            ),
          ),
          const SizedBox(height: 12),
          _FiltersCard(
            searchController: _searchController,
            sort: _sort,
            pageSize: _pageSize,
            includeHidden: _includeHidden,
            onSortChanged: (value) => setState(() {
              _sort = value;
              _page = 1;
              _future = _load();
            }),
            onPageSizeChanged: (value) => setState(() {
              _pageSize = value;
              _page = 1;
              _future = _load();
            }),
            onIncludeHiddenChanged: (value) => setState(() {
              _includeHidden = value;
              _page = 1;
              _future = _load();
            }),
            onSearch: () => setState(() {
              _page = 1;
              _future = _load();
            }),
          ),
          const SizedBox(height: 12),
          FutureBuilder<_CriteriaPayload>(
            future: _future,
            builder: (context, snapshot) {
              if (snapshot.connectionState == ConnectionState.waiting) {
                return const _LoadingBlock();
              }
              if (snapshot.hasError) {
                return _MessageCard(text: _messageFrom(snapshot.error!));
              }
              final payload = snapshot.data!;
              final page = payload.page;
              if (page.items.isEmpty) {
                return const _MessageCard(text: 'Критерии не найдены.');
              }
              return Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Padding(
                    padding: const EdgeInsets.only(bottom: 8),
                    child: Text('Всего: ${page.totalCount}'),
                  ),
                  for (final item in page.items) ...[
                    _CriterionCard(
                      item: item,
                      onEdit: () => _openCriterionSheet(item: item),
                      onHiddenToggle: () => _setHidden(item, !item.isHidden),
                      onDelete: () => _delete(item),
                    ),
                    const SizedBox(height: 8),
                  ],
                  if (page.totalPages > 1)
                    _Pagination(
                      page: page.page,
                      totalPages: page.totalPages,
                      onChanged: (value) => setState(() {
                        _page = value;
                        _future = _load();
                      }),
                    ),
                ],
              );
            },
          ),
        ],
      ),
    );
  }

  Future<_CriteriaPayload> _load() async {
    final repo = ref.read(adminManagementRepositoryProvider);
    final page = await repo.getCriteriaPage(
      page: _page,
      pageSize: _pageSize,
      includeHidden: _includeHidden,
      search: _searchController.text,
      sort: _sort,
    );
    final all = await repo.getCriteria(includeHidden: true);
    return _CriteriaPayload(page: page, all: all);
  }

  Future<void> _openCriterionSheet({AdminCriterion? item}) async {
    final payload = await _future.catchError((_) => const _CriteriaPayload());
    if (!mounted) {
      return;
    }
    final result = await showModalBottomSheet<AdminCriterionFormData>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.zero),
      builder: (context) =>
          _CriterionFormSheet(item: item, existing: payload.all),
    );
    if (result == null) {
      return;
    }
    await _runAction(() {
      final repo = ref.read(adminManagementRepositoryProvider);
      if (item == null) {
        return repo.createCriterion(result);
      }
      return repo.updateCriterion(criterionId: item.id, data: result);
    }, item == null ? 'Критерий создан.' : 'Критерий обновлен.');
  }

  Future<void> _setHidden(AdminCriterion item, bool isHidden) async {
    await _runAction(
      () => ref
          .read(adminManagementRepositoryProvider)
          .setCriterionHidden(criterionId: item.id, isHidden: isHidden),
      isHidden ? 'Критерий скрыт.' : 'Критерий отображается.',
    );
  }

  Future<void> _delete(AdminCriterion item) async {
    final confirmed = await _confirm(
      context,
      title: 'Удалить критерий?',
      text: '${item.displayName}\n${item.code}',
      danger: true,
    );
    if (!confirmed) {
      return;
    }
    await _runAction(
      () =>
          ref.read(adminManagementRepositoryProvider).deleteCriterion(item.id),
      'Критерий удален.',
    );
  }

  Future<void> _runAction(
    Future<void> Function() action,
    String success,
  ) async {
    try {
      await action();
      _showSnack(success);
      _reload();
    } catch (error) {
      _showSnack(_messageFrom(error));
    }
  }

  void _reload() {
    setState(() => _future = _load());
  }

  void _showSnack(String message) {
    if (!mounted) {
      return;
    }
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(SnackBar(content: Text(message)));
  }
}

class _CriteriaPayload {
  const _CriteriaPayload({
    this.page = const AdminCriterionPage(
      page: 1,
      pageSize: 20,
      totalCount: 0,
      items: [],
    ),
    this.all = const [],
  });

  final AdminCriterionPage page;
  final List<AdminCriterion> all;
}

class _FiltersCard extends StatelessWidget {
  const _FiltersCard({
    required this.searchController,
    required this.sort,
    required this.pageSize,
    required this.includeHidden,
    required this.onSortChanged,
    required this.onPageSizeChanged,
    required this.onIncludeHiddenChanged,
    required this.onSearch,
  });

  final TextEditingController searchController;
  final String sort;
  final int pageSize;
  final bool includeHidden;
  final ValueChanged<String> onSortChanged;
  final ValueChanged<int> onPageSizeChanged;
  final ValueChanged<bool> onIncludeHiddenChanged;
  final VoidCallback onSearch;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          children: [
            TextField(
              controller: searchController,
              decoration: const InputDecoration(
                labelText: 'Поиск',
                hintText: 'Код, название, категория',
                prefixIcon: Icon(Icons.search),
              ),
              onSubmitted: (_) => onSearch(),
            ),
            const SizedBox(height: 8),
            DropdownButtonFormField<String>(
              initialValue: sort,
              isExpanded: true,
              decoration: const InputDecoration(labelText: 'Сортировка'),
              items: const [
                DropdownMenuItem(
                  value: 'name_asc',
                  child: Text('По названию (А-Я)'),
                ),
                DropdownMenuItem(
                  value: 'name_desc',
                  child: Text('По названию (Я-А)'),
                ),
                DropdownMenuItem(
                  value: 'created_desc',
                  child: Text('Сначала новые'),
                ),
                DropdownMenuItem(
                  value: 'created_asc',
                  child: Text('Сначала старые'),
                ),
                DropdownMenuItem(
                  value: 'code_asc',
                  child: Text('По коду (А-Я)'),
                ),
                DropdownMenuItem(
                  value: 'code_desc',
                  child: Text('По коду (Я-А)'),
                ),
              ],
              onChanged: (value) => onSortChanged(value ?? 'name_asc'),
            ),
            const SizedBox(height: 8),
            DropdownButtonFormField<int>(
              initialValue: pageSize,
              decoration: const InputDecoration(labelText: 'На странице'),
              items: const [
                DropdownMenuItem(value: 10, child: Text('10')),
                DropdownMenuItem(value: 20, child: Text('20')),
                DropdownMenuItem(value: 50, child: Text('50')),
              ],
              onChanged: (value) => onPageSizeChanged(value ?? 20),
            ),
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('Показывать скрытые'),
              value: includeHidden,
              onChanged: onIncludeHiddenChanged,
            ),
            Align(
              alignment: Alignment.centerRight,
              child: FilledButton.icon(
                onPressed: onSearch,
                icon: const Icon(Icons.search),
                label: const Text('Найти'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _CriterionCard extends StatelessWidget {
  const _CriterionCard({
    required this.item,
    required this.onEdit,
    required this.onHiddenToggle,
    required this.onDelete,
  });

  final AdminCriterion item;
  final VoidCallback onEdit;
  final VoidCallback onHiddenToggle;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    item.displayName,
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                ),
                if (item.isHidden) const Chip(label: Text('Скрыт')),
              ],
            ),
            const SizedBox(height: 4),
            Text('${item.code} · ${criterionValueTypeLabel(item.valueType)}'),
            Text(
              'Категория: ${item.category?.trim().isNotEmpty == true ? item.category : '-'}',
            ),
            Text('Опций: ${item.options.length}'),
            if (item.description?.trim().isNotEmpty == true) ...[
              const SizedBox(height: 6),
              Text(item.description!),
            ],
            const SizedBox(height: 8),
            Wrap(
              spacing: 8,
              children: [
                OutlinedButton.icon(
                  onPressed: onEdit,
                  icon: const Icon(Icons.edit_outlined),
                  label: const Text('Изменить'),
                ),
                OutlinedButton.icon(
                  onPressed: onHiddenToggle,
                  icon: Icon(
                    item.isHidden
                        ? Icons.visibility_outlined
                        : Icons.visibility_off_outlined,
                  ),
                  label: Text(item.isHidden ? 'Отобразить' : 'Скрыть'),
                ),
                OutlinedButton.icon(
                  onPressed: onDelete,
                  icon: const Icon(Icons.delete_outline),
                  label: const Text('Удалить'),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: Theme.of(context).colorScheme.error,
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _CriterionFormSheet extends StatefulWidget {
  const _CriterionFormSheet({required this.existing, this.item});

  final AdminCriterion? item;
  final List<AdminCriterion> existing;

  @override
  State<_CriterionFormSheet> createState() => _CriterionFormSheetState();
}

class _CriterionFormSheetState extends State<_CriterionFormSheet> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _code;
  late final TextEditingController _displayName;
  late final TextEditingController _category;
  late final TextEditingController _description;
  late String _valueType;
  late bool _isHidden;
  late List<_OptionDraft> _options;

  @override
  void initState() {
    super.initState();
    final item = widget.item;
    _code = TextEditingController(text: item?.code ?? '');
    _displayName = TextEditingController(text: item?.displayName ?? '');
    _category = TextEditingController(text: item?.category ?? '');
    _description = TextEditingController(text: item?.description ?? '');
    _valueType = item?.valueType ?? 'Boolean';
    _isHidden = item?.isHidden ?? false;
    _options = [
      for (final option in item?.options ?? const <AdminCriterionOption>[])
        _OptionDraft(option.value, option.label),
    ];
    if (_options.isEmpty) {
      _options.add(_OptionDraft('', ''));
    }
  }

  @override
  void dispose() {
    _code.dispose();
    _displayName.dispose();
    _category.dispose();
    _description.dispose();
    for (final option in _options) {
      option.dispose();
    }
    super.dispose();
  }

  bool get _usesOptions =>
      _valueType == 'SingleSelect' || _valueType == 'MultiSelect';

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Padding(
        padding: EdgeInsets.only(
          left: 16,
          right: 16,
          top: 16,
          bottom: MediaQuery.of(context).viewInsets.bottom + 16,
        ),
        child: Form(
          key: _formKey,
          child: ListView(
            shrinkWrap: true,
            children: [
              Text(
                widget.item == null ? 'Создать критерий' : 'Изменить критерий',
                style: Theme.of(context).textTheme.titleLarge,
              ),
              const SizedBox(height: 12),
              Row(
                children: [
                  Expanded(
                    child: TextFormField(
                      controller: _code,
                      decoration: const InputDecoration(labelText: 'Код'),
                      maxLength: 100,
                      validator: _codeValidator,
                    ),
                  ),
                  const SizedBox(width: 8),
                  IconButton.filledTonal(
                    onPressed: _generateCode,
                    icon: const Icon(Icons.auto_fix_high),
                    tooltip: 'Сгенерировать',
                  ),
                ],
              ),
              TextFormField(
                controller: _displayName,
                decoration: const InputDecoration(labelText: 'Название'),
                maxLength: 200,
                validator: _required,
              ),
              DropdownButtonFormField<String>(
                initialValue: _valueType,
                isExpanded: true,
                decoration: const InputDecoration(labelText: 'Тип значения'),
                items: [
                  for (final type in adminCriterionValueTypes)
                    DropdownMenuItem(
                      value: type,
                      child: Text(criterionValueTypeLabel(type)),
                    ),
                ],
                onChanged: (value) =>
                    setState(() => _valueType = value ?? 'Boolean'),
              ),
              const SizedBox(height: 8),
              TextFormField(
                controller: _category,
                decoration: const InputDecoration(labelText: 'Категория'),
                maxLength: 150,
              ),
              TextFormField(
                controller: _description,
                decoration: const InputDecoration(labelText: 'Описание'),
                maxLength: 2000,
                minLines: 1,
                maxLines: 3,
              ),
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Скрытый критерий'),
                value: _isHidden,
                onChanged: (value) => setState(() => _isHidden = value),
              ),
              if (_usesOptions) ...[
                const SizedBox(height: 8),
                Text(
                  'Опции для выбора',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 8),
                for (var i = 0; i < _options.length; i++) ...[
                  _OptionEditor(
                    index: i,
                    draft: _options[i],
                    canMoveUp: i > 0,
                    canMoveDown: i < _options.length - 1,
                    onRemove: () => _removeOption(i),
                    onMoveUp: () => _moveOption(i, -1),
                    onMoveDown: () => _moveOption(i, 1),
                  ),
                  const SizedBox(height: 8),
                ],
                Align(
                  alignment: Alignment.centerLeft,
                  child: OutlinedButton.icon(
                    onPressed: _addOption,
                    icon: const Icon(Icons.add),
                    label: const Text('Добавить опцию'),
                  ),
                ),
              ],
              const SizedBox(height: 12),
              FilledButton(onPressed: _submit, child: const Text('Сохранить')),
            ],
          ),
        ),
      ),
    );
  }

  void _generateCode() {
    _code.text = _buildCode(_displayName.text);
  }

  void _addOption() {
    setState(() => _options.add(_OptionDraft('', '')));
  }

  void _removeOption(int index) {
    if (_options.length == 1) {
      _options[index].value.clear();
      _options[index].label.clear();
      return;
    }
    setState(() => _options.removeAt(index).dispose());
  }

  void _moveOption(int index, int offset) {
    final target = index + offset;
    if (target < 0 || target >= _options.length) {
      return;
    }
    setState(() {
      final item = _options.removeAt(index);
      _options.insert(target, item);
    });
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    final duplicate = widget.existing.any(
      (item) =>
          item.id != widget.item?.id &&
          item.code.trim().toLowerCase() == _code.text.trim().toLowerCase(),
    );
    if (duplicate) {
      _showLocalSnack('Критерий с таким кодом уже существует.');
      return;
    }

    final options = _collectOptions();
    if (options == null) {
      return;
    }

    final data = AdminCriterionFormData(
      code: _code.text,
      displayName: _displayName.text,
      valueType: _valueType,
      category: _category.text,
      description: _description.text,
      isHidden: _isHidden,
      options: _usesOptions ? options : const [],
    );

    if (widget.item == null) {
      final similar = _findSimilar(data, options);
      if (similar != null) {
        final confirmed = await _confirm(
          context,
          title: 'Существует похожий критерий',
          text: '${similar.displayName}\n${similar.code}\n\nВсе равно создать?',
        );
        if (!confirmed) {
          return;
        }
      }
    }

    if (!mounted) {
      return;
    }
    Navigator.of(context).pop(data);
  }

  List<AdminCriterionOption>? _collectOptions() {
    if (!_usesOptions) {
      return const [];
    }

    final result = <AdminCriterionOption>[];
    for (var i = 0; i < _options.length; i++) {
      final value = _options[i].value.text.trim();
      final label = _options[i].label.text.trim();
      if (value.isEmpty && label.isEmpty) {
        continue;
      }
      if (value.isEmpty || label.isEmpty) {
        _showLocalSnack('Для каждой опции заполните значение и подпись.');
        return null;
      }
      if (value.length > 100 || label.length > 200) {
        _showLocalSnack('Проверьте длину значения и подписи опции.');
        return null;
      }
      result.add(
        AdminCriterionOption(value: value, label: label, sortOrder: i),
      );
    }

    if (result.isEmpty) {
      _showLocalSnack('Добавьте хотя бы одну опцию для типа выбора.');
      return null;
    }
    final unique = result.map((item) => item.value.toLowerCase()).toSet();
    if (unique.length != result.length) {
      _showLocalSnack('Значения опций не должны повторяться.');
      return null;
    }
    return result;
  }

  AdminCriterion? _findSimilar(
    AdminCriterionFormData data,
    List<AdminCriterionOption> options,
  ) {
    final currentWords = _words(
      '${data.displayName} ${data.description ?? ''} ${options.map((e) => '${e.value} ${e.label}').join(' ')}',
    );
    if (currentWords.isEmpty) {
      return null;
    }

    AdminCriterion? best;
    var bestScore = 0.0;
    for (final item in widget.existing) {
      final score = _similarityScore(data, options, item, currentWords);
      if (score > bestScore) {
        bestScore = score;
        best = item;
      }
    }
    return bestScore >= 0.58 ? best : null;
  }

  double _similarityScore(
    AdminCriterionFormData current,
    List<AdminCriterionOption> options,
    AdminCriterion existing,
    Set<String> currentWords,
  ) {
    final nameA = _normalize(current.displayName);
    final nameB = _normalize(existing.displayName);
    if (nameA.isNotEmpty && nameA == nameB) {
      return 1;
    }
    if (nameA.length >= 4 &&
        nameB.length >= 4 &&
        (nameA.contains(nameB) || nameB.contains(nameA))) {
      return 0.82;
    }
    final descriptionA = _normalize(current.description ?? '');
    final descriptionB = _normalize(existing.description ?? '');
    if (descriptionA.isNotEmpty && descriptionA == descriptionB) {
      return 0.75;
    }
    final existingWords = _words(
      '${existing.displayName} ${existing.description ?? ''} ${existing.options.map((e) => '${e.value} ${e.label}').join(' ')}',
    );
    return _wordOverlap(currentWords, existingWords);
  }

  void _showLocalSnack(String message) {
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(SnackBar(content: Text(message)));
  }
}

class _OptionDraft {
  _OptionDraft(String value, String label)
    : value = TextEditingController(text: value),
      label = TextEditingController(text: label);

  final TextEditingController value;
  final TextEditingController label;

  void dispose() {
    value.dispose();
    label.dispose();
  }
}

class _OptionEditor extends StatelessWidget {
  const _OptionEditor({
    required this.index,
    required this.draft,
    required this.canMoveUp,
    required this.canMoveDown,
    required this.onRemove,
    required this.onMoveUp,
    required this.onMoveDown,
  });

  final int index;
  final _OptionDraft draft;
  final bool canMoveUp;
  final bool canMoveDown;
  final VoidCallback onRemove;
  final VoidCallback onMoveUp;
  final VoidCallback onMoveDown;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        border: Border.all(color: Theme.of(context).dividerColor),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Column(
        children: [
          Row(
            children: [
              Expanded(child: Text('Опция ${index + 1}')),
              IconButton(
                onPressed: canMoveUp ? onMoveUp : null,
                icon: const Icon(Icons.arrow_upward),
                tooltip: 'Выше',
              ),
              IconButton(
                onPressed: canMoveDown ? onMoveDown : null,
                icon: const Icon(Icons.arrow_downward),
                tooltip: 'Ниже',
              ),
              IconButton(
                onPressed: onRemove,
                icon: const Icon(Icons.close),
                tooltip: 'Удалить',
              ),
            ],
          ),
          TextFormField(
            controller: draft.value,
            decoration: const InputDecoration(labelText: 'Значение'),
            maxLength: 100,
          ),
          TextFormField(
            controller: draft.label,
            decoration: const InputDecoration(labelText: 'Подпись'),
            maxLength: 200,
          ),
        ],
      ),
    );
  }
}

class _Pagination extends StatelessWidget {
  const _Pagination({
    required this.page,
    required this.totalPages,
    required this.onChanged,
  });

  final int page;
  final int totalPages;
  final ValueChanged<int> onChanged;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        IconButton(
          onPressed: page > 1 ? () => onChanged(page - 1) : null,
          icon: const Icon(Icons.chevron_left),
        ),
        Text('$page / $totalPages'),
        IconButton(
          onPressed: page < totalPages ? () => onChanged(page + 1) : null,
          icon: const Icon(Icons.chevron_right),
        ),
      ],
    );
  }
}

class _LoadingBlock extends StatelessWidget {
  const _LoadingBlock();

  @override
  Widget build(BuildContext context) {
    return const Padding(
      padding: EdgeInsets.only(top: 48),
      child: Center(child: CircularProgressIndicator()),
    );
  }
}

class _MessageCard extends StatelessWidget {
  const _MessageCard({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Text(text, textAlign: TextAlign.center),
      ),
    );
  }
}

Future<bool> _confirm(
  BuildContext context, {
  required String title,
  required String text,
  bool danger = false,
}) async {
  return await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: Text(title),
          content: Text(text),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(context).pop(false),
              child: const Text('Отмена'),
            ),
            FilledButton(
              onPressed: () => Navigator.of(context).pop(true),
              style: danger
                  ? FilledButton.styleFrom(
                      backgroundColor: Theme.of(context).colorScheme.error,
                      foregroundColor: Theme.of(context).colorScheme.onError,
                    )
                  : null,
              child: const Text('Да'),
            ),
          ],
        ),
      ) ??
      false;
}

String _messageFrom(Object error) {
  if (error is ApiException) {
    return error.message;
  }
  return 'Не удалось выполнить действие.';
}

String? _required(String? value) {
  return value == null || value.trim().isEmpty ? 'Заполните поле' : null;
}

String? _codeValidator(String? value) {
  final text = value?.trim() ?? '';
  if (text.isEmpty) {
    return 'Заполните поле';
  }
  if (!RegExp(r'^[A-Za-z0-9_-]+$').hasMatch(text)) {
    return 'Латиница, цифры, _ и -';
  }
  if (text.length > 100) {
    return 'Максимум 100 символов';
  }
  return null;
}

String _buildCode(String value) {
  final transliterated = value
      .toLowerCase()
      .split('')
      .map((char) => _translit[char] ?? char)
      .join()
      .replaceAll(RegExp(r'[^a-z0-9_-]+'), '_')
      .replaceAll(RegExp(r'_+'), '_')
      .replaceAll(RegExp(r'^_+|_+$'), '');
  return (transliterated.isEmpty ? 'criterion' : transliterated).substring(
    0,
    (transliterated.isEmpty ? 'criterion' : transliterated).length.clamp(
      0,
      100,
    ),
  );
}

String _normalize(String value) {
  return value
      .toLowerCase()
      .replaceAll('ё', 'е')
      .replaceAll(RegExp(r'[^a-zа-я0-9]+'), ' ')
      .trim()
      .replaceAll(RegExp(r'\s+'), ' ');
}

Set<String> _words(String value) {
  const stopWords = {
    'для',
    'или',
    'что',
    'это',
    'как',
    'при',
    'the',
    'and',
    'with',
  };
  return _normalize(value)
      .split(' ')
      .where((word) => word.length >= 3 && !stopWords.contains(word))
      .toSet();
}

double _wordOverlap(Set<String> left, Set<String> right) {
  if (left.isEmpty || right.isEmpty) {
    return 0;
  }
  final intersection = left.where(right.contains).length;
  final minSize = left.length < right.length ? left.length : right.length;
  return intersection / minSize;
}

const _translit = {
  'а': 'a',
  'б': 'b',
  'в': 'v',
  'г': 'g',
  'д': 'd',
  'е': 'e',
  'ё': 'e',
  'ж': 'zh',
  'з': 'z',
  'и': 'i',
  'й': 'y',
  'к': 'k',
  'л': 'l',
  'м': 'm',
  'н': 'n',
  'о': 'o',
  'п': 'p',
  'р': 'r',
  'с': 's',
  'т': 't',
  'у': 'u',
  'ф': 'f',
  'х': 'h',
  'ц': 'c',
  'ч': 'ch',
  'ш': 'sh',
  'щ': 'sch',
  'ъ': '',
  'ы': 'y',
  'ь': '',
  'э': 'e',
  'ю': 'yu',
  'я': 'ya',
};
