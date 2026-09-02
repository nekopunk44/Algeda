import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/auth/auth_controller.dart';
import '../../../../core/config/api_config.dart';
import '../../../../core/errors/api_exception.dart';
import '../../../../core/widgets/password_text_form_field.dart';
import '../../data/repository/auth_repository.dart';

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _apiBaseUrlController = TextEditingController();
  String? _lastSyncedApiBaseUrl;
  bool _isSubmitting = false;
  int _titleTapCount = 0;
  DateTime? _lastTitleTapAt;

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    _apiBaseUrlController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final authState = ref.watch(authControllerProvider);
    final apiSettings = ref.watch(apiSettingsControllerProvider);
    _syncApiSettings(apiSettings);

    return Scaffold(
      appBar: AppBar(
        title: GestureDetector(
          behavior: HitTestBehavior.opaque,
          onTap: _handleTitleTap,
          child: const Padding(
            padding: EdgeInsets.symmetric(vertical: 8),
            child: Text('Вход'),
          ),
        ),
      ),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Text(
              'Мобильный кабинет сотрудников',
              style: Theme.of(context).textTheme.headlineSmall,
            ),
            const SizedBox(height: 8),
            const Text(
              'Вход доступен для риелторов и администраторов агентства.',
            ),
            const SizedBox(height: 16),
            Card(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Form(
                  key: _formKey,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      TextFormField(
                        controller: _emailController,
                        keyboardType: TextInputType.emailAddress,
                        textInputAction: TextInputAction.next,
                        decoration: const InputDecoration(
                          labelText: 'Email',
                          prefixIcon: Icon(Icons.email_outlined),
                        ),
                        validator: _emailValidator,
                      ),
                      const SizedBox(height: 12),
                      PasswordTextFormField(
                        controller: _passwordController,
                        textInputAction: TextInputAction.done,
                        labelText: 'Пароль',
                        validator: (value) {
                          if ((value ?? '').isEmpty) {
                            return 'Введите пароль.';
                          }
                          return null;
                        },
                        onFieldSubmitted: (_) => _submit(),
                      ),
                      if (authState.errorMessage != null) ...[
                        const SizedBox(height: 12),
                        _ErrorBanner(message: authState.errorMessage!),
                      ],
                      const SizedBox(height: 16),
                      FilledButton.icon(
                        onPressed: _isSubmitting ? null : _submit,
                        icon: _isSubmitting
                            ? const SizedBox.square(
                                dimension: 18,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2,
                                ),
                              )
                            : const Icon(Icons.login),
                        label: Text(_isSubmitting ? 'Входим...' : 'Войти'),
                      ),
                      const SizedBox(height: 8),
                      TextButton(
                        onPressed: _showForgotPassword,
                        child: const Text('Забыли пароль?'),
                      ),
                      OutlinedButton.icon(
                        onPressed: _showRegisterRealtor,
                        icon: const Icon(Icons.person_add_alt_1_outlined),
                        label: const Text('Подать заявку риелтора'),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    setState(() => _isSubmitting = true);

    try {
      await _saveServerBaseUrl(_apiBaseUrlController.text);

      await ref
          .read(authControllerProvider.notifier)
          .login(
            email: _emailController.text,
            password: _passwordController.text,
          );
    } finally {
      if (mounted) {
        setState(() => _isSubmitting = false);
      }
    }

    if (!mounted) {
      return;
    }

    final state = ref.read(authControllerProvider);
    final role = state.role;
    if (state.isAuthenticated && role != null) {
      context.go(role.homePath);
    }
  }

  void _syncApiSettings(ApiSettings settings) {
    if (_lastSyncedApiBaseUrl == settings.baseUrl) {
      return;
    }

    final currentText = _apiBaseUrlController.text.trim();
    final canUpdateField =
        currentText.isEmpty || currentText == _lastSyncedApiBaseUrl;
    _lastSyncedApiBaseUrl = settings.baseUrl;

    if (!canUpdateField) {
      return;
    }

    _apiBaseUrlController.text = settings.baseUrl;
  }

  String? _validateApiBaseUrl(String? value) {
    final text = value?.trim() ?? '';
    final normalized = ApiConfig.normalizeBaseUrl(text);
    final uri = Uri.tryParse(normalized);
    if (uri == null || !uri.hasScheme || !uri.hasAuthority) {
      return 'Введите адрес API, например https://api.example.com';
    }
    return null;
  }

  void _handleTitleTap() {
    final now = DateTime.now();
    final lastTapAt = _lastTitleTapAt;
    if (lastTapAt == null || now.difference(lastTapAt).inSeconds > 3) {
      _titleTapCount = 0;
    }

    _lastTitleTapAt = now;
    _titleTapCount += 1;

    if (_titleTapCount < 5) {
      return;
    }

    _titleTapCount = 0;
    _showServerSettingsDialog();
  }

  Future<void> _showServerSettingsDialog() async {
    final messenger = ScaffoldMessenger.of(context);
    final baseUrl = await showDialog<String>(
      context: context,
      builder: (context) => _ServerSettingsDialog(
        initialBaseUrl: _apiBaseUrlController.text,
        validateBaseUrl: _validateApiBaseUrl,
        checkConnection: _checkServerConnection,
      ),
    );

    if (baseUrl == null) {
      return;
    }

    await _saveServerBaseUrl(baseUrl);

    if (!mounted) {
      return;
    }

    messenger.showSnackBar(
      const SnackBar(content: Text('Настройки сохранены.')),
    );
  }

  Future<void> _showRegisterRealtor() async {
    final result = await showModalBottomSheet<_RegisterRealtorResult>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.zero),
      builder: (context) => const _RegisterRealtorSheet(),
    );
    if (result == null) {
      return;
    }

    try {
      final message = await ref
          .read(authRepositoryProvider)
          .registerRealtorRequest(
            firstName: result.firstName,
            lastName: result.lastName,
            middleName: result.middleName,
            email: result.email,
            phoneNumber: result.phoneNumber,
            password: result.password,
            confirmPassword: result.confirmPassword,
          );
      _showSnack(message);
      await _showConfirmEmail(initialEmail: result.email);
    } catch (error) {
      _showSnack(_messageFrom(error));
    }
  }

  Future<void> _showConfirmEmail({String? initialEmail}) async {
    final result = await showModalBottomSheet<_EmailConfirmationResult>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.zero),
      builder: (context) =>
          _EmailConfirmationSheet(initialEmail: initialEmail?.trim() ?? ''),
    );
    if (result == null) {
      return;
    }

    try {
      final repo = ref.read(authRepositoryProvider);
      final message = result.sendNewCode
          ? await repo.resendConfirmation(result.email)
          : await repo.confirmEmail(email: result.email, code: result.code);
      _showSnack(message);
    } catch (error) {
      _showSnack(_messageFrom(error));
    }
  }

  Future<void> _showForgotPassword() async {
    final result = await showModalBottomSheet<_PasswordRecoveryResult>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.zero),
      builder: (context) => const _PasswordRecoverySheet(),
    );
    if (result == null) {
      return;
    }

    try {
      final repo = ref.read(authRepositoryProvider);
      final message = result.requestCode
          ? await repo.forgotPassword(result.email)
          : await repo.resetPassword(
              email: result.email,
              code: result.code,
              newPassword: result.newPassword!,
              confirmPassword: result.confirmPassword!,
            );
      _showSnack(message);
    } catch (error) {
      _showSnack(_messageFrom(error));
    }
  }

  void _showSnack(String message) {
    if (!mounted) {
      return;
    }
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), duration: const Duration(seconds: 6)),
    );
  }

  String _messageFrom(Object error) {
    if (error is ApiException) {
      return error.message;
    }
    return 'Не удалось выполнить действие.';
  }

  Future<void> _saveServerBaseUrl(String baseUrl) async {
    final normalized = ApiConfig.normalizeBaseUrl(baseUrl);
    await ref
        .read(apiSettingsControllerProvider.notifier)
        .save(baseUrl: normalized, webBaseUrl: normalized);
    _lastSyncedApiBaseUrl = normalized;
    _apiBaseUrlController.text = normalized;
  }

  Future<void> _checkServerConnection(String baseUrl) async {
    final normalized = ApiConfig.normalizeBaseUrl(baseUrl);

    final dio = Dio(
      BaseOptions(
        baseUrl: '$normalized/',
        connectTimeout: const Duration(seconds: 10),
        receiveTimeout: const Duration(seconds: 10),
        sendTimeout: const Duration(seconds: 10),
        headers: const {'Accept': 'application/json'},
      ),
    );

    await dio.get<Map<String, dynamic>>('api/health/db');
  }
}

class _ErrorBanner extends StatelessWidget {
  const _ErrorBanner({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: Theme.of(context).colorScheme.errorContainer,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(
              Icons.error_outline,
              color: Theme.of(context).colorScheme.onErrorContainer,
            ),
            const SizedBox(width: 8),
            Expanded(
              child: Text(
                message,
                style: TextStyle(
                  color: Theme.of(context).colorScheme.onErrorContainer,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ServerSettingsDialog extends StatefulWidget {
  const _ServerSettingsDialog({
    required this.initialBaseUrl,
    required this.validateBaseUrl,
    required this.checkConnection,
  });

  final String initialBaseUrl;
  final FormFieldValidator<String> validateBaseUrl;
  final Future<void> Function(String baseUrl) checkConnection;

  @override
  State<_ServerSettingsDialog> createState() => _ServerSettingsDialogState();
}

class _ServerSettingsDialogState extends State<_ServerSettingsDialog> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _controller;
  bool _isChecking = false;

  @override
  void initState() {
    super.initState();
    _controller = TextEditingController(text: widget.initialBaseUrl);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Настройки сервера'),
      content: Form(
        key: _formKey,
        child: TextFormField(
          controller: _controller,
          keyboardType: TextInputType.url,
          decoration: const InputDecoration(
            labelText: 'Адрес сервера',
            hintText: 'https://api.example.com',
            prefixIcon: Icon(Icons.dns_outlined),
          ),
          validator: widget.validateBaseUrl,
        ),
      ),
      actions: [
        TextButton(
          onPressed: _isChecking ? null : () => Navigator.of(context).pop(),
          child: const Text('Отмена'),
        ),
        OutlinedButton.icon(
          onPressed: _isChecking ? null : _checkConnection,
          icon: _isChecking
              ? const SizedBox.square(
                  dimension: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : const Icon(Icons.wifi_tethering),
          label: Text(_isChecking ? 'Проверяем...' : 'Проверить'),
        ),
        FilledButton(
          onPressed: _isChecking ? null : _save,
          child: const Text('Сохранить'),
        ),
      ],
    );
  }

  Future<void> _checkConnection() async {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    final baseUrl = ApiConfig.normalizeBaseUrl(_controller.text);
    setState(() => _isChecking = true);

    try {
      await widget.checkConnection(baseUrl);

      if (!mounted) {
        return;
      }

      ScaffoldMessenger.of(
        context,
      ).showSnackBar(const SnackBar(content: Text('Сервер доступен.')));
    } on DioException catch (error) {
      if (!mounted) {
        return;
      }

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(ApiException.fromDio(error).message),
          duration: const Duration(seconds: 8),
        ),
      );
    } finally {
      if (mounted) {
        setState(() => _isChecking = false);
      }
    }
  }

  void _save() {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    Navigator.of(context).pop(ApiConfig.normalizeBaseUrl(_controller.text));
  }
}

class _RegisterRealtorResult {
  const _RegisterRealtorResult({
    required this.firstName,
    required this.lastName,
    required this.middleName,
    required this.email,
    required this.phoneNumber,
    required this.password,
    required this.confirmPassword,
  });

  final String firstName;
  final String lastName;
  final String? middleName;
  final String email;
  final String phoneNumber;
  final String password;
  final String confirmPassword;
}

class _RegisterRealtorSheet extends StatefulWidget {
  const _RegisterRealtorSheet();

  @override
  State<_RegisterRealtorSheet> createState() => _RegisterRealtorSheetState();
}

class _RegisterRealtorSheetState extends State<_RegisterRealtorSheet> {
  final _formKey = GlobalKey<FormState>();
  final _lastName = TextEditingController();
  final _firstName = TextEditingController();
  final _middleName = TextEditingController();
  final _email = TextEditingController();
  final _phone = TextEditingController();
  final _password = TextEditingController();
  final _confirmPassword = TextEditingController();

  @override
  void dispose() {
    _lastName.dispose();
    _firstName.dispose();
    _middleName.dispose();
    _email.dispose();
    _phone.dispose();
    _password.dispose();
    _confirmPassword.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return _AuthSheetFrame(
      title: 'Заявка риелтора',
      icon: Icons.person_add_alt_1_outlined,
      child: Form(
        key: _formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            TextFormField(
              controller: _lastName,
              textInputAction: TextInputAction.next,
              decoration: const InputDecoration(labelText: 'Фамилия'),
              validator: (value) => _requiredText(value, 'Введите фамилию.'),
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _firstName,
              textInputAction: TextInputAction.next,
              decoration: const InputDecoration(labelText: 'Имя'),
              validator: (value) => _requiredText(value, 'Введите имя.'),
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _middleName,
              textInputAction: TextInputAction.next,
              decoration: const InputDecoration(labelText: 'Отчество'),
              validator: (value) => _maxLength(value, 100),
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _email,
              keyboardType: TextInputType.emailAddress,
              textInputAction: TextInputAction.next,
              decoration: const InputDecoration(labelText: 'Email'),
              validator: _emailValidator,
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _phone,
              keyboardType: TextInputType.phone,
              textInputAction: TextInputAction.next,
              decoration: const InputDecoration(labelText: 'Телефон'),
              validator: _phoneValidator,
            ),
            const SizedBox(height: 12),
            PasswordTextFormField(
              controller: _password,
              textInputAction: TextInputAction.next,
              labelText: 'Пароль',
              validator: _passwordValidator,
            ),
            const SizedBox(height: 12),
            PasswordTextFormField(
              controller: _confirmPassword,
              textInputAction: TextInputAction.done,
              labelText: 'Повторите пароль',
              validator: (value) {
                final text = value ?? '';
                if (text != _password.text) {
                  return 'Пароли не совпадают.';
                }
                return _passwordValidator(value);
              },
            ),
            const SizedBox(height: 20),
            FilledButton.icon(
              onPressed: _submit,
              icon: const Icon(Icons.send_outlined),
              label: const Text('Отправить заявку'),
            ),
          ],
        ),
      ),
    );
  }

  void _submit() {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    Navigator.of(context).pop(
      _RegisterRealtorResult(
        firstName: _firstName.text.trim(),
        lastName: _lastName.text.trim(),
        middleName: _middleName.text.trim().isEmpty
            ? null
            : _middleName.text.trim(),
        email: _email.text.trim(),
        phoneNumber: _phone.text.trim(),
        password: _password.text,
        confirmPassword: _confirmPassword.text,
      ),
    );
  }
}

class _EmailConfirmationResult {
  const _EmailConfirmationResult.confirm({
    required this.email,
    required this.code,
  }) : sendNewCode = false;

