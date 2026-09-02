# Algeda Mobile

Flutter-приложение для Android в составе монорепозитория Algeda.

Приложение предназначено только для сотрудников агентства:

- `Realtor`;
- `Admin` и `SuperAdmin` как административная мобильная роль.

Клиентские сценарии в мобильный проект не добавляются.

## Что уже есть

- базовая Flutter-структура `app`, `core`, `features`;
- feature-first разбиение `data/domain/presentation`;
- `go_router` с role-based shell;
- отдельная нижняя навигация для риелтора и администратора;
- рабочие экраны основных разделов с loading/empty/error states;
- базовая тема в стиле существующего Web: светлый фон, синий primary, компактные карточки и адаптация под маленькие Android-экраны;
- `Dio`-клиент с JWT;
- `flutter_secure_storage` для хранения access token.
- Android `minSdk` поднят минимум до 24 для совместимости с выбором изображений и защищенным хранилищем.
- вход по `api/auth/login` без клиентской регистрации;
- сохранение JWT в `flutter_secure_storage`;
- восстановление сессии по JWT и роли;
- профиль через `api/profile/me`, смена пароля и аватар риелтора.
- живые главные сводки для Realtor/Admin с быстрыми переходами;
- раздел заявок для Realtor/Admin через `api/deals/*`, включая фильтры, детали, основные workflow-действия, заметки и индикатор непрочитанных сообщений.
- компактная Admin-аналитика по агентству, риелторам, объектам и жалобам.
- тестовый подбор недвижимости для Realtor/Admin через preview endpoint без клиентских подписок.

## Структура

```text
lib/
  main.dart
  app/
    app.dart
    app_theme.dart
    role_shell.dart
    router.dart
  core/
    auth/
    config/
    errors/
    network/
    storage/
    utils/
    widgets/
  features/
    auth/
    profile/
    dashboard/
    deals/
    properties/
    chats/
    complaints/
    analytics/
    realtor_efficiency/
    matching/
    admin/
```

Внутри feature используется структура:

```text
data/
  dto/
  api/
  repository/
domain/
  models/
presentation/
  screens/
  widgets/
  controllers/
```

## Backend API

## Чаты

Раздел чатов использует существующий backend-стек:

- Realtor получает список доступных чатов через свои сделки из `api/deals/mine` и индикаторы из `api/deal-chats/notifications/unread`;
- Admin получает список через `api/deal-chats/admin` и может фильтровать по ID сделки, клиента или риелтора;
- экран переписки всегда загружает историю через `GET api/deal-chats/{dealId}/messages`;
- отправка идет через `POST api/deal-chats/{dealId}/messages`;
- live-обновления подключены через SignalR hub `/hubs/chat` и метод `JoinDealChat`;
- если SignalR недоступен, история и отправка продолжают работать через HTTP API.

## Недвижимость и фото

Раздел объектов использует существующие DTO и endpoints backend:

- список объектов загружается через `GET api/properties?limit=500`;
- поиск, фильтр по типу и фильтр по статусу выполняются на мобильной стороне, потому что API списка принимает только `limit`;
- карточка управления открывается через `GET api/properties/{id}/management`;
- создание и редактирование используют `POST api/properties` и `PUT api/properties/{id}`;
- фото загружаются через `POST api/property-photos/upload`;
- порядок фото сохраняется порядком массива `photoPaths`, который отправляется в create/update;
- действия `Скрыть`, `Показать`, `Продано` используют существующие patch endpoints.

## Жалобы и эффективность

В мобильном приложении разделены права Realtor/Admin:

- Realtor видит только `GET api/complaints/my-realtor` без действий модерации;
- Admin работает со списком `GET api/complaints` и деталями `GET api/complaints/{id}`;
- обработка жалобы использует `PATCH api/complaints/{id}/in-progress`, `PATCH api/complaints/{id}/opened` и `PATCH api/complaints/{id}/resolve`;
- “Моя эффективность” Realtor использует только `api/realtor-efficiency/me/scores/latest` и `me/scores/history`;
- Admin выбирает риелтора из `api/realtors` и смотрит `api/realtor-efficiency/realtors/{id}/scores/*`;
- настройки ограничений дорогих заявок вынесены в Admin-экран `api/realtor-efficiency/eligibility-settings`.

## Аналитика и подбор

Мобильная аналитика не дублирует всю Web-админку:

