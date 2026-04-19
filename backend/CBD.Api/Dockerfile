FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY backend/CBD.Api/CBD.Api.csproj backend/CBD.Api/
RUN dotnet restore backend/CBD.Api/CBD.Api.csproj

COPY . .
RUN dotnet publish backend/CBD.Api/CBD.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CBD.Api.dll"]