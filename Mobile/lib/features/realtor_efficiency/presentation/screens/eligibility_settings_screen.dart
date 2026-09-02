import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/api_exception.dart';
import '../../data/repository/efficiency_repository.dart';
import '../../domain/models/efficiency_models.dart';

class SystemSettingsScreen extends ConsumerStatefulWidget {
  const SystemSettingsScreen({super.key});

  @override
  ConsumerState<SystemSettingsScreen> createState() =>
      _SystemSettingsScreenState();
}

class _SystemSettingsScreenState extends ConsumerState<SystemSettingsScreen> {
  late Future<_SettingsBundle> _future;
  bool _savingEligibility = false;
  bool _savingCommissions = false;
  bool _savingLevels = false;

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<_SettingsBundle>(
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

        final bundle = snapshot.data!;
        return ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Text(
              'Настройки системы',
              style: Theme.of(context).textTheme.headlineSmall,
            ),
            const SizedBox(height: 16),
            _EligibilitySection(
              settings: bundle.eligibility,
              isSaving: _savingEligibility,
              onSave: _saveEligibility,
            ),
            const SizedBox(height: 12),
            _CommissionSection(
              settings: bundle.commissions,
              isSaving: _savingCommissions,
              onSave: _saveCommissions,
            ),
            const SizedBox(height: 12),
            _LevelRulesSection(
              settings: bundle.levels,
              isSaving: _savingLevels,
              onSave: _saveLevels,
            ),
          ],
        );
      },
    );
  }

  Future<_SettingsBundle> _load() async {
    final repository = ref.read(efficiencyRepositoryProvider);
    final results = await Future.wait([
      repository.getEligibilitySettings(),
      repository.getCommissionSettings(),
      repository.getLevelSettings(),
    ]);

    return _SettingsBundle(
      eligibility: results[0] as EligibilitySettings,
      commissions: results[1] as CommissionSettings,
      levels: results[2] as LevelSettings,
    );
  }

  void _reload() {
    setState(() => _future = _load());
  }

  Future<void> _saveEligibility(EligibilitySettings settings) async {
    setState(() => _savingEligibility = true);
    try {
      await ref
          .read(efficiencyRepositoryProvider)
          .updateEligibilitySettings(settings);
      _showSnack('Настройки ограничений сохранены.');
      _reload();
    } catch (error) {
      _showSnack(_messageFrom(error));
    } finally {
      if (mounted) {
        setState(() => _savingEligibility = false);
      }
    }
  }

  Future<void> _saveCommissions(CommissionSettings settings) async {
    setState(() => _savingCommissions = true);
    try {
      await ref
          .read(efficiencyRepositoryProvider)
          .updateCommissionSettings(settings);
      _showSnack('Комиссии сохранены.');
      _reload();
    } catch (error) {
      _showSnack(_messageFrom(error));
    } finally {
      if (mounted) {
        setState(() => _savingCommissions = false);
      }
    }
  }

  Future<void> _saveLevels(LevelSettings settings) async {
    setState(() => _savingLevels = true);
    try {
      await ref
          .read(efficiencyRepositoryProvider)
          .updateLevelSettings(settings);
      _showSnack('Правила уровней сохранены.');
      _reload();
    } catch (error) {
      _showSnack(_messageFrom(error));
    } finally {
      if (mounted) {
        setState(() => _savingLevels = false);
      }
    }
  }

  String _messageFrom(Object error) {
    if (error is ApiException) {
      return error.message;
    }
    return 'Не удалось загрузить настройки.';
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

class _SettingsBundle {
  const _SettingsBundle({
    required this.eligibility,
    required this.commissions,
    required this.levels,
  });

  final EligibilitySettings eligibility;
  final CommissionSettings commissions;
  final LevelSettings levels;
}

class _EligibilitySection extends StatefulWidget {
  const _EligibilitySection({
    required this.settings,
    required this.isSaving,
    required this.onSave,
  });

  final EligibilitySettings settings;
  final bool isSaving;
  final ValueChanged<EligibilitySettings> onSave;

  @override
  State<_EligibilitySection> createState() => _EligibilitySectionState();
}

class _EligibilitySectionState extends State<_EligibilitySection> {
  late bool _enabled;
  final List<_TierLimitController> _tiers = [];

  @override
  void initState() {
    super.initState();
    _enabled = widget.settings.restrictionsEnabled;
    _tiers.addAll(
      widget.settings.priceTiers.map(
        (tier) => _TierLimitController.fromTier(tier),
      ),
    );
  }

  @override
  void dispose() {
    for (final tier in _tiers) {
      tier.dispose();
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              'Настройки ограничений дорогих заявок',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            const Text(
              'Все уровни могут брать заявки от 0. Для Junior и Standard задается максимальная цена, Top может брать любую заявку.',
            ),
            const SizedBox(height: 12),
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              value: _enabled,
              onChanged: (value) => setState(() => _enabled = value),
              title: const Text('Включить ограничения по уровню риелтора'),
            ),
            const SizedBox(height: 8),
            for (final tier in _tiers) ...[
              _PriceTierRow(tier: tier),
              const SizedBox(height: 8),
            ],
            FilledButton.icon(
              onPressed: widget.isSaving ? null : _submit,
              icon: const Icon(Icons.save_outlined),
              label: Text(
                widget.isSaving ? 'Сохраняем...' : 'Сохранить ограничения',
              ),
            ),
          ],
        ),
      ),
    );
  }

  void _submit() {
    widget.onSave(
      EligibilitySettings(
        restrictionsEnabled: _enabled,
        minConfirmedHistoryDeals: 0,
        criticalComplaintLookbackDays: 365,
        scoreSnapshotMaxAgeHours: 24,
        blockOnCriticalComplaints: false,
        priceTiers: [
          for (var i = 0; i < _tiers.length; i++) _tiers[i].toTier(i),
        ],
      ),
    );
  }
}

