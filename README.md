# CBD-GR-24

Monorepo base para el proyecto de barajas Pokemon TCG.

## Estructura

- `backend/CBD.Api`: API en .NET 8 con MongoDB.
- `frontend`: app React + Vite + TypeScript.
- `docker-compose.yml`: MongoDB local para desarrollo.

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

## API

- `GET /` devuelve un estado básico.
- `GET /api/health/mongo` comprueba conexión con MongoDB.
- `POST /api/auth/register` registra usuario.
- `POST /api/auth/login` inicia sesión.

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

## Registro y login (simple)

1. Levanta MongoDB y backend.
2. Arranca frontend con `npm run dev` dentro de `frontend`.
3. En la UI puedes registrarte o loguearte.
4. Al autenticarte verás la pantalla con `Barajas` y un botón de cerrar sesión.
