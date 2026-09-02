import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:image_picker/image_picker.dart';
import 'package:intl/intl.dart';

import '../../../../core/auth/auth_controller.dart';
import '../../../../core/config/api_config.dart';
import '../../../../core/errors/api_exception.dart';
import '../../../../core/widgets/password_text_form_field.dart';
import '../../data/repository/profile_repository.dart';
import '../../domain/models/account_session.dart';
import '../../domain/models/user_profile.dart';

class ProfileScreen extends ConsumerStatefulWidget {
  const ProfileScreen({super.key});

  @override
  ConsumerState<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends ConsumerState<ProfileScreen> {
  final _profileFormKey = GlobalKey<FormState>();
  final _passwordFormKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _firstNameController = TextEditingController();
  final _lastNameController = TextEditingController();
  final _middleNameController = TextEditingController();
  final _phoneController = TextEditingController();
  final _currentPasswordController = TextEditingController();
  final _newPasswordController = TextEditingController();
  final _confirmPasswordController = TextEditingController();
  final _imagePicker = ImagePicker();

  String? _loadedEmail;
  String? _revokingSessionId;
  bool _isSavingProfile = false;
  bool _isChangingPassword = false;
  bool _isChangingAvatar = false;

  @override
  void dispose() {
    _emailController.dispose();
    _firstNameController.dispose();
    _lastNameController.dispose();
    _middleNameController.dispose();
    _phoneController.dispose();
    _currentPasswordController.dispose();
    _newPasswordController.dispose();
    _confirmPasswordController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final profileState = ref.watch(currentProfileProvider);
    final apiSettings = ref.watch(apiSettingsControllerProvider);

    return profileState.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (error, stackTrace) => _LoadError(
        message: _messageFrom(error),
        onRetry: () => ref.invalidate(currentProfileProvider),
      ),
      data: (profile) {
        _syncControllers(profile);
        final sessionsState = ref.watch(accountSessionsProvider);

        return ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Text('Аккаунт', style: Theme.of(context).textTheme.headlineSmall),
            const SizedBox(height: 16),
            _ProfileHeader(
              profile: profile,
              apiSettings: apiSettings,
              isChangingAvatar: _isChangingAvatar,
              onUploadAvatar: profile.isRealtor ? _uploadAvatar : null,
              onDeleteAvatar: profile.isRealtor ? _deleteAvatar : null,
            ),
            const SizedBox(height: 12),
            _profileForm(profile),
            const SizedBox(height: 12),
            _passwordForm(),
            const SizedBox(height: 12),
            _sessionsSection(sessionsState),
          ],
        );
      },
    );
  }

  Widget _profileForm(UserProfile profile) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Form(
          key: _profileFormKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                'Данные аккаунта',
                style: Theme.of(context).textTheme.titleMedium,
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _lastNameController,
                decoration: const InputDecoration(labelText: 'Фамилия'),
                validator: _required('Введите фамилию.'),
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _firstNameController,
                decoration: const InputDecoration(labelText: 'Имя'),
                validator: _required('Введите имя.'),
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _middleNameController,
                decoration: const InputDecoration(labelText: 'Отчество'),
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _phoneController,
                keyboardType: TextInputType.phone,
                decoration: const InputDecoration(labelText: 'Телефон'),
                validator: profile.isRealtor
                    ? _required('Введите телефон.')
                    : null,
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _emailController,
                keyboardType: TextInputType.emailAddress,
                decoration: const InputDecoration(
                  labelText: 'Email',
                  helperText: 'При смене email потребуется код подтверждения.',
                ),
                validator: (value) {
                  final text = value?.trim() ?? '';
                  if (text.isEmpty) {
                    return 'Введите email.';
                  }
                  if (!text.contains('@')) {
                    return 'Введите корректный email.';
                  }
                  return null;
                },
              ),
              const SizedBox(height: 16),
              FilledButton.icon(
                onPressed: _isSavingProfile ? null : _saveProfile,
                icon: _isSavingProfile
                    ? const SizedBox.square(
                        dimension: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.save_outlined),
                label: Text(_isSavingProfile ? 'Сохраняем...' : 'Сохранить'),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _passwordForm() {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Form(
          key: _passwordFormKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                'Смена пароля',
                style: Theme.of(context).textTheme.titleMedium,
              ),
              const SizedBox(height: 12),
              PasswordTextFormField(
                controller: _currentPasswordController,
                labelText: 'Текущий пароль',
                validator: _required('Введите текущий пароль.'),
              ),
              const SizedBox(height: 12),
              PasswordTextFormField(
                controller: _newPasswordController,
                labelText: 'Новый пароль',
                validator: (value) {
                  final text = value ?? '';
                  if (text.isEmpty) {
                    return 'Введите новый пароль.';
                  }
                  if (text.length < 8) {
                    return 'Минимум 8 символов.';
                  }
                  return null;
                },
              ),
              const SizedBox(height: 12),
              PasswordTextFormField(
                controller: _confirmPasswordController,
                labelText: 'Подтверждение',
                validator: (value) {
                  if ((value ?? '').isEmpty) {
                    return 'Подтвердите пароль.';
                  }
                  if (value != _newPasswordController.text) {
                    return 'Пароли не совпадают.';
                  }
                  return null;
                },
              ),
              const SizedBox(height: 16),
              OutlinedButton.icon(
                onPressed: _isChangingPassword ? null : _changePassword,
                icon: _isChangingPassword
                    ? const SizedBox.square(
                        dimension: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.lock_reset),
                label: Text(
                  _isChangingPassword ? 'Меняем...' : 'Изменить пароль',
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _sessionsSection(AsyncValue<List<AccountSession>> sessionsState) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              'Текущие подключения',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 4),
            Text(
              'Устройства, где сейчас открыт ваш аккаунт.',
              style: Theme.of(context).textTheme.bodySmall,
            ),
            const SizedBox(height: 12),
            sessionsState.when(
              loading: () => const Padding(
                padding: EdgeInsets.symmetric(vertical: 12),
                child: Center(child: CircularProgressIndicator()),
              ),
              error: (error, stackTrace) => _InlineError(
                message: _messageFrom(error),
                onRetry: () => ref.invalidate(accountSessionsProvider),
              ),
              data: (sessions) {
                if (sessions.isEmpty) {
                  return const _EmptyState(
                    icon: Icons.devices_other_outlined,
                    message: 'Активных подключений пока нет.',
                  );
                }

                return Column(
                  children: [
                    for (final session in sessions) ...[
                      _SessionTile(
                        session: session,
                        isBusy: _revokingSessionId == session.id,
                        onRevoke: () => _confirmRevokeSession(session),
                      ),
                      if (session != sessions.last) const Divider(height: 20),
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

  void _syncControllers(UserProfile profile) {
    if (_loadedEmail == profile.email) {
      return;
    }

    _loadedEmail = profile.email;
    _emailController.text = profile.email;
    _firstNameController.text = profile.firstName;
    _lastNameController.text = profile.lastName;
    _middleNameController.text = profile.middleName ?? '';
    _phoneController.text = profile.phoneNumber;
  }

  Future<void> _saveProfile() async {
    if (!_profileFormKey.currentState!.validate()) {
      return;
    }

    setState(() => _isSavingProfile = true);
    try {
      final requestedEmail = _emailController.text.trim();
      if (_loadedEmail != null &&
          requestedEmail.toLowerCase() != _loadedEmail!.toLowerCase()) {
        await _requestEmailChange(requestedEmail);
        return;
      }

      await ref
          .read(profileRepositoryProvider)
          .updateMe(
            email: _emailController.text,
            firstName: _firstNameController.text,
            lastName: _lastNameController.text,
            middleName: _middleNameController.text,
            phoneNumber: _phoneController.text,
          );

      ref.invalidate(currentProfileProvider);
      _showMessage('Аккаунт сохранен.');
    } catch (error) {
      _showMessage(_messageFrom(error), isError: true);
    } finally {
      if (mounted) {
        setState(() => _isSavingProfile = false);
      }
    }
  }

  Future<void> _requestEmailChange(String newEmail) async {
    await ref
        .read(profileRepositoryProvider)
        .requestEmailChange(newEmail: newEmail);

    if (!mounted) {
      return;
    }

    setState(() => _isSavingProfile = false);
    _showMessage('Мы отправили код подтверждения на новый email.');

    final confirmed = await _showEmailChangeDialog(newEmail);
    if (confirmed != true || !mounted) {
      return;
    }

    await ref.read(authControllerProvider.notifier).signOut();
    _showMessage('Email изменен. Войдите снова с новым адресом.');
  }

  Future<bool?> _showEmailChangeDialog(String newEmail) async {
    final formKey = GlobalKey<FormState>();
    final codeController = TextEditingController();

    try {
      return await showDialog<bool>(
        context: context,
        barrierDismissible: false,
        builder: (context) {
          var isSubmitting = false;

          return StatefulBuilder(
            builder: (context, setDialogState) => AlertDialog(
              title: const Text('Подтверждение email'),
              content: Form(
                key: formKey,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(
                      'Введите код, который пришел на $newEmail.',
                      style: Theme.of(context).textTheme.bodyMedium,
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      controller: codeController,
                      keyboardType: TextInputType.number,
                      textInputAction: TextInputAction.done,
                      autofillHints: const [AutofillHints.oneTimeCode],
                      maxLength: 6,
                      decoration: const InputDecoration(
                        labelText: 'Код подтверждения',
                        counterText: '',
                      ),
                      validator: (value) {
                        final code = value?.trim() ?? '';
                        if (!RegExp(r'^\d{6}$').hasMatch(code)) {
                          return 'Код должен состоять из 6 цифр.';
                        }
                        return null;
                      },
                    ),
                  ],
                ),
              ),
              actions: [
                TextButton(
                  onPressed: isSubmitting
                      ? null
                      : () => Navigator.of(context).pop(false),
                  child: const Text('Позже'),
                ),
                FilledButton(
                  onPressed: isSubmitting
                      ? null
                      : () async {
                          if (!formKey.currentState!.validate()) {
                            return;
                          }

                          setDialogState(() => isSubmitting = true);
                          try {
                            await ref
                                .read(profileRepositoryProvider)
                                .confirmEmailChange(
                                  newEmail: newEmail,
                                  code: codeController.text,
                                );

                            if (context.mounted) {
                              Navigator.of(context).pop(true);
                            }
                          } catch (error) {
                            if (context.mounted) {
                              ScaffoldMessenger.of(context).showSnackBar(
                                SnackBar(
                                  content: Text(_messageFrom(error)),
                                  backgroundColor: Theme.of(
                                    context,
                                  ).colorScheme.error,
                                ),
                              );
                            }
                          } finally {
                            if (context.mounted) {
                              setDialogState(() => isSubmitting = false);
                            }
                          }
                        },
                  child: isSubmitting
                      ? const SizedBox.square(
                          dimension: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Text('Подтвердить'),
                ),
              ],
            ),
          );
        },
      );
    } finally {
      codeController.dispose();
    }
  }

  Future<void> _changePassword() async {
    if (!_passwordFormKey.currentState!.validate()) {
      return;
    }

    setState(() => _isChangingPassword = true);
    try {
      await ref
          .read(profileRepositoryProvider)
          .changePassword(
            currentPassword: _currentPasswordController.text,
            newPassword: _newPasswordController.text,
            confirmPassword: _confirmPasswordController.text,
          );

      _currentPasswordController.clear();
      _newPasswordController.clear();
      _confirmPasswordController.clear();
      _showMessage('Пароль успешно изменен.');
    } catch (error) {
      _showMessage(_messageFrom(error), isError: true);
    } finally {
      if (mounted) {
        setState(() => _isChangingPassword = false);
      }
    }
  }

  Future<void> _uploadAvatar() async {
    final image = await _imagePicker.pickImage(
      source: ImageSource.gallery,
      maxWidth: 1200,
      imageQuality: 88,
    );

    if (image == null) {
      return;
    }

    setState(() => _isChangingAvatar = true);
    try {
      await ref
          .read(profileRepositoryProvider)
          .uploadAvatar(image.path, image.name);

      ref.invalidate(currentProfileProvider);
      _showMessage('Аватар обновлен.');
    } catch (error) {
      _showMessage(_messageFrom(error), isError: true);
    } finally {
      if (mounted) {
        setState(() => _isChangingAvatar = false);
      }
    }
  }

  Future<void> _deleteAvatar() async {
    setState(() => _isChangingAvatar = true);
    try {
      await ref.read(profileRepositoryProvider).deleteAvatar();
      ref.invalidate(currentProfileProvider);
      _showMessage('Аватар удален.');
    } catch (error) {
      _showMessage(_messageFrom(error), isError: true);
    } finally {
      if (mounted) {
        setState(() => _isChangingAvatar = false);
      }
    }
  }

  Future<void> _confirmRevokeSession(AccountSession session) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Завершить подключение?'),
        content: Text(
          session.isCurrent
              ? 'Это текущее устройство. После завершения нужно будет войти заново.'
              : 'Аккаунт будет закрыт на устройстве "${session.deviceName}".',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('Нет'),
          ),
          FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor: Theme.of(context).colorScheme.error,
              foregroundColor: Theme.of(context).colorScheme.onError,
            ),
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text('Да, завершить'),
          ),
        ],
      ),
    );

    if (confirmed != true) {
      return;
    }

    setState(() => _revokingSessionId = session.id);
    try {
      await ref.read(profileRepositoryProvider).revokeSession(session.id);

      if (session.isCurrent) {
        await ref.read(authControllerProvider.notifier).signOut();
        return;
      }

      ref.invalidate(accountSessionsProvider);
      _showMessage('Подключение завершено.');
    } catch (error) {
      _showMessage(_messageFrom(error), isError: true);
    } finally {
      if (mounted) {
        setState(() => _revokingSessionId = null);
      }
    }
  }

  FormFieldValidator<String> _required(String message) {
    return (value) {
      if ((value ?? '').trim().isEmpty) {
        return message;
      }

      return null;
    };
  }

  String _messageFrom(Object error) {
    if (error is ApiException) {
      return error.message;
    }

    return 'Не удалось выполнить действие. Попробуйте еще раз.';
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

class _ProfileHeader extends StatelessWidget {
  const _ProfileHeader({
    required this.profile,
    required this.apiSettings,
    required this.isChangingAvatar,
    required this.onUploadAvatar,
    required this.onDeleteAvatar,
  });

  final UserProfile profile;
  final ApiSettings apiSettings;
  final bool isChangingAvatar;
  final VoidCallback? onUploadAvatar;
  final VoidCallback? onDeleteAvatar;

  @override
  Widget build(BuildContext context) {
    final avatarUrl = apiSettings.resolveFileUrl(profile.avatarPath);

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          children: [
            CircleAvatar(
              radius: 54,
              backgroundImage: avatarUrl == null
                  ? null
                  : NetworkImage(avatarUrl),
              child: avatarUrl == null
                  ? const Icon(Icons.person_outline, size: 44)
                  : null,
            ),
            const SizedBox(height: 12),
            Text(
              profile.fullName.trim().isEmpty
                  ? profile.email
                  : profile.fullName,
              style: Theme.of(context).textTheme.titleMedium,
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 4),
            Text(profile.email, textAlign: TextAlign.center),
            if (profile.phoneNumber.trim().isNotEmpty)
              Text(profile.phoneNumber, textAlign: TextAlign.center),
            const SizedBox(height: 12),
            Wrap(
              alignment: WrapAlignment.center,
              spacing: 8,
              runSpacing: 8,
              children: [
                if (profile.isAdmin) const Chip(label: Text('Администратор')),
                if (profile.isRealtor) const Chip(label: Text('Риелтор')),
              ],
            ),
            if (profile.isRealtor) ...[
              const SizedBox(height: 12),
              _RealtorStats(profile: profile),
              const SizedBox(height: 12),
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: isChangingAvatar ? null : onUploadAvatar,
                      icon: const Icon(Icons.photo_camera_outlined),
                      label: const Text('Загрузить'),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: isChangingAvatar ? null : onDeleteAvatar,
                      icon: const Icon(Icons.delete_outline),
                      label: const Text('Удалить'),
                    ),
                  ),
                ],
              ),
              if (isChangingAvatar) ...[
                const SizedBox(height: 8),
                const LinearProgressIndicator(),
              ],
            ],
          ],
        ),
      ),
    );
  }
}

