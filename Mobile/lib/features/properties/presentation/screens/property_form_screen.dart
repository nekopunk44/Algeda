import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:geolocator/geolocator.dart';
import 'package:go_router/go_router.dart';
import 'package:image_picker/image_picker.dart';

import '../../../../core/auth/app_role.dart';
import '../../../../core/config/api_config.dart';
import '../../../../core/errors/api_exception.dart';
import '../../data/repository/properties_repository.dart';
import '../../domain/models/property_models.dart';

class PropertyFormScreen extends ConsumerStatefulWidget {
  const PropertyFormScreen({required this.role, this.propertyId, super.key});

  final AppRole role;
  final String? propertyId;

  @override
  ConsumerState<PropertyFormScreen> createState() => _PropertyFormScreenState();
}

class _PropertyFormScreenState extends ConsumerState<PropertyFormScreen> {
  final _formKey = GlobalKey<FormState>();
  final _titleController = TextEditingController();
  final _addressController = TextEditingController();
  final _priceController = TextEditingController();
  final _currencyController = TextEditingController(text: 'USD');
  final _areaController = TextEditingController();
  final _roomsController = TextEditingController();
  final _latitudeController = TextEditingController();
  final _longitudeController = TextEditingController();
  final _ownerNameController = TextEditingController();
  final _ownerEmailController = TextEditingController();
  final _ownerPhoneController = TextEditingController();

  late Future<_PropertyFormPayload> _future;
  final List<String> _photoPaths = [];
  final List<XFile> _pendingPhotos = [];
  final List<PropertyCriterionDraft> _criteriaDrafts = [];
  final Set<String> _existingCriterionIds = {};

  String _type = 'Apartment';
  String? _responsibleRealtorId;
  bool _didPopulate = false;
  bool _isSaving = false;
  bool _isLocating = false;

