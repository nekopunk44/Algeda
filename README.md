# Algeda

Единый монорепозиторий агентства недвижимости: публичный и внутренний Web,
HTTP API, доменная логика, PostgreSQL/PostGIS и Android-приложение для
риелторов и администраторов.

## Состав

```text
API/             ASP.NET Core API, JWT, SignalR
Application/     сценарии, DTO, валидация
Domain/          сущности и бизнес-правила
Infrastructure/  EF Core, Identity, PostgreSQL, email, хранение фото
Web/             ASP.NET Core MVC
Mobile/          Flutter-приложение для Android
tests/           unit- и integration-тесты .NET
```

В репозитории нет дампов, локальных файлов БД, seed-пользователей, паролей,
JWT-ключей и production-конфигурации. Схема БД хранится только в EF Core
миграциях `Infrastructure/Persistence/Migrations`.

## Быстрый запуск через Docker

Понадобится Docker Compose.

```powershell
Copy-Item .env.example .env
```

Заполни в `.env` как минимум `POSTGRES_PASSWORD` и `JWT_SIGNING_KEY`.
JWT-ключ должен содержать не менее 32 символов. Сгенерировать случайное
значение можно так:

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
```

После этого:

```powershell
docker compose up --build
```

- Web: `http://localhost:5000`
- API: `http://localhost:5142`
- PostgreSQL: только `127.0.0.1:5433`

При первом подключении API применит миграции. Автоматическое создание
пользователей и демонстрационных данных отключено.

## Локальный запуск .NET

Требуется .NET SDK 10.0.400 (версия зафиксирована в `global.json`) и доступный
PostgreSQL с PostGIS. Проверить установленный SDK можно командой `dotnet --list-sdks`.
Перед запуском задай
секреты через user-secrets или переменные окружения:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5433;Database=algeda;Username=algeda;Password=..."
$env:Jwt__SigningKey = "...минимум 32 символа..."
dotnet run --project API/API.csproj --launch-profile http
```

Во втором терминале:

```powershell
dotnet run --project Web/Web.csproj --launch-profile http
```

Пустая конфигурация БД допускается только в Development/Testing и не создаёт
данных. В production API завершает запуск с понятной ошибкой, если строка БД
или JWT-ключ отсутствуют.

## Android

Требуются Flutter 3.41.9 и Dart 3.11.5.

```powershell
cd Mobile
flutter pub get
flutter run -d android `
  --dart-define=API_BASE_URL=http://10.0.2.2:5142 `
  --dart-define=WEB_BASE_URL=http://10.0.2.2:5000
```

`10.0.2.2` — адрес компьютера из стандартного Android Emulator. Для
физического устройства используй локальный IP компьютера. Release-сборка
намеренно не содержит debug-подписи: ключ публикации следует настроить вне
репозитория.

Подробнее: [Mobile/README.md](Mobile/README.md).

## Проверки

```powershell
dotnet test Algeda.slnx -c Release
dotnet list Algeda.slnx package --vulnerable --include-transitive

cd Mobile
dart format --output=none --set-exit-if-changed lib test
flutter analyze
flutter test
flutter build apk --debug `
  --dart-define=API_BASE_URL=http://10.0.2.2:5142 `
  --dart-define=WEB_BASE_URL=http://10.0.2.2:5000
```

GitHub Actions выполняет эти проверки для каждого push и pull request.

## Безопасность

Не коммить секреты и данные клиентов. Локальные `.env`, дампы и ключи подписи
игнорируются Git. Инструкция по сообщению об уязвимостях находится в
[SECURITY.md](SECURITY.md).
