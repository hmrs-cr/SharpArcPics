FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-stage

RUN apt-get update && apt-get install -y --no-install-recommends clang

ARG BUILD_CONFIGURATION=Release

WORKDIR /src
COPY . ./
RUN dotnet test
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish  ./PicArchiver/PicArchiver.csproj  /p:Version=$(date "+%y").$(date "+%m%d").$(date "+%H%M").$(date "+%S") -c $BUILD_CONFIGURATION -o /app

FROM scratch AS export-stage
COPY --from=build-stage /app .