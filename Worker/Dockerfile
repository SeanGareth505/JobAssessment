FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Backend/Backend.csproj", "Backend/"]
COPY ["Worker/Worker.csproj", "Worker/"]
RUN dotnet restore "Worker/Worker.csproj"
COPY Backend/ Backend/
COPY Worker/ Worker/
WORKDIR "/src/Worker"
RUN dotnet build "Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN cp -r /app/build /app/publish && cp /src/Worker/appsettings.json /src/Worker/appsettings.Development.json /app/publish/ 2>/dev/null || true

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Worker.dll"]