  const _EmailConfirmationResult.resend({required this.email})
    : code = '',
      sendNewCode = true;

  final String email;
  final String code;
  final bool sendNewCode;
}

class _EmailConfirmationSheet extends StatefulWidget {
  const _EmailConfirmationSheet({required this.initialEmail});

  final String initialEmail;

  @override
  State<_EmailConfirmationSheet> createState() =>
      _EmailConfirmationSheetState();
}

class _EmailConfirmationSheetState extends State<_EmailConfirmationSheet> {
  final _formKey = GlobalKey<FormState>();
  final _email = TextEditingController();
  final _code = TextEditingController();
  Timer? _resendTimer;
  int _resendSecondsLeft = 30;

  @override
  void initState() {
    super.initState();
    _email.text = widget.initialEmail;
    _startResendTimer();
  }

  @override
  void dispose() {
    _resendTimer?.cancel();
    _email.dispose();
    _code.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return _AuthSheetFrame(
      title: 'Подтверждение email',
      icon: Icons.mark_email_read_outlined,
      child: Form(
        key: _formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            TextFormField(
              controller: _email,
              keyboardType: TextInputType.emailAddress,
              textInputAction: TextInputAction.next,
              decoration: const InputDecoration(labelText: 'Email'),
              validator: _emailValidator,
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _code,
              keyboardType: TextInputType.number,
              textInputAction: TextInputAction.done,
              autofillHints: const [AutofillHints.oneTimeCode],
              maxLength: 6,
              decoration: const InputDecoration(
                labelText: 'Код из письма',
                counterText: '',
              ),
              validator: _codeValidator,
            ),
            const SizedBox(height: 20),
            FilledButton.icon(
              onPressed: _confirm,
              icon: const Icon(Icons.check_circle_outline),
              label: const Text('Подтвердить'),
            ),
            const SizedBox(height: 8),
            OutlinedButton.icon(
              onPressed: _resendSecondsLeft == 0 ? _resend : null,
              icon: const Icon(Icons.refresh),
              label: Text(
                _resendSecondsLeft == 0
                    ? 'Получить новый код'
                    : 'Новый код через $_resendSecondsLeft сек.',
              ),
            ),
          ],
        ),
      ),
    );
  }

  void _confirm() {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    Navigator.of(context).pop(
      _EmailConfirmationResult.confirm(
        email: _email.text.trim(),
        code: _code.text.trim(),
      ),
    );
  }

  void _resend() {
    final error = _emailValidator(_email.text);
    if (error != null) {
      _formKey.currentState!.validate();
      return;
    }

    Navigator.of(
      context,
    ).pop(_EmailConfirmationResult.resend(email: _email.text.trim()));
  }

  void _startResendTimer() {
    _resendTimer?.cancel();
    _resendSecondsLeft = 30;
    _resendTimer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (!mounted) {
        timer.cancel();
        return;
      }

      if (_resendSecondsLeft <= 1) {
        timer.cancel();
        setState(() => _resendSecondsLeft = 0);
        return;
      }

      setState(() => _resendSecondsLeft -= 1);
    });
  }
}