class _PriceTierRow extends StatelessWidget {
  const _PriceTierRow({required this.tier});

  final _TierLimitController tier;

  @override
  Widget build(BuildContext context) {
    final isTop = tier.isTop;
    return DecoratedBox(
      decoration: BoxDecoration(
        border: Border.all(color: Theme.of(context).dividerColor),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              _levelTitle(tier.name),
              style: Theme.of(context).textTheme.titleSmall,
            ),
            const SizedBox(height: 8),
            const _ReadonlyLine(label: 'Цена от (USD)', value: '0'),
            const SizedBox(height: 8),
            if (isTop)
              const _ReadonlyLine(
                label: 'Максимальная цена заявки (USD)',
                value: 'Без ограничения',
              )
            else
              TextField(
                controller: tier.maxPrice,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(
                  labelText: 'Максимальная цена заявки (USD)',
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _CommissionSection extends StatefulWidget {
  const _CommissionSection({
    required this.settings,
    required this.isSaving,
    required this.onSave,
  });

  final CommissionSettings settings;
  final bool isSaving;
  final ValueChanged<CommissionSettings> onSave;

  @override
  State<_CommissionSection> createState() => _CommissionSectionState();
}

class _CommissionSectionState extends State<_CommissionSection> {
  final List<_CommissionController> _items = [];

  @override
  void initState() {
    super.initState();
    _items.addAll(widget.settings.items.map(_CommissionController.fromLevel));
  }

  @override
  void dispose() {
    for (final item in _items) {
      item.dispose();
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              'Комиссии риелторов',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 12),
            for (final item in _items) ...[
              TextField(
                controller: item.percent,
                keyboardType: TextInputType.number,
                decoration: InputDecoration(
                  labelText: _levelTitle(item.level),
                  suffixText: '%',
                ),
              ),
              const SizedBox(height: 8),
            ],
            FilledButton.icon(
              onPressed: widget.isSaving ? null : _submit,
              icon: const Icon(Icons.save_outlined),
              label: Text(
                widget.isSaving ? 'Сохраняем...' : 'Сохранить комиссии',
              ),
            ),
          ],
        ),
      ),
    );
  }

  void _submit() {
    widget.onSave(
      CommissionSettings(items: _items.map((item) => item.toLevel()).toList()),
    );
  }
}

class _LevelRulesSection extends StatefulWidget {
  const _LevelRulesSection({
    required this.settings,
    required this.isSaving,
    required this.onSave,
  });

  final LevelSettings settings;
  final bool isSaving;
  final ValueChanged<LevelSettings> onSave;

  @override
  State<_LevelRulesSection> createState() => _LevelRulesSectionState();
}

class _LevelRulesSectionState extends State<_LevelRulesSection> {
  final List<_LevelRuleController> _rules = [];

  @override
  void initState() {
    super.initState();
    _rules.addAll(widget.settings.rules.map(_LevelRuleController.fromRule));
  }

  @override
  void dispose() {
    for (final rule in _rules) {
      rule.dispose();
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              'Правила уровней риелторов',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 12),
            for (final rule in _rules) ...[
              _LevelRuleEditor(rule: rule),
              const SizedBox(height: 8),
            ],
            FilledButton.icon(
              onPressed: widget.isSaving ? null : _submit,
              icon: const Icon(Icons.save_outlined),
              label: Text(
                widget.isSaving ? 'Сохраняем...' : 'Сохранить правила уровней',
              ),
            ),
          ],
        ),
      ),
    );
  }

  void _submit() {
    widget.onSave(
      LevelSettings(
        demotionBuffer: widget.settings.demotionBuffer,
        rules: [for (var i = 0; i < _rules.length; i++) _rules[i].toRule(i)],
      ),
    );
  }
}

