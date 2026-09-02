import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:latlong2/latlong.dart';

import '../../../../core/auth/app_role.dart';
import '../../../../core/errors/api_exception.dart';
import '../../../properties/domain/models/property_models.dart';
import '../../data/repository/matching_repository.dart';
import '../../domain/models/matching_models.dart';

class MatchingScreen extends ConsumerStatefulWidget {
  const MatchingScreen({required this.role, super.key});

  final AppRole role;

  @override
  ConsumerState<MatchingScreen> createState() => _MatchingScreenState();
}

class _MatchingScreenState extends ConsumerState<MatchingScreen> {
  final _formKey = GlobalKey<FormState>();
  final _mapController = MapController();
  final _latitudeController = TextEditingController(text: '47.0105');
  final _longitudeController = TextEditingController(text: '28.8638');
  final _radiusController = TextEditingController(text: '5');
  final _minPriceController = TextEditingController(text: '0');
  final _maxPriceController = TextEditingController(text: '200000');
  final _minAreaController = TextEditingController(text: '30');
  final _maxAreaController = TextEditingController();
  final _addressController = TextEditingController();
  final _minMatchController = TextEditingController(text: '70');
  final _limitController = TextEditingController(text: '20');

  final Set<String> _selectedTypes = <String>{};
  final List<MatchingCriterionValue> _criteria = [];
  late Future<_MatchingMeta> _metaFuture;
  Future<List<PropertyMatch>>? _future;
  MatchingSort _sort = MatchingSort.score;
  String _currencyCode = 'USD';
  bool _ignoreArea = false;
  bool _addressSearchInProgress = false;

  @override
  void initState() {
    super.initState();
    _radiusController.addListener(_refreshSearchForm);
    _minMatchController.addListener(_refreshSearchForm);
    _metaFuture = _loadMeta();
  }

  @override
  void dispose() {
    _radiusController.removeListener(_refreshSearchForm);
    _minMatchController.removeListener(_refreshSearchForm);
    _latitudeController.dispose();
    _longitudeController.dispose();
    _radiusController.dispose();
    _minPriceController.dispose();
    _maxPriceController.dispose();
    _minAreaController.dispose();
    _maxAreaController.dispose();
    _addressController.dispose();
    _minMatchController.dispose();
    _limitController.dispose();
    super.dispose();
  }

  void _refreshSearchForm() {
    if (mounted) {
      setState(() {});
    }
  }