class _PasswordRecoveryResult {
  const _PasswordRecoveryResult.request({required this.email})
    : code = '',
      newPassword = null,
      confirmPassword = null,
      requestCode = true;

  const _PasswordRecoveryResult.reset({
    required this.email,
    required this.code,
    required this.newPassword,
    required this.confirmPassword,
  }) : requestCode = false;

  final String email;
  final String code;
  final String? newPassword;
  final String? confirmPassword;
  final bool requestCode;
}

class _PasswordRecoverySheet extends StatefulWidget {
  const _PasswordRecoverySheet();

  @override
  State<_PasswordRecoverySheet> createState() => _PasswordRecoverySheetState();
}

class _PasswordRecoverySheetState extends State<_PasswordRecoverySheet> {
  final _formKey = GlobalKey<FormState>();
  final _email = TextEditingController();
  final _code = TextEditingController();
  final _password = TextEditingController();
  final _confirmPassword = TextEditingController();

  @override
  void dispose() {
    _email.dispose();
    _code.dispose();
    _password.dispose();
    _confirmPassword.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return _AuthSheetFrame(
      title: 'Восстановление доступа',
      icon: Icons.lock_reset_outlined,
      child: Form(
        key: _formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            TextFormField(
              controller: _email,
              keyboardType: TextInputType.emailAddress,
              textInputAction: TextInputAction.next,
              decoration: const InputDecoration(labelText: 'Email'),
              validator: _emailValidator,
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _code,
              keyboardType: TextInputType.number,
              textInputAction: TextInputAction.next,
              autofillHints: const [AutofillHints.oneTimeCode],
              maxLength: 6,
              decoration: const InputDecoration(
                labelText: 'Код из письма',
                counterText: '',
              ),
              validator: (value) {
                final hasPasswords =
                    _password.text.isNotEmpty ||
                    _confirmPassword.text.isNotEmpty;
                return hasPasswords ? _codeValidator(value) : null;
              },
            ),
            const SizedBox(height: 12),
            PasswordTextFormField(
              controller: _password,
              textInputAction: TextInputAction.next,
              labelText: 'Новый пароль',
              validator: (value) {
                if (_code.text.trim().isEmpty && (value ?? '').isEmpty) {
                  return null;
                }
                return _passwordValidator(value);
              },
            ),
            const SizedBox(height: 12),
            PasswordTextFormField(
              controller: _confirmPassword,
              textInputAction: TextInputAction.done,
              labelText: 'Повторите новый пароль',
              validator: (value) {
                if (_code.text.trim().isEmpty && (value ?? '').isEmpty) {
                  return null;
                }
                final text = value ?? '';
                if (text != _password.text) {
                  return 'Пароли не совпадают.';
                }
                return _passwordValidator(value);
              },
            ),
            const SizedBox(height: 20),
            FilledButton.icon(
              onPressed: _resetPassword,
              icon: const Icon(Icons.check_circle_outline),
              label: const Text('Сменить пароль'),
            ),
            const SizedBox(height: 8),
            OutlinedButton.icon(
              onPressed: _requestCode,
              icon: const Icon(Icons.mail_outline),
              label: const Text('Получить код'),
            ),
          ],
        ),
      ),
    );
  }

  void _requestCode() {
    final error = _emailValidator(_email.text);
    if (error != null) {
      _formKey.currentState!.validate();
      return;
    }

    Navigator.of(
      context,
    ).pop(_PasswordRecoveryResult.request(email: _email.text.trim()));
  }

  void _resetPassword() {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    if (_codeValidator(_code.text) != null) {
      _formKey.currentState!.validate();
      return;
    }

    Navigator.of(context).pop(
      _PasswordRecoveryResult.reset(
        email: _email.text.trim(),
        code: _code.text.trim(),
        newPassword: _password.text,
        confirmPassword: _confirmPassword.text,
      ),
    );
  }
}

