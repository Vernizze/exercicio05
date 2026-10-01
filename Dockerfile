# Imagem da API para o ambiente do desafio. Imagens-base fixadas por versão e digest.

FROM mcr.microsoft.com/dotnet/sdk:10.0.401@sha256:35d40304542c8689331f8cab17c65926cdf48fe711e289321d71924b230a7d29 AS build
WORKDIR /src

# Restore em modo bloqueado, usando somente a fonte NuGet autorizada e o lock file versionado.
COPY global.json NuGet.Config Directory.Build.props .editorconfig ./
COPY Questao5/Questao5.csproj Questao5/packages.lock.json Questao5/
RUN dotnet restore Questao5/Questao5.csproj --locked-mode --configfile NuGet.Config

COPY Questao5/ Questao5/
RUN dotnet publish Questao5/Questao5.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:ContinuousIntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12@sha256:2d584d8147faddb0d678c5748d47953e5b8e18621ed4fb7049a91381d9d7746f AS final
WORKDIR /app

# Diretório do banco operacional, gravável pelo usuário sem privilégios da imagem.
RUN mkdir /data && chown "$APP_UID:$APP_UID" /data

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0 \
    DatabaseName="Data Source=/data/database.sqlite;Foreign Keys=True"

USER $APP_UID
EXPOSE 8080
VOLUME /data

ENTRYPOINT ["dotnet", "Questao5.dll"]
