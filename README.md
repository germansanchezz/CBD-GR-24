# CBD-GR-24

Monorepo base para el proyecto DeckBuilder multijuego.

## Estructura

- `backend/CBD.Api`: API en .NET 8 con MongoDB.
- `frontend`: app React + Vite + TypeScript.
- `docker-compose.yml`: MongoDB local y orquestación Docker del stack.

## Requisitos

- .NET 8 SDK.
- Node.js 22 o superior.
- Docker, si quieres levantar MongoDB con compose.

## Arranque

Backend:

```powershell
dotnet run --project .\backend\CBD.Api\CBD.Api.csproj
```

Frontend:

```powershell
Set-Location .\frontend
npm install
npm run dev
```

MongoDB local:

```powershell
docker compose up -d mongo
```

Todo el stack con Docker:

```powershell
docker compose up -d --build
```

## API

- `GET /` devuelve un estado básico.
- `GET /api/health/mongo` comprueba conexión con MongoDB.
- `POST /api/auth/register` registra usuario.
- `POST /api/auth/login` inicia sesión.
- `GET/POST/PUT/DELETE /api/decks` CRUD de barajas del usuario autenticado.

Para usar `/api/decks`, envia la cabecera `X-User-Id` con el `id` devuelto por login/registro.

## Como comprobar MongoDB

1. Levanta MongoDB:

```powershell
docker compose up -d mongo
```

2. Arranca la API:

```powershell
dotnet run --project .\backend\CBD.Api\CBD.Api.csproj
```

3. Prueba el healthcheck:

```powershell
Invoke-RestMethod http://localhost:5000/api/health/mongo
```

Si todo va bien, devuelve `status: ok`.

## MongoDB Compass

Si quieres ver los datos visualmente, usa MongoDB Compass con esta cadena de conexión:

```text
mongodb://localhost:27017
```

Pasos:

1. Abre MongoDB Compass.
2. Pega `mongodb://localhost:27017` en la pantalla de conexión.
3. Conecta y entra en la base `deckbuilder_cards`.

## Docker

- Frontend: `http://localhost:5173`
- Backend: `http://localhost:5000`
- MongoDB: `mongodb://localhost:27017`

El frontend ya se construye apuntando a `http://localhost:5000`, y la API usa `mongodb://mongo:27017` dentro de Docker.

## Registro y login (simple)

1. Levanta MongoDB y backend.
2. Arranca frontend con `npm run dev` dentro de `frontend`.
3. En la UI puedes registrarte o loguearte.
4. Al autenticarte verás la pantalla con `Barajas` y un botón de cerrar sesión.