class _SessionTile extends StatelessWidget {
  const _SessionTile({
    required this.session,
    required this.isBusy,
    required this.onRevoke,
  });

  final AccountSession session;
  final bool isBusy;
  final VoidCallback onRevoke;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Container(
          width: 42,
          height: 42,
          decoration: BoxDecoration(
            color: colorScheme.primaryContainer,
            borderRadius: BorderRadius.circular(8),
          ),
          child: Icon(
            session.deviceName.toLowerCase().contains('android')
                ? Icons.phone_android
                : Icons.desktop_windows_outlined,
            color: colorScheme.onPrimaryContainer,
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Wrap(
                spacing: 8,
                runSpacing: 6,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  Text(
                    session.deviceName,
                    style: Theme.of(context).textTheme.titleSmall,
                  ),
                  if (session.isCurrent)
                    const Chip(
                      label: Text('Это устройство'),
                      visualDensity: VisualDensity.compact,
                    ),
                ],
              ),
              const SizedBox(height: 4),
              Text('Вход: ${_formatDate(session.createdAtUtc)}'),
              Text('Активность: ${_formatDate(session.lastSeenAtUtc)}'),
              if (session.ipAddress?.trim().isNotEmpty == true)
                Text('IP: ${session.ipAddress}'),
              const SizedBox(height: 8),
              Align(
                alignment: Alignment.centerLeft,
                child: OutlinedButton.icon(
                  onPressed: isBusy ? null : onRevoke,
                  style: OutlinedButton.styleFrom(
                    foregroundColor: colorScheme.error,
                    side: BorderSide(color: colorScheme.error),
                  ),
                  icon: isBusy
                      ? const SizedBox.square(
                          dimension: 16,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.logout),
                  label: const Text('Завершить'),
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }

  static String _formatDate(DateTime value) {
    return DateFormat('dd.MM.yyyy HH:mm').format(value.toLocal());
  }
}

class _RealtorStats extends StatelessWidget {
  const _RealtorStats({required this.profile});

  final UserProfile profile;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        border: Border.all(color: Theme.of(context).dividerColor),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          children: [
            _StatRow(label: 'Уровень', value: profile.realtorLevel ?? '-'),
            _StatRow(
              label: 'Выплата',
              value: '${profile.realtorCommissionPercent ?? 0}%',
            ),
            _StatRow(
              label: 'За месяц',
              value:
                  '${profile.realtorPayoutThisMonth ?? 0} ${profile.realtorPayoutCurrency}',
            ),
            _StatRow(
              label: 'Всего',
              value:
                  '${profile.realtorPayoutTotal ?? 0} ${profile.realtorPayoutCurrency}',
            ),
          ],
        ),
      ),
    );
  }
}

class _StatRow extends StatelessWidget {
  const _StatRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label),
          const SizedBox(width: 12),
          Flexible(
            child: Text(
              value,
              textAlign: TextAlign.end,
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
          ),
        ],
      ),
    );
  }
}

class _InlineError extends StatelessWidget {
  const _InlineError({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(message, textAlign: TextAlign.center),
        const SizedBox(height: 8),
        OutlinedButton.icon(
          onPressed: onRetry,
          icon: const Icon(Icons.refresh),
          label: const Text('Повторить'),
        ),
      ],
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState({required this.icon, required this.message});

  final IconData icon;
  final String message;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 12),
      child: Column(
        children: [
          Icon(icon, size: 36, color: Theme.of(context).colorScheme.outline),
          const SizedBox(height: 8),
          Text(message, textAlign: TextAlign.center),
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
