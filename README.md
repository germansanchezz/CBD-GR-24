# CBD-GR-24

Monorepo del proyecto DeckBuilder multijuego.

Este README está pensado como:

- Manual de Usuario (cómo usar la app).
- Manual de Despliegue (local y Render).

## URLs de producción (Render)

- Backend: https://deckbuilder-backend.onrender.com
- Frontend: https://deckbuilder-frontend.onrender.com

Orden recomendado de comprobación:

1. Backend raíz: https://deckbuilder-backend.onrender.com/
2. Health MongoDB: https://deckbuilder-backend.onrender.com/api/health/mongo
3. Frontend: https://deckbuilder-frontend.onrender.com

## Estructura del repo

- `backend/CBD.Api`: API en .NET 8 con MongoDB.
- `frontend`: app React + Vite + TypeScript.
- `docker-compose.yml`: orquestación local de MongoDB, backend y frontend.

## Requisitos

- .NET 8 SDK.
- Node.js 22 o superior.
- Docker (opcional, recomendado para MongoDB local).
- En Windows, Docker Desktop debe estar iniciado para usar `docker compose`.

## Variables de entorno

Solo necesitas archivo `.env` en frontend.

Al clonar el repositorio:

1. Duplica `frontend/.env.example`.
2. Renombra la copia a `frontend/.env` (quitando `.example`).

Para backend no hace falta `.env.example` en local, porque usa `appsettings.json` y `appsettings.Development.json`.
Las variables de entorno del backend se usan sobre todo en despliegue (Render, Docker, etc.).

### Backend

Variables soportadas:

- `MongoDb__ConnectionString`
- `MongoDb__DatabaseName`
- `MongoDb__UsersCollectionName` (opcional, por defecto `users`)
- `MongoDb__DecksCollectionName` (opcional, por defecto `decks`)
- `MongoDb__UserCardsCollectionName` (opcional, por defecto `user_cards`)

Valores típicos:

- Local con Mongo en host: `mongodb://localhost:27017`
- Docker interno: `mongodb://mongo:27017`

### Frontend

- `VITE_API_BASE_URL`: URL base de la API usada por la app React.

Ejemplos:

- Local: `http://localhost:5000`
- Producción: `https://deckbuilder-backend.onrender.com`

## Arranque local (desarrollo)

### Opción A: desarrollo rápido (Mongo en Docker + backend/frontend en local)

1. Levantar MongoDB:

```powershell
docker compose up -d mongo
```

2. Arrancar backend:

```powershell
cd .\backend\CBD.Api
dotnet run
```

3. Arrancar frontend:

```powershell
cd .\frontend
npm install
npm run dev
```

Si quieres forzar URL de API sin tocar código, asegúrate de tener `frontend/.env` creado desde `frontend/.env.example`.

4. Abrir app:

- Frontend: `http://localhost:5173`
- Backend: `http://localhost:5000`

### Opción B: stack completo con Docker

```powershell
docker compose up -d --build
```

URLs:

- Frontend: `http://localhost:5173`
- Backend: `http://localhost:5000`
- MongoDB: `mongodb://localhost:27017`

## Manual de usuario

## 1) Registro e inicio de sesión

1. En la pantalla inicial, elige `Registro` o `Login`.
2. Registro: completa `Nombre`, `Email` y `Password`.
3. Login: usa `Email` y `Password`.
4. Tras autenticar, accedes a `Colección de barajas`.

## 2) Crear una baraja

1. Pulsa `Nueva baraja`.
2. Completa:
    - Nombre (obligatorio).
    - Descripción (opcional).
    - Tipo (`pokemon`, `magic`, `yugioh`).
3. Pulsa `Crear baraja`.

## 3) Editar una baraja y añadir cartas

1. En una baraja, pulsa `Abrir baraja`.
2. Pulsa `Abrir buscador`.
3. Busca cartas por nombre.
4. Usa `+` y `-` para ajustar cantidades.

Reglas aplicadas en la UI/API:

- Máximo 60 cartas por baraja.
- Copias máximas por nombre:
   - Yu-Gi-Oh: 3
   - Pokémon/Magic: 4

## 4) Eliminar una baraja

1. Dentro de la baraja, pulsa `Eliminar baraja`.
2. Confirma en el diálogo.

## API (referencia rápida)

- `GET /` estado básico del backend.
- `GET /api/health/mongo` healthcheck de MongoDB.
- `POST /api/auth/register` alta de usuario.
- `POST /api/auth/login` login de usuario.
- `GET /api/cards/search?gameType={pokemon|magic|yugioh}&name={texto}` búsqueda de cartas.
- `GET /api/decks` listar barajas del usuario.
- `GET /api/decks/{deckId}` detalle de baraja.
- `POST /api/decks` crear baraja.
- `PUT /api/decks/{deckId}` actualizar baraja.
- `DELETE /api/decks/{deckId}` eliminar baraja.
- `GET /api/user-cards` listar colección del usuario (filtros: `gameType`, `name`, `rarity`, `setName`).
- `POST /api/user-cards` guardar o acumular carta en colección del usuario.
- `DELETE /api/user-cards/{userCardId}` eliminar carta de colección del usuario.
- `GET /api/user-cards/stats` estadísticas de colección del usuario (opcional filtro `gameType`).

Para `/api/decks`, envía la cabecera `X-User-Id` con el `id` devuelto por login/registro.

Para `/api/user-cards` y `/api/user-cards/stats`, también debes enviar `X-User-Id`.

## Despliegue en Render (paso a paso)

## 1) Backend (Web Service)

Configuración recomendada:

- Root Directory: `backend/CBD.Api`
- Runtime: `Docker` (usando `backend/CBD.Api/Dockerfile`) o `.NET` nativo.
- Health Check Path: `/api/health/mongo`

Variables de entorno mínimas:

- `MongoDb__ConnectionString` = cadena de MongoDB de Render.
- `MongoDb__DatabaseName` = `deckbuilder_db` (o el nombre que quieras).
- `MongoDb__UsersCollectionName` = `users` (opcional).
- `MongoDb__DecksCollectionName` = `decks` (opcional).

## 2) Frontend (Static Site o Web Service)

Si despliegas el frontend con Docker, define al menos:

- `VITE_API_BASE_URL` = URL pública del backend Render.

Si despliegas como Static Site (sin Docker), usa build command `npm run build` en `frontend` y publica `dist`.

## 3) Verificación post-despliegue

1. `GET /` en backend responde `status: running`.
2. `GET /api/health/mongo` responde `status: ok`.
3. Frontend carga sin errores de red/CORS.
4. Registro y login funcionan.
5. CRUD de barajas funciona.
6. Búsqueda de cartas funciona para los tres juegos.

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

Cadena de conexión local:

```text
mongodb://localhost:27017
```

Pasos:

1. Abre MongoDB Compass.
2. Pega `mongodb://localhost:27017`.
3. Conecta y entra en la base `deckbuilder_db`.
