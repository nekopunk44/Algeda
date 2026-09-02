class DealDocument {
  const DealDocument({
    required this.id,
    required this.dealId,
    required this.title,
    required this.originalFileName,
    required this.contentType,
    required this.fileSizeBytes,
    required this.uploadedByDisplayName,
    required this.createdDate,
    this.uploadedByEmail,
  });

  final String id;
  final String dealId;
  final String title;
  final String originalFileName;
  final String contentType;
  final int fileSizeBytes;
  final String uploadedByDisplayName;
  final String? uploadedByEmail;
  final DateTime createdDate;

  String get uploadedByText {
    final email = uploadedByEmail?.trim();
    if (email == null || email.isEmpty) {
      return uploadedByDisplayName;
    }
    return '$uploadedByDisplayName ($email)';
  }
}

class DealDocumentAccessLog {
  const DealDocumentAccessLog({
    required this.id,
    required this.dealId,
    required this.documentId,
    required this.documentTitle,
    required this.documentFileName,
    required this.action,
    required this.actorDisplayName,
    required this.actorRole,
    required this.createdDate,
    required this.dealSummary,
    required this.clientSummary,
    required this.realtorSummary,
    this.actorEmail,
    this.ipAddress,
  });

  final String id;
  final String dealId;
  final String documentId;
  final String documentTitle;
  final String documentFileName;
  final String action;
  final String actorDisplayName;
  final String? actorEmail;
  final String actorRole;
  final DateTime createdDate;
  final String? ipAddress;
  final String dealSummary;
  final String clientSummary;
  final String realtorSummary;

  String get actorText {
    final email = actorEmail?.trim();
    if (email == null || email.isEmpty) {
      return actorDisplayName;
    }
    return '$actorDisplayName ($email)';
  }
}

String dealDocumentActionLabel(String action) {
  return switch (action.toLowerCase()) {
    'added' => 'Добавление',
    'opened' => 'Открытие',
    'deleted' => 'Удаление',
    _ => action,
  };
}

String formatDealDocumentSize(int bytes) {
  if (bytes <= 0) {
    return '0 КБ';
  }

  if (bytes < 1024 * 1024) {
    final kb = (bytes / 1024).ceil();
    return '$kb КБ';
  }

  final mb = bytes / (1024 * 1024);
  return '${mb.toStringAsFixed(mb >= 10 ? 0 : 1)} МБ';
}
