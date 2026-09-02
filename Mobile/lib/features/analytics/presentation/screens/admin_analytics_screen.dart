import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../../core/config/api_config.dart';
import '../../../../core/errors/api_exception.dart';
import '../../../realtor_efficiency/data/repository/efficiency_repository.dart';
import '../../../realtor_efficiency/domain/models/efficiency_models.dart';
import '../../data/repository/analytics_repository.dart';
import '../../domain/models/analytics_models.dart';

class AdminAnalyticsScreen extends ConsumerStatefulWidget {
  const AdminAnalyticsScreen({super.key});

  @override
  ConsumerState<AdminAnalyticsScreen> createState() {
    return _AdminAnalyticsScreenState();
  }
}

class _AdminAnalyticsScreenState extends ConsumerState<AdminAnalyticsScreen> {
  late Future<AdminAnalyticsPayload> _future;
  AnalyticsFilters _filters = const AnalyticsFilters();
  int _tabIndex = 0;

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
                  'Аналитика',
                  style: Theme.of(context).textTheme.headlineSmall,
                ),
              ),
              IconButton(
                onPressed: _openPdf,
                icon: const Icon(Icons.picture_as_pdf_outlined),
                tooltip: 'Скачать PDF',
              ),
              IconButton(
                onPressed: _reload,
                icon: const Icon(Icons.refresh),
                tooltip: 'Обновить',
              ),
            ],
          ),
          const SizedBox(height: 12),
          _FilterCard(
            filters: _filters,
            onChanged: (value) {
              setState(() {
                _filters = value;
                _future = _load();
              });
            },
          ),
          const SizedBox(height: 12),
          _AnalyticsTabs(
            selectedIndex: _tabIndex,
            onChanged: (value) => setState(() => _tabIndex = value),
          ),
          const SizedBox(height: 12),
          FutureBuilder<AdminAnalyticsPayload>(
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
              final payload = snapshot.data!;
              return switch (_tabIndex) {
                0 => _RealtorsTab(payload: payload),
                1 => _AgencyTab(payload: payload),
                2 => _PropertiesTab(payload: payload),
                _ => _QualityTab(payload: payload),
              };
            },
          ),
        ],
      ),
    );
  }

  Future<AdminAnalyticsPayload> _load() {
    return ref.read(analyticsRepositoryProvider).loadAdminAnalytics(_filters);
  }

  void _reload() {
    setState(() => _future = _load());
  }

  Future<void> _openPdf() async {
    final query = <String, String>{
      'tab': _tabName,
      'dealType': _filters.dealType,
      'source': _filters.source,
      if (_filters.dateFrom != null)
        'dateFrom': DateFormat('yyyy-MM-dd').format(_filters.dateFrom!),
      if (_filters.dateTo != null)
        'dateTo': DateFormat('yyyy-MM-dd').format(_filters.dateTo!),
    };
    final uri = ref.read(apiSettingsControllerProvider).analyticsPdfUri(query);
    if (!await launchUrl(uri, mode: LaunchMode.externalApplication)) {
      _showSnack('Не удалось открыть PDF-отчет.');
    }
  }

  String get _tabName {
    return switch (_tabIndex) {
      0 => 'realtors',
      1 => 'agency',
      2 => 'properties',
      3 => 'quality',
      _ => 'realtors',
    };
  }

  void _showSnack(String message) {
    if (!mounted) {
      return;
    }
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(SnackBar(content: Text(message)));
  }

  String _messageFrom(Object error) {
    if (error is ApiException) {
      return error.message;
    }
    return 'Не удалось загрузить аналитику.';
  }
}

class _FilterCard extends StatelessWidget {
  const _FilterCard({required this.filters, required this.onChanged});