  @override
  Widget build(BuildContext context) {
    final future = _future;

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text(
          'Подбор недвижимости',
          style: Theme.of(context).textTheme.headlineSmall,
        ),
        const SizedBox(height: 12),
        FutureBuilder<_MatchingMeta>(
          future: _metaFuture,
          builder: (context, snapshot) {
            final meta = snapshot.data ?? _MatchingMeta.fallback();
            return _SearchCard(
              formKey: _formKey,
              mapController: _mapController,
              selectedTypes: _selectedTypes,
              ignoreArea: _ignoreArea,
              latitudeController: _latitudeController,
              longitudeController: _longitudeController,
              radiusController: _radiusController,
              minPriceController: _minPriceController,
              maxPriceController: _maxPriceController,
              minAreaController: _minAreaController,
              maxAreaController: _maxAreaController,
              addressController: _addressController,
              minMatchController: _minMatchController,
              limitController: _limitController,
              currencies: meta.currencies,
              selectedCurrencyCode: _currencyCode,
              definitions: meta.criteriaDefinitions,
              criteria: _criteria,
              sort: _sort,
              loadingMeta: snapshot.connectionState == ConnectionState.waiting,
              searchingAddress: _addressSearchInProgress,
              metaError: snapshot.hasError
                  ? _messageFrom(snapshot.error!)
                  : null,
              onTypeToggled: _toggleType,
              onIgnoreAreaChanged: (value) {
                setState(() => _ignoreArea = value);
              },
              onCurrencyChanged: (value) {
                setState(() => _currencyCode = value);
              },
              onSortChanged: (value) {
                setState(() {
                  _sort = value;
                  _sortCurrentResults();
                });
              },
              onPointChanged: _setPoint,
              onAddressSearch: _searchAddress,
              onAddCriterion: _addCriterion,
              onUpdateCriterion: _updateCriterion,
              onRemoveCriterion: _removeCriterion,
              onSubmit: () => _runPreview(meta),
            );
          },
        ),
        const SizedBox(height: 12),
        if (future != null)
          FutureBuilder<List<PropertyMatch>>(
            future: future,
            builder: (context, snapshot) {
              if (snapshot.connectionState == ConnectionState.waiting) {
                return const Padding(
                  padding: EdgeInsets.only(top: 40),
                  child: Center(child: CircularProgressIndicator()),
                );
              }
              if (snapshot.hasError) {
                return _MessageCard(text: _messageFrom(snapshot.error!));
              }
              final items = _sorted(snapshot.data ?? const []);
              if (items.isEmpty) {
                return const _MessageCard(
                  text: 'Подходящие объекты не найдены.',
                );
              }

              return Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _ResultsMap(items: items),
                  const SizedBox(height: 12),
                  Text(
                    'Найдено объектов: ${items.length}',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  const SizedBox(height: 8),
                  for (final item in items) ...[
                    _MatchCard(
                      item: item,
                      showDistance: !_ignoreArea,
                      onOpen: () {
                        context.push(
                          '${widget.role.homePath}/properties/${item.propertyId}',
                        );
                      },
                    ),
                    const SizedBox(height: 8),
                  ],
                ],
              );
            },
          ),
      ],
    );
  }

  Future<_MatchingMeta> _loadMeta() async {
    final repo = ref.read(matchingRepositoryProvider);
    final result = await Future.wait([
      repo.getCurrencies(),
      repo.getCriteriaDefinitions(),
    ]);
    final currencies = (result[0] as List<MatchingCurrency>);
    if (currencies.any((item) => item.code == 'USD')) {
      _currencyCode = 'USD';
    } else if (currencies.isNotEmpty) {
      _currencyCode = currencies.first.code;
    }

    return _MatchingMeta(
      currencies: currencies.isEmpty
          ? _MatchingMeta.fallback().currencies
          : currencies,
      criteriaDefinitions: result[1] as List<MatchingCriterionDefinition>,
    );
  }

  void _toggleType(String type, bool selected) {
    setState(() {
      if (selected) {
        _selectedTypes.add(type);
      } else {
        _selectedTypes.remove(type);
      }
    });
  }

  void _setPoint(LatLng point) {
    setState(() {
      _latitudeController.text = point.latitude.toStringAsFixed(6);
      _longitudeController.text = point.longitude.toStringAsFixed(6);
    });
  }

  Future<void> _searchAddress() async {
    final query = _addressController.text.trim();
    if (query.isEmpty) {
      _showSnack('Введите адрес для поиска.');
      return;
    }

    setState(() => _addressSearchInProgress = true);
    try {
      final point = await ref
          .read(matchingRepositoryProvider)
          .searchAddress(query);
      if (!mounted) {
        return;
      }
      if (point == null) {
        _showSnack('Адрес не найден.');
        return;
      }
      final latLng = LatLng(point.latitude, point.longitude);
      _setPoint(latLng);
      _mapController.move(latLng, 13);
    } catch (error) {
      if (mounted) {
        _showSnack(_messageFrom(error));
      }
    } finally {
      if (mounted) {
        setState(() => _addressSearchInProgress = false);
      }
    }
  }

  Future<void> _addCriterion(
    List<MatchingCriterionDefinition> definitions,
  ) async {
    final selectedIds = _criteria.map((item) => item.definition.id).toSet();
    final available = definitions
        .where((item) => !selectedIds.contains(item.id))
        .toList();
    if (available.isEmpty) {
      _showSnack('Все доступные критерии уже добавлены.');
      return;
    }

    final definition = await showModalBottomSheet<MatchingCriterionDefinition>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.zero),
      builder: (context) => _CriterionPicker(definitions: available),
    );
    if (definition == null) {
      return;
    }

    setState(() {
      _criteria.add(
        MatchingCriterionValue(
          definition: definition,
          priority: MatchingCriterionPriority.important,
          value: _initialCriterionValue(definition),
        ),
      );
    });
  }

  void _updateCriterion(int index, MatchingCriterionValue value) {
    setState(() => _criteria[index] = value);
  }

  void _removeCriterion(int index) {
    setState(() => _criteria.removeAt(index));
  }

  void _runPreview(_MatchingMeta meta) {
    if (!_formKey.currentState!.validate()) {
      return;
    }

    final currency = meta.currencies.firstWhere(
      (item) => item.code == _currencyCode,
      orElse: () => const MatchingCurrency(
        code: 'USD',
        name: 'Доллар США',
        symbol: r'$',
        rateToBase: 1,
      ),
    );
    final minPrice = _readDouble(_minPriceController) ?? 0;
    final maxPrice = _readDouble(_maxPriceController) ?? 200000;
    final request = MatchingSearchRequest(
      desiredTypes: _selectedTypes.toList(),
      ignoreArea: _ignoreArea,
      latitude: _readDouble(_latitudeController) ?? 47.0105,
      longitude: _readDouble(_longitudeController) ?? 28.8638,
      searchRadiusKm: _readDouble(_radiusController) ?? 5,
      minPrice: minPrice * currency.rateToBase,
      maxPrice: maxPrice * currency.rateToBase,
      minArea: _readDouble(_minAreaController) ?? 30,
      maxArea: _readDouble(_maxAreaController),
      addressQuery: _addressController.text,
      minMatchPercent: _readDouble(_minMatchController) ?? 70,
      criteria: _criteria.where((item) => item.hasValue).toList(),
      sort: _sort,
      limit: (_readInt(_limitController) ?? 20).clamp(1, 100).toInt(),
    );

    setState(() {
      _future = ref.read(matchingRepositoryProvider).preview(request);
    });
  }

  void _sortCurrentResults() {
    final future = _future;
    if (future == null) {
      return;
    }
    _future = future.then(_sorted);
  }

  List<PropertyMatch> _sorted(List<PropertyMatch> source) {
    final items = [...source];
    switch (_sort) {
      case MatchingSort.distance:
        items.sort(
          (a, b) => (a.distanceMeters ?? double.infinity).compareTo(
            b.distanceMeters ?? double.infinity,
          ),
        );
      case MatchingSort.price:
        items.sort((a, b) => a.price.compareTo(b.price));
      case MatchingSort.score:
        items.sort((a, b) => b.matchScore.compareTo(a.matchScore));
    }
    return items;
  }

  double? _readDouble(TextEditingController controller) {
    final value = controller.text.trim().replaceAll(',', '.');
    if (value.isEmpty) {
      return null;
    }

    return double.tryParse(value);
  }

  int? _readInt(TextEditingController controller) {
    final value = controller.text.trim();
    if (value.isEmpty) {
      return null;
    }

    return int.tryParse(value);
  }

  String? _initialCriterionValue(MatchingCriterionDefinition definition) {
    if (definition.isBoolean) {
      return 'true';
    }
    if (definition.isSingleSelect && definition.options.isNotEmpty) {
      return definition.options.first.value;
    }
    return null;
  }

  void _showSnack(String message) {
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(SnackBar(content: Text(message)));
  }

  String _messageFrom(Object error) {
    if (error is ApiException) {
      return error.message;
    }

    return 'Не удалось выполнить подбор недвижимости.';
  }
}