class _AuthSheetFrame extends StatelessWidget {
  const _AuthSheetFrame({
    required this.title,
    required this.icon,
    required this.child,
  });

  final String title;
  final IconData icon;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final bottom = MediaQuery.viewInsetsOf(context).bottom;

    return SafeArea(
      child: Padding(
        padding: EdgeInsets.fromLTRB(16, 16, 16, bottom + 16),
        child: ConstrainedBox(
          constraints: BoxConstraints(
            maxHeight: MediaQuery.sizeOf(context).height * 0.86,
          ),
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Row(
                  children: [
                    Icon(icon, color: Theme.of(context).colorScheme.primary),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        title,
                        style: Theme.of(context).textTheme.titleLarge,
                      ),
                    ),
                    IconButton(
                      onPressed: () => Navigator.of(context).pop(),
                      icon: const Icon(Icons.close),
                      tooltip: 'Закрыть',
                    ),
                  ],
                ),
                const SizedBox(height: 16),
                child,
              ],
            ),
          ),
        ),
      ),
    );
  }
}

String? _requiredText(String? value, String message) {
  final text = value?.trim() ?? '';
  if (text.isEmpty) {
    return message;
  }
  return _maxLength(value, 100);
}

String? _maxLength(String? value, int maxLength) {
  final text = value?.trim() ?? '';
  if (text.length > maxLength) {
    return 'Не больше $maxLength символов.';
  }
  return null;
}

