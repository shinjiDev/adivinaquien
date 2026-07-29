# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Necesario para compilar el cliente Blazor WebAssembly dentro del contenedor.
RUN dotnet workload install wasm-tools

COPY NuGet.Config Directory.Build.props AdivinaQue.slnx ./
COPY src/AdivinaQue.Contracts/AdivinaQue.Contracts.csproj src/AdivinaQue.Contracts/
COPY src/AdivinaQue.Engine/AdivinaQue.Engine.csproj src/AdivinaQue.Engine/
COPY src/AdivinaQue.Client/AdivinaQue.Client.csproj src/AdivinaQue.Client/
COPY src/AdivinaQue.Server/AdivinaQue.Server.csproj src/AdivinaQue.Server/
COPY src/AdivinaQue.PackTool/AdivinaQue.PackTool.csproj src/AdivinaQue.PackTool/
RUN dotnet restore src/AdivinaQue.Server/AdivinaQue.Server.csproj

COPY src/ src/
RUN dotnet publish src/AdivinaQue.Server/AdivinaQue.Server.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "AdivinaQue.Server.dll"]
