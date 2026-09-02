import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../../core/auth/app_role.dart';
import '../../../../core/errors/api_exception.dart';
import '../../data/repository/efficiency_repository.dart';
import '../../domain/models/efficiency_models.dart';

class EfficiencyScreen extends ConsumerStatefulWidget {
  const EfficiencyScreen({required this.role, super.key});

  final AppRole role;

  @override
  ConsumerState<EfficiencyScreen> createState() => _EfficiencyScreenState();
}

class _EfficiencyScreenState extends ConsumerState<EfficiencyScreen> {
  late Future<_EfficiencyPayload> _future;
  String? _selectedRealtorId;
  String _search = '';

  bool get _isAdmin => widget.role == AppRole.admin;

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
                  _isAdmin ? 'Эффективность риелторов' : 'Моя эффективность',
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
          FutureBuilder<_EfficiencyPayload>(
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
              return Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (_isAdmin) _realtorPicker(payload.realtors),
                  if (payload.latest == null)
                    const _EmptyCard()
                  else ...[
                    _ScoreCard(snapshot: payload.latest!),
                    const SizedBox(height: 12),
                    _BreakdownCard(snapshot: payload.latest!),
                    const SizedBox(height: 12),
                    Text(
                      'История',
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                    const SizedBox(height: 8),
                    for (final item in payload.history) ...[
                      _HistoryTile(snapshot: item),
                      const SizedBox(height: 8),
                    ],
                  ],
                ],
              );
            },
          ),
        ],
      ),
    );
  }

  Widget _realtorPicker(List<RealtorOption> realtors) {
    final filtered = realtors.where((item) {
      final query = _search.trim().toLowerCase();
      if (query.isEmpty) {
        return true;
      }
      return item.fullName.toLowerCase().contains(query) ||
          item.phoneNumber.toLowerCase().contains(query) ||
          item.id.toLowerCase().contains(query);
    }).toList();
    final value = filtered.any((item) => item.id == _selectedRealtorId)
        ? _selectedRealtorId
        : null;

    return Card(
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
              initialValue: value,
              isExpanded: true,
              decoration: const InputDecoration(labelText: 'Риелтор'),
              items: [
                for (final item in filtered)
                  DropdownMenuItem(
                    value: item.id,
                    child: Text(item.fullName, overflow: TextOverflow.ellipsis),
                  ),
              ],
              onChanged: (value) {
                setState(() {
                  _selectedRealtorId = value;
                  _future = _load();
                });
              },
            ),
          ],
        ),
      ),
    );
  }

  Future<_EfficiencyPayload> _load() async {
    final repo = ref.read(efficiencyRepositoryProvider);
    if (!_isAdmin) {
      final latest = await _tryLoadSnapshot(repo.getMyLatest);
      final history = latest == null
          ? const <RealtorScoreSnapshot>[]
          : await _tryLoadHistory(repo.getMyHistory);
      return _EfficiencyPayload(latest: latest, history: history);
    }

    final realtors = await repo.getRealtors();
    final selected =
        _selectedRealtorId ?? (realtors.isEmpty ? null : realtors.first.id);
    _selectedRealtorId = selected;
    if (selected == null) {
      return _EfficiencyPayload(realtors: realtors);
    }

    final latest = await _tryLoadSnapshot(
      () => repo.getLatestForRealtor(selected),
    );
    final history = latest == null
        ? const <RealtorScoreSnapshot>[]
        : await _tryLoadHistory(() => repo.getHistoryForRealtor(selected));
    return _EfficiencyPayload(
      latest: latest,
      history: history,
      realtors: realtors,
    );
  }

  Future<RealtorScoreSnapshot?> _tryLoadSnapshot(
    Future<RealtorScoreSnapshot> Function() load,
  ) async {
    try {
      return await load();
    } on ApiException catch (error) {
      if (error.isNotFound) {
        return null;
      }
      rethrow;
    }
  }

  Future<List<RealtorScoreSnapshot>> _tryLoadHistory(
    Future<List<RealtorScoreSnapshot>> Function() load,
  ) async {
    try {
      return await load();
    } on ApiException catch (error) {
      if (error.isNotFound) {
        return const [];
      }
      rethrow;
    }
  }

  void _reload() {
    setState(() => _future = _load());
  }

  String _messageFrom(Object error) {
    if (error is ApiException) {
      return error.message;
    }
    return 'Не удалось загрузить эффективность.';
  }
}

class _EfficiencyPayload {
  const _EfficiencyPayload({
    this.latest,
    this.history = const [],
    this.realtors = const [],
  });

  final RealtorScoreSnapshot? latest;
  final List<RealtorScoreSnapshot> history;
  final List<RealtorOption> realtors;
}

class _ScoreCard extends StatelessWidget {
  const _ScoreCard({required this.snapshot});

  final RealtorScoreSnapshot snapshot;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            Expanded(
              child: _ScoreBox(label: 'CTS', value: snapshot.clientTrustScore),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: _ScoreBox(
                label: 'APS',
                value: snapshot.adminPerformanceScore,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ScoreBox extends StatelessWidget {
  const _ScoreBox({required this.label, required this.value});

  final String label;
  final double value;

  @override
  Widget build(BuildContext context) {
    final clamped = normalizeScore(value);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: Theme.of(context).textTheme.titleMedium),
        Text('${scoreText(clamped)} / 5'),
        const SizedBox(height: 6),
        LinearProgressIndicator(value: clamped / 5),
      ],
    );
  }
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
              'Компоненты оценки',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            _row('Сервис клиентов', cts.clientServiceScoreComponent),
            _row('Точность объекта', cts.propertyAccuracyScoreComponent),
            _row('Штраф жалоб', cts.complaintPenaltyComponent),
            const Divider(),
            _row('Качество карточек', aps.propertyDataQualityComponent),
            _row('Дисциплина сделок', aps.workflowDisciplineComponent),
            _row('Бизнес-результат', aps.businessResultComponent),
            _row('Риск репутации', aps.reputationRiskComponent),
          ],
        ),
      ),
    );
  }

  Widget _row(String label, double value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        children: [
          Expanded(child: Text(label)),
          Text(
            scoreText(value),
            style: const TextStyle(fontWeight: FontWeight.w600),
          ),
        ],
      ),
    );
  }
}

class _HistoryTile extends StatelessWidget {
  const _HistoryTile({required this.snapshot});

  final RealtorScoreSnapshot snapshot;

  @override
  Widget build(BuildContext context) {
    final date = DateFormat(
      'dd.MM.yyyy HH:mm',
    ).format(snapshot.createdDate.toLocal());
    return Card(
      child: ListTile(
        title: Text(date),
        subtitle: Text(
          'CTS ${scoreText(snapshot.clientTrustScore)} · '
          'APS ${scoreText(snapshot.adminPerformanceScore)}',
        ),
      ),
    );
  }
}

class _EmptyCard extends StatelessWidget {
  const _EmptyCard();

  @override
  Widget build(BuildContext context) {
    return const Card(
      child: Padding(
        padding: EdgeInsets.all(16),
        child: Text('Данные эффективности не найдены.'),
      ),
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
