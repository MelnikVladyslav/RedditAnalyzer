# Reddit Analyzer API

Web API сервіс для аналізу постів із Reddit, написаний на **C# / ASP.NET Core 8**.

---

## Запуск через Docker

### Вимоги
- [Docker](https://www.docker.com/) та Docker Compose

### Кроки

```bash
# 1. Клонувати репозиторій
git clone https://github.com/your-username/reddit-analyzer.git
cd RedditAnalyzer

# 2. Зібрати та запустити контейнер
docker compose up --build

# Сервіс буде доступний на http://localhost:8080
```

Swagger UI: [http://localhost:8080/swagger](http://localhost:8080/swagger)

Логи зберігаються у `./logs/out.log` на хост-машині (через volume mount).

---

## Ендпоінти

### `POST /api/reddit/analyze`

Отримує пости із вказаних subreddit-ів та фільтрує їх за ключовими словами.

### `POST /api/reddit/analyze/download`

Перетворює пост у файл json, який можна завантажити

#### Формат запиту

```json
{
  "items": [
    {
      "subreddit": "r/nature",
      "keywords": ["forest", "river"]
    },
    {
      "subreddit": "r/aww",
      "keywords": ["cat", "dog"]
    }
  ],
  "limit": 25
}
```

| Поле | Тип | Опис |
|---|---|---|
| `items` | array | Список subreddit-ів із ключовими словами |
| `items[].subreddit` | string | Назва subreddit (можна з `r/` або без) |
| `items[].keywords` | array | Ключові слова/фрази для фільтрації |
| `limit` | int | Кількість постів для завантаження (1–100) |

#### Формат відповіді

```json
{
  "/r/nature": [
    "Beautiful forest in autumn",
    "River in the mountains"
  ],
  "/r/aww": [
    "Cute dog playing",
    "Cat sleeping"
  ]
}
```

#### Приклад з curl

```bash
curl -X POST http://localhost:8080/api/reddit/analyze \
  -H "Content-Type: application/json" \
  -d '{
    "items": [
      { "subreddit": "r/nature", "keywords": ["forest", "river"] },
      { "subreddit": "r/aww", "keywords": ["cat", "dog"] }
    ],
    "limit": 25
  }'
```

**Ключові рішення:**
- **Багатопоточність**: всі subreddit-и завантажуються паралельно через `Task.WhenAll`.
- **Фільтрація**: перевіряється як заголовок (`title`), так і тіло поста (`selftext`).
- **Логування**: Serilog записує логи одночасно в консоль та у файл `out.log`.
- **Обробка помилок**: мережеві помилки, невалідні відповіді та недоступні subreddit-и обробляються gracefully — повертається порожній список без падіння сервісу.
- **Джерело даних**: використовується офіційний Reddit JSON endpoint (`reddit.com/r/{name}.json`), що не потребує API-ключа і є значно надійнішим за парсинг HTML.

---

## Теоретичне питання

> Які проблеми можуть виникнути при такому підході до отримання даних із веб-сторінок (через HTTP + парсинг HTML)? Як би ви їх вирішували?

### Проблеми та рішення

**1. Зміна структури HTML**
Сайт може будь-коли змінити верстку, через що всі CSS-селектори/XPath-патерни зламаються без жодного попередження.
*Рішення:* за можливістю використовувати офіційний API або стабільний JSON endpoint замість парсингу HTML. Додати моніторинг та автотести на структуру відповіді.

**2. Блокування ботів (Anti-scraping)**
Сайти активно захищаються від парсерів: перевіряють `User-Agent`, встановлюють rate limits, використовують CAPTCHA, Cloudflare тощо.
*Рішення:* встановлювати коректний `User-Agent`, дотримуватися затримок між запитами, поважати `robots.txt`. Для серйозних проєктів — використовувати офіційний API.

**3. JavaScript-рендеринг**
Сучасні SPA (React, Vue) рендерять контент на стороні клієнта — звичайний HTTP-запит поверне порожній HTML без даних.
*Рішення:* використовувати headless браузер (Playwright, Puppeteer) або шукати XHR/fetch запити, які SPA робить до свого бекенду.

**4. Rate limiting та тимчасова недоступність**
Сервер може повертати `429 Too Many Requests` або бути тимчасово недоступним.
*Рішення:* реалізувати exponential backoff retry-логіку, кешування результатів, circuit breaker патерн.

**5. Кодування та локалізація**
Різні кодування (`UTF-8`, `Windows-1251`), HTML-entities, RTL-текст можуть призвести до некоректного відображення даних.
*Рішення:* явно вказувати кодування при парсингу, використовувати бібліотеки (наприклад, `HtmlAgilityPack`), які коректно обробляють HTML-entities.

**6. Непередбачувані дані**
Відсутні поля, null-значення, неочікуваний формат — все це може призвести до NullReferenceException або некоректних результатів.
*Рішення:* defensive programming, nullable types, детальне логування помилок, повернення часткового результату замість падіння.
