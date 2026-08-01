# syntax=docker/dockerfile:1

# --- Compilacion -------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Se copian primero los .csproj para aprovechar la cache de capas: restaurar
# solo se repite cuando cambian las dependencias, no en cada cambio de codigo.
COPY Directory.Build.props ./
COPY EPS.Admisiones.sln ./
COPY src/EPS.Admisiones.Domain/*.csproj src/EPS.Admisiones.Domain/
COPY src/EPS.Admisiones.Application/*.csproj src/EPS.Admisiones.Application/
COPY src/EPS.Admisiones.Infrastructure/*.csproj src/EPS.Admisiones.Infrastructure/
COPY src/EPS.Admisiones.Web/*.csproj src/EPS.Admisiones.Web/
COPY tests/EPS.Admisiones.Tests/*.csproj tests/EPS.Admisiones.Tests/
RUN dotnet restore EPS.Admisiones.sln

COPY . .
RUN dotnet publish src/EPS.Admisiones.Web/EPS.Admisiones.Web.csproj \
    -c Release -o /app/publish --no-restore

# --- Ejecucion ---------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Usuario sin privilegios: requisito basico de cumplimiento (HIPAA/HABEAS DATA).
USER $APP_UID

COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "EPS.Admisiones.Web.dll"]