  final AnalyticsFilters filters;
  final ValueChanged<AnalyticsFilters> onChanged;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          children: [
            LayoutBuilder(
              builder: (context, constraints) {
                final from = _DateButton(
                  icon: Icons.date_range,
                  label: _dateLabel(filters.dateFrom, 'С даты'),
                  onPressed: () => _pick(context, true),
                );
                final to = _DateButton(
                  icon: Icons.event,
                  label: _dateLabel(filters.dateTo, 'По дату'),
                  onPressed: () => _pick(context, false),
                );

                if (constraints.maxWidth < 340) {
                  return Column(
                    children: [
                      SizedBox(width: double.infinity, child: from),
                      const SizedBox(height: 8),
                      SizedBox(width: double.infinity, child: to),
                    ],
                  );
                }

                return Row(
                  children: [
                    Expanded(child: from),
                    const SizedBox(width: 8),
                    Expanded(child: to),
                  ],
                );
              },
            ),
            const SizedBox(height: 8),
            DropdownButtonFormField<String>(
              initialValue: filters.dealType,
              isExpanded: true,
              decoration: const InputDecoration(labelText: 'Тип сделки'),
              items: const [
                DropdownMenuItem(value: 'all', child: Text('Все')),
                DropdownMenuItem(value: 'purchase', child: Text('Покупка')),
                DropdownMenuItem(value: 'sale', child: Text('Продажа')),
              ],
              onChanged: (value) {
                onChanged(filters.copyWith(dealType: value ?? 'all'));
              },
            ),
            const SizedBox(height: 8),
            DropdownButtonFormField<String>(
              initialValue: filters.source,
              isExpanded: true,
              decoration: const InputDecoration(labelText: 'Источник'),
              items: const [
                DropdownMenuItem(value: 'all', child: Text('Все источники')),
                DropdownMenuItem(value: 'Home', child: Text('Каталог')),
                DropdownMenuItem(value: 'Matching', child: Text('Подбор')),
                DropdownMenuItem(value: 'Manual', child: Text('Ручной ввод')),
                DropdownMenuItem(value: 'Sale', child: Text('Продажа')),
              ],
              onChanged: (value) {
                onChanged(filters.copyWith(source: value ?? 'all'));
              },
            ),
            const SizedBox(height: 8),
            Align(
              alignment: Alignment.centerRight,
              child: TextButton.icon(
                onPressed: () => onChanged(const AnalyticsFilters()),
                icon: const Icon(Icons.restart_alt),
                label: const Text('Сброс'),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _pick(BuildContext context, bool isFrom) async {
    final selected = await showDatePicker(
      context: context,
      firstDate: DateTime(2020),
      lastDate: DateTime.now().add(const Duration(days: 1)),
      initialDate:
          (isFrom ? filters.dateFrom : filters.dateTo) ?? DateTime.now(),
    );
    if (selected == null) {
      return;
    }
    onChanged(
      isFrom
          ? filters.copyWith(dateFrom: selected)
          : filters.copyWith(dateTo: selected),
    );
  }

  String _dateLabel(DateTime? value, String fallback) {
    if (value == null) {
      return fallback;
    }
    return DateFormat('dd.MM.yyyy').format(value);
  }
}

class _DateButton extends StatelessWidget {
  const _DateButton({
    required this.icon,
    required this.label,
    required this.onPressed,
  });

  final IconData icon;
  final String label;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return OutlinedButton.icon(
      onPressed: onPressed,
      icon: Icon(icon),
      label: Text(label, maxLines: 1, overflow: TextOverflow.ellipsis),
    );
  }
}

class _AnalyticsTabs extends StatelessWidget {
  const _AnalyticsTabs({required this.selectedIndex, required this.onChanged});

  final int selectedIndex;
  final ValueChanged<int> onChanged;

  static const _items = [
    (Icons.groups_outlined, 'Риелторы'),
    (Icons.business_center_outlined, 'Агентство'),
    (Icons.home_work_outlined, 'Объекты'),
    (Icons.verified_outlined, 'Качество'),
  ];

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(
        children: [
          for (var index = 0; index < _items.length; index++) ...[
            ChoiceChip(
              selected: selectedIndex == index,
              showCheckmark: false,
              avatar: Icon(_items[index].$1, size: 16),
              label: Text(_items[index].$2),
              onSelected: (_) => onChanged(index),
            ),
            if (index != _items.length - 1) const SizedBox(width: 8),
          ],
        ],
      ),
    );
  }
}