class _MatchingMeta {
  const _MatchingMeta({
    required this.currencies,
    required this.criteriaDefinitions,
  });

  factory _MatchingMeta.fallback() {
    return const _MatchingMeta(
      currencies: [
        MatchingCurrency(
          code: 'USD',
          name: 'Доллар США',
          symbol: r'$',
          rateToBase: 1,
        ),
      ],
      criteriaDefinitions: [],
    );
  }

  final List<MatchingCurrency> currencies;
  final List<MatchingCriterionDefinition> criteriaDefinitions;
}

class _SearchCard extends StatelessWidget {
  const _SearchCard({
    required this.formKey,
    required this.mapController,
    required this.selectedTypes,
    required this.ignoreArea,
    required this.latitudeController,
    required this.longitudeController,
    required this.radiusController,
    required this.minPriceController,
    required this.maxPriceController,
    required this.minAreaController,
    required this.maxAreaController,
    required this.addressController,
    required this.minMatchController,
    required this.limitController,
    required this.currencies,
    required this.selectedCurrencyCode,
    required this.definitions,
    required this.criteria,
    required this.sort,
    required this.loadingMeta,
    required this.searchingAddress,
    required this.onTypeToggled,
    required this.onIgnoreAreaChanged,
    required this.onCurrencyChanged,
    required this.onSortChanged,
    required this.onPointChanged,
    required this.onAddressSearch,
    required this.onAddCriterion,
    required this.onUpdateCriterion,
    required this.onRemoveCriterion,
    required this.onSubmit,
    this.metaError,
  });

