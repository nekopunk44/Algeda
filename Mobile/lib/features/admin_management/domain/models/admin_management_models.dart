class AdminClient {
  const AdminClient({
    required this.id,
    required this.firstName,
    required this.lastName,
    required this.phoneNumber,
    required this.createdDate,
    this.middleName,
    this.email,
  });

  final String id;
  final String firstName;
  final String lastName;
  final String? middleName;
  final String phoneNumber;
  final String? email;
  final DateTime createdDate;

  String get fullName {
    return [
      lastName,
      firstName,
      if (middleName != null && middleName!.trim().isNotEmpty) middleName,
    ].whereType<String>().join(' ');
  }
}

class AdminCurrency {
  const AdminCurrency({
    required this.id,
    required this.code,
    required this.name,
    required this.symbol,
    required this.rateToBase,
    required this.isActive,
    required this.updatedAtUtc,
    required this.createdDate,
  });

  final String id;
  final String code;
  final String name;
  final String symbol;
  final double rateToBase;
  final bool isActive;
  final DateTime updatedAtUtc;
  final DateTime createdDate;
}

class AdminCriterionPage {
  const AdminCriterionPage({
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.items,
  });

  final int page;
  final int pageSize;
  final int totalCount;
  final List<AdminCriterion> items;

  int get totalPages {
    if (pageSize <= 0) {
      return 1;
    }
    final pages = (totalCount / pageSize).ceil();
    return pages < 1 ? 1 : pages;
  }
}

class AdminCriterion {
  const AdminCriterion({
    required this.id,
    required this.code,
    required this.displayName,
    required this.valueType,
    required this.isHidden,
    required this.options,
    required this.createdDate,
    this.category,
    this.description,
  });

  final String id;
  final String code;
  final String displayName;
  final String valueType;
  final String? category;
  final String? description;
  final bool isHidden;
  final List<AdminCriterionOption> options;
  final DateTime createdDate;

  bool get usesOptions =>
      valueType == 'SingleSelect' || valueType == 'MultiSelect';
}

class AdminCriterionOption {
  const AdminCriterionOption({
    required this.value,
    required this.label,
    required this.sortOrder,
  });

  final String value;
  final String label;
  final int sortOrder;
}

class AdminCriterionFormData {
  const AdminCriterionFormData({
    required this.code,
    required this.displayName,
    required this.valueType,
    required this.isHidden,
    this.category,
    this.description,
    this.options = const [],
  });

  final String code;
  final String displayName;
  final String valueType;
  final String? category;
  final String? description;
  final bool isHidden;
  final List<AdminCriterionOption> options;

  Map<String, dynamic> toJson() {
    return {
      'code': code.trim(),
      'displayName': displayName.trim(),
      'valueType': valueType,
      'category': _nullable(category),
      'description': _nullable(description),
      'isHidden': isHidden,
      'options': options
          .map(
            (item) => {
              'value': item.value.trim(),
              'label': item.label.trim(),
              'sortOrder': item.sortOrder,
            },
          )
          .toList(),
    };
  }
}

class AdminAccessUser {
  const AdminAccessUser({
    required this.userId,
    required this.email,
    required this.emailConfirmed,
    required this.isFrozen,
    required this.roles,
    this.displayName,
  });

  final String userId;
  final String email;
  final String? displayName;
  final bool emailConfirmed;
  final bool isFrozen;
  final List<String> roles;

  bool hasRole(String role) {
    return roles.any((item) => item.toLowerCase() == role.toLowerCase());
  }
}

String? _nullable(String? value) {
  final trimmed = value?.trim();
  return trimmed == null || trimmed.isEmpty ? null : trimmed;
}

const adminCriterionValueTypes = [
  'Boolean',
  'Number',
  'Text',
  'SingleSelect',
  'MultiSelect',
];

const adminAccessRoles = ['Admin', 'SuperAdmin', 'Realtor', 'Client'];

String criterionValueTypeLabel(String value) {
  return switch (value) {
    'Boolean' => 'Логический',
    'Number' => 'Число',
    'Text' => 'Текст',
    'SingleSelect' => 'Одиночный выбор',
    'MultiSelect' => 'Множественный выбор',
    _ => value,
  };
}
