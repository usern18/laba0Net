# Мініпроєкт: Auth, Catalog і Ordering за API Gateway (ЛР №2)

Рішення поступово розділяє моноліт за патерном Strangler Fig. AuthService володіє користувачами, ролями та refresh-токенами; CatalogService — позиціями й категоріями; OrderingService — замовленнями. Ocelot маршрутизує Auth, Catalog і Orders на окремі сервіси, а решту API — до моноліту.

## Проєкти та порти

| Проєкт | Порт | База даних |
|---|---:|---|
| `ApiGateway` | 5000 | — |
| `AuthService` | 5001 | `AuthService/miniproject_auth.db` |
| `CatalogService` (`WebApplication1CatalogService`) | 5002 | `WebApplication1CatalogService/miniproject_catalog.db` |
| `OrderingService` | 5003 | `OrderingService/ordering.db` |
| `laba0Net` (моноліт) | 5100 | `laba0Net/monolith.db` |

Auth, Catalog і Ordering мають окремі DbContext, міграції та SQLite бази. Жоден сервіс не читає чужу БД; Ordering звертається до Catalog через HTTP. `Shared` містить спільне налаштування локальної JWT-перевірки.

## Конфігурація секретів

Перед запуском задай однакові JWT-параметри для Auth, Catalog і Ordering через user-secrets або змінні середовища.

```powershell
dotnet user-secrets set "Jwt:Key" "<спільний випадковий ключ довжиною щонайменше 32 байти>" --project AuthService\AuthService.csproj
dotnet user-secrets set "Jwt:Key" "<той самий ключ>" --project WebApplication1CatalogService\CatalogService.csproj
dotnet user-secrets set "Jwt:Key" "<той самий ключ>" --project OrderingService\OrderingService.csproj
dotnet user-secrets set "BootstrapAdmin:Email" "admin@example.test" --project AuthService\AuthService.csproj
dotnet user-secrets set "BootstrapAdmin:Password" "<пароль адміністратора щонайменше 12 символів>" --project AuthService\AuthService.csproj
```

`Jwt:Issuer` і `Jwt:Audience` мають збігатися в конфігурації всіх трьох сервісів. Auth автоматично застосовує власні міграції та створює bootstrap-адміністратора під час запуску.

## Підготовка баз і одноразове перенесення каталогу

Застосуй міграцію Catalog (після оновлення моделі міграція звільняє ID 1–3 для збереження ідентифікаторів товарів моноліту):

```powershell
dotnet ef database update --project WebApplication1CatalogService\CatalogService.csproj --startup-project WebApplication1CatalogService\CatalogService.csproj
```

Зупини застосунки, якщо вони працюють, і запусти одноразову утиліту копіювання. Вона читає монолітну базу лише як зовнішній інструмент міграції, зберігає ID і залишок товарів, створює відсутні категорії, не перезаписує конфліктні записи й безпечно підтримує повторний запуск. Після перемикання каталогу цей скрипт більше не запускай: CatalogService стає єдиним власником даних.

```powershell
python scripts\import_catalog.py
```

Після першого запуску OrderingService, який застосує його міграції, зупини моноліт і OrderingService та перенеси замовлення зі старої SQLite БД (Id зберігаються, зовнішніх ключів на Catalog немає):

```powershell
python scripts\import_orders.py
```

Скрипт одноразовий і відмовляється записувати в непорожню БД призначення. Legacy-замовлення, для яких моноліт не зберігав дату й статус, отримують час імпорту в UTC та статус `Pending`.

## Запуск

В окремих терміналах із кореня рішення:

```powershell
dotnet run --project laba0Net\laba0Net.csproj --urls http://localhost:5100
dotnet run --project AuthService\AuthService.csproj --launch-profile http
dotnet run --project WebApplication1CatalogService\CatalogService.csproj --launch-profile http
dotnet run --project OrderingService\OrderingService.csproj --launch-profile http
dotnet run --project ApiGateway\ApiGateway.csproj --launch-profile http
```

OpenAPI OrderingService доступний на `http://localhost:5003/openapi/v1.json` у Development; документ містить Bearer security scheme, приклади запитів і відповідей та XML-описи полів. Health endpoints: `http://localhost:5001/health`, `http://localhost:5002/health`, `http://localhost:5003/health`; gateway `/health` перевіряє всі три сервіси.

## Перевірка через шлюз

```powershell
# Перевірка готовності сервісів і старого маршруту моноліту
Invoke-RestMethod http://localhost:5000/health
Invoke-RestMethod http://localhost:5000/api/catalog
# Legacy POST/DELETE /api/catalog повертають 410 Gone; новий власник — CatalogService.

# Реєстрація та вхід
$body = @{ email = "user@example.test"; password = "Strong-Password-123!"; displayName = "Test User" } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri http://localhost:5000/api/v1/auth/register -ContentType "application/json" -Body $body

# Анонімний каталог із пагінацією
Invoke-RestMethod "http://localhost:5000/api/v1/catalog/items?page=1&pageSize=5&search=квиток"
```

Захищені маршрути потребують `Authorization: Bearer <access-token>`. Перевір невірний пароль (401), конфлікт реєстрації (409), невалідну пагінацію (400) і доступ до каталогу за ролями. Логи шлюзу містять статус і correlation ID.

Auth, Catalog і Ordering перевіряють підпис JWT локально з однаковими `Jwt:Key`, `Issuer` та `Audience`; мережевого запиту до Auth для перевірки кожного токена немає.

## OrderingService та інтеграційні тести

`/api/orders` за шлюзом веде до OrderingService. Ендпоінти захищені Bearer JWT. `POST /api/orders` бере UserId з токена; `GET /api/orders` підтримує фільтр за статусом і пагінацію; `GET /api/orders/{id}` приховує чужі записи за 404; `POST /api/orders/{id}/cancel` безпечно повторювати. `Idempotency-Key` запобігає дублюванню; виклики Catalog мають таймаут 2 секунди і повертають 503 з `Retry-After` при мережевій помилці.

Запуск інтеграційних тестів із тестовою SQLite in-memory БД та стабом Catalog:

```powershell
dotnet test tests\OrderingService.IntegrationTests\OrderingService.IntegrationTests.csproj

# Після входу через Auth виконай перевірку реальної маршрутизації за шлюзом
$token = "<access-token>"
.\scripts\verify_gateway.ps1 -AccessToken $token
# За потреби перевір також POST і Idempotency-Key; це створить одне тестове замовлення
.\scripts\verify_gateway.ps1 -AccessToken $token -VerifyCreate
```

Baseline із моноліту — `tests/baseline/orders.json`. Сценарій через Ocelot автоматично перевіряє readiness усіх сервісів, Catalog, Auth, незахищений доступ до замовлень (401), перелік замовлень із токеном і збереження catch-all маршруту моноліту.

## Незмінність історії замовлення

Замовлення тепер отримує актуальну назву й ціну позиції через Catalog API, а в записі замовлення зберігаються `ProductName`, `UnitPrice` і `TotalPrice` як snapshot на момент покупки.

Для повної перевірки зупини будь-який сервіс і переконайся, що відповідний health endpoint та шлюзовий `/health` повертають 503; запусти його знову — має бути 200.