  final GlobalKey<FormState> formKey;
  final MapController mapController;
  final Set<String> selectedTypes;
  final bool ignoreArea;
  final TextEditingController latitudeController;
  final TextEditingController longitudeController;
  final TextEditingController radiusController;
  final TextEditingController minPriceController;
  final TextEditingController maxPriceController;
  final TextEditingController minAreaController;
  final TextEditingController maxAreaController;
  final TextEditingController addressController;
  final TextEditingController minMatchController;
  final TextEditingController limitController;
  final List<MatchingCurrency> currencies;
  final String selectedCurrencyCode;
  final List<MatchingCriterionDefinition> definitions;
  final List<MatchingCriterionValue> criteria;
  final MatchingSort sort;
  final bool loadingMeta;
  final bool searchingAddress;
  final String? metaError;
  final void Function(String type, bool selected) onTypeToggled;
  final ValueChanged<bool> onIgnoreAreaChanged;
  final ValueChanged<String> onCurrencyChanged;
  final ValueChanged<MatchingSort> onSortChanged;
  final ValueChanged<LatLng> onPointChanged;
  final VoidCallback onAddressSearch;
  final ValueChanged<List<MatchingCriterionDefinition>> onAddCriterion;
  final void Function(int index, MatchingCriterionValue value)
  onUpdateCriterion;
  final ValueChanged<int> onRemoveCriterion;
  final VoidCallback onSubmit;

