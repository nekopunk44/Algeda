import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/errors/api_exception.dart';
import '../../../complaints/data/repository/complaints_repository.dart';
import '../../../complaints/domain/models/complaint_models.dart';
import '../../../deals/data/repository/deals_repository.dart';
import '../../../deals/domain/models/deal_models.dart';
import '../../../realtor_efficiency/data/repository/efficiency_repository.dart';
import '../../../realtor_efficiency/domain/models/efficiency_models.dart';

class RealtorDashboardScreen extends ConsumerStatefulWidget {
  const RealtorDashboardScreen({super.key});

  @override
  ConsumerState<RealtorDashboardScreen> createState() {
    return _RealtorDashboardScreenState();
  }
}

class _RealtorDashboardScreenState
    extends ConsumerState<RealtorDashboardScreen> {
  late Future<_RealtorDashboardPayload> _future;

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
                  'Главная',
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
          FutureBuilder<_RealtorDashboardPayload>(
            future: _future,
            builder: (context, snapshot) {
              if (snapshot.connectionState == ConnectionState.waiting) {
                return const _LoadingCard();
              }
              if (snapshot.hasError) {
                return _ErrorCard(
                  message: _messageFrom(snapshot.error!),
                  onRetry: _reload,
                );
              }

              final payload = snapshot.data!;
              return Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _MetricGrid(
                    items: [
                      _Metric(
                        'Входящие',
                        payload.incoming.length.toString(),
                        Icons.inbox_outlined,
                      ),
                      _Metric(
                        'Мои активные',
                        payload.activeDeals.toString(),
                        Icons.assignment_ind_outlined,
                      ),
                      _Metric(
                        'Непрочитано',
                        payload.unreadMessages.toString(),
                        Icons.mark_chat_unread_outlined,
                      ),
                      _Metric(
                        'Жалобы',
                        payload.openComplaints.toString(),
                        Icons.report_outlined,
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  _QuickActions(
                    items: [
                      _ActionItem(
                        label: 'Заявки',
                        icon: Icons.assignment_outlined,
                        path: '/realtor/deals',
                      ),
                      _ActionItem(
                        label: 'Чаты',
                        icon: Icons.chat_bubble_outline,
                        path: '/realtor/chats',
                      ),
                      _ActionItem(
                        label: 'Объекты',
                        icon: Icons.home_work_outlined,
                        path: '/realtor/properties',
                      ),
                      _ActionItem(
                        label: 'Эффективность',
                        icon: Icons.insights_outlined,
                        path: '/realtor/efficiency',
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  _EfficiencyCard(score: payload.score),
                  const SizedBox(height: 12),
                  _RecentDealsCard(deals: payload.mine),
                ],
              );
            },
          ),
        ],
      ),
    );
  }

  Future<_RealtorDashboardPayload> _load() async {
    final dealsRepository = ref.read(dealsRepositoryProvider);
    final complaintsRepository = ref.read(complaintsRepositoryProvider);
    final efficiencyRepository = ref.read(efficiencyRepositoryProvider);

    final incomingFuture = dealsRepository.getDeals(
      const DealFilters(scope: DealScope.incoming),
    );
    final mineFuture = dealsRepository.getDeals(
      const DealFilters(scope: DealScope.mine),
    );
    final complaintsFuture = complaintsRepository.getMyRealtorComplaints();
    final scoreFuture = _tryLatestScore(efficiencyRepository);

    return _RealtorDashboardPayload(
      incoming: await incomingFuture,
      mine: await mineFuture,
      complaints: await complaintsFuture,
      score: await scoreFuture,
    );
  }

  Future<RealtorScoreSnapshot?> _tryLatestScore(
    EfficiencyRepository repository,
  ) async {
    try {
      return await repository.getMyLatest();
    } catch (_) {
      return null;
    }
  }

  void _reload() {
    setState(() => _future = _load());
  }

  String _messageFrom(Object error) {
    if (error is ApiException) {
      return error.message;
    }

    return 'Не удалось загрузить главную сводку.';
  }
}

