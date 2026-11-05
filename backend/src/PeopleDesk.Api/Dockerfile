FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first for better layer caching.
COPY backend/PeopleDesk.slnx ./
COPY backend/src/PeopleDesk.Domain/PeopleDesk.Domain.csproj src/PeopleDesk.Domain/
COPY backend/src/PeopleDesk.Application/PeopleDesk.Application.csproj src/PeopleDesk.Application/
COPY backend/src/PeopleDesk.Infrastructure/PeopleDesk.Infrastructure.csproj src/PeopleDesk.Infrastructure/
COPY backend/src/PeopleDesk.Api/PeopleDesk.Api.csproj src/PeopleDesk.Api/
RUN dotnet restore

COPY backend/src/ src/
RUN dotnet publish src/PeopleDesk.Api -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Non-root user
RUN adduser --disabled-password --gecos "" --uid 1001 appuser \
    && mkdir -p /app/data/files /app/logs \
    && chown -R appuser:appuser /app/data /app/logs
USER appuser

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "PeopleDesk.Api.dll"]