  bool get _isEdit =>
      widget.propertyId != null && widget.propertyId!.isNotEmpty;
  bool get _isAdmin => widget.role == AppRole.admin;
  String get _prefix => _isAdmin ? '/admin' : '/realtor';

  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  @override
  void dispose() {
    _titleController.dispose();
    _addressController.dispose();
    _priceController.dispose();
    _currencyController.dispose();
    _areaController.dispose();
    _roomsController.dispose();
    _latitudeController.dispose();
    _longitudeController.dispose();
    _ownerNameController.dispose();
    _ownerEmailController.dispose();
    _ownerPhoneController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final apiSettings = ref.watch(apiSettingsControllerProvider);
    return FutureBuilder<_PropertyFormPayload>(
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

        final payload = snapshot.data ?? const _PropertyFormPayload();
        final loaded = payload.property;
        if (!_didPopulate) {
          _populate(loaded?.toFormData() ?? PropertyFormData.empty());
          _populateCriteria(payload.definitions, payload.criteria);
        }

        return Form(
          key: _formKey,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              Row(
                children: [
                  IconButton(
                    onPressed: () => _goBack(loaded?.id),
                    icon: const Icon(Icons.arrow_back),
                    tooltip: 'Назад',
                  ),
                  Expanded(
                    child: Text(
                      _isEdit ? 'Редактировать объект' : 'Добавить объект',
                      style: Theme.of(context).textTheme.headlineSmall,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              _MainFields(
                titleController: _titleController,
                addressController: _addressController,
                priceController: _priceController,
                currencyController: _currencyController,
                areaController: _areaController,
                roomsController: _roomsController,
                latitudeController: _latitudeController,
                longitudeController: _longitudeController,
                type: _type,
                onTypeChanged: (value) => setState(() => _type = value),
                isLocating: _isLocating,
                onUseCurrentLocation: _useCurrentLocation,
              ),
              const SizedBox(height: 12),
              _OwnerFields(
                ownerNameController: _ownerNameController,
                ownerEmailController: _ownerEmailController,
                ownerPhoneController: _ownerPhoneController,
              ),
              const SizedBox(height: 12),
              _PhotosEditor(
                photoPaths: _photoPaths,
                pendingPhotos: _pendingPhotos,
                apiSettings: apiSettings,
                onPick: _pickPhotos,
                onRemove: (index) =>
                    setState(() => _photoPaths.removeAt(index)),
                onMoveUp: (index) => _movePhoto(index, -1),
                onMoveDown: (index) => _movePhoto(index, 1),
                onClearPending: () => setState(_pendingPhotos.clear),
              ),
              const SizedBox(height: 12),
              _PropertyCriteriaEditor(
                drafts: _criteriaDrafts,
                onChanged: (index, draft) {
                  setState(() => _criteriaDrafts[index] = draft);
                },
              ),
              const SizedBox(height: 16),
              FilledButton.icon(
                onPressed: _isSaving ? null : _save,
                icon: _isSaving
                    ? const SizedBox.square(
                        dimension: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.save_outlined),
                label: Text(_isEdit ? 'Сохранить' : 'Создать'),
              ),
            ],
          ),
        );
      },
    );
  }

  Future<_PropertyFormPayload> _load() async {
    final id = widget.propertyId;
    final repo = ref.read(propertiesRepositoryProvider);
    if (id == null || id.isEmpty) {
      final definitions = await repo.getCriterionDefinitions();
      return _PropertyFormPayload(definitions: definitions);
    }

    final result = await Future.wait([
      repo.getPropertyForManagement(id),
      repo.getCriterionDefinitions(),
      repo.getCriteria(id),
    ]);

    return _PropertyFormPayload(
      property: result[0] as PropertyItem,
      definitions: result[1] as List<PropertyCriterionDefinition>,
      criteria: result[2] as List<PropertyCriterionValue>,
    );
  }

  void _reload() {
    setState(() {
      _didPopulate = false;
      _pendingPhotos.clear();
      _future = _load();
    });
  }

  void _populate(PropertyFormData data) {
    _titleController.text = data.title;
    _addressController.text = data.address;
    _priceController.text = data.price.toStringAsFixed(0);
    _currencyController.text = data.priceCurrency;
    _areaController.text = data.area.toString();
    _roomsController.text = data.roomsCount.toString();
    _latitudeController.text = data.latitude.toStringAsFixed(6);
    _longitudeController.text = data.longitude.toStringAsFixed(6);
    _ownerNameController.text = data.ownerFullName;
    _ownerEmailController.text = data.ownerEmail;
    _ownerPhoneController.text = data.ownerPhoneNumber;
    _type = propertyTypes.contains(data.type) ? data.type : 'Apartment';
    _responsibleRealtorId = data.responsibleRealtorId;
    _photoPaths
      ..clear()
      ..addAll(data.photoPaths);
    _didPopulate = true;
  }

  void _populateCriteria(
    List<PropertyCriterionDefinition> definitions,
    List<PropertyCriterionValue> values,
  ) {
    final valueMap = {
      for (final value in values) value.criterionDefinitionId: value,
    };
    _existingCriterionIds
      ..clear()
      ..addAll(valueMap.keys);
    _criteriaDrafts
      ..clear()
      ..addAll([
        for (final definition in definitions)
          _draftFromValue(definition, valueMap[definition.id]),
      ]);
  }

  Future<void> _pickPhotos() async {
    final picked = await ImagePicker().pickMultiImage();
    if (picked.isEmpty) {
      return;
    }

    if (_photoPaths.length + _pendingPhotos.length + picked.length > 20) {
      _showSnack('Можно загрузить не более 20 фотографий.');
      return;
    }

    setState(() {
      _pendingPhotos.addAll(picked);
    });
  }

  void _movePhoto(int index, int delta) {
    final newIndex = index + delta;
    if (newIndex < 0 || newIndex >= _photoPaths.length) {
      return;
    }

    setState(() {
      final item = _photoPaths.removeAt(index);
      _photoPaths.insert(newIndex, item);
    });
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate() || _isSaving) {
      return;
    }
    final invalidCriterion = _criteriaDrafts
        .where((item) => item.enabled)
        .any((item) => !item.hasValue);
    if (invalidCriterion) {
      _showSnack('Заполните включенные доп. характеристики или отключите их.');
      return;
    }

    setState(() {
      _isSaving = true;
    });

    try {
      final repo = ref.read(propertiesRepositoryProvider);
      final uploaded = await repo.uploadPhotos(_pendingPhotos);
      if (uploaded.isNotEmpty) {
        if (!mounted) {
          return;
        }

        setState(() {
          _photoPaths.addAll(uploaded);
          _pendingPhotos.clear();
        });
      }

      final data = _collectData(_photoPaths);
      final saved = _isEdit
          ? await repo.updateProperty(id: widget.propertyId!, data: data)
          : await repo.createProperty(data);
      await _saveCriteria(repo, saved.id);

      if (!mounted) {
        return;
      }

      context.go('$_prefix/properties/${saved.id}');
    } on ApiException catch (error) {
      _showSnack(error.message);
    } catch (_) {
      _showSnack('Не удалось сохранить объект.');
    } finally {
      if (mounted) {
        setState(() {
          _isSaving = false;
        });
      }
    }
  }

