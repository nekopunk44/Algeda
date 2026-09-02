import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/api_exception.dart';
import '../../../../core/widgets/password_text_form_field.dart';
import '../../../profile/data/repository/profile_repository.dart';
import '../../data/repository/admin_management_repository.dart';
import '../../domain/models/admin_management_models.dart';

class AdminAccessScreen extends ConsumerStatefulWidget {
  const AdminAccessScreen({super.key});

  @override
  ConsumerState<AdminAccessScreen> createState() => _AdminAccessScreenState();
}

class _AdminAccessScreenState extends ConsumerState<AdminAccessScreen> {
  late Future<_AccessPayload> _future;
  final _searchController = TextEditingController();
  String? _role;

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
                  'Управление доступом',
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
                      hintText: 'Email или имя',
                      prefixIcon: Icon(Icons.search),
                    ),
                    onSubmitted: (_) => _reload(),
                  ),
                  const SizedBox(height: 8),
                  DropdownButtonFormField<String?>(
                    initialValue: _role,
                    isExpanded: true,
                    decoration: const InputDecoration(labelText: 'Роль'),
                    items: const [
                      DropdownMenuItem(value: null, child: Text('Все')),
                      DropdownMenuItem(value: 'Admin', child: Text('Admin')),
                      DropdownMenuItem(
                        value: 'SuperAdmin',
                        child: Text('SuperAdmin'),
                      ),
                      DropdownMenuItem(
                        value: 'Realtor',
                        child: Text('Realtor'),
                      ),
                      DropdownMenuItem(value: 'Client', child: Text('Client')),
                    ],
                    onChanged: (value) => setState(() {
                      _role = value;
                      _future = _load();
                    }),
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
          FutureBuilder<_AccessPayload>(
            future: _future,
            builder: (context, snapshot) {
              if (snapshot.connectionState == ConnectionState.waiting) {
                return const _LoadingBlock();
              }
              if (snapshot.hasError) {
                return _MessageCard(text: _messageFrom(snapshot.error!));
              }
              final payload = snapshot.data!;
              if (payload.users.isEmpty) {
                return const _MessageCard(text: 'Пользователи не найдены.');
              }
              return Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  for (final user in payload.users) ...[
                    _AccessUserCard(
                      user: user,
                      currentUserId: payload.currentUserId,
                      currentIsSuperAdmin: payload.currentIsSuperAdmin,
                      onAssignRole: (role) => _assignRole(user, role),
                      onRemoveRole: (role) => _removeRole(user, role),
                      onFreeze: () => _freeze(user),
                      onUnfreeze: () => _unfreeze(user),
                      onTransferSuperAdmin: () => _transferSuperAdmin(user),
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

  Future<_AccessPayload> _load() async {
    final repo = ref.read(adminManagementRepositoryProvider);
    final profile = await ref.read(profileRepositoryProvider).getMe();
    final users = await repo.getAccessUsers(
      search: _searchController.text,
      role: _role,
      excludeClients: false,
    );
    AdminAccessUser? current;
    for (final user in users) {
      if (user.email.toLowerCase() == profile.email.toLowerCase()) {
        current = user;
        break;
      }
    }
    return _AccessPayload(
      users: users,
      currentUserId: current?.userId ?? '',
      currentIsSuperAdmin: current?.hasRole('SuperAdmin') ?? false,
    );
  }

  Future<void> _assignRole(AdminAccessUser user, String role) async {
    await _runAction(
      () => ref
          .read(adminManagementRepositoryProvider)
          .assignRole(userId: user.userId, role: role),
      'Роль назначена.',
    );
  }

  Future<void> _removeRole(AdminAccessUser user, String role) async {
    final confirmed = await _confirm(
      context,
      title: 'Снять роль?',
      text: '${user.email}\n$role',
      danger: true,
    );
    if (!confirmed) {
      return;
    }
    await _runAction(
      () => ref
          .read(adminManagementRepositoryProvider)
          .removeRole(userId: user.userId, role: role),
      'Роль снята.',
    );
  }

  Future<void> _freeze(AdminAccessUser user) async {
    final confirmed = await _confirm(
      context,
      title: 'Заморозить аккаунт?',
      text: user.email,
      danger: true,
    );
    if (!confirmed) {
      return;
    }
    await _runAction(
      () => ref
          .read(adminManagementRepositoryProvider)
          .freezeAccount(user.userId),
      'Аккаунт заморожен.',
    );
  }

  Future<void> _unfreeze(AdminAccessUser user) async {
    await _runAction(
      () => ref
          .read(adminManagementRepositoryProvider)
          .unfreezeAccount(user.userId),
      'Аккаунт разморожен.',
    );
  }

  Future<void> _transferSuperAdmin(AdminAccessUser user) async {
    final password = await _passwordDialog(user);
    if (password == null) {
      return;
    }
    await _runAction(
      () => ref
          .read(adminManagementRepositoryProvider)
          .transferSuperAdmin(userId: user.userId, password: password),
      'Права главного администратора переданы.',
    );
  }

  Future<String?> _passwordDialog(AdminAccessUser user) async {
    final controller = TextEditingController();
    var confirmed = false;
    final formKey = GlobalKey<FormState>();
    final result = await showDialog<String>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setState) => AlertDialog(
          title: const Text('Передать права главного администратора'),
          content: Form(
            key: formKey,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text('Пользователь: ${user.email}'),
                const SizedBox(height: 8),
                CheckboxListTile(
                  contentPadding: EdgeInsets.zero,
                  value: confirmed,
                  onChanged: (value) =>
                      setState(() => confirmed = value ?? false),
                  title: const Text(
                    'Я понимаю, что потеряю права главного администратора',
                  ),
                ),
                PasswordTextFormField(
                  controller: controller,
                  labelText: 'Ваш пароль',
                  validator: (value) {
                    if (!confirmed) {
                      return 'Подтвердите действие';
                    }
                    if (value == null || value.isEmpty) {
                      return 'Введите пароль';
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
                if (formKey.currentState!.validate()) {
                  Navigator.of(context).pop(controller.text);
                }
              },
              child: const Text('Передать'),
            ),
          ],
        ),
      ),
    );
    controller.dispose();
    return result;
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

class _AccessPayload {
  const _AccessPayload({
    required this.users,
    required this.currentUserId,
    required this.currentIsSuperAdmin,
  });

  final List<AdminAccessUser> users;
  final String currentUserId;
  final bool currentIsSuperAdmin;
}

class _AccessUserCard extends StatelessWidget {
  const _AccessUserCard({
    required this.user,
    required this.currentUserId,
    required this.currentIsSuperAdmin,
    required this.onAssignRole,
    required this.onRemoveRole,
    required this.onFreeze,
    required this.onUnfreeze,
    required this.onTransferSuperAdmin,
  });

  final AdminAccessUser user;
  final String currentUserId;
  final bool currentIsSuperAdmin;
  final ValueChanged<String> onAssignRole;
  final ValueChanged<String> onRemoveRole;
  final VoidCallback onFreeze;
  final VoidCallback onUnfreeze;
  final VoidCallback onTransferSuperAdmin;

  @override
  Widget build(BuildContext context) {
    final isSelf = user.userId == currentUserId;
    final isAdminTarget = user.hasRole('Admin') || user.hasRole('SuperAdmin');
    final canManageFreeze =
        !isSelf &&
        (currentIsSuperAdmin ||
            (!isAdminTarget &&
                (user.hasRole('Realtor') || user.hasRole('Client'))));

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              user.displayName?.trim().isNotEmpty == true
                  ? user.displayName!
                  : 'Без имени',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 4),
            Text(user.email, style: Theme.of(context).textTheme.bodySmall),
            const SizedBox(height: 8),
            Wrap(
              spacing: 6,
              runSpacing: 4,
              children: [
                if (user.roles.isEmpty)
                  const Chip(label: Text('Нет ролей'))
                else
                  for (final role in user.roles) Chip(label: Text(role)),
                Chip(label: Text(user.isFrozen ? 'Заморожен' : 'Активен')),
              ],
            ),
            const SizedBox(height: 8),
            if (currentIsSuperAdmin) ...[
              _RoleAction(
                role: 'Admin',
                assigned: user.hasRole('Admin'),
                onAssign: onAssignRole,
                onRemove: onRemoveRole,
              ),
              if (!user.hasRole('SuperAdmin'))
                OutlinedButton.icon(
                  onPressed: onTransferSuperAdmin,
                  icon: const Icon(Icons.admin_panel_settings_outlined),
                  label: const Text('Сделать главным'),
                ),
            ] else
              Text(
                'Admin меняет только главный администратор',
                style: Theme.of(context).textTheme.bodySmall,
              ),
            _RoleAction(
              role: 'Realtor',
              assigned: user.hasRole('Realtor'),
              onAssign: onAssignRole,
              onRemove: onRemoveRole,
            ),
            const SizedBox(height: 4),
            if (canManageFreeze)
              OutlinedButton.icon(
                onPressed: user.isFrozen ? onUnfreeze : onFreeze,
                icon: Icon(
                  user.isFrozen ? Icons.lock_open : Icons.lock_outline,
                ),
                label: Text(user.isFrozen ? 'Разморозить' : 'Заморозить'),
                style: user.isFrozen
                    ? null
                    : OutlinedButton.styleFrom(
                        foregroundColor: Theme.of(context).colorScheme.error,
                      ),
              )
            else if (isSelf)
              Text(
                'Свой аккаунт нельзя заморозить',
                style: Theme.of(context).textTheme.bodySmall,
              ),
          ],
        ),
      ),
    );
  }
}

class _RoleAction extends StatelessWidget {
  const _RoleAction({
    required this.role,
    required this.assigned,
    required this.onAssign,
    required this.onRemove,
  });

  final String role;
  final bool assigned;
  final ValueChanged<String> onAssign;
  final ValueChanged<String> onRemove;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: OutlinedButton.icon(
        onPressed: () => assigned ? onRemove(role) : onAssign(role),
        icon: Icon(
          assigned ? Icons.remove_circle_outline : Icons.add_circle_outline,
        ),
        label: Text(assigned ? 'Снять $role' : 'Назначить $role'),
        style: assigned
            ? OutlinedButton.styleFrom(
                foregroundColor: Theme.of(context).colorScheme.error,
              )
            : null,
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
