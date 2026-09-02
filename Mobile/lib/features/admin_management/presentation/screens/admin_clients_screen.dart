import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../core/errors/api_exception.dart';
import '../../data/repository/admin_management_repository.dart';
import '../../domain/models/admin_management_models.dart';

class AdminClientsScreen extends ConsumerStatefulWidget {
  const AdminClientsScreen({super.key});

  @override
  ConsumerState<AdminClientsScreen> createState() => _AdminClientsScreenState();
}

class _AdminClientsScreenState extends ConsumerState<AdminClientsScreen> {
  late Future<List<AdminClient>> _future;
  String _search = '';

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
          _Header(
            title: 'Клиенты',
            onRefresh: _reload,
            action: FilledButton.icon(
              onPressed: () => _openClientDialog(),
              icon: const Icon(Icons.add),
              label: const Text('Создать'),
            ),
          ),
          const SizedBox(height: 12),
          TextField(
            decoration: const InputDecoration(
              labelText: 'Поиск',
              hintText: 'ФИО, email, телефон',
              prefixIcon: Icon(Icons.search),
            ),
            onChanged: (value) => setState(() => _search = value),
          ),
          const SizedBox(height: 12),
          FutureBuilder<List<AdminClient>>(
            future: _future,
            builder: (context, snapshot) {
              if (snapshot.connectionState == ConnectionState.waiting) {
                return const _LoadingBlock();
              }
              if (snapshot.hasError) {
                return _MessageCard(text: _messageFrom(snapshot.error!));
              }
              final items = _filter(snapshot.data ?? const []);
              if (items.isEmpty) {
                return const _MessageCard(text: 'Клиенты не найдены.');
              }

              return Column(
                children: [
                  for (final client in items) ...[
                    _ClientCard(
                      client: client,
                      onUpdatePhone: () => _openPhoneDialog(client),
                      onDelete: () => _deleteClient(client),
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

  Future<List<AdminClient>> _load() {
    return ref.read(adminManagementRepositoryProvider).getClients();
  }

  List<AdminClient> _filter(List<AdminClient> items) {
    final query = _search.trim().toLowerCase();
    if (query.isEmpty) {
      return items;
    }
    final digits = _digits(query);
    return items.where((item) {
      final text = '${item.fullName} ${item.email ?? ''} ${item.phoneNumber}'
          .toLowerCase();
      return text.contains(query) ||
          (digits.isNotEmpty && _digits(item.phoneNumber).contains(digits));
    }).toList();
  }

  Future<void> _openClientDialog() async {
    final result = await showModalBottomSheet<_ClientFormResult>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.zero),
      builder: (context) => const _ClientFormSheet(),
    );
    if (result == null) {
      return;
    }
    await _runAction(
      () => ref
          .read(adminManagementRepositoryProvider)
          .createClient(
            firstName: result.firstName,
            lastName: result.lastName,
            middleName: result.middleName,
            phoneNumber: result.phoneNumber,
            email: result.email,
          ),
      'Клиент создан.',
    );
  }

  Future<void> _openPhoneDialog(AdminClient client) async {
    final controller = TextEditingController(text: client.phoneNumber);
    final formKey = GlobalKey<FormState>();
    final value = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Обновить телефон'),
        content: Form(
          key: formKey,
          child: TextFormField(
            controller: controller,
            decoration: const InputDecoration(labelText: 'Телефон'),
            keyboardType: TextInputType.phone,
            validator: _phoneValidator,
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
    if (value == null) {
      return;
    }
    await _runAction(
      () => ref
          .read(adminManagementRepositoryProvider)
          .updateClientPhone(clientId: client.id, phoneNumber: value),
      'Телефон обновлен.',
    );
  }

  Future<void> _deleteClient(AdminClient client) async {
    final confirmed = await _confirm(
      context,
      title: 'Удалить клиента?',
      text: client.fullName,
      danger: true,
    );
    if (!confirmed) {
      return;
    }
    await _runAction(
      () => ref.read(adminManagementRepositoryProvider).deleteClient(client.id),
      'Клиент удален.',
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

class _ClientCard extends StatelessWidget {
  const _ClientCard({
    required this.client,
    required this.onUpdatePhone,
    required this.onDelete,
  });

  final AdminClient client;
  final VoidCallback onUpdatePhone;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    final date = DateFormat('dd.MM.yyyy').format(client.createdDate.toLocal());
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              client.fullName,
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 4),
            Text('Создан: $date', style: Theme.of(context).textTheme.bodySmall),
            const SizedBox(height: 8),
            _InfoRow(icon: Icons.phone_outlined, text: client.phoneNumber),
            _InfoRow(
              icon: Icons.mail_outline,
              text: client.email?.trim().isNotEmpty == true
                  ? client.email!
                  : '-',
            ),
            const SizedBox(height: 8),
            Wrap(
              spacing: 8,
              children: [
                OutlinedButton.icon(
                  onPressed: onUpdatePhone,
                  icon: const Icon(Icons.edit_outlined),
                  label: const Text('Обновить'),
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

class _ClientFormSheet extends StatefulWidget {
  const _ClientFormSheet();

  @override
  State<_ClientFormSheet> createState() => _ClientFormSheetState();
}

class _ClientFormSheetState extends State<_ClientFormSheet> {
  final _formKey = GlobalKey<FormState>();
  final _firstName = TextEditingController();
  final _lastName = TextEditingController();
  final _middleName = TextEditingController();
  final _phone = TextEditingController();
  final _email = TextEditingController();

  @override
  void dispose() {
    _firstName.dispose();
    _lastName.dispose();
    _middleName.dispose();
    _phone.dispose();
    _email.dispose();
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
                'Создать клиента',
                style: Theme.of(context).textTheme.titleLarge,
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _lastName,
                decoration: const InputDecoration(labelText: 'Фамилия'),
                maxLength: 100,
                validator: _required,
              ),
              TextFormField(
                controller: _firstName,
                decoration: const InputDecoration(labelText: 'Имя'),
                maxLength: 100,
                validator: _required,
              ),
              TextFormField(
                controller: _middleName,
                decoration: const InputDecoration(labelText: 'Отчество'),
                maxLength: 100,
              ),
              TextFormField(
                controller: _phone,
                decoration: const InputDecoration(labelText: 'Телефон'),
                keyboardType: TextInputType.phone,
                maxLength: 32,
                validator: _phoneValidator,
              ),
              TextFormField(
                controller: _email,
                decoration: const InputDecoration(labelText: 'Email'),
                keyboardType: TextInputType.emailAddress,
                maxLength: 256,
                validator: _emailValidator,
              ),
              const SizedBox(height: 8),
              FilledButton(
                onPressed: () {
                  if (!_formKey.currentState!.validate()) {
                    return;
                  }
                  Navigator.of(context).pop(
                    _ClientFormResult(
                      firstName: _firstName.text,
                      lastName: _lastName.text,
                      middleName: _middleName.text,
                      phoneNumber: _phone.text,
                      email: _email.text,
                    ),
                  );
                },
                child: const Text('Создать'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ClientFormResult {
  const _ClientFormResult({
    required this.firstName,
    required this.lastName,
    required this.middleName,
    required this.phoneNumber,
    required this.email,
  });

  final String firstName;
  final String lastName;
  final String middleName;
  final String phoneNumber;
  final String email;
}

class _Header extends StatelessWidget {
  const _Header({required this.title, required this.onRefresh, this.action});

  final String title;
  final VoidCallback onRefresh;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: Text(title, style: Theme.of(context).textTheme.headlineSmall),
        ),
        ?action,
        IconButton(
          onPressed: onRefresh,
          icon: const Icon(Icons.refresh),
          tooltip: 'Обновить',
        ),
      ],
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 4),
      child: Row(
        children: [
          Icon(icon, size: 18, color: Theme.of(context).colorScheme.primary),
          const SizedBox(width: 8),
          Expanded(child: Text(text, overflow: TextOverflow.ellipsis)),
        ],
      ),
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

String _digits(String value) => value.replaceAll(RegExp(r'\D'), '');

String? _required(String? value) {
  return value == null || value.trim().isEmpty ? 'Заполните поле' : null;
}

String? _emailValidator(String? value) {
  final text = value?.trim() ?? '';
  if (text.isEmpty) {
    return null;
  }
  final regex = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');
  return regex.hasMatch(text) ? null : 'Введите корректный email';
}

String? _phoneValidator(String? value) {
  final text = value?.trim() ?? '';
  if (text.isEmpty) {
    return 'Укажите телефон';
  }
  final plusCount = '+'.allMatches(text).length;
  final validChars = RegExp(r'^[0-9+\- ()]+$').hasMatch(text);
  final digits = _digits(text).length;
  if (plusCount > 1 ||
      (plusCount == 1 && !text.startsWith('+')) ||
      !validChars) {
    return 'Недопустимый формат телефона';
  }
  if (digits < 7 || digits > 15) {
    return 'В телефоне должно быть 7-15 цифр';
  }
  return null;
}
