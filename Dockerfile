FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props .editorconfig ./
COPY src/ScHauler.csproj src/
RUN dotnet restore src/ScHauler.csproj
COPY src/ src/
RUN dotnet publish src/ScHauler.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPSNETCORE_URLS=http://+:8080
USER app
ENTRYPOINT [ "dotnet", "ScHauler.dll" ]