String? _emailValidator(String? value) {
  final text = value?.trim() ?? '';
  if (text.isEmpty) {
    return 'Введите email.';
  }
  if (text.length > 256 ||
      !RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(text)) {
    return 'Введите корректный email.';
  }
  return null;
}

String? _codeValidator(String? value) {
  final text = value?.replaceAll(RegExp(r'\D'), '') ?? '';
  if (text.isEmpty) {
    return 'Введите код.';
  }
  if (text.length != 6) {
    return 'Код должен состоять из 6 цифр.';
  }
  return null;
}

String? _phoneValidator(String? value) {
  final text = value?.trim() ?? '';
  if (text.isEmpty) {
    return 'Введите телефон.';
  }
  if (text.length > 32 || !RegExp(r'^\+?[0-9\s().-]+$').hasMatch(text)) {
    return 'Введите корректный телефон.';
  }
  final digits = RegExp(r'\d').allMatches(text).length;
  if (digits < 7 || digits > 15) {
    return 'Телефон должен содержать от 7 до 15 цифр.';
  }
  return null;
}

String? _passwordValidator(String? value) {
  final text = value ?? '';
  if (text.isEmpty) {
    return 'Введите пароль.';
  }
  if (text.length < 8) {
    return 'Минимум 8 символов.';
  }
  if (text.length > 128) {
    return 'Не больше 128 символов.';
  }
  return null;
}