  Future<void> _saveCriteria(
    PropertiesRepository repo,
    String propertyId,
  ) async {
    final activeIds = <String>{};
    for (final draft in _criteriaDrafts) {
      if (draft.hasValue) {
        activeIds.add(draft.definition.id);
        await repo.upsertCriterion(propertyId: propertyId, draft: draft);
      }
    }

    for (final criterionId in _existingCriterionIds.difference(activeIds)) {
      await repo.removeCriterion(
        propertyId: propertyId,
        criterionDefinitionId: criterionId,
      );
    }
  }

  Future<void> _useCurrentLocation() async {
    if (_isLocating) {
      return;
    }

    setState(() => _isLocating = true);
    try {
      final serviceEnabled = await Geolocator.isLocationServiceEnabled();
      if (!serviceEnabled) {
        _showSnack('Включите геолокацию на телефоне.');
        return;
      }

      var permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
      }
      if (permission == LocationPermission.denied ||
          permission == LocationPermission.deniedForever) {
        _showSnack('Разрешите доступ к геолокации для приложения.');
        return;
      }

      final position = await Geolocator.getCurrentPosition(
        locationSettings: const LocationSettings(
          accuracy: LocationAccuracy.high,
          timeLimit: Duration(seconds: 12),
        ),
      );
      if (!mounted) {
        return;
      }

      setState(() {
        _latitudeController.text = position.latitude.toStringAsFixed(6);
        _longitudeController.text = position.longitude.toStringAsFixed(6);
      });
      _showSnack('Точка объекта обновлена по вашей геолокации.');
    } catch (_) {
      _showSnack('Не удалось получить геолокацию.');
    } finally {
      if (mounted) {
        setState(() => _isLocating = false);
      }
    }
  }

  PropertyFormData _collectData(List<String> photoPaths) {
    return PropertyFormData(
      title: _titleController.text,
      address: _addressController.text,
      price: double.parse(_priceController.text.replaceAll(',', '.')),
      priceCurrency: _currencyController.text,
      area: double.parse(_areaController.text.replaceAll(',', '.')),
      roomsCount: int.parse(_roomsController.text),
      latitude: double.parse(_latitudeController.text.replaceAll(',', '.')),
      longitude: double.parse(_longitudeController.text.replaceAll(',', '.')),
      type: _type,
      photoPaths: photoPaths,
      ownerFullName: _ownerNameController.text,
      ownerEmail: _ownerEmailController.text,
      ownerPhoneNumber: _ownerPhoneController.text,
      responsibleRealtorId: _responsibleRealtorId,
    );
  }

  void _goBack(String? id) {
    if (context.canPop()) {
      context.pop();
      return;
    }

    if (id != null && id.isNotEmpty) {
      context.go('$_prefix/properties/$id');
      return;
    }

    context.go('$_prefix/properties');
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

    return 'Не удалось загрузить объект.';
  }
}

