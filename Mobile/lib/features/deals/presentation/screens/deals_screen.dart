import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../../core/auth/app_role.dart';
import '../../../../core/errors/api_exception.dart';
import '../../data/repository/deals_repository.dart';
import '../../domain/models/deal_models.dart';

class DealsScreen extends ConsumerStatefulWidget {
  const DealsScreen({required this.role, super.key});

  final AppRole role;

  @override
  ConsumerState<DealsScreen> createState() => _DealsScreenState();
}

class _DealsScreenState extends ConsumerState<DealsScreen> {
  final _searchController = TextEditingController();
  late DealFilters _filters;
  late Future<List<DealWorkflow>> _future;

  bool get _isAdmin => widget.role == AppRole.admin;

  @override
  void initState() {
    super.initState();
    _filters = DealFilters(
      scope: _isAdmin ? DealScope.all : DealScope.incoming,
    );
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
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text('Заявки', style: Theme.of(context).textTheme.headlineSmall),
              IconButton(
                onPressed: _reload,
                icon: const Icon(Icons.refresh),
                tooltip: 'Обновить',
              ),
            ],
          ),
          const SizedBox(height: 12),
          if (!_isAdmin) _scopeTabs(),
          _filtersCard(),
          const SizedBox(height: 12),
          FutureBuilder<List<DealWorkflow>>(
            future: _future,
            builder: (context, snapshot) {
              if (snapshot.connectionState == ConnectionState.waiting) {
                return const Padding(
                  padding: EdgeInsets.only(top: 48),
                  child: Center(child: CircularProgressIndicator()),
                );
              }

              if (snapshot.hasError) {
                return _ErrorCard(
                  message: _messageFrom(snapshot.error!),
                  onRetry: _reload,
                );
              }

              final deals = snapshot.data ?? const [];
              if (deals.isEmpty) {
                return const _EmptyDealsCard();
              }

              return Column(
                children: [
                  Align(
                    alignment: Alignment.centerLeft,
                    child: Text(
                      'Найдено: ${deals.length}',
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                  ),
                  const SizedBox(height: 8),
                  for (final deal in deals) ...[
                    _DealCard(
                      deal: deal,
                      role: widget.role,
                      onTap: () => context.push(_detailPath(deal.id)),
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

  Widget _scopeTabs() {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: SegmentedButton<DealScope>(
        segments: const [
          ButtonSegment(
            value: DealScope.incoming,
            label: Text('Входящие'),
            icon: Icon(Icons.inbox_outlined),
          ),
          ButtonSegment(
            value: DealScope.mine,
            label: Text('Мои'),
            icon: Icon(Icons.assignment_ind_outlined),
          ),
        ],
        selected: {_filters.scope},
        onSelectionChanged: (value) {
          _setFilters(_filters.copyWith(scope: value.first));
        },
      ),
    );
  }

  Widget _filtersCard() {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          children: [
            TextField(
              controller: _searchController,
              textInputAction: TextInputAction.search,
              decoration: InputDecoration(
                labelText: 'Поиск по клиенту',
                hintText: 'ФИО, телефон или email',
                prefixIcon: const Icon(Icons.search),
                suffixIcon: _searchController.text.isEmpty
                    ? null
                    : IconButton(
                        onPressed: () {
                          _searchController.clear();
                          _setFilters(_filters.copyWith(search: ''));
                        },
                        icon: const Icon(Icons.clear),
                      ),
              ),
              onSubmitted: (value) {
                _setFilters(_filters.copyWith(search: value));
              },
            ),
            const SizedBox(height: 12),
            LayoutBuilder(
              builder: (context, constraints) {
                final sourceDropdown =
                    DropdownButtonFormField<DealSourceFilter>(
                      initialValue: _filters.source,
                      isExpanded: true,
                      decoration: const InputDecoration(labelText: 'Тип'),
                      items: [
                        for (final item in DealSourceFilter.values)
                          DropdownMenuItem(
                            value: item,
                            child: Text(
                              item.label,
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                      ],
                      onChanged: (value) {
                        if (value != null) {
                          _setFilters(_filters.copyWith(source: value));
                        }
                      },
                    );
                final statusDropdown =
                    DropdownButtonFormField<DealStatusFilter>(
                      initialValue: _filters.status,
                      isExpanded: true,
                      decoration: const InputDecoration(labelText: 'Статус'),
                      items: [
                        for (final item in DealStatusFilter.values)
                          DropdownMenuItem(
                            value: item,
                            child: Text(
                              item.label,
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                      ],
                      onChanged: (value) {
                        if (value != null) {
                          _setFilters(_filters.copyWith(status: value));
                        }
                      },
                    );

                if (constraints.maxWidth < 360) {
                  return Column(
                    children: [
                      sourceDropdown,
                      const SizedBox(height: 12),
                      statusDropdown,
                    ],
                  );
                }

                return Row(
                  children: [
                    Expanded(child: sourceDropdown),
                    const SizedBox(width: 8),
                    Expanded(child: statusDropdown),
                  ],
                );
              },
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: () {
                      _setFilters(
                        _filters.copyWith(search: _searchController.text),
                      );
                    },
                    icon: const Icon(Icons.filter_alt_outlined),
                    label: const Text('Применить'),
                  ),
                ),
                const SizedBox(width: 8),
                IconButton.outlined(
                  onPressed: _resetFilters,
                  icon: const Icon(Icons.restart_alt),
                  tooltip: 'Сбросить',
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Future<List<DealWorkflow>> _load() {
    return ref.read(dealsRepositoryProvider).getDeals(_filters);
  }

  void _setFilters(DealFilters filters) {
    setState(() {
      _filters = filters;
      _future = _load();
    });
  }

  void _resetFilters() {
    _searchController.clear();
    _setFilters(DealFilters(scope: _isAdmin ? DealScope.all : _filters.scope));
  }

  void _reload() {
    setState(() {
      _future = _load();
    });
  }

  String _detailPath(String id) {
    return _isAdmin ? '/admin/deals/$id' : '/realtor/deals/$id';
  }

  String _messageFrom(Object error) {
    if (error is ApiException) {
      return error.message;
    }

    return 'Не удалось загрузить заявки.';
  }
}

class _DealCard extends StatelessWidget {
  const _DealCard({
    required this.deal,
    required this.role,
    required this.onTap,
  });

  final DealWorkflow deal;
  final AppRole role;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final formatter = DateFormat('dd.MM.yyyy HH:mm');

    return Card(
      child: InkWell(
        borderRadius: BorderRadius.circular(8),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Text(
                      deal.clientFullName,
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                  ),
                  if (deal.hasUnread)
                    Badge(
                      label: Text(deal.unreadCount.toString()),
                      child: const Icon(Icons.mark_chat_unread_outlined),
                    ),
                ],
              ),
              const SizedBox(height: 4),
              Text(
                deal.propertyTitle?.isNotEmpty == true
                    ? deal.propertyTitle!
                    : 'Объект не указан',
              ),
              const SizedBox(height: 8),
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  Chip(label: Text(dealSourceLabel(deal.source))),
                  Chip(label: Text(dealStatusLabel(deal.status))),
                  if (deal.isIncoming) const Chip(label: Text('Входящая')),
                ],
              ),
              const SizedBox(height: 8),
              _InfoLine(
                icon: Icons.phone_outlined,
                text: deal.clientPhoneNumber,
              ),
              if (deal.clientEmail?.isNotEmpty == true)
                _InfoLine(icon: Icons.email_outlined, text: deal.clientEmail!),
              if (deal.realtorFullName?.isNotEmpty == true)
                _InfoLine(
                  icon: Icons.badge_outlined,
                  text: 'Риелтор: ${deal.realtorFullName}',
                ),
              _InfoLine(
                icon: Icons.schedule,
                text: formatter.format(deal.createdDate.toLocal()),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _InfoLine extends StatelessWidget {
  const _InfoLine({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: 4),
      child: Row(
        children: [
          Icon(icon, size: 16),
          const SizedBox(width: 6),
          Expanded(child: Text(text)),
        ],
      ),
    );
  }
}

class _EmptyDealsCard extends StatelessWidget {
  const _EmptyDealsCard();

  @override
  Widget build(BuildContext context) {
    return const Card(
      child: Padding(
        padding: EdgeInsets.all(16),
        child: Text('По текущим фильтрам заявки не найдены.'),
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
    );
  }
}
