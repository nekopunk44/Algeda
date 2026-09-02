import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../../../core/auth/app_role.dart';
import '../../../../core/config/api_config.dart';
import '../../../../core/errors/api_exception.dart';
import '../../data/repository/properties_repository.dart';
import '../../domain/models/property_models.dart';

class PropertyDetailsScreen extends ConsumerStatefulWidget {
  const PropertyDetailsScreen({
    required this.propertyId,
    required this.role,
    super.key,
  });

  final String propertyId;
  final AppRole role;

  @override
  ConsumerState<PropertyDetailsScreen> createState() =>
      _PropertyDetailsScreenState();
}

class _PropertyDetailsScreenState extends ConsumerState<PropertyDetailsScreen> {
  late Future<_PropertyDetailsPayload> _future;
  bool _isBusy = false;

  bool get _isAdmin => widget.role == AppRole.admin;
  String get _prefix => _isAdmin ? '/admin' : '/realtor';

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  @override
  Widget build(BuildContext context) {
    final apiSettings = ref.watch(apiSettingsControllerProvider);
    return FutureBuilder<_PropertyDetailsPayload>(
      future: _future,
      builder: (context, snapshot) {
        if (snapshot.connectionState == ConnectionState.waiting) {
          return const Center(child: CircularProgressIndicator());
        }

        if (snapshot.hasError) {
          return _LoadError(
            message: _messageFrom(snapshot.error!),
            onRetry: _reload,
          );
        }

        final payload = snapshot.data!;
        final property = payload.property;
        return RefreshIndicator(
          onRefresh: () async => _reload(),
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              Row(
                children: [
                  IconButton(
                    onPressed: () => _goBack(context, '$_prefix/properties'),
                    icon: const Icon(Icons.arrow_back),
                    tooltip: 'К списку',
                  ),
                  Expanded(
                    child: Text(
                      property.title,
                      style: Theme.of(context).textTheme.headlineSmall,
                    ),
                  ),
                  IconButton(
                    onPressed: () {
                      context.push('$_prefix/properties/${property.id}/edit');
                    },
                    icon: const Icon(Icons.edit_outlined),
                    tooltip: 'Редактировать',
                  ),
                ],
              ),
              const SizedBox(height: 12),
              _PhotosBlock(
                paths: property.photoPaths,
                apiSettings: apiSettings,
              ),
              const SizedBox(height: 12),
              _InfoCard(property: property),
              const SizedBox(height: 12),
              _OwnerCard(property: property),
              const SizedBox(height: 12),
              _CriteriaCard(criteria: payload.criteria),
              const SizedBox(height: 12),
              _ActionsCard(
                property: property,
                isBusy: _isBusy,
                onHide: () => _runAction(() => _repo.hide(property.id)),
                onShow: () => _runAction(() => _repo.show(property.id)),
                onSold: () => _runAction(() => _repo.markAsSold(property.id)),
              ),
            ],
          ),
        );
      },
    );
  }

  PropertiesRepository get _repo => ref.read(propertiesRepositoryProvider);

  Future<_PropertyDetailsPayload> _load() async {
    final result = await Future.wait([
      _repo.getPropertyForManagement(widget.propertyId),
      _repo.getCriteria(widget.propertyId),
    ]);

    return _PropertyDetailsPayload(
      property: result[0] as PropertyItem,
      criteria: result[1] as List<PropertyCriterionValue>,
    );
  }

  void _reload() {
    setState(() {
      _future = _load();
    });
  }

  Future<void> _runAction(Future<PropertyItem> Function() action) async {
    setState(() {
      _isBusy = true;
    });

    try {
      final updated = await action();
      setState(() {
        _future = _repo
            .getCriteria(updated.id)
            .then(
              (criteria) => _PropertyDetailsPayload(
                property: updated,
                criteria: criteria,
              ),
            );
      });
    } on ApiException catch (error) {
      _showSnack(error.message);
    } catch (_) {
      _showSnack('Не удалось выполнить действие.');
    } finally {
      if (mounted) {
        setState(() {
          _isBusy = false;
        });
      }
    }
  }

  void _showSnack(String message) {
    if (!mounted) {
      return;
    }

    ScaffoldMessenger.of(
      context,
    ).showSnackBar(SnackBar(content: Text(message)));
  }

  void _goBack(BuildContext context, String fallbackPath) {
    if (context.canPop()) {
      context.pop();
      return;
    }

    context.go(fallbackPath);
  }

  String _messageFrom(Object error) {
    if (error is ApiException) {
      return error.message;
    }

    return 'Не удалось открыть объект.';
  }
}

class _PropertyDetailsPayload {
  const _PropertyDetailsPayload({
    required this.property,
    required this.criteria,
  });

  final PropertyItem property;
  final List<PropertyCriterionValue> criteria;
}

class _PhotosBlock extends StatelessWidget {
  const _PhotosBlock({required this.paths, required this.apiSettings});

  final List<String> paths;
  final ApiSettings apiSettings;

