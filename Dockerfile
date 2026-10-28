# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore src/UserManagement.Web/UserManagement.Web.csproj

RUN dotnet publish src/UserManagement.Web/UserManagement.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app

COPY --from=build /app/publish .

RUN addgroup --system appgroup \
    && adduser --system --ingroup appgroup appuser \
    && mkdir -p /app/Logs /app/wwwroot/uploads \
    && chown -R appuser:appgroup /app

ENV ASPNETCORE_URLS=http://0.0.0.0:10000

EXPOSE 10000

USER appuser

ENTRYPOINT ["dotnet", "UserManagement.Web.dll"]
