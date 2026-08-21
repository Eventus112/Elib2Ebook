# Ranobes: локальный запуск

Адаптер работает в два слоя: Elib2Ebook обычно загружает страницы напрямую, а при HTTP-200 странице `Just a moment...` передаёт только заблокированный запрос браузеру FlareSolverr. Полученные браузером cookies затем используются обычным HTTP-клиентом.

## Подготовка

```bash
cd /home/maxnim/ai/Elib2Ebook-ranobes
docker compose up -d --build web
```

Веб-интерфейс по умолчанию: <http://127.0.0.1:8080>. В Compose уже настроены задержка, таймаут и внутренний адрес FlareSolverr.

Для доступа только через Tailscale создайте `.env` из примера и задайте адрес интерфейса `tailscale0`:

```bash
cp .env.example .env
sed -i 's/ELIB_WEB_BIND=.*/ELIB_WEB_BIND=100.64.0.1/' .env
sed -i 's/ELIB_WEB_PORT=.*/ELIB_WEB_PORT=8090/' .env
docker compose up -d web
```

Замените `100.64.0.1` результатом команды `tailscale ip -4`. После этого интерфейс будет доступен по `http://TAILSCALE_IP:8090` и не будет опубликован в LAN.

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
