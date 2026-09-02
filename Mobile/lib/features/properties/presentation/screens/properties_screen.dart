import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/auth/app_role.dart';
import '../../../../core/config/api_config.dart';
import '../../../../core/errors/api_exception.dart';
import '../../data/repository/properties_repository.dart';
import '../../domain/models/property_models.dart';

class PropertiesScreen extends ConsumerStatefulWidget {
  const PropertiesScreen({required this.role, super.key});

  final AppRole role;

  @override
  ConsumerState<PropertiesScreen> createState() => _PropertiesScreenState();
}

class _PropertiesScreenState extends ConsumerState<PropertiesScreen> {
  final _searchController = TextEditingController();
  late Future<List<PropertyItem>> _future;

  String _type = '';
  String _status = '';

  bool get _isAdmin => widget.role == AppRole.admin;
  String get _prefix => _isAdmin ? '/admin' : '/realtor';

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
    final apiSettings = ref.watch(apiSettingsControllerProvider);
    return Scaffold(
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => context.push('$_prefix/properties/new'),
        icon: const Icon(Icons.add_home_work_outlined),
        label: const Text('Добавить'),
      ),
      body: RefreshIndicator(
        onRefresh: () async => _reload(),
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    _isAdmin ? 'Недвижимость' : 'Объекты',
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
            _filters(),
            const SizedBox(height: 12),
            FutureBuilder<List<PropertyItem>>(
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

                final items = _applyFilters(snapshot.data ?? const []);
                if (items.isEmpty) {
                  return const _EmptyCard();
                }

                return Column(
                  children: [
                    for (final item in items) ...[
                      _PropertyCard(
                        property: item,
                        apiSettings: apiSettings,
                        onTap: () =>
                            context.push('$_prefix/properties/${item.id}'),
                      ),
                      const SizedBox(height: 10),
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

  Widget _filters() {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          children: [
            TextField(
              controller: _searchController,
              decoration: const InputDecoration(
                labelText: 'Поиск',
                hintText: 'Название, адрес, собственник',
                prefixIcon: Icon(Icons.search),
              ),
              onChanged: (_) => setState(() {}),
            ),
            const SizedBox(height: 8),
            LayoutBuilder(
              builder: (context, constraints) {
                final typeField = DropdownButtonFormField<String>(
                  initialValue: _type,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Тип'),
                  items: [
                    const DropdownMenuItem(value: '', child: Text('Все')),
                    for (final type in propertyTypes)
                      DropdownMenuItem(
                        value: type,
                        child: Text(
                          propertyTypeLabel(type),
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                  ],
                  onChanged: (value) => setState(() => _type = value ?? ''),
                );
                final statusField = DropdownButtonFormField<String>(
                  initialValue: _status,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Статус'),
                  items: [
                    const DropdownMenuItem(value: '', child: Text('Все')),
                    for (final status in propertyStatuses)
                      DropdownMenuItem(
                        value: status,
                        child: Text(
                          propertyStatusLabel(status),
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                  ],
                  onChanged: (value) => setState(() => _status = value ?? ''),
                );

                if (constraints.maxWidth < 340) {
                  return Column(
                    children: [
                      typeField,
                      const SizedBox(height: 8),
                      statusField,
                    ],
                  );
                }

                return Row(
                  children: [
                    Expanded(child: typeField),
                    const SizedBox(width: 8),
                    Expanded(child: statusField),
                  ],
                );
              },
            ),
          ],
        ),
      ),
    );
  }

  Future<List<PropertyItem>> _load() {
    return ref.read(propertiesRepositoryProvider).getProperties();
  }

  void _reload() {
    setState(() {
      _future = _load();
    });
  }

  List<PropertyItem> _applyFilters(List<PropertyItem> source) {
    final query = _searchController.text.trim().toLowerCase();
    return source.where((item) {
      if (_type.isNotEmpty && item.type != _type) {
        return false;
      }
      if (_status.isNotEmpty && item.status != _status) {
        return false;
      }
      if (query.isEmpty) {
        return true;
      }

      return item.title.toLowerCase().contains(query) ||
          item.address.toLowerCase().contains(query) ||
          (item.ownerFullName?.toLowerCase().contains(query) ?? false) ||
          (item.ownerEmail?.toLowerCase().contains(query) ?? false) ||
          (item.ownerPhoneNumber?.toLowerCase().contains(query) ?? false);
    }).toList();
  }

  String _messageFrom(Object error) {
    if (error is ApiException) {
      return error.message;
    }

    return 'Не удалось загрузить объекты.';
  }
}

class _PropertyCard extends StatelessWidget {
  const _PropertyCard({
    required this.property,
    required this.apiSettings,
    required this.onTap,
  });

  final PropertyItem property;
  final ApiSettings apiSettings;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final imageUrl = apiSettings.resolveFileUrl(property.mainPhotoPath);

    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            AspectRatio(
              aspectRatio: 16 / 9,
              child: imageUrl == null
                  ? const ColoredBox(
                      color: Color(0xFFE8EDF5),
                      child: Center(child: Icon(Icons.home_work_outlined)),
                    )
                  : Image.network(
                      imageUrl,
                      fit: BoxFit.cover,
                      errorBuilder: (_, _, _) {
                        return const Center(child: Icon(Icons.broken_image));
                      },
                    ),
            ),
            Padding(
              padding: const EdgeInsets.all(12),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          property.title,
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                          style: Theme.of(context).textTheme.titleMedium,
                        ),
                      ),
                      const Icon(Icons.chevron_right),
                    ],
                  ),
                  const SizedBox(height: 4),
                  Text(
                    property.address,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.bodySmall,
                  ),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      Chip(
                        label: Text(
                          formatPropertyPrice(
                            property.originalPriceAmount,
                            property.originalPriceCurrency,
                          ),
                        ),
                      ),
                      Chip(label: Text(propertyTypeLabel(property.type))),
                      Chip(label: Text(propertyStatusLabel(property.status))),
                      Chip(label: Text('${property.roomsCount} комн.')),
                      Chip(
                        label: Text('${property.area.toStringAsFixed(1)} м²'),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ],
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
        child: Text('Объекты не найдены.'),
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
