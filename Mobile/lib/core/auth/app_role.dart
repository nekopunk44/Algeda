enum AppRole {
  realtor,
  admin;

  String get title {
    return switch (this) {
      AppRole.realtor => 'Риелтор',
      AppRole.admin => 'Администратор',
    };
  }

  String get homePath {
    return switch (this) {
      AppRole.realtor => '/realtor',
      AppRole.admin => '/admin',
    };
  }

  bool ownsPath(String path) {
    return switch (this) {
      AppRole.realtor => path.startsWith('/realtor'),
      AppRole.admin => path.startsWith('/admin'),
    };
  }

  static AppRole fromBackendRoles(Iterable<String> roles) {
    final normalized = roles.map((role) => role.toLowerCase()).toSet();
    if (normalized.contains('admin') || normalized.contains('superadmin')) {
      return AppRole.admin;
    }

    return AppRole.realtor;
  }
}