class _RealtorDashboardPayload {
  const _RealtorDashboardPayload({
    required this.incoming,
    required this.mine,
    required this.complaints,
    required this.score,
  });

  final List<DealWorkflow> incoming;
  final List<DealWorkflow> mine;
  final List<ComplaintItem> complaints;
  final RealtorScoreSnapshot? score;

  int get activeDeals => mine.where((item) => item.isInProgress).length;
  int get unreadMessages => mine.fold(0, (sum, item) => sum + item.unreadCount);
  int get openComplaints {
    return complaints.where((item) => item.status != 'Resolved').length;
  }
}

class _EfficiencyCard extends StatelessWidget {
  const _EfficiencyCard({required this.score});

  final RealtorScoreSnapshot? score;

  @override
  Widget build(BuildContext context) {
    final value = score;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: value == null
            ? const Text('Расчет эффективности пока не найден.')
            : Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Эффективность',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  const SizedBox(height: 12),
                  _ScoreLine(label: 'CTS', value: value.clientTrustScore),
                  const SizedBox(height: 8),
                  _ScoreLine(label: 'APS', value: value.adminPerformanceScore),
                ],
              ),
      ),
    );
  }
}

class _RecentDealsCard extends StatelessWidget {
  const _RecentDealsCard({required this.deals});

  final List<DealWorkflow> deals;

  @override
  Widget build(BuildContext context) {
    final recent = deals.take(3).toList();

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Последние сделки',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            if (recent.isEmpty)
              const Text('Активных сделок пока нет.')
            else
              for (final deal in recent)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(deal.clientFullName),
                  subtitle: Text(dealStatusLabel(deal.status)),
                  trailing: deal.hasUnread
                      ? Badge(label: Text(deal.unreadCount.toString()))
                      : null,
                  onTap: () => context.push('/realtor/deals/${deal.id}'),
                ),
          ],
        ),
      ),
    );
  }
}

class _ScoreLine extends StatelessWidget {
  const _ScoreLine({required this.label, required this.value});

  final String label;
  final double value;

  @override
  Widget build(BuildContext context) {
    final normalized = normalizeScore(value);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(child: Text(label)),
            Text('${scoreText(normalized)} / 5'),
          ],
        ),
        const SizedBox(height: 4),
        LinearProgressIndicator(value: normalized / 5),
      ],
    );
  }
}

class _QuickActions extends StatelessWidget {
  const _QuickActions({required this.items});

  final List<_ActionItem> items;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: 8,
      runSpacing: 8,
      children: [
        for (final item in items)
          ActionChip(
            avatar: Icon(item.icon, size: 18),
            label: Text(item.label),
            onPressed: () => context.go(item.path),
          ),
      ],
    );
  }
}

class _ActionItem {
  const _ActionItem({
    required this.label,
    required this.icon,
    required this.path,
  });

  final String label;
  final IconData icon;
  final String path;
}

class _MetricGrid extends StatelessWidget {
  const _MetricGrid({required this.items});

  final List<_Metric> items;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final columns = constraints.maxWidth < 360 ? 1 : 2;
        return GridView.count(
          crossAxisCount: columns,
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          childAspectRatio: columns == 1 ? 3.6 : 1.8,
          crossAxisSpacing: 8,
          mainAxisSpacing: 8,
          children: [
            for (final item in items)
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Row(
                    children: [
                      Icon(item.icon),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Text(
                          item.label,
                          style: Theme.of(context).textTheme.bodyMedium,
                        ),
                      ),
                      Text(
                        item.value,
                        style: Theme.of(context).textTheme.titleLarge,
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
  const _Metric(this.label, this.value, this.icon);

  final String label;
  final String value;
  final IconData icon;
}

class _LoadingCard extends StatelessWidget {
  const _LoadingCard();

  @override
  Widget build(BuildContext context) {
    return const Card(
      child: Padding(
        padding: EdgeInsets.all(24),
        child: Center(child: CircularProgressIndicator()),
      ),
    );
  }
}

class _ErrorCard extends StatelessWidget {
  const _ErrorCard({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          children: [
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 8),
            TextButton.icon(
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