class _PropertyFormPayload {
  const _PropertyFormPayload({
    this.property,
    this.definitions = const [],
    this.criteria = const [],
  });

  final PropertyItem? property;
  final List<PropertyCriterionDefinition> definitions;
  final List<PropertyCriterionValue> criteria;
}

PropertyCriterionDraft _draftFromValue(
  PropertyCriterionDefinition definition,
  PropertyCriterionValue? value,
) {
  if (value == null) {
    return PropertyCriterionDraft(
      definition: definition,
      value: definition.isBoolean ? 'true' : null,
    );
  }

  return PropertyCriterionDraft(
    definition: definition,
    enabled: true,
    value: definition.isMultiSelect ? null : value.value,
    values: definition.isMultiSelect ? _readMultiValue(value.value) : const [],
  );
}

List<String> _readMultiValue(String raw) {
  try {
    final decoded = jsonDecode(raw);
    if (decoded is List) {
      return decoded.map((item) => item.toString()).toList();
    }
  } catch (_) {
    // Старые значения могли храниться обычной строкой.
  }
  return raw
      .split(',')
      .map((item) => item.trim())
      .where((item) => item.isNotEmpty)
      .toList();
}

class _PropertyCriteriaEditor extends StatelessWidget {
  const _PropertyCriteriaEditor({
    required this.drafts,
    required this.onChanged,
  });

  final List<PropertyCriterionDraft> drafts;
  final void Function(int index, PropertyCriterionDraft draft) onChanged;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Доп. характеристики',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 4),
            Text(
              'Заполните характеристики, которые важны для подбора и качества карточки.',
              style: Theme.of(context).textTheme.bodySmall,
            ),
            const SizedBox(height: 12),
            if (drafts.isEmpty)
              const Text('Справочник критериев пока пуст.')
            else
              for (var i = 0; i < drafts.length; i++) ...[
                _CriterionDraftTile(
                  draft: drafts[i],
                  onChanged: (draft) => onChanged(i, draft),
                ),
                if (i != drafts.length - 1) const Divider(),
              ],
          ],
        ),
      ),
    );
  }
}

class _CriterionDraftTile extends StatelessWidget {
  const _CriterionDraftTile({required this.draft, required this.onChanged});

  final PropertyCriterionDraft draft;
  final ValueChanged<PropertyCriterionDraft> onChanged;

