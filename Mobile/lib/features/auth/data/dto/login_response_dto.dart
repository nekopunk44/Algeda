class LoginResponseDto {
  const LoginResponseDto({
    required this.accessToken,
    required this.expiresAtUtc,
    required this.tokenType,
    required this.roles,
  });

  final String accessToken;
  final DateTime expiresAtUtc;
  final String tokenType;
  final List<String> roles;

  factory LoginResponseDto.fromJson(Map<String, dynamic> json) {
    final rolesJson = json['roles'] ?? json['Roles'];

    return LoginResponseDto(
      accessToken:
          (json['accessToken'] ?? json['AccessToken'])?.toString() ?? '',
      expiresAtUtc: DateTime.parse(
        (json['expiresAtUtc'] ?? json['ExpiresAtUtc']).toString(),
      ),
      tokenType:
          (json['tokenType'] ?? json['TokenType'])?.toString() ?? 'Bearer',
      roles: [
        for (final role in rolesJson as List? ?? const []) role.toString(),
      ],
    );
  }
}