class _LevelRuleEditor extends StatelessWidget {
  const _LevelRuleEditor({required this.rule});

  final _LevelRuleController rule;

  @override
  Widget build(BuildContext context) {
    final isJunior = rule.isJunior;
    return DecoratedBox(
      decoration: BoxDecoration(
        border: Border.all(color: Theme.of(context).dividerColor),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              _levelTitle(rule.level),
              style: Theme.of(context).textTheme.titleSmall,
            ),
            const SizedBox(height: 8),
            if (isJunior)
              const _ReadonlyLine(
                label: 'Минимум завершенных сделок',
                value: 'Базовый уровень',
              )
            else
              TextField(
                controller: rule.minDeals,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(
                  labelText: 'Мин. завершенных сделок',
                ),
              ),
            if (!isJunior) ...[
              const SizedBox(height: 8),
              TextField(
                controller: rule.minCts,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(labelText: 'Мин. CTS'),
              ),
              const SizedBox(height: 8),
              TextField(
                controller: rule.minAps,
                keyboardType: TextInputType.number,
                decoration: const InputDecoration(labelText: 'Мин. APS'),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _ReadonlyLine extends StatelessWidget {
  const _ReadonlyLine({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(child: Text(label)),
        const SizedBox(width: 12),
        Flexible(
          child: Text(
            value,
            textAlign: TextAlign.end,
            style: const TextStyle(fontWeight: FontWeight.w600),
          ),
        ),
      ],
    );
  }
}

class _TierLimitController {
  _TierLimitController({required this.name, required this.maxPrice});

  factory _TierLimitController.fromTier(EligibilityTier tier) {
    return _TierLimitController(
      name: tier.name,
      maxPrice: TextEditingController(
        text: tier.maxPrice == null ? '' : _formatNumber(tier.maxPrice!),
      ),
    );
  }

  final String name;
  final TextEditingController maxPrice;

  bool get isTop => name.toLowerCase() == 'top';

  EligibilityTier toTier(int index) {
    return EligibilityTier(
      name: name,
      minPrice: 0,
      maxPrice: isTop ? null : _decimal(maxPrice.text),
      minClientTrustScore: 0,
      minAdminPerformanceScore: 0,
      sortOrder: index,
    );
  }

  void dispose() {
    maxPrice.dispose();
  }
}

class _CommissionController {
  _CommissionController({required this.level, required this.percent});

  factory _CommissionController.fromLevel(CommissionLevel level) {
    return _CommissionController(
      level: level.level,
      percent: TextEditingController(text: _formatNumber(level.percent)),
    );
  }

  final String level;
  final TextEditingController percent;

  CommissionLevel toLevel() {
    return CommissionLevel(level: level, percent: _decimal(percent.text) ?? 0);
  }

  void dispose() {
    percent.dispose();
  }
}

class _LevelRuleController {
  _LevelRuleController({
    required this.level,
    required this.minDeals,
    required this.minCts,
    required this.minAps,
  });

  factory _LevelRuleController.fromRule(LevelRule rule) {
    return _LevelRuleController(
      level: rule.level,
      minDeals: TextEditingController(text: rule.minCompletedDeals.toString()),
      minCts: TextEditingController(
        text: _formatNumber(rule.minClientTrustScore),
      ),
      minAps: TextEditingController(
        text: _formatNumber(rule.minAdminPerformanceScore),
      ),
    );
  }

  final String level;
  final TextEditingController minDeals;
  final TextEditingController minCts;
  final TextEditingController minAps;

  bool get isJunior => level.toLowerCase() == 'junior';

  LevelRule toRule(int index) {
    return LevelRule(
      level: level,
      minCompletedDeals: isJunior ? 0 : int.tryParse(minDeals.text) ?? 0,
      minClientTrustScore: isJunior ? 0 : _decimal(minCts.text) ?? 0,
      minAdminPerformanceScore: isJunior ? 0 : _decimal(minAps.text) ?? 0,
      sortOrder: index,
    );
  }

  void dispose() {
    minDeals.dispose();
    minCts.dispose();
    minAps.dispose();
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
              const Icon(Icons.error_outline),
              const SizedBox(height: 8),
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

String _levelTitle(String level) {
  return switch (level.toLowerCase()) {
    'junior' => 'Junior',
    'standard' => 'Standard',
    'top' => 'Top',
    _ => level,
  };
}

String _formatNumber(num value) {
  if (value % 1 == 0) {
    return value.toStringAsFixed(0);
  }

  return value.toStringAsFixed(2);
}

double? _decimal(String value) {
  final normalized = value.trim().replaceAll(',', '.');
  if (normalized.isEmpty) {
    return null;
  }
  return double.tryParse(normalized);
}