  @override
  Widget build(BuildContext context) {
    final lat = _read(latitudeController) ?? 47.0105;
    final lng = _read(longitudeController) ?? 28.8638;
    final radiusKm = _read(radiusController) ?? 5;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Form(
          key: formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (loadingMeta) const LinearProgressIndicator(minHeight: 2),
              if (metaError != null) ...[
                _InlineWarning(text: metaError!),
                const SizedBox(height: 8),
              ],
              Text(
                'Тип недвижимости',
                style: Theme.of(context).textTheme.titleSmall,
              ),
              const SizedBox(height: 8),
              Wrap(
                spacing: 8,
                runSpacing: 4,
                children: [
                  for (final type in propertyTypes)
                    FilterChip(
                      label: Text(propertyTypeLabel(type)),
                      selected: selectedTypes.contains(type),
                      onSelected: (value) => onTypeToggled(type, value),
                    ),
                ],
              ),
              const SizedBox(height: 12),
              _TwoFields(
                first: _NumberField(
                  controller: minPriceController,
                  label: 'Цена от',
                  suffix: selectedCurrencyCode,
                  required: true,
                  min: 0,
                ),
                second: _NumberField(
                  controller: maxPriceController,
                  label: 'Цена до',
                  suffix: selectedCurrencyCode,
                  required: true,
                  minExclusive: 0,
                  compareMinController: minPriceController,
                ),
              ),
              const SizedBox(height: 8),
              DropdownButtonFormField<String>(
                initialValue:
                    currencies.any((item) => item.code == selectedCurrencyCode)
                    ? selectedCurrencyCode
                    : currencies.first.code,
                isExpanded: true,
                decoration: const InputDecoration(labelText: 'Валюта цены'),
                items: [
                  for (final currency in currencies)
                    DropdownMenuItem(
                      value: currency.code,
                      child: Text(currency.label),
                    ),
                ],
                onChanged: (value) => onCurrencyChanged(value ?? 'USD'),
              ),
              const SizedBox(height: 8),
              _TwoFields(
                first: _NumberField(
                  controller: minAreaController,
                  label: 'Площадь от',
                  suffix: 'м²',
                  required: true,
                  minExclusive: 0,
                ),
                second: _NumberField(
                  controller: maxAreaController,
                  label: 'Площадь до',
                  suffix: 'м²',
                  compareMinController: minAreaController,
                ),
              ),
              const SizedBox(height: 12),
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('Искать без ограничения района'),
                value: ignoreArea,
                onChanged: onIgnoreAreaChanged,
              ),
              AnimatedOpacity(
                opacity: ignoreArea ? 0.48 : 1,
                duration: const Duration(milliseconds: 160),
                child: IgnorePointer(
                  ignoring: ignoreArea,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      _AddressRow(
                        controller: addressController,
                        searching: searchingAddress,
                        onSearch: onAddressSearch,
                      ),
                      const SizedBox(height: 8),
                      _AreaMap(
                        controller: mapController,
                        center: LatLng(lat, lng),
                        radiusMeters: radiusKm * 1000,
                        onPointChanged: onPointChanged,
                      ),
                      const SizedBox(height: 8),
                      Row(
                        children: [
                          Expanded(
                            child: _ReadonlyField(
                              label: 'Широта',
                              value: lat.toStringAsFixed(6),
                            ),
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: _ReadonlyField(
                              label: 'Долгота',
                              value: lng.toStringAsFixed(6),
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 8),
                      _SliderNumberField(
                        controller: radiusController,
                        label: 'Радиус поиска',
                        suffix: 'км',
                        min: 0.1,
                        max: 200,
                        divisions: 1999,
                        decimalDigits: 1,
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 12),
              _CriteriaBlock(
                definitions: definitions,
                criteria: criteria,
                onAdd: onAddCriterion,
                onUpdate: onUpdateCriterion,
                onRemove: onRemoveCriterion,
              ),
              const SizedBox(height: 12),
              _SliderNumberField(
                controller: minMatchController,
                label: 'Совпадение от',
                suffix: '%',
                min: 1,
                max: 100,
                divisions: 99,
                decimalDigits: 0,
              ),
              const SizedBox(height: 8),
              _NumberField(
                controller: limitController,
                label: 'Лимит',
                required: true,
                min: 1,
                max: 100,
                decimal: false,
              ),
              const SizedBox(height: 8),
              DropdownButtonFormField<MatchingSort>(
                initialValue: sort,
                decoration: const InputDecoration(labelText: 'Сортировка'),
                items: [
                  for (final item in MatchingSort.values)
                    DropdownMenuItem(value: item, child: Text(item.label)),
                ],
                onChanged: (value) =>
                    onSortChanged(value ?? MatchingSort.score),
              ),
              const SizedBox(height: 12),
              SizedBox(
                width: double.infinity,
                child: FilledButton.icon(
                  onPressed: onSubmit,
                  icon: const Icon(Icons.manage_search),
                  label: const Text('Найти недвижимость'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  double? _read(TextEditingController controller) {
    return double.tryParse(controller.text.trim().replaceAll(',', '.'));
  }
}

class _AreaMap extends StatelessWidget {
  const _AreaMap({
    required this.controller,
    required this.center,
    required this.radiusMeters,
    required this.onPointChanged,
  });

  final MapController controller;
  final LatLng center;
  final double radiusMeters;
  final ValueChanged<LatLng> onPointChanged;

  @override
  Widget build(BuildContext context) {
    return ClipRRect(
      borderRadius: BorderRadius.circular(8),
      child: SizedBox(
        height: 240,
        child: FlutterMap(
          mapController: controller,
          options: MapOptions(
            initialCenter: center,
            initialZoom: 12,
            onTap: (_, point) => onPointChanged(point),
          ),
          children: [
            TileLayer(
              urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
              userAgentPackageName: 'com.nsp.realestate.real_estate_mobile',
            ),
            CircleLayer(
              circles: [
                CircleMarker(
                  point: center,
                  radius: radiusMeters,
                  useRadiusInMeter: true,
                  color: Theme.of(
                    context,
                  ).colorScheme.primary.withValues(alpha: 0.14),
                  borderColor: Theme.of(context).colorScheme.primary,
                  borderStrokeWidth: 2,
                ),
              ],
            ),
            MarkerLayer(
              markers: [
                Marker(
                  point: center,
                  width: 42,
                  height: 42,
                  child: Icon(
                    Icons.location_on,
                    size: 38,
                    color: Theme.of(context).colorScheme.primary,
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _ResultsMap extends StatelessWidget {
  const _ResultsMap({required this.items});

  final List<PropertyMatch> items;

  @override
  Widget build(BuildContext context) {
    final first = items.first;
    return Card(
      clipBehavior: Clip.antiAlias,
      child: SizedBox(
        height: 220,
        child: FlutterMap(
          options: MapOptions(
            initialCenter: LatLng(first.latitude, first.longitude),
            initialZoom: 12,
          ),
          children: [
            TileLayer(
              urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
              userAgentPackageName: 'com.nsp.realestate.real_estate_mobile',
            ),
            MarkerLayer(
              markers: [
                for (final item in items)
                  Marker(
                    point: LatLng(item.latitude, item.longitude),
                    width: 42,
                    height: 42,
                    child: Tooltip(
                      message: '${item.title}\n${item.matchPercentText}',
                      child: Icon(
                        Icons.location_on,
                        size: 34,
                        color: Theme.of(context).colorScheme.primary,
                      ),
                    ),
                  ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _AddressRow extends StatelessWidget {
  const _AddressRow({
    required this.controller,
    required this.searching,
    required this.onSearch,
  });

  final TextEditingController controller;
  final bool searching;
  final VoidCallback onSearch;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: TextFormField(
            controller: controller,
            decoration: const InputDecoration(labelText: 'Адрес или район'),
            maxLength: 500,
          ),
        ),
        const SizedBox(width: 8),
        IconButton.filledTonal(
          onPressed: searching ? null : onSearch,
          icon: searching
              ? const SizedBox(
                  width: 18,
                  height: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : const Icon(Icons.search),
          tooltip: 'Найти на карте',
        ),
      ],
    );
  }
}

class _CriteriaBlock extends StatelessWidget {
  const _CriteriaBlock({
    required this.definitions,
    required this.criteria,
    required this.onAdd,
    required this.onUpdate,
    required this.onRemove,
  });

  final List<MatchingCriterionDefinition> definitions;
  final List<MatchingCriterionValue> criteria;
  final ValueChanged<List<MatchingCriterionDefinition>> onAdd;
  final void Function(int index, MatchingCriterionValue value) onUpdate;
  final ValueChanged<int> onRemove;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                'Критерии',
                style: Theme.of(context).textTheme.titleSmall,
              ),
            ),
            TextButton.icon(
              onPressed: definitions.isEmpty ? null : () => onAdd(definitions),
              icon: const Icon(Icons.add),
              label: const Text('Добавить'),
            ),
          ],
        ),
        if (criteria.isEmpty)
          const Text('Можно уточнить поиск по критериям объекта.')
        else
          for (var index = 0; index < criteria.length; index++) ...[
            _CriterionEditor(
              value: criteria[index],
              onChanged: (value) => onUpdate(index, value),
              onRemove: () => onRemove(index),
            ),
            const SizedBox(height: 8),
          ],
      ],
    );
  }
}

class _CriterionEditor extends StatelessWidget {
  const _CriterionEditor({
    required this.value,
    required this.onChanged,
    required this.onRemove,
  });

  final MatchingCriterionValue value;
  final ValueChanged<MatchingCriterionValue> onChanged;
  final VoidCallback onRemove;

  @override
  Widget build(BuildContext context) {
    final definition = value.definition;
    return Container(
      decoration: BoxDecoration(
        border: Border.all(color: Theme.of(context).dividerColor),
        borderRadius: BorderRadius.circular(8),
      ),
      padding: const EdgeInsets.all(12),
      child: Column(
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  definition.displayName,
                  style: Theme.of(context).textTheme.titleSmall,
                ),
              ),
              IconButton(
                onPressed: onRemove,
                icon: const Icon(Icons.close),
                tooltip: 'Удалить критерий',
              ),
            ],
          ),
          DropdownButtonFormField<MatchingCriterionPriority>(
            initialValue: value.priority,
            decoration: const InputDecoration(labelText: 'Приоритет'),
            items: [
              for (final item in MatchingCriterionPriority.values)
                DropdownMenuItem(value: item, child: Text(item.label)),
            ],
            onChanged: (priority) {
              if (priority != null) {
                onChanged(value.copyWith(priority: priority));
              }
            },
          ),
          const SizedBox(height: 8),
          _criterionValueField(definition),
        ],
      ),
    );
  }

  Widget _criterionValueField(MatchingCriterionDefinition definition) {
    if (definition.isBoolean) {
      return SwitchListTile(
        contentPadding: EdgeInsets.zero,
        title: const Text('Требуется'),
        value: value.value?.toLowerCase() != 'false',
        onChanged: (selected) => onChanged(value.copyWith(value: '$selected')),
      );
    }
    if (definition.isSingleSelect) {
      return DropdownButtonFormField<String>(
        initialValue:
            definition.options.any((item) => item.value == value.value)
            ? value.value
            : (definition.options.isEmpty
                  ? null
                  : definition.options.first.value),
        isExpanded: true,
        decoration: const InputDecoration(labelText: 'Значение'),
        items: [
          for (final option in definition.options)
            DropdownMenuItem(value: option.value, child: Text(option.label)),
        ],
        onChanged: (selected) => onChanged(value.copyWith(value: selected)),
      );
    }
    if (definition.isMultiSelect) {
      return Wrap(
        spacing: 8,
        runSpacing: 4,
        children: [
          for (final option in definition.options)
            FilterChip(
              label: Text(option.label),
              selected: value.values.contains(option.value),
              onSelected: (selected) {
                final values = [...value.values];
                selected
                    ? values.add(option.value)
                    : values.remove(option.value);
                onChanged(value.copyWith(values: values));
              },
            ),
        ],
      );
    }

    return TextFormField(
      initialValue: value.value,
      keyboardType: definition.isNumber
          ? const TextInputType.numberWithOptions(decimal: true)
          : TextInputType.text,
      decoration: const InputDecoration(labelText: 'Значение'),
      onChanged: (text) => onChanged(value.copyWith(value: text)),
    );
  }
}

class _CriterionPicker extends StatefulWidget {
  const _CriterionPicker({required this.definitions});

  final List<MatchingCriterionDefinition> definitions;

  @override
  State<_CriterionPicker> createState() => _CriterionPickerState();
}

class _CriterionPickerState extends State<_CriterionPicker> {
  String _query = '';

  @override
  Widget build(BuildContext context) {
    final query = _query.trim().toLowerCase();
    final items = widget.definitions.where((item) {
      if (query.isEmpty) {
        return true;
      }
      return item.displayName.toLowerCase().contains(query) ||
          item.code.toLowerCase().contains(query) ||
          (item.category ?? '').toLowerCase().contains(query);
    }).toList();

    return SafeArea(
      child: Padding(
        padding: EdgeInsets.only(
          left: 16,
          right: 16,
          top: 16,
          bottom: MediaQuery.of(context).viewInsets.bottom + 16,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(
              autofocus: true,
              decoration: const InputDecoration(
                labelText: 'Поиск критерия',
                prefixIcon: Icon(Icons.search),
              ),
              onChanged: (value) => setState(() => _query = value),
            ),
            const SizedBox(height: 8),
            Flexible(
              child: ListView.builder(
                shrinkWrap: true,
                itemCount: items.length,
                itemBuilder: (context, index) {
                  final item = items[index];
                  return ListTile(
                    title: Text(item.displayName),
                    subtitle: item.category == null
                        ? null
                        : Text(item.category!),
                    onTap: () => Navigator.of(context).pop(item),
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _TwoFields extends StatelessWidget {
  const _TwoFields({required this.first, required this.second});

  final Widget first;
  final Widget second;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        if (constraints.maxWidth < 340) {
          return Column(children: [first, const SizedBox(height: 8), second]);
        }

        return Row(
          children: [
            Expanded(child: first),
            const SizedBox(width: 8),
            Expanded(child: second),
          ],
        );
      },
    );
  }
}

class _NumberField extends StatelessWidget {
  const _NumberField({
    required this.controller,
    required this.label,
    this.suffix,
    this.required = false,
    this.min,
    this.minExclusive,
    this.max,
    this.compareMinController,
    this.decimal = true,
  });

  final TextEditingController controller;
  final String label;
  final String? suffix;
  final bool required;
  final double? min;
  final double? minExclusive;
  final double? max;
  final TextEditingController? compareMinController;
  final bool decimal;

  @override
  Widget build(BuildContext context) {
    return TextFormField(
      controller: controller,
      keyboardType: TextInputType.numberWithOptions(decimal: decimal),
      decoration: InputDecoration(labelText: label, suffixText: suffix),
      validator: (value) {
        final text = value?.trim() ?? '';
        if (required && text.isEmpty) {
          return 'Укажите значение';
        }
        if (text.isEmpty) {
          return null;
        }
        final number = double.tryParse(text.replaceAll(',', '.'));
        if (number == null) {
          return 'Введите число';
        }
        if (min != null && number < min!) {
          return 'Минимум ${_format(min!)}';
        }
        if (minExclusive != null && number <= minExclusive!) {
          return 'Больше ${_format(minExclusive!)}';
        }
        if (max != null && number > max!) {
          return 'Максимум ${_format(max!)}';
        }
        final compareMin = compareMinController == null
            ? null
            : double.tryParse(
                compareMinController!.text.trim().replaceAll(',', '.'),
              );
        if (compareMin != null && number < compareMin) {
          return 'Не меньше ${_format(compareMin)}';
        }

        return null;
      },
    );
  }

  String _format(double value) {
    return value == value.roundToDouble()
        ? value.toInt().toString()
        : value.toStringAsFixed(1);
  }
}

class _SliderNumberField extends StatelessWidget {
  const _SliderNumberField({
    required this.controller,
    required this.label,
    required this.min,
    required this.max,
    required this.divisions,
    required this.decimalDigits,
    this.suffix,
  });

  final TextEditingController controller;
  final String label;
  final double min;
  final double max;
  final int divisions;
  final int decimalDigits;
  final String? suffix;

  @override
  Widget build(BuildContext context) {
    final parsed = double.tryParse(controller.text.trim().replaceAll(',', '.'));
    final value = (parsed ?? min).clamp(min, max).toDouble();
    final displayValue = decimalDigits == 0
        ? value.round().toString()
        : value.toStringAsFixed(decimalDigits);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: Theme.of(context).textTheme.titleSmall),
        const SizedBox(height: 4),
        Row(
          children: [
            Expanded(
              child: Slider(
                value: value,
                min: min,
                max: max,
                divisions: divisions,
                label: suffix == null ? displayValue : '$displayValue $suffix',
                onChanged: (next) {
                  controller.text = decimalDigits == 0
                      ? next.round().toString()
                      : next.toStringAsFixed(decimalDigits);
                },
              ),
            ),
            const SizedBox(width: 8),
            SizedBox(
              width: 96,
              child: TextFormField(
                controller: controller,
                keyboardType: TextInputType.numberWithOptions(
                  decimal: decimalDigits > 0,
                ),
                decoration: InputDecoration(suffixText: suffix),
                validator: (raw) {
                  final text = raw?.trim() ?? '';
                  if (text.isEmpty) {
                    return 'Укажите значение';
                  }
                  final number = double.tryParse(text.replaceAll(',', '.'));
                  if (number == null) {
                    return 'Введите число';
                  }
                  if (number < min) {
                    return 'Мин. ${_formatSliderNumber(min, decimalDigits)}';
                  }
                  if (number > max) {
                    return 'Макс. ${_formatSliderNumber(max, decimalDigits)}';
                  }
                  return null;
                },
              ),
            ),
          ],
        ),
      ],
    );
  }
}

String _formatSliderNumber(double value, int digits) {
  return digits == 0 ? value.round().toString() : value.toStringAsFixed(digits);
}

class _ReadonlyField extends StatelessWidget {
  const _ReadonlyField({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return InputDecorator(
      decoration: InputDecoration(labelText: label),
      child: Text(value, maxLines: 1, overflow: TextOverflow.ellipsis),
    );
  }
}

class _MatchCard extends StatelessWidget {
  const _MatchCard({
    required this.item,
    required this.showDistance,
    required this.onOpen,
  });

  final PropertyMatch item;
  final bool showDistance;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final progress = item.matchScore.clamp(0, 1).toDouble();

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Text(
                    item.title,
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                ),
                const SizedBox(width: 8),
                Text(
                  item.matchPercentText,
                  style: Theme.of(context).textTheme.titleMedium,
                ),
              ],
            ),
            const SizedBox(height: 6),
            Text(item.address),
            const SizedBox(height: 8),
            LinearProgressIndicator(value: progress),
            const SizedBox(height: 8),
            Wrap(
              spacing: 12,
              runSpacing: 4,
              children: [
                Text(item.priceText),
                Text('${item.area.toStringAsFixed(1)} м²'),
                Text('${item.roomsCount} комн.'),
                if (showDistance) Text(item.distanceText),
              ],
            ),
            const SizedBox(height: 8),
            Text(
              'База ${(item.baseScore * 100).round()}% · '
              'Критерии ${(item.criteriaScore * 100).round()}%',
              style: Theme.of(context).textTheme.bodySmall,
            ),
            Align(
              alignment: Alignment.centerRight,
              child: TextButton.icon(
                onPressed: onOpen,
                icon: const Icon(Icons.open_in_new),
                label: const Text('Открыть объект'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _InlineWarning extends StatelessWidget {
  const _InlineWarning({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        color: Theme.of(context).colorScheme.errorContainer,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Text(
        text,
        style: TextStyle(color: Theme.of(context).colorScheme.onErrorContainer),
      ),
    );
  }
}

class _MessageCard extends StatelessWidget {
  const _MessageCard({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Text(text, textAlign: TextAlign.center),
      ),
    );
  }
}
