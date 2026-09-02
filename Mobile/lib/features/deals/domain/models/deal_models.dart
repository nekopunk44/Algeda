import '../../../../core/auth/app_role.dart';

enum DealScope { incoming, mine, all }

enum DealSourceFilter { all, purchase, sale }

enum DealStatusFilter { all, created, inProgress, completed, cancelled }

class DealFilters {
  const DealFilters({
    required this.scope,
    this.source = DealSourceFilter.all,
    this.status = DealStatusFilter.all,
    this.search = '',
  });

  final DealScope scope;
  final DealSourceFilter source;
  final DealStatusFilter status;
  final String search;

  DealFilters copyWith({
    DealScope? scope,
    DealSourceFilter? source,
    DealStatusFilter? status,
    String? search,
  }) {
    return DealFilters(
      scope: scope ?? this.scope,
      source: source ?? this.source,
      status: status ?? this.status,
      search: search ?? this.search,
    );
  }
}

class DealWorkflow {
  const DealWorkflow({
    required this.id,
    required this.clientId,
    required this.clientFullName,
    required this.clientPhoneNumber,
    required this.source,
    required this.status,
    required this.isIncoming,
    required this.notes,
    required this.createdDate,
    this.clientEmail,
    this.propertyId,
    this.propertyTitle,
    this.clientRequirementId,
    this.realtorId,
    this.realtorFullName,
    this.realtorPhoneNumber,
    this.realtorEmail,
    this.requestMessage,
    this.acceptedAtUtc,
    this.rejectedAtUtc,
    this.priorityRealtorId,
    this.priorityUntilUtc,
    this.completedAtUtc,
    this.unreadCount = 0,
  });

  final String id;
  final String clientId;
  final String clientFullName;
  final String clientPhoneNumber;
  final String? clientEmail;
  final String? propertyId;
  final String? propertyTitle;
  final String? clientRequirementId;
  final String source;
  final String status;
  final bool isIncoming;
  final String? realtorId;
  final String? realtorFullName;
  final String? realtorPhoneNumber;
  final String? realtorEmail;
  final String? requestMessage;
  final DateTime? acceptedAtUtc;
  final DateTime? rejectedAtUtc;
  final String? priorityRealtorId;
  final DateTime? priorityUntilUtc;
  final DateTime? completedAtUtc;
  final List<DealNote> notes;
  final DateTime createdDate;
  final int unreadCount;

  bool get isSaleRequest => source.toLowerCase() == 'sale';
  bool get isCreated => status.toLowerCase() == 'created';
  bool get isInProgress => status.toLowerCase() == 'inprogress';
  bool get isCompleted => status.toLowerCase() == 'completed';
  bool get isCancelled => status.toLowerCase() == 'cancelled';
  bool get hasUnread => unreadCount > 0;

  DealWorkflow withUnreadCount(int value) {
    return DealWorkflow(
      id: id,
      clientId: clientId,
      clientFullName: clientFullName,
      clientPhoneNumber: clientPhoneNumber,
      clientEmail: clientEmail,
      propertyId: propertyId,
      propertyTitle: propertyTitle,
      clientRequirementId: clientRequirementId,
      source: source,
      status: status,
      isIncoming: isIncoming,
      realtorId: realtorId,
      realtorFullName: realtorFullName,
      realtorPhoneNumber: realtorPhoneNumber,
      realtorEmail: realtorEmail,
      requestMessage: requestMessage,
      acceptedAtUtc: acceptedAtUtc,
      rejectedAtUtc: rejectedAtUtc,
      priorityRealtorId: priorityRealtorId,
      priorityUntilUtc: priorityUntilUtc,
      completedAtUtc: completedAtUtc,
      notes: notes,
      createdDate: createdDate,
      unreadCount: value,
    );
  }
}

class DealNote {
  const DealNote({
    required this.id,
    required this.text,
    required this.createdDate,
    this.authorRealtorId,
    this.updatedAtUtc,
  });

  final String id;
  final String? authorRealtorId;
  final String text;
  final DateTime? updatedAtUtc;
  final DateTime createdDate;
}

class RealtorCandidate {
  const RealtorCandidate({
    required this.realtorId,
    required this.fullName,
    required this.phoneNumber,
    this.email,
  });

  final String realtorId;
  final String fullName;
  final String phoneNumber;
  final String? email;

  String get display {
    final contact = email == null || email!.isEmpty ? phoneNumber : email;
    return '$fullName · $contact';
  }
}

extension DealScopeLabels on DealScope {
  String get label {
    return switch (this) {
      DealScope.incoming => 'Входящие',
      DealScope.mine => 'Мои',
      DealScope.all => 'Все',
    };
  }
}

extension DealSourceFilterLabels on DealSourceFilter {
  String get label {
    return switch (this) {
      DealSourceFilter.all => 'Все',
      DealSourceFilter.purchase => 'Покупка',
      DealSourceFilter.sale => 'Продажа',
    };
  }

  String? get apiValue {
    return switch (this) {
      DealSourceFilter.sale => 'Sale',
      _ => null,
    };
  }
}

extension DealStatusFilterLabels on DealStatusFilter {
  String get label {
    return switch (this) {
      DealStatusFilter.all => 'Все',
      DealStatusFilter.created => 'Создана',
      DealStatusFilter.inProgress => 'В работе',
      DealStatusFilter.completed => 'Завершена',
      DealStatusFilter.cancelled => 'Отменена',
    };
  }

  String? get apiValue {
    return switch (this) {
      DealStatusFilter.all => null,
      DealStatusFilter.created => 'Created',
      DealStatusFilter.inProgress => 'InProgress',
      DealStatusFilter.completed => 'Completed',
      DealStatusFilter.cancelled => 'Cancelled',
    };
  }
}

String dealStatusLabel(String status) {
  return switch (status.toLowerCase()) {
    'created' => 'Создана',
    'inprogress' => 'В работе',
    'completed' => 'Завершена',
    'cancelled' => 'Отменена',
    _ => status,
  };
}

String dealSourceLabel(String source) {
  return switch (source.toLowerCase()) {
    'manual' => 'Вручную',
    'home' => 'Каталог',
    'matching' => 'Подбор',
    'sale' => 'Продажа',
    _ => source,
  };
}

bool canUseRealtorActions(AppRole role) => role == AppRole.realtor;
bool canUseAdminActions(AppRole role) => role == AppRole.admin;
