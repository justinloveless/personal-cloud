# build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy projects (Orchestrator depends on Api via ProjectReference)
COPY ./src/Orchestrator/ Orchestrator/
COPY ./src/Api/ Api/

RUN dotnet restore Orchestrator/Orchestrator.csproj
RUN dotnet publish Orchestrator/Orchestrator.csproj -c Release -o /out

# runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "Orchestrator.dll"]