  @override
  Widget build(BuildContext context) {
    if (paths.isEmpty) {
      return const AspectRatio(
        aspectRatio: 16 / 9,
        child: Card(child: Center(child: Icon(Icons.home_work_outlined))),
      );
    }

    return SizedBox(
      height: 220,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: paths.length,
        separatorBuilder: (_, _) => const SizedBox(width: 8),
        itemBuilder: (context, index) {
          final url = apiSettings.resolveFileUrl(paths[index]);
          return AspectRatio(
            aspectRatio: 4 / 3,
            child: Card(
              clipBehavior: Clip.antiAlias,
              child: url == null
                  ? const Center(child: Icon(Icons.image_not_supported))
                  : Image.network(
                      url,
                      fit: BoxFit.cover,
                      errorBuilder: (_, _, _) {
                        return const Center(child: Icon(Icons.broken_image));
                      },
                    ),
            ),
          );
        },
      ),
    );
  }
}

class _InfoCard extends StatelessWidget {
  const _InfoCard({required this.property});

  final PropertyItem property;

  @override
  Widget build(BuildContext context) {
    final soldAt = property.soldAtUtc;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Данные объекта',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 12),
            _InfoRow(label: 'Цена', value: _price),
            _InfoRow(label: 'Тип', value: propertyTypeLabel(property.type)),
            _InfoRow(
              label: 'Статус',
              value: propertyStatusLabel(property.status),
            ),
            _InfoRow(label: 'Адрес', value: property.address),
            _InfoRow(
              label: 'Площадь',
              value: '${property.area.toStringAsFixed(1)} м2',
            ),
            _InfoRow(label: 'Комнат', value: property.roomsCount.toString()),
            _InfoRow(
              label: 'Широта',
              value: property.latitude.toStringAsFixed(6),
            ),
            _InfoRow(
              label: 'Долгота',
              value: property.longitude.toStringAsFixed(6),
            ),
            if (soldAt != null)
              _InfoRow(
                label: 'Продан',
                value: DateFormat('dd.MM.yyyy HH:mm').format(soldAt.toLocal()),
              ),
          ],
        ),
      ),
    );
  }

  String get _price {
    return formatPropertyPrice(
      property.originalPriceAmount,
      property.originalPriceCurrency,
    );
  }
}

class _OwnerCard extends StatelessWidget {
  const _OwnerCard({required this.property});

  final PropertyItem property;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Собственник', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 12),
            _InfoRow(label: 'ФИО', value: property.ownerFullName ?? '-'),
            _InfoRow(label: 'Email', value: property.ownerEmail ?? '-'),
            _InfoRow(label: 'Телефон', value: property.ownerPhoneNumber ?? '-'),
          ],
        ),
      ),
    );
  }
}

class _CriteriaCard extends StatelessWidget {
  const _CriteriaCard({required this.criteria});

  final List<PropertyCriterionValue> criteria;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Доп. характеристики',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 12),
            if (criteria.isEmpty)
              const Text('Дополнительные характеристики пока не заполнены.')
            else
              for (final item in criteria) ...[
                _CriterionValueTile(item: item),
                if (item != criteria.last) const Divider(),
              ],
          ],
        ),
      ),
    );
  }
}

class _CriterionValueTile extends StatelessWidget {
  const _CriterionValueTile({required this.item});

  final PropertyCriterionValue item;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Text(
                  item.criterionDisplayName,
                  style: Theme.of(context).textTheme.titleSmall,
                ),
              ),
              const SizedBox(width: 8),
              Flexible(
                child: Text(
                  item.effectiveValue,
                  textAlign: TextAlign.end,
                  style: Theme.of(context).textTheme.titleSmall,
                ),
              ),
            ],
          ),
          if (item.category?.trim().isNotEmpty == true) ...[
            const SizedBox(height: 2),
            Text(item.category!, style: Theme.of(context).textTheme.bodySmall),
          ],
          if (item.criterionDescription?.trim().isNotEmpty == true) ...[
            const SizedBox(height: 4),
            Text(item.criterionDescription!),
          ],
        ],
      ),
    );
  }
}

class _ActionsCard extends StatelessWidget {
  const _ActionsCard({
    required this.property,
    required this.isBusy,
    required this.onHide,
    required this.onShow,
    required this.onSold,
  });

  final PropertyItem property;
  final bool isBusy;
  final VoidCallback onHide;
  final VoidCallback onShow;
  final VoidCallback onSold;

  @override
  Widget build(BuildContext context) {
    final isHidden = property.status == 'Hidden';
    final isSold = property.status == 'Sold';

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            if (isHidden)
              FilledButton.icon(
                onPressed: isBusy ? null : onShow,
                icon: const Icon(Icons.visibility_outlined),
                label: const Text('Показать'),
              )
            else
              OutlinedButton.icon(
                onPressed: isBusy ? null : onHide,
                icon: const Icon(Icons.visibility_off_outlined),
                label: const Text('Скрыть'),
              ),
            FilledButton.tonalIcon(
              onPressed: isBusy || isSold ? null : onSold,
              icon: const Icon(Icons.sell_outlined),
              label: const Text('Продано'),
            ),
          ],
        ),
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 110,
            child: Text(label, style: Theme.of(context).textTheme.bodySmall),
          ),
          Expanded(child: Text(value)),
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
