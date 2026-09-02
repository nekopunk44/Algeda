import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/errors/api_exception.dart';
import '../../../analytics/data/repository/analytics_repository.dart';
import '../../../analytics/domain/models/analytics_models.dart';
import '../../../chats/data/repository/chat_repository.dart';
import '../../../chats/domain/models/chat_models.dart';
import '../../../deals/domain/models/deal_models.dart';

class AdminDashboardScreen extends ConsumerStatefulWidget {
  const AdminDashboardScreen({super.key});

  @override
  ConsumerState<AdminDashboardScreen> createState() {
    return _AdminDashboardScreenState();
  }
}

class _AdminDashboardScreenState extends ConsumerState<AdminDashboardScreen> {
  late Future<_AdminDashboardPayload> _future;

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
          FutureBuilder<_AdminDashboardPayload>(
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
                        'Активные сделки',
                        payload.analytics.activeDeals.toString(),
                        Icons.assignment_outlined,
                      ),
                      _Metric(
                        'Открытые жалобы',
                        payload.openComplaints.toString(),
                        Icons.report_problem_outlined,
                      ),
                      _Metric(
                        'Чаты',
                        payload.chats.length.toString(),
                        Icons.forum_outlined,
                      ),
                      _Metric(
                        'Объекты',
                        payload.analytics.properties.length.toString(),
                        Icons.home_work_outlined,
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  _QuickActions(
                    items: [
                      _ActionItem(
                        label: 'Заявки',
                        icon: Icons.assignment_outlined,
                        path: '/admin/deals',
                      ),
                      _ActionItem(
                        label: 'Жалобы',
                        icon: Icons.report_problem_outlined,
                        path: '/admin/complaints',
                      ),
                      _ActionItem(
                        label: 'Аналитика',
                        icon: Icons.analytics_outlined,
                        path: '/admin/analytics',
                      ),
                      _ActionItem(
                        label: 'Чаты',
                        icon: Icons.forum_outlined,
                        path: '/admin/chats',
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  _QualityCard(payload: payload.analytics),
                  const SizedBox(height: 12),
                  _RecentDealsCard(deals: payload.analytics.deals),
                ],
              );
            },
          ),
        ],
      ),
    );
  }

  Future<_AdminDashboardPayload> _load() async {
    final analytics = await ref
        .read(analyticsRepositoryProvider)
        .loadAdminAnalytics(const AnalyticsFilters());
    final chats = await ref.read(chatRepositoryProvider).getAdminChats();
    return _AdminDashboardPayload(analytics: analytics, chats: chats);
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

class _AdminDashboardPayload {
  const _AdminDashboardPayload({required this.analytics, required this.chats});

  final AdminAnalyticsPayload analytics;
  final List<DealChatSummary> chats;

  int get openComplaints {
    return analytics.openComplaints + analytics.inProgressComplaints;
  }
}

class _QualityCard extends StatelessWidget {
  const _QualityCard({required this.payload});

  final AdminAnalyticsPayload payload;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Качество', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 12),
            _Line(
              label: 'Конверсия заявок',
              value: '${payload.conversionRate.toStringAsFixed(1)}%',
            ),
            _Line(
              label: 'Качество карточек',
              value: '${payload.averageCardQuality.toStringAsFixed(2)} / 5',
            ),
            _Line(
              label: 'Решенные жалобы',
              value: payload.resolvedComplaints.toString(),
            ),
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
              'Последние заявки',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            if (recent.isEmpty)
              const Text('Заявки пока не найдены.')
            else
              for (final deal in recent)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(deal.clientFullName),
                  subtitle: Text(dealStatusLabel(deal.status)),
                  trailing: deal.hasUnread
                      ? Badge(label: Text(deal.unreadCount.toString()))
                      : null,
                  onTap: () => context.push('/admin/deals/${deal.id}'),
                ),
          ],
        ),
      ),
    );
  }
}

class _Line extends StatelessWidget {
  const _Line({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        children: [
          Expanded(child: Text(label)),
          Text(value, style: Theme.of(context).textTheme.titleSmall),
        ],
      ),
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
