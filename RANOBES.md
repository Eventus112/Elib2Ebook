# Ranobes: локальный запуск

Адаптер работает в два слоя: Elib2Ebook обычно загружает страницы напрямую, а при HTTP-200 странице `Just a moment...` передаёт только заблокированный запрос браузеру FlareSolverr. Полученные браузером cookies затем используются обычным HTTP-клиентом.

## Подготовка

```bash
cd /home/maxnim/ai/Elib2Ebook-ranobes
docker compose up -d --build web
```

Веб-интерфейс: <http://127.0.0.1:8080>. В Compose уже настроены задержка, таймаут и внутренний адрес FlareSolverr.

## Консольный запуск

### Проверка одной главы

```bash
docker compose run --rm elib2ebook \
  --url 'https://ranobes.com/ranobe/317729-shadow-slave.html' \
  --format epub \
  --save /Save \
  --start 1 --end 1 \
  --no-image \
  --delay 1 \
  --timeout 300 \
  --flare http://flaresolverr:8191
```

Файл появится в `books/`. Для всей книги уберите `--start 1 --end 1`. Не убирайте `--delay 1`: Ranobes включает антибот при серии быстрых запросов.

Можно создать несколько форматов за один проход:

```text
--format epub,fb2,txt
```

Остановить браузерный сервис:

```bash
docker compose down
```
