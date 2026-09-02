import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../core/errors/api_exception.dart';
import '../../data/repository/admin_management_repository.dart';
import '../../domain/models/admin_management_models.dart';

class AdminCurrenciesScreen extends ConsumerStatefulWidget {
  const AdminCurrenciesScreen({super.key});

  @override
  ConsumerState<AdminCurrenciesScreen> createState() {
    return _AdminCurrenciesScreenState();
  }
}

class _AdminCurrenciesScreenState extends ConsumerState<AdminCurrenciesScreen> {
  late Future<List<AdminCurrency>> _future;
  bool _includeInactive = true;

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
                  'Валюты и курсы',
                  style: Theme.of(context).textTheme.headlineSmall,
                ),
              ),
              FilledButton.icon(
                onPressed: () => _openCurrencyDialog(),
                icon: const Icon(Icons.add),
                label: const Text('Создать'),
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
            child: SwitchListTile(
              title: const Text('Показывать неактивные валюты'),
              value: _includeInactive,
              onChanged: (value) {
                setState(() {
                  _includeInactive = value;
                  _future = _load();
                });
              },
            ),
          ),
          const SizedBox(height: 12),
          FutureBuilder<List<AdminCurrency>>(
            future: _future,
            builder: (context, snapshot) {
              if (snapshot.connectionState == ConnectionState.waiting) {
                return const _LoadingBlock();
              }
              if (snapshot.hasError) {
                return _MessageCard(text: _messageFrom(snapshot.error!));
              }
              final items = snapshot.data ?? const [];
              if (items.isEmpty) {
                return const _MessageCard(text: 'Валюты не найдены.');
              }

              return Column(
                children: [
                  for (final item in items) ...[
                    _CurrencyCard(
                      item: item,
                      onEdit: () => _openCurrencyDialog(item: item),
                      onToggle: () => _setActive(item, !item.isActive),
                      onDelete: () => _delete(item),
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

  Future<List<AdminCurrency>> _load() {
    return ref
        .read(adminManagementRepositoryProvider)
        .getCurrencies(includeInactive: _includeInactive);
  }

  Future<void> _openCurrencyDialog({AdminCurrency? item}) async {
    final result = await showModalBottomSheet<_CurrencyFormResult>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.zero),
      builder: (context) => _CurrencyFormSheet(item: item),
    );
    if (result == null) {
      return;
    }

    await _runAction(() {
      final repo = ref.read(adminManagementRepositoryProvider);
      if (item == null) {
        return repo.createCurrency(
          code: result.code,
          name: result.name,
          symbol: result.symbol,
          rateToBase: result.rateToBase,
          isActive: result.isActive,
        );
      }
      return repo.updateCurrency(
        currencyId: item.id,
        code: result.code,
        name: result.name,
        symbol: result.symbol,
        rateToBase: result.rateToBase,
        isActive: result.isActive,
      );
    }, item == null ? 'Валюта создана.' : 'Валюта обновлена.');
  }

  Future<void> _setActive(AdminCurrency item, bool isActive) async {
    await _runAction(
      () => ref
          .read(adminManagementRepositoryProvider)
          .setCurrencyActive(currencyId: item.id, isActive: isActive),
      isActive ? 'Валюта активирована.' : 'Валюта деактивирована.',
    );
  }

  Future<void> _delete(AdminCurrency item) async {
    final confirmed = await _confirm(
      context,
      title: 'Удалить валюту?',
      text: '${item.code} · ${item.name}',
      danger: true,
    );
    if (!confirmed) {
      return;
    }
    await _runAction(
      () => ref.read(adminManagementRepositoryProvider).deleteCurrency(item.id),
      'Валюта удалена.',
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

class _CurrencyCard extends StatelessWidget {
  const _CurrencyCard({
    required this.item,
    required this.onEdit,
    required this.onToggle,
    required this.onDelete,
  });

  final AdminCurrency item;
  final VoidCallback onEdit;
  final VoidCallback onToggle;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    final date = DateFormat(
      'dd.MM.yyyy HH:mm',
    ).format(item.updatedAtUtc.toLocal());
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
                    '${item.code} · ${item.name}',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                ),
                Chip(
                  label: Text(item.isActive ? 'Активна' : 'Неактивна'),
                  backgroundColor: item.isActive
                      ? Theme.of(context).colorScheme.primaryContainer
                      : Theme.of(context).colorScheme.surfaceContainerHighest,
                ),
              ],
            ),
            const SizedBox(height: 8),
            Text('Символ: ${item.symbol}'),
            Text('Курс к базовой валюте USD: ${_rateText(item.rateToBase)}'),
            Text(
              '1 ${item.code} = ${_rateText(item.rateToBase)} USD',
              style: Theme.of(context).textTheme.bodySmall,
            ),
            Text(
              'Обновлена: $date',
              style: Theme.of(context).textTheme.bodySmall,
            ),
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
                  onPressed: onToggle,
                  icon: Icon(
                    item.isActive ? Icons.block : Icons.check_circle_outline,
                  ),
                  label: Text(
                    item.isActive ? 'Деактивировать' : 'Активировать',
                  ),
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

  String _rateText(double value) {
    return value
        .toStringAsFixed(6)
        .replaceFirst(RegExp(r'0+$'), '')
        .replaceFirst(RegExp(r'\.$'), '');
  }
}

class _CurrencyFormSheet extends StatefulWidget {
  const _CurrencyFormSheet({this.item});

  final AdminCurrency? item;

  @override
  State<_CurrencyFormSheet> createState() => _CurrencyFormSheetState();
}

class _CurrencyFormSheetState extends State<_CurrencyFormSheet> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _code;
  late final TextEditingController _name;
  late final TextEditingController _symbol;
  late final TextEditingController _rate;
  late bool _isActive;

  @override
  void initState() {
    super.initState();
    final item = widget.item;
    _code = TextEditingController(text: item?.code ?? '');
    _name = TextEditingController(text: item?.name ?? '');
    _symbol = TextEditingController(text: item?.symbol ?? '');
    _rate = TextEditingController(
      text: item == null ? '' : '${item.rateToBase}',
    );
    _isActive = item?.isActive ?? true;
  }

  @override
  void dispose() {
    _code.dispose();
    _name.dispose();
    _symbol.dispose();
    _rate.dispose();
    super.dispose();
  }

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
                widget.item == null ? 'Создать валюту' : 'Изменить валюту',
                style: Theme.of(context).textTheme.titleLarge,
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _code,
                decoration: const InputDecoration(labelText: 'Код'),
                textCapitalization: TextCapitalization.characters,
                maxLength: 3,
                validator: _currencyCodeValidator,
              ),
              TextFormField(
                controller: _name,
                decoration: const InputDecoration(labelText: 'Название'),
                maxLength: 100,
                validator: _required,
              ),
              TextFormField(
                controller: _symbol,
                decoration: const InputDecoration(labelText: 'Символ'),
                maxLength: 10,
                validator: _required,
              ),
              TextFormField(
                controller: _rate,
                decoration: const InputDecoration(
                  labelText: 'Курс к базовой валюте USD',
                  helperText: 'Например: 1 EUR = 1.08 USD',
                ),
                keyboardType: const TextInputType.numberWithOptions(
                  decimal: true,
                ),
                validator: _rateValidator,
              ),
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Активна'),
                value: _isActive,
                onChanged: (value) => setState(() => _isActive = value),
              ),
              FilledButton(
                onPressed: () {
                  if (!_formKey.currentState!.validate()) {
                    return;
                  }
                  Navigator.of(context).pop(
                    _CurrencyFormResult(
                      code: _code.text,
                      name: _name.text,
                      symbol: _symbol.text,
                      rateToBase: double.parse(_rate.text.replaceAll(',', '.')),
                      isActive: _isActive,
                    ),
                  );
                },
                child: const Text('Сохранить'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _CurrencyFormResult {
  const _CurrencyFormResult({
    required this.code,
    required this.name,
    required this.symbol,
    required this.rateToBase,
    required this.isActive,
  });

  final String code;
  final String name;
  final String symbol;
  final double rateToBase;
  final bool isActive;
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

String? _currencyCodeValidator(String? value) {
  final text = value?.trim() ?? '';
  if (!RegExp(r'^[A-Za-z]{3}$').hasMatch(text)) {
    return 'Код должен состоять из 3 латинских букв';
  }
  return null;
}

String? _rateValidator(String? value) {
  final parsed = double.tryParse((value ?? '').trim().replaceAll(',', '.'));
  if (parsed == null) {
    return 'Введите число';
  }
  if (parsed <= 0) {
    return 'Курс должен быть больше 0';
  }
  return null;
}