class _AgencyTab extends StatelessWidget {
  const _AgencyTab({required this.payload});

  final AdminAnalyticsPayload payload;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        _MetricGrid(
          items: [
            _Metric('Всего сделок', payload.totalDeals.toString()),
            _Metric('Завершено', payload.completedDeals.toString()),
            _Metric('Отменено', payload.cancelledDeals.toString()),
            _Metric(
              'Конверсия',
              '${payload.conversionRate.toStringAsFixed(2)}%',
            ),
          ],
        ),
        const SizedBox(height: 8),
        _MetricGrid(
          items: [
            _Metric('Комиссия агентства', _money(payload.totalCommissionUsd)),
            _Metric('Выплаты риелторам', _money(payload.realtorPayoutUsd)),
            _Metric('Чистый доход', _money(payload.agencyNetCommissionUsd)),
            _Metric(
              'Средний срок сделки (дни)',
              payload.averageDealDurationDays.toStringAsFixed(2),
            ),
            _Metric(
              'Подтвержденные жалобы',
              (payload.confirmedComplaints +
                      payload.partiallyConfirmedComplaints)
                  .toString(),
            ),
          ],
        ),
        const SizedBox(height: 12),
        _NamedCounts(
          title: 'Динамика завершенных сделок',
          items: payload.monthlyCompletedDeals,
          emptyText: 'Недостаточно данных для графика.',
          asBars: true,
        ),
      ],
    );
  }

  String _money(double value) => '${value.toStringAsFixed(0)} USD';
}

class _RealtorsTab extends ConsumerStatefulWidget {
  const _RealtorsTab({required this.payload});

  final AdminAnalyticsPayload payload;

  @override
  ConsumerState<_RealtorsTab> createState() => _RealtorsTabState();
}

class _RealtorsTabState extends ConsumerState<_RealtorsTab> {
  String? _selectedId;
  String _search = '';
  Future<_RealtorPanelPayload>? _future;