  @override
  Widget build(BuildContext context) {
    final definition = draft.definition;

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SwitchListTile.adaptive(
            contentPadding: EdgeInsets.zero,
            value: draft.enabled,
            onChanged: (enabled) {
              onChanged(
                draft.copyWith(
                  enabled: enabled,
                  value: _normalizedInitialValue(definition, draft),
                ),
              );
            },
            title: Text(
              definition.displayName,
              style: Theme.of(context).textTheme.titleSmall,
            ),
            subtitle: Text(
              [
                propertyCriterionValueTypeLabel(definition.valueType),
                if (definition.category?.trim().isNotEmpty == true)
                  definition.category!,
              ].join(' · '),
            ),
          ),
          if (definition.description?.trim().isNotEmpty == true)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Text(
                definition.description!,
                style: Theme.of(context).textTheme.bodySmall,
              ),
            ),
          if (draft.enabled) _valueEditor(context, definition),
        ],
      ),
    );
  }

  Widget _valueEditor(
    BuildContext context,
    PropertyCriterionDefinition definition,
  ) {
    if (definition.isBoolean) {
      return SwitchListTile.adaptive(
        contentPadding: EdgeInsets.zero,
        value: draft.value?.toLowerCase() != 'false',
        title: const Text('Да'),
        onChanged: (value) => onChanged(draft.copyWith(value: '$value')),
      );
    }

    if (definition.isSingleSelect) {
      return DropdownButtonFormField<String>(
        initialValue:
            definition.options.any((item) => item.value == draft.value)
            ? draft.value
            : (definition.options.isEmpty
                  ? null
                  : definition.options.first.value),
        isExpanded: true,
        decoration: const InputDecoration(labelText: 'Значение'),
        items: [
          for (final option in definition.options)
            DropdownMenuItem(value: option.value, child: Text(option.label)),
        ],
        onChanged: (value) => onChanged(draft.copyWith(value: value)),
        validator: (_) => draft.hasValue ? null : 'Выберите значение.',
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
              selected: draft.values.contains(option.value),
              onSelected: (selected) {
                final values = [...draft.values];
                selected
                    ? values.add(option.value)
                    : values.remove(option.value);
                onChanged(draft.copyWith(values: values));
              },
            ),
        ],
      );
    }

    return TextFormField(
      initialValue: draft.value,
      keyboardType: definition.isNumber
          ? const TextInputType.numberWithOptions(decimal: true)
          : TextInputType.text,
      decoration: InputDecoration(
        labelText: definition.isNumber ? 'Числовое значение' : 'Значение',
      ),
      validator: (value) {
        final text = value?.trim() ?? '';
        if (text.isEmpty) {
          return 'Заполните значение.';
        }
        if (definition.isNumber &&
            double.tryParse(text.replaceAll(',', '.')) == null) {
          return 'Введите число.';
        }
        return null;
      },
      onChanged: (value) => onChanged(draft.copyWith(value: value)),
    );
  }

  String? _normalizedInitialValue(
    PropertyCriterionDefinition definition,
    PropertyCriterionDraft draft,
  ) {
    if (draft.value?.trim().isNotEmpty == true) {
      return draft.value;
    }
    if (definition.isBoolean) {
      return 'true';
    }
    if (definition.isSingleSelect && definition.options.isNotEmpty) {
      return definition.options.first.value;
    }
    return draft.value;
  }
}

class _MainFields extends StatelessWidget {
  const _MainFields({
    required this.titleController,
    required this.addressController,
    required this.priceController,
    required this.currencyController,
    required this.areaController,
    required this.roomsController,
    required this.latitudeController,
    required this.longitudeController,
    required this.type,
    required this.onTypeChanged,
    required this.isLocating,
    required this.onUseCurrentLocation,
  });

