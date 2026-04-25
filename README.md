# CBD-GR-24

Monorepo del proyecto **DeckBuilder multijuego**.

Este README combina:
- Manual de uso de la app.
- Guía de arranque local.
- Referencia rápida de API.
- Nota de despliegue en Render.

## URLs de producción (Render)

- Backend: https://deckbuilder-backend.onrender.com
- Frontend: https://deckbuilder-frontend.onrender.com

Orden recomendado de comprobación:
1. Backend raíz: https://deckbuilder-backend.onrender.com/
2. Health MongoDB: https://deckbuilder-backend.onrender.com/api/health/mongo
3. Frontend: https://deckbuilder-frontend.onrender.com

## Estructura del repo

- `backend/CBD.Api`: API en .NET 8 + MongoDB.
- `frontend`: React + Vite + TypeScript.
- `docker-compose.yml`: stack local (MongoDB + backend + frontend).

## Requisitos

- .NET 8 SDK.
- Node.js 22 o superior.
- Docker (opcional, recomendado para MongoDB local).
- En Windows, Docker Desktop iniciado para `docker compose`.

## Variables de entorno

### Frontend

Necesitas `frontend/.env`.

1. Duplica `frontend/.env.example`.
2. Renombra la copia a `frontend/.env`.

Variable:
- `VITE_API_BASE_URL`

Ejemplos:
- Local: `http://localhost:5000`
- Producción: `https://deckbuilder-backend.onrender.com`

### Backend

En local no hace falta `.env`; usa `appsettings.json` y `appsettings.Development.json`.

Variables soportadas (útiles para despliegue):
- `MongoDb__ConnectionString`
- `MongoDb__DatabaseName`
- `MongoDb__UsersCollectionName` (opcional, por defecto `users`)
- `MongoDb__DecksCollectionName` (opcional, por defecto `decks`)
- `MongoDb__UserCardsCollectionName` (opcional, por defecto `user_cards`)

Valores típicos:
- Local host: `mongodb://localhost:27017`
- Red interna Docker: `mongodb://mongo:27017`

## Arranque local

### Opción A: rápido (Mongo en Docker + backend/frontend local)

1. Levanta MongoDB:

```powershell
docker compose up -d mongo
```

2. Arranca backend:

```powershell
cd .\backend\CBD.Api
dotnet run
```

3. Arranca frontend:

```powershell
cd .\frontend
npm install
npm run dev
```

4. Abre:
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

### 1) Registro e inicio de sesión

1. En la pantalla inicial, elige `Registro` o `Login`.
2. Registro: completa `Nombre`, `Email` y `Password`.
3. Login: usa `Email` y `Password`.
4. Tras autenticar, accedes a la pantalla principal.

### 2) Vista Barajas

Desde la pestaña `Barajas`:

1. Pulsa `Nueva baraja`.
2. Define:
- Nombre (obligatorio).
- Descripción (opcional).
- Tipo (`pokemon`, `magic`, `yugioh`).
3. Pulsa `Crear baraja`.
4. Puedes `Abrir baraja`, `Editar baraja` y `Eliminar baraja`.

### 3) Edición de baraja

1. Abre una baraja.
2. Pulsa `Abrir buscador`.
3. Busca cartas por nombre.
4. Usa `+` y `-` para ajustar cantidades.

Reglas aplicadas por UI/API:
- Máximo 60 cartas por baraja.
- Copias máximas por nombre:
- Yu-Gi-Oh!: 3
- Pokémon/Magic: 4

### 4) Vista Mi colección

Desde la pestaña `Mi colección`:

- Puedes filtrar estadísticas por juego (`Todos`, `Pokémon`, `Magic`, `Yu-Gi-Oh!`).
- Métricas visibles:
- Cartas únicas.
- Copias totales.
- Sets distintos.
- Media de copias por carta.
- Top de cartas guardadas (top 3 por copias).

## API (referencia rápida)

### Endpoints base

- `GET /` estado básico del backend.
- `GET /api/health/mongo` healthcheck de MongoDB.
- `POST /api/auth/register` alta de usuario.
- `POST /api/auth/login` login de usuario.
- `GET /api/cards/search?gameType={pokemon|magic|yugioh}&name={texto}` búsqueda de cartas.

### Decks

- `GET /api/decks` listar barajas del usuario.
- `GET /api/decks/{deckId}` detalle de baraja.
- `POST /api/decks` crear baraja.
- `PUT /api/decks/{deckId}` actualizar baraja (incluye cartas).
- `DELETE /api/decks/{deckId}` eliminar baraja.

### User Cards

- `GET /api/user-cards`
- Filtros: `gameType`, `name`, `rarity`, `setName`, `inUseOnly`, `minQuantityOwned`, `maxQuantityOwned`.

- `GET /api/user-cards/stats`
- Opcional: `gameType`.
- Devuelve: `totalUniqueCards`, `totalOwnedCopies`, `distinctSets`, `averageCopiesPerCard`, distribuciones (`gameType`, `rarity`, `set`) y `topCards`.

- `POST /api/user-cards`
- Guarda carta o acumula copias si ya existe (`quantityOwned` se suma).
- Requiere: `gameType`, `externalCardId`, `name`, `imageUrl`, `quantityOwned`.
- Límites relevantes:
- `quantityOwned` > 0 y <= 999.
- `searchTags` máximo 25 elementos.
- Cada tag máximo 40 chars.
- Si faltan campos de detalle (`setName`, `rarity`, `typeLine`, `mainText`, `searchTags`), el backend intenta enriquecer desde APIs externas (TCGdex, Scryfall, YGOPRODeck).

- `POST /api/user-cards/{userCardId}/quantity`
- Ajusta copias con `delta` (positivo o negativo).
- Restricciones: `delta != 0`, `|delta| <= 100`, y resultado final entre `0` y `999`.

- `DELETE /api/user-cards/{userCardId}`
- Elimina una carta de la colección del usuario.

### Cabecera obligatoria

Para endpoints de usuario (`/api/decks`, `/api/user-cards`, `/api/user-cards/stats`), envía:

- `X-User-Id: <id de usuario>`

Ese id se obtiene en login/registro.

## Notas de sincronización colección/barajas

- Al crear/editar/eliminar barajas, el backend sincroniza el uso de cartas en barajas (`quantityInDecks`) para el usuario.
- Al consultar `/api/user-cards` y `/api/user-cards/stats`, también intenta completar datos faltantes de cartas.

## Despliegue en Render (resumen)

### Backend

Configuración típica:
- Root Directory: `backend/CBD.Api`
- Runtime: `Docker` (con `backend/CBD.Api/Dockerfile`) o `.NET` nativo.
- Health Check Path: `/api/health/mongo`

Variables mínimas:
- `MongoDb__ConnectionString`
- `MongoDb__DatabaseName`
- Opcionales: nombres de colecciones.

### Frontend

- Si usas Docker: define `VITE_API_BASE_URL` con URL pública del backend.
- Si usas Static Site: build command `npm run build` en `frontend` y publica `dist`.

### Importante sobre CORS

Actualmente la API permite orígenes locales (`http://localhost:*` y `http://127.0.0.1:*`).
Si vas a consumirla desde un dominio público distinto en producción, ajusta la política CORS en `backend/CBD.Api/Program.cs`.

## Comprobar MongoDB en local

1. Levanta MongoDB:

```powershell
docker compose up -d mongo
```

2. Arranca API:

```powershell
cd .\backend\CBD.Api
dotnet run
```

3. Prueba healthcheck:

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
