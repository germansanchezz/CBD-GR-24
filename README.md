# CBD-GR-24

Monorepo del proyecto **DeckBuilder multijuego**.

Este README está ordenado para seguir el flujo más natural:
1. Entender la estructura del proyecto.
2. Preparar entorno y arrancar en local.
3. Usar la aplicación y consultar la API.
4. Revisar el despliegue en producción.

## Índice

1. [Estructura del repo](#estructura-del-repo)
2. [Requisitos](#requisitos)
3. [Variables de entorno](#variables-de-entorno)
4. [Arranque local](#arranque-local)
5. [Comprobar MongoDB en local](#comprobar-mongodb-en-local)
6. [MongoDB Compass](#mongodb-compass)
7. [Manual de usuario](#manual-de-usuario)
8. [API](#api-referencia-rapida)
9. [Manual de despliegue](#manual-de-despliegue)
10. [URLs de producción](#urls-de-produccion-render)

## Estructura del repo

- `backend/CBD.Api`: API en .NET 8 + MongoDB.
- `frontend`: React + Vite + TypeScript.
- `memoria`: memoria del proyecto en LaTeX.
- `docker-compose.yml`: stack local (MongoDB + backend + frontend).

## Requisitos

- .NET 8 SDK.
- Node.js 22 o superior.
- Docker (opcional, pero recomendado para MongoDB local).
- En Windows, Docker Desktop debe estar iniciado para usar `docker compose`.

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

Variables soportadas para despliegue o entornos personalizados:
- `MongoDb__ConnectionString`
- `MongoDb__DatabaseName`
- `MongoDb__UsersCollectionName` (opcional, por defecto `users`)
- `MongoDb__DecksCollectionName` (opcional, por defecto `decks`)
- `MongoDb__UserCardsCollectionName` (opcional, por defecto `user_cards`)

Valores típicos:
- Local host: `mongodb://localhost:27017`
- Red interna Docker: `mongodb://mongo:27017`

## Arranque local

Para que todo funcione bien en local, MongoDB debe estar levantado antes de arrancar el backend y el frontend.

### Orden recomendado

1. Levanta MongoDB.
2. Arranca el backend.
3. Arranca el frontend.

### Opción A: Mongo en Docker + backend/frontend local

1. Levanta MongoDB:

```powershell
docker compose up -d mongo
```

2. Arranca el backend:

```powershell
cd .\backend\CBD.Api
dotnet run
```

3. Arranca el frontend:

```powershell
cd .\frontend
npm install
npm run dev
```

4. Abre:
- Frontend: `http://localhost:5173`
- Backend: `http://localhost:5000`

### Opción B: stack completo con Docker

Si prefieres levantar todo junto, usa este comando:

```powershell
docker compose up -d --build
```

Orden interno esperado del stack:
1. MongoDB.
2. Backend.
3. Frontend.

URLs locales:
- Frontend: `http://localhost:5173`
- Backend: `http://localhost:5000`
- MongoDB: `mongodb://localhost:27017`

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

## Manual de usuario

### 1. Registro e inicio de sesión

1. En la pantalla inicial, elige `Registro` o `Login`.
2. En registro, completa `Nombre`, `Email` y `Password`.
3. En login, usa `Email` y `Password`.
4. Tras autenticarte, accedes a la pantalla principal.
5. Desde la pantalla principal también puedes eliminar tu cuenta con la opción `Eliminar cuenta`.

### 2. Vista Barajas

Desde la pestaña `Barajas`:

1. Pulsa `Nueva baraja`.
2. Define:
- Nombre (obligatorio).
- Descripción (opcional).
- Tipo (`pokemon`, `magic`, `yugioh`).
3. Pulsa `Crear baraja`.
4. Después puedes `Abrir baraja`, `Editar baraja` y `Eliminar baraja`.

### 3. Edición de baraja

1. Abre una baraja.
2. Pulsa `Abrir buscador`.
3. Busca cartas por nombre.
4. Usa `+` y `-` para ajustar cantidades.

Reglas aplicadas por UI/API:
- Máximo 60 cartas por baraja.
- Copias máximas por nombre:
  - Yu-Gi-Oh!: 3.
  - Pokémon/Magic: 4.

### 4. Filtros de visualización

En la vista de edición de baraja, cuando el buscador está cerrado, puedes usar el panel `Filtros de visualización` para centrarte en un tipo concreto de carta:

1. Cierra el buscador de cartas si está abierto.
2. Marca una o varias propiedades en el panel de filtros.
3. La cuadrícula mostrará solo las cartas que coincidan con esas propiedades.
4. Usa `Limpiar filtros` para volver a ver todas las cartas de la baraja.

### 5. Vista Mi colección

Desde la pestaña `Mi colección`:

- Puedes filtrar estadísticas por juego (`Todos`, `Pokémon`, `Magic`, `Yu-Gi-Oh!`).
- Métricas visibles:
  - Cartas únicas.
  - Copias totales.
  - Sets distintos.
  - Media de copias por carta.
  - Top de cartas guardadas (top 3 por copias).

## API Endpoints

### Endpoints base

- `GET /` estado básico del backend.
- `GET /api/health/mongo` healthcheck de MongoDB.
- `POST /api/auth/register` alta de usuario.
- `POST /api/auth/login` login de usuario.
- `DELETE /api/auth/me` elimina la cuenta autenticada y borra también sus barajas y cartas de colección.
- `GET /api/cards/search?gameType={pokemon|magic|yugioh}&name={texto}` búsqueda de cartas.

### Decks

- `GET /api/decks` listar barajas del usuario.
- `GET /api/decks/{deckId}` detalle de baraja.
- `POST /api/decks` crear baraja.
- `PUT /api/decks/{deckId}` actualizar baraja, incluyendo cartas.
- `DELETE /api/decks/{deckId}` eliminar baraja.

### User Cards

- `GET /api/user-cards`
- Filtros: `gameType`, `name`, `rarity`, `setName`, `inUseOnly`, `minQuantityOwned`, `maxQuantityOwned`.

- `GET /api/user-cards/stats`
- Opcional: `gameType`.
- Devuelve: `totalUniqueCards`, `totalOwnedCopies`, `distinctSets`, `averageCopiesPerCard`, distribuciones (`gameType`, `rarity`, `set`) y `topCards`.

- `POST /api/user-cards`
- Guarda una carta o acumula copias si ya existe (`quantityOwned` se suma).
- Requiere: `gameType`, `externalCardId`, `name`, `imageUrl`, `quantityOwned`.
- Límites relevantes:
  - `quantityOwned` > 0 y <= 999.
  - `searchTags` máximo 25 elementos.
  - Cada tag máximo 40 caracteres.
  - Si faltan campos de detalle (`setName`, `rarity`, `typeLine`, `mainText`, `searchTags`), el backend intenta enriquecer desde APIs externas (TCGdex, Scryfall, YGOPRODeck).

- `POST /api/user-cards/{userCardId}/quantity`
- Ajusta copias con `delta` (positivo o negativo).
- Restricciones: `delta != 0`, `|delta| <= 100`, y resultado final entre `0` y `999`.

- `DELETE /api/user-cards/{userCardId}`
- Elimina una carta de la colección del usuario.

### Cabecera obligatoria

Para endpoints de usuario (`/api/auth/me`, `/api/decks`, `/api/user-cards`, `/api/user-cards/stats`), envía:

- `X-User-Id: <id de usuario>`

Ese id se obtiene en login o registro.

## Manual de despliegue

La configuración que hemos seguido es la siguiente:

- Backend como `Web Service` en Render usando el `Dockerfile` de `backend/CBD.Api`.
- Frontend como `Static Site` en Render, con `redirect and rewrite rules` para que las rutas funcionen correctamente.
- Creado `cluster` en MongoDB Atlas, para alojar la base de datos.

Con esta estructura, el backend queda conectado a Atlas y el frontend queda accesible sin depender de variables de entorno ni de CORS.

### Backend

Configuración típica:
- Root Directory: `backend/CBD.Api`
- Runtime recomendado: `Docker` como `Web Service` con `backend/CBD.Api/Dockerfile`.
- Health Check Path: `/api/health/mongo`

Variables mínimas:
- `MongoDb__ConnectionString` apuntando a MongoDB Atlas.
- `MongoDb__DatabaseName`
- Opcionales: nombres de colecciones.

### Frontend

- Configuración recomendada: `Static Site`.
- Comando recomendado: `cd frontend && npm ci && npm run build`.
- Publica `dist` como directorio de salida.
- Añade `redirect and rewrite rules`.
- En este enfoque no necesitas configurar CORS para el frontend.
- La variable a definir `VITE_API_BASE_URL`, déjala con valor `.`.

Regla de rewrite para la API:
- Source: `/api/*`
- Destination: `https://deckbuilder-backend.onrender.com/api/*`
- Action: `Rewrite`

### Importante sobre accesibilidad y CORS

La API debe quedar accesible desde el frontend público y también seguir respondiendo al healthcheck.
Si en algún despliegue decides consumir el backend desde otro origen distinto al Static Site recomendado, entonces sí tendrías que revisar la política CORS en `backend/CBD.Api/Program.cs`.

## URLs de producción (Render)

El proyecto está desplegado en Render y conectado a una base de datos MongoDB alojada en Mongo Atlas. Puedes usar la app sin instalar nada localmente a través de estos enlaces.

- Backend: https://deckbuilder-backend.onrender.com
- Frontend: https://deckbuilder-frontend.onrender.com

Orden recomendado de comprobación:
1. Backend raíz: https://deckbuilder-backend.onrender.com/
2. Health MongoDB: https://deckbuilder-backend.onrender.com/api/health/mongo
3. Frontend: https://deckbuilder-frontend.onrender.com
