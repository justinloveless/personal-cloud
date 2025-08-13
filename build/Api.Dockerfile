# build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ./src/Api/ Api/
COPY ./src/Orchestrator/ Orchestrator/
COPY ./src/Shared/ Shared/
RUN dotnet restore Api/Api.csproj
RUN dotnet restore Orchestrator/Orchestrator.csproj
RUN dotnet restore Shared/Shared.csproj
RUN dotnet publish Api/Api.csproj -c Release -o /out
RUN dotnet publish Orchestrator/Orchestrator.csproj -c Release -o /out-orchestrator
RUN dotnet publish Shared/Shared.csproj -c Release -o /out-shared

# runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out ./
COPY --from=build /out-orchestrator /orchestrator
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Api.dll"]