- Admin видит сводки по агентству, риелторам, объектам, жалобам и качеству;
- фильтры периода применяются на мобильной стороне к данным из существующих API;
- фильтр типа сделки использует `api/deals/all` и существующую логику источника заявок;
- кнопка PDF открывает существующий Web endpoint `/Analytics/ExportPdf`;
- Realtor не получает агентскую аналитику и использует только личную эффективность;
- подбор недвижимости для Realtor/Admin отправляет параметры в `POST api/property-matching/preview`;
- мобильное приложение не создает клиентские требования, подписки и уведомления.

Приложение работает с API из каталога `API`, а PDF-отчёты открывает через `Web`.

Основные маршруты, заложенные в экраны:

- `POST api/auth/login`;
- `GET api/profile/me`;
- `POST api/profile/change-password`;
- `POST api/profile/avatar`;
- `GET api/deals/incoming`;
- `GET api/deals/mine`;
- `GET api/deals/all`;
- `GET api/deals/{id}/workflow`;
- `GET api/deal-chats/{dealId}/messages`;
- `POST api/deal-chats/{dealId}/messages`;
- `GET api/deal-chats/admin`;
- `GET api/deal-chats/notifications/unread`;
- `GET api/complaints`;
- `GET api/complaints/my-realtor`;
- `GET api/realtor-efficiency/me/scores/latest`;
- `GET api/realtor-efficiency/me/scores/history`;
- `GET api/realtor-efficiency/realtors/{id}/scores/latest`;
- `GET api/properties`;
- `POST api/property-photos/upload`;
- `POST api/property-matching/preview`;
- SignalR hub: `/hubs/chat`.

## Где менять API baseUrl

Без `--dart-define` используется заведомо недоступный адрес-заглушка:

```text
https://api.algeda.invalid
```

Это предотвращает случайное подключение debug/release-сборки к старой или
рабочей базе. Значение находится в:

```text
lib/core/config/api_config.dart
```

Для запуска с другим адресом используй `--dart-define`:

```powershell
flutter run -d android `
  --dart-define=API_BASE_URL=http://10.0.2.2:5142 `
  --dart-define=WEB_BASE_URL=http://10.0.2.2:5000
```

Для физического Android-устройства укажи IP компьютера в локальной сети:

```powershell
flutter run -d android `
  --dart-define=API_BASE_URL=http://192.168.1.10:5142 `
  --dart-define=WEB_BASE_URL=http://192.168.1.10:5000
```

`WEB_BASE_URL` нужен только для открытия существующего PDF-отчета Web MVC.
Если Web запущен на другом порту, укажи его отдельно.
Если Web-страница отчета защищена cookie-авторизацией, внешний браузер
попросит войти в Web-приложение.

## Как запустить Android

1. Из корня монорепозитория запусти API:

```powershell
dotnet run --project API\API.csproj --launch-profile http
```

2. В другом терминале из корня запусти Web:

```powershell
dotnet run --project Web\Web.csproj --launch-profile http
```

3. Установи Flutter-зависимости:

```powershell
cd Mobile
flutter pub get
```

4. Запусти приложение:

```powershell
flutter run -d android `
  --dart-define=API_BASE_URL=http://10.0.2.2:5142 `
  --dart-define=WEB_BASE_URL=http://10.0.2.2:5000
```

## Smoke-проверка

Репозиторий не содержит seed-пользователей, паролей или данных рабочей БД.
Перед проверкой создай учётные записи через разрешённый регистрационный процесс
либо настрой bootstrap-аккаунт локально через переменные окружения.

После запуска backend проверь:

- вход под Realtor открывает `/realtor` и риелторскую нижнюю навигацию;
- вход под Admin открывает `/admin` и админскую нижнюю навигацию;
- Realtor не может попасть на `/admin/*`, Admin не может попасть на `/realtor/*`;
- основные разделы открываются без клиентских сценариев: заявки, объекты, чаты, жалобы, профиль, эффективность, аналитика Admin, тестовый подбор.

## Проверки перед сдачей

```powershell
flutter analyze
flutter test
flutter build apk --debug `
  --dart-define=API_BASE_URL=http://10.0.2.2:5142 `
  --dart-define=WEB_BASE_URL=http://10.0.2.2:5000
```

## Текущий статус

Сейчас реализованы каркас, авторизация, logout, role-based навигация, живые главные сводки, профиль сотрудника, базовая работа с заявками/сделками, чаты для Realtor/Admin, управление объектами недвижимости с фото, жалобы, эффективность риелторов, Admin-аналитика и тестовый подбор недвижимости.

На будущее можно оставить: push-уведомления Android, офлайн-кэш списков, полноценные справочники Admin, PDF-просмотр внутри приложения и интеграционные тесты против поднятого backend.