  @override
  void didUpdateWidget(covariant _RealtorsTab oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.payload.realtors != widget.payload.realtors) {
      _selectedId = null;
      _future = null;
    }
  }

  @override
  Widget build(BuildContext context) {
    final realtors = widget.payload.realtors;
    if (realtors.isEmpty) {
      return const _EmptyCard(text: 'Риелторы не найдены.');
    }

    _selectedId ??= realtors.first.id;
    _future ??= _load(_selectedId!);
    final filtered = _filtered(realtors);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Card(
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Column(
              children: [
                TextField(
                  decoration: const InputDecoration(
                    labelText: 'Поиск риелтора',
                    prefixIcon: Icon(Icons.search),
                  ),
                  onChanged: (value) => setState(() => _search = value),
                ),
                const SizedBox(height: 8),
                DropdownButtonFormField<String>(
                  initialValue: filtered.any((item) => item.id == _selectedId)
                      ? _selectedId
                      : null,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Риелтор'),
                  items: [
                    for (final item in filtered)
                      DropdownMenuItem(
                        value: item.id,
                        child: Text(
                          item.fullName,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                  ],
                  onChanged: (value) {
                    if (value == null) {
                      return;
                    }
                    setState(() {
                      _selectedId = value;
                      _future = _load(value);
                    });
                  },
                ),
                if (filtered.isEmpty)
                  const Padding(
                    padding: EdgeInsets.only(top: 8),
                    child: Text('Поиск не нашел риелторов.'),
                  ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 12),
        FutureBuilder<_RealtorPanelPayload>(
          future: _future,
          builder: (context, snapshot) {
            if (snapshot.connectionState == ConnectionState.waiting) {
              return const Padding(
                padding: EdgeInsets.only(top: 24),
                child: Center(child: CircularProgressIndicator()),
              );
            }
            if (snapshot.hasError) {
              return _ErrorCard(message: _messageFrom(snapshot.error!));
            }
            final data = snapshot.data!;
            final latest = data.latest;
            if (latest == null) {
              return const _EmptyCard(
                text: 'Данные по эффективности пока отсутствуют.',
              );
            }

            return Column(
              children: [
                _MetricGrid(
                  items: [
                    _Metric('CTS', scoreText(latest.clientTrustScore)),
                    _Metric('APS', scoreText(latest.adminPerformanceScore)),
                  ],
                ),
                const SizedBox(height: 12),
                _BreakdownCard(snapshot: latest),
                const SizedBox(height: 12),
                _HistoryCard(items: data.history),
              ],
            );
          },
        ),
      ],
    );
  }

  List<RealtorOption> _filtered(List<RealtorOption> source) {
    final query = _search.trim().toLowerCase();
    if (query.isEmpty) {
      return source;
    }
    final digits = query.replaceAll(RegExp(r'\D'), '');
    return source.where((item) {
      final text = '${item.fullName} ${item.phoneNumber} ${item.id}'
          .toLowerCase();
      final phoneDigits = item.phoneNumber.replaceAll(RegExp(r'\D'), '');
      return text.contains(query) ||
          (digits.isNotEmpty && phoneDigits.contains(digits));
    }).toList();
  }

  Future<_RealtorPanelPayload> _load(String realtorId) async {
    final repo = ref.read(efficiencyRepositoryProvider);
    try {
      final latest = await repo.getLatestForRealtor(realtorId);
      final history = await repo.getHistoryForRealtor(realtorId);
      return _RealtorPanelPayload(latest: latest, history: history);
    } on ApiException catch (error) {
      if (error.isNotFound) {
        return const _RealtorPanelPayload();
      }
      rethrow;
    }
  }

  String _messageFrom(Object error) {
    if (error is ApiException) {
      return error.message;
    }
    return 'Не удалось загрузить эффективность риелтора.';
  }
}

class _RealtorPanelPayload {
  const _RealtorPanelPayload({this.latest, this.history = const []});

  final RealtorScoreSnapshot? latest;
  final List<RealtorScoreSnapshot> history;
}

class _BreakdownCard extends StatelessWidget {
  const _BreakdownCard({required this.snapshot});

  final RealtorScoreSnapshot snapshot;

  @override
  Widget build(BuildContext context) {
    final cts = snapshot.clientTrustBreakdown;
    final aps = snapshot.adminPerformanceBreakdown;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Детализация CTS',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            _ScoreRow('Сервис клиента', cts.clientServiceScoreComponent),
            _ScoreRow('Штраф по жалобам', cts.complaintPenaltyComponent),
            const Divider(),
            Text(
              'Детализация APS',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            _ScoreRow('Качество карточек', aps.propertyDataQualityComponent),
            _ScoreRow('Дисциплина', aps.workflowDisciplineComponent),
            _ScoreRow('Результативность', aps.businessResultComponent),
            _ScoreRow('Репутационные риски', aps.reputationRiskComponent),
          ],
        ),
      ),
    );
  }
}

class _ScoreRow extends StatelessWidget {
  const _ScoreRow(this.label, this.value);

  final String label;
  final double value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        children: [
          Expanded(child: Text(label)),
          Text(
            scoreText(value),
            style: const TextStyle(fontWeight: FontWeight.w700),
          ),
        ],
      ),
    );
  }
}

class _HistoryCard extends StatelessWidget {
  const _HistoryCard({required this.items});

  final List<RealtorScoreSnapshot> items;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('История', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            if (items.isEmpty)
              const Text('Данные отсутствуют.')
            else
              for (final item in items) ...[
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        DateFormat(
                          'dd.MM.yyyy HH:mm',
                        ).format(item.createdDate.toLocal()),
                      ),
                    ),
                    Text('CTS ${scoreText(item.clientTrustScore)}'),
                    const SizedBox(width: 12),
                    Text('APS ${scoreText(item.adminPerformanceScore)}'),
                  ],
                ),
                const Divider(height: 16),
              ],
          ],
        ),
      ),
    );
  }
}

class _PropertiesTab extends StatelessWidget {
  const _PropertiesTab({required this.payload});