  final TextEditingController titleController;
  final TextEditingController addressController;
  final TextEditingController priceController;
  final TextEditingController currencyController;
  final TextEditingController areaController;
  final TextEditingController roomsController;
  final TextEditingController latitudeController;
  final TextEditingController longitudeController;
  final String type;
  final ValueChanged<String> onTypeChanged;
  final bool isLocating;
  final VoidCallback onUseCurrentLocation;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          children: [
            TextFormField(
              controller: titleController,
              decoration: const InputDecoration(labelText: 'Заголовок'),
              validator: _required,
            ),
            const SizedBox(height: 8),
            DropdownButtonFormField<String>(
              initialValue: type,
              decoration: const InputDecoration(labelText: 'Тип'),
              items: [
                for (final item in propertyTypes)
                  DropdownMenuItem(
                    value: item,
                    child: Text(propertyTypeLabel(item)),
                  ),
              ],
              onChanged: (value) {
                if (value != null) {
                  onTypeChanged(value);
                }
              },
            ),
            const SizedBox(height: 8),
            TextFormField(
              controller: addressController,
              decoration: const InputDecoration(labelText: 'Адрес'),
              validator: _required,
            ),
            const SizedBox(height: 8),
            Row(
              children: [
                Expanded(
                  flex: 2,
                  child: TextFormField(
                    controller: priceController,
                    decoration: const InputDecoration(labelText: 'Цена'),
                    keyboardType: TextInputType.number,
                    validator: _positiveNumber,
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: TextFormField(
                    controller: currencyController,
                    decoration: const InputDecoration(labelText: 'Валюта'),
                    textCapitalization: TextCapitalization.characters,
                    validator: _currency,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Row(
              children: [
                Expanded(
                  child: TextFormField(
                    controller: areaController,
                    decoration: const InputDecoration(labelText: 'Площадь, м2'),
                    keyboardType: TextInputType.number,
                    validator: _positiveNumber,
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: TextFormField(
                    controller: roomsController,
                    decoration: const InputDecoration(labelText: 'Комнат'),
                    keyboardType: TextInputType.number,
                    validator: _positiveInt,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Row(
              children: [
                Expanded(
                  child: TextFormField(
                    controller: latitudeController,
                    decoration: const InputDecoration(labelText: 'Широта'),
                    keyboardType: TextInputType.number,
                    validator: (value) => _rangeNumber(value, -90, 90),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: TextFormField(
                    controller: longitudeController,
                    decoration: const InputDecoration(labelText: 'Долгота'),
                    keyboardType: TextInputType.number,
                    validator: (value) => _rangeNumber(value, -180, 180),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Align(
              alignment: Alignment.centerLeft,
              child: OutlinedButton.icon(
                onPressed: isLocating ? null : onUseCurrentLocation,
                icon: isLocating
                    ? const SizedBox.square(
                        dimension: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.my_location_outlined),
                label: const Text('Моя точка'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _OwnerFields extends StatelessWidget {
  const _OwnerFields({
    required this.ownerNameController,
    required this.ownerEmailController,
    required this.ownerPhoneController,
  });

  final TextEditingController ownerNameController;
  final TextEditingController ownerEmailController;
  final TextEditingController ownerPhoneController;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          children: [
            TextFormField(
              controller: ownerNameController,
              decoration: const InputDecoration(labelText: 'ФИО собственника'),
              validator: _required,
            ),
            const SizedBox(height: 8),
            TextFormField(
              controller: ownerEmailController,
              decoration: const InputDecoration(
                labelText: 'Email собственника',
              ),
              keyboardType: TextInputType.emailAddress,
              validator: _email,
            ),
            const SizedBox(height: 8),
            TextFormField(
              controller: ownerPhoneController,
              decoration: const InputDecoration(
                labelText: 'Телефон собственника',
              ),
              keyboardType: TextInputType.phone,
              validator: _required,
            ),
          ],
        ),
      ),
    );
  }
}

class _PhotosEditor extends StatelessWidget {
  const _PhotosEditor({
    required this.photoPaths,
    required this.pendingPhotos,
    required this.apiSettings,
    required this.onPick,
    required this.onRemove,
    required this.onMoveUp,
    required this.onMoveDown,
    required this.onClearPending,
  });

  final List<String> photoPaths;
  final List<XFile> pendingPhotos;
  final ApiSettings apiSettings;
  final VoidCallback onPick;
  final ValueChanged<int> onRemove;
  final ValueChanged<int> onMoveUp;
  final ValueChanged<int> onMoveDown;
  final VoidCallback onClearPending;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    'Фото объекта',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                ),
                IconButton.filledTonal(
                  onPressed: onPick,
                  icon: const Icon(Icons.add_photo_alternate_outlined),
                  tooltip: 'Загрузить фото',
                ),
              ],
            ),
            const SizedBox(height: 8),
            if (photoPaths.isEmpty && pendingPhotos.isEmpty)
              const Text('Фото пока не выбраны.'),
            for (var i = 0; i < photoPaths.length; i++) ...[
              _PhotoPathTile(
                path: photoPaths[i],
                index: i,
                totalCount: photoPaths.length,
                apiSettings: apiSettings,
                onRemove: () => onRemove(i),
                onMoveUp: () => onMoveUp(i),
                onMoveDown: () => onMoveDown(i),
              ),
              const SizedBox(height: 8),
            ],
            if (pendingPhotos.isNotEmpty) ...[
              const Divider(),
              Row(
                children: [
                  Expanded(child: Text('К загрузке: ${pendingPhotos.length}')),
                  TextButton.icon(
                    onPressed: onClearPending,
                    icon: const Icon(Icons.close),
                    label: const Text('Очистить'),
                  ),
                ],
              ),
              for (final file in pendingPhotos)
                ListTile(
                  leading: const Icon(Icons.image_outlined),
                  title: Text(file.name),
                  dense: true,
                ),
            ],
          ],
        ),
      ),
    );
  }
}

class _PhotoPathTile extends StatelessWidget {
  const _PhotoPathTile({
    required this.path,
    required this.index,
    required this.totalCount,
    required this.apiSettings,
    required this.onRemove,
    required this.onMoveUp,
    required this.onMoveDown,
  });

  final String path;
  final int index;
  final int totalCount;
  final ApiSettings apiSettings;
  final VoidCallback onRemove;
  final VoidCallback onMoveUp;
  final VoidCallback onMoveDown;

  @override
  Widget build(BuildContext context) {
    final url = apiSettings.resolveFileUrl(path);

    return ListTile(
      contentPadding: EdgeInsets.zero,
      leading: ClipRRect(
        borderRadius: BorderRadius.circular(6),
        child: SizedBox.square(
          dimension: 56,
          child: url == null
              ? const ColoredBox(color: Color(0xFFE8EDF5))
              : Image.network(url, fit: BoxFit.cover),
        ),
      ),
      title: Text(index == 0 ? 'Главное фото' : 'Фото ${index + 1}'),
      subtitle: Text(path, maxLines: 1, overflow: TextOverflow.ellipsis),
      trailing: Wrap(
        spacing: 2,
        children: [
          IconButton(
            onPressed: index == 0 ? null : onMoveUp,
            icon: const Icon(Icons.arrow_upward),
            tooltip: 'Выше',
          ),
          IconButton(
            onPressed: index == totalCount - 1 ? null : onMoveDown,
            icon: const Icon(Icons.arrow_downward),
            tooltip: 'Ниже',
          ),
          IconButton(
            onPressed: onRemove,
            icon: const Icon(Icons.delete_outline),
            tooltip: 'Удалить',
          ),
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

String? _required(String? value) {
  if (value == null || value.trim().isEmpty) {
    return 'Заполните поле.';
  }

  return null;
}

String? _positiveNumber(String? value) {
  final normalized = value?.replaceAll(',', '.');
  final parsed = double.tryParse(normalized ?? '');
  if (parsed == null || parsed <= 0) {
    return 'Введите число больше 0.';
  }

  return null;
}

String? _positiveInt(String? value) {
  final parsed = int.tryParse(value ?? '');
  if (parsed == null || parsed < 1) {
    return 'Введите целое число от 1.';
  }

  return null;
}

String? _rangeNumber(String? value, double min, double max) {
  final normalized = value?.replaceAll(',', '.');
  final parsed = double.tryParse(normalized ?? '');
  if (parsed == null || parsed < min || parsed > max) {
    return 'Диапазон: $min - $max.';
  }

  return null;
}

String? _currency(String? value) {
  final normalized = value?.trim();
  if (normalized == null || normalized.length != 3) {
    return 'Код из 3 букв.';
  }

  return null;
}

String? _email(String? value) {
  final requiredError = _required(value);
  if (requiredError != null) {
    return requiredError;
  }

  final normalized = value!.trim();
  if (!normalized.contains('@') || !normalized.contains('.')) {
    return 'Введите корректный email.';
  }

  return null;
}
