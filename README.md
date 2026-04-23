# CBD-GR-24

Monorepo base para el proyecto DeckBuilder multijuego.

## Despliegue en Render

- Backend: https://deckbuilder-backend.onrender.com
- Frontend: https://deckbuilder-frontend.onrender.com

Orden recomendado de comprobación en producción:

1. Abre primero el backend y verifica que responde:
   - https://deckbuilder-backend.onrender.com/
2. Comprueba el healthcheck de MongoDB:
   - https://deckbuilder-backend.onrender.com/api/health/mongo
3. Si ambos endpoints responden correctamente, abre el frontend:
   - https://deckbuilder-frontend.onrender.com
4. Ya puedes registrarte/iniciar sesión y usar la aplicación.

## Estructura

- `backend/CBD.Api`: API en .NET 8 con MongoDB.
- `frontend`: app React + Vite + TypeScript.
- `docker-compose.yml`: MongoDB local y orquestación Docker del stack.

## Requisitos

- .NET 8 SDK.
- Node.js 22 o superior.
- Docker, si quieres levantar MongoDB con compose.
- En Windows, Docker Desktop debe estar abierto para usar `docker compose`.

## Arranque local

Backend:

```powershell
cd .\backend\CBD.Api
dotnet run
```

Frontend:

```powershell
cd .\frontend
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

## Endpoints API

- `GET /` devuelve un estado básico.
- `GET /api/health/mongo` comprueba conexión con MongoDB.
- `POST /api/auth/register` registra usuario.
- `POST /api/auth/login` inicia sesión.
- `GET/POST/PUT/DELETE /api/decks` CRUD de barajas del usuario autenticado.

Para usar `/api/decks`, envía la cabecera `X-User-Id` con el `id` devuelto por login/registro.

## Comprobar MongoDB en local

1. Levanta MongoDB:

```powershell
docker compose up -d mongo
```

2. Arranca la API:

```powershell
cd .\backend\CBD.Api
dotnet run
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
3. Conecta y entra en la base `deckbuilder_db`.

## URLs locales con Docker

- Frontend: `http://localhost:5173`
- Backend: `http://localhost:5000`
- MongoDB: `mongodb://localhost:27017`

El frontend se construye apuntando a `http://localhost:5000`, y la API usa `mongodb://mongo:27017` dentro de Docker.

## Registro y login (local)

1. Levanta MongoDB y backend.
2. Arranca frontend con `npm run dev` dentro de `frontend`.
3. En la UI puedes registrarte o loguearte.
4. Al autenticarte verás la pantalla con `Barajas` y un botón de cerrar sesión.

