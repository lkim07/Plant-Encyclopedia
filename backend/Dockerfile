# Plant Encyclopedia API (design 2026-10-27). Build context: backend/.
#   runtime  — the API (default target)
#   migrator — EF Core migrations bundle + psql, used once by docker-compose.e2e.yml to set up the database

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY PlantEncyclopedia.sln dotnet-tools.json ./
COPY PlantEncyclopedia.Domain/PlantEncyclopedia.Domain.csproj PlantEncyclopedia.Domain/
COPY PlantEncyclopedia.Application/PlantEncyclopedia.Application.csproj PlantEncyclopedia.Application/
COPY PlantEncyclopedia.Infrastructure/PlantEncyclopedia.Infrastructure.csproj PlantEncyclopedia.Infrastructure/
COPY PlantEncyclopedia.Api/PlantEncyclopedia.Api.csproj PlantEncyclopedia.Api/
RUN dotnet restore PlantEncyclopedia.Api/PlantEncyclopedia.Api.csproj && dotnet tool restore
COPY PlantEncyclopedia.Domain/ PlantEncyclopedia.Domain/
COPY PlantEncyclopedia.Application/ PlantEncyclopedia.Application/
COPY PlantEncyclopedia.Infrastructure/ PlantEncyclopedia.Infrastructure/
COPY PlantEncyclopedia.Api/ PlantEncyclopedia.Api/
RUN dotnet publish PlantEncyclopedia.Api/PlantEncyclopedia.Api.csproj -c Release --no-restore -o /out/api

FROM build AS bundle
ARG TARGETARCH
# Building the bundle starts the app to find the DbContext. As "Testing" no outside service is set up, and the
# connection string is never used: the real one is passed to the bundle when it runs.
ENV ASPNETCORE_ENVIRONMENT=Testing ConnectionStrings__PlantEncyclopedia="Host=unused" Accounts__PublicBaseUrl=https://localhost
RUN rid="linux-$([ "$TARGETARCH" = "arm64" ] && echo arm64 || echo x64)" \
 && dotnet ef migrations bundle --configuration Release --self-contained -r "$rid" \
      --project PlantEncyclopedia.Infrastructure --startup-project PlantEncyclopedia.Api -o /out/efbundle

# Debian-based, so it has psql and the libraries the self-contained bundle needs.
FROM postgres:16 AS migrator
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
COPY --from=bundle /out/efbundle /usr/local/bin/efbundle
COPY --from=build /out/api/appsettings.json /usr/local/bin/appsettings.json

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out/api ./
# Runs as the image's non-root "app" user; listens on 8080 (plain HTTP behind the website's proxy).
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "PlantEncyclopedia.Api.dll"]
