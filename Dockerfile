# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# python-is-python3 provee el binario `python` (no solo `python3`) que busca el paso de
# compilación nativa de emscripten al publicar en Release; sin él, dotnet publish falla
# con "unable to find python in $PATH" recién en el último paso del build, tras minutos
# de instalar el workload — la imagen base del SDK no lo trae.
RUN apt-get update && apt-get install -y --no-install-recommends python3 python-is-python3 \
    && rm -rf /var/lib/apt/lists/*

# Necesario para compilar el cliente Blazor WebAssembly dentro del contenedor.
RUN dotnet workload install wasm-tools

COPY NuGet.Config Directory.Build.props AdivinaQue.slnx ./
COPY src/AdivinaQue.Contracts/AdivinaQue.Contracts.csproj src/AdivinaQue.Contracts/
COPY src/AdivinaQue.Engine/AdivinaQue.Engine.csproj src/AdivinaQue.Engine/
COPY src/AdivinaQue.Client/AdivinaQue.Client.csproj src/AdivinaQue.Client/
COPY src/AdivinaQue.Server/AdivinaQue.Server.csproj src/AdivinaQue.Server/
COPY src/AdivinaQue.PackTool/AdivinaQue.PackTool.csproj src/AdivinaQue.PackTool/
RUN dotnet restore src/AdivinaQue.Server/AdivinaQue.Server.csproj -p:EnableReadyToRun=true

COPY src/ src/
# ReadyToRun (precompilado nativo) recorta el tiempo de arranque en frío — justo lo que
# más importa con autoescalado a cero, donde el primer jugador de una partida puede
# encontrarse con un contenedor recién arrancando (ver plan de despliegue, Fase 1).
# EnableReadyToRun activa RuntimeIdentifier/SelfContained/PublishReadyToRun DENTRO de
# AdivinaQue.Server.csproj (ver ese archivo) en vez de pasarlos acá como -r/--self-contained:
# esos SÍ son properties conocidas por todo el SDK y se filtrarían al Client WASM
# referenciado, chocando con su propio trimming (NETSDK1102).
RUN dotnet publish src/AdivinaQue.Server/AdivinaQue.Server.csproj -c Release -o /app/publish --no-restore \
    -p:EnableReadyToRun=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY content/ content/

# Hermano del publish (WORKDIR /app), no "../../content" como en `dotnet run` local —
# ver la nota en Program.cs sobre ContentPack:RootDirectory.
ENV ContentPack__RootDirectory=content

EXPOSE 8080
ENTRYPOINT ["dotnet", "AdivinaQue.Server.dll"]