  final AdminAnalyticsPayload payload;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _MetricGrid(
          items: [
            _Metric('Продано объектов', payload.soldObjects.toString()),
            _Metric('Средняя цена', _money(payload.averageSoldPrice)),
            _Metric('Медианная цена', _money(payload.medianSoldPrice)),
            _Metric(
              'Средняя скорость продажи (дни)',
              payload.averageSaleDurationDays.toStringAsFixed(2),
            ),
            _Metric(
              'Среднее качество карточки',
              '${payload.averageCardQuality.toStringAsFixed(2)} / 5',
            ),
          ],
        ),
        const SizedBox(height: 12),
        _NamedCounts(title: 'Продано по типам', items: payload.soldByType),
      ],
    );
  }

  String _money(double value) => '${value.toStringAsFixed(0)} USD';
}

class _QualityTab extends StatelessWidget {
  const _QualityTab({required this.payload});

  final AdminAnalyticsPayload payload;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        _MetricGrid(
          items: [
            _Metric('Открытые', payload.openComplaints.toString()),
            _Metric('В работе', payload.inProgressComplaints.toString()),
            _Metric('Решенные', payload.resolvedComplaints.toString()),
            _Metric('Подтверждено', payload.confirmedComplaints.toString()),
            _Metric(
              'Частично',
              payload.partiallyConfirmedComplaints.toString(),
            ),
            _Metric(
              'Не подтверждено',
              payload.notConfirmedComplaints.toString(),
            ),
          ],
        ),
        const SizedBox(height: 12),
        _NamedCounts(
          title: 'Жалобы по категориям',
          items: payload.complaintsByCategory,
        ),
      ],
    );
  }
}

class _MetricGrid extends StatelessWidget {
  const _MetricGrid({required this.items});

  final List<_Metric> items;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final columns = constraints.maxWidth < 330 ? 1 : 2;

        return GridView.count(
          crossAxisCount: columns,
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          childAspectRatio: columns == 1 ? 3.3 : 1.45,
          crossAxisSpacing: 8,
          mainAxisSpacing: 8,
          children: [
            for (final item in items)
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        item.label,
                        maxLines: 3,
                        overflow: TextOverflow.ellipsis,
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                      const Spacer(),
                      FittedBox(
                        fit: BoxFit.scaleDown,
                        alignment: Alignment.centerLeft,
                        child: Text(
                          item.value,
                          style: Theme.of(context).textTheme.titleLarge,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
          ],
        );
      },
    );
  }
}

class _Metric {
  const _Metric(this.label, this.value);

  final String label;
  final String value;
}

class _NamedCounts extends StatelessWidget {
  const _NamedCounts({
    required this.title,
    required this.items,
    this.emptyText = 'Данные отсутствуют.',
    this.asBars = false,
  });

  final String title;
  final List<NamedCount> items;
  final String emptyText;
  final bool asBars;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(title, style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            if (items.isEmpty)
              Text(emptyText)
            else if (asBars)
              _BarList(items: items)
            else
              for (final item in items)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(
                    item.name,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                  ),
                  trailing: Text(
                    item.count.toString(),
                    style: Theme.of(context).textTheme.titleSmall,
                  ),
                ),
          ],
        ),
      ),
    );
  }
}

class _BarList extends StatelessWidget {
  const _BarList({required this.items});

  final List<NamedCount> items;

  @override
  Widget build(BuildContext context) {
    final maxValue = items
        .map((item) => item.count)
        .fold<int>(1, (max, value) => value > max ? value : max);
    return Column(
      children: [
        for (final item in items) ...[
          Row(
            children: [
              Expanded(child: Text(item.name)),
              Text(
                item.count.toString(),
                style: const TextStyle(fontWeight: FontWeight.w700),
              ),
            ],
          ),
          const SizedBox(height: 4),
          LinearProgressIndicator(value: item.count / maxValue),
          const SizedBox(height: 10),
        ],
      ],
    );
  }
}

class _EmptyCard extends StatelessWidget {
  const _EmptyCard({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(padding: const EdgeInsets.all(16), child: Text(text)),
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
