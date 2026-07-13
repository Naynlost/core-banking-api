# --- Derleme aşaması ---------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Önce yalnızca proje dosyaları: restore katmanı, kod değişse de cache'ten gelir.
COPY Directory.Build.props ./
COPY src/Banking.Domain/Banking.Domain.csproj src/Banking.Domain/
COPY src/Banking.Application/Banking.Application.csproj src/Banking.Application/
COPY src/Banking.Infrastructure/Banking.Infrastructure.csproj src/Banking.Infrastructure/
COPY src/Banking.Api/Banking.Api.csproj src/Banking.Api/
RUN dotnet restore src/Banking.Api/Banking.Api.csproj

COPY src/ src/
RUN dotnet publish src/Banking.Api/Banking.Api.csproj -c Release -o /app/publish --no-restore

# --- Çalışma aşaması ----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Konteyner içinde root olmayan kullanıcıyla çalış (imajla birlikte gelir).
USER app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Banking.Api.dll"]
