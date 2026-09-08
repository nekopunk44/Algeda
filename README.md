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

## Продакшен-деплой

Продакшен-контур описан в `compose.production.yml` (PostGIS, миграции, API,
Web, Caddy с автоматическим TLS) и каталоге `deploy/`. Секреты передаются
только через файлы (`secrets:` в Compose), не через переменные окружения.

Первый запуск на сервере:

```bash
cp .env.production.example .env.production
# Заполни хосты, email для ACME и SMTP-настройки.

bash deploy/generate-secrets.sh
# Создаст deploy/secrets/ со случайными паролями БД, JWT-ключом и паролем
# администратора. Файлы email_password и automapper_license_key нужно
# заполнить вручную.

docker compose -f compose.production.yml --env-file .env.production build
docker compose -f compose.production.yml --env-file .env.production up -d
# Сервис migrate применит миграции, после чего поднимутся api, web и caddy.

docker compose -f compose.production.yml --env-file .env.production \
  --profile ops run --rm bootstrap-admin
# Одноразовое создание администратора из BOOTSTRAP_ADMIN_EMAIL и
# deploy/secrets/bootstrap_admin_password.
```

DNS-записи `WEB_HOSTNAME` и `API_HOSTNAME` должны указывать на сервер до
старта Caddy, иначе выпуск сертификатов не пройдёт.

Обновление: собрать новые образы (или сменить `ALGEDA_IMAGE_TAG`) и повторить
`up -d` — миграции применятся автоматически до перезапуска API.

Бэкапы: `deploy/backup-postgres.sh` снимает проверенный дамп в `db-backups/`
(копируй его в зашифрованное хранилище вне сервера), восстановление —
`deploy/restore-postgres.sh`. Роли и права БД создаются скриптом
`deploy/initdb/20-create-algeda-roles.sh` только при инициализации пустого
тома; при восстановлении на новом сервере сначала поднимается чистый
postgres, затем выполняется restore.

## Проверки

```powershell
dotnet test Algeda.slnx -c Release
dotnet format Algeda.slnx --verify-no-changes
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
