FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG RUN_TESTS
WORKDIR /src

# Copy everything
COPY . ./
    
RUN if [ "${RUN_TESTS}" = "yes" ]; then dotnet test; fi
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish PicArchiver.Web/PicArchiver.Web.csproj /p:Version=$(date "+%y").$(date "+%m%d").$(date "+%H%M").$(date "+%S") -c Release -o out

RUN apt-get update && apt-get install -y --no-install-recommends nodejs npm && npm install html-minifier -g && \
    cd /src/out/ && html-minifier --file-ext html --collapse-whitespace --remove-comments --minify-css true --minify-js true --input-dir wwwroot/ --output-dir wwwroot/ && \
    html-minifier --file-ext css --collapse-whitespace --remove-comments --minify-css true --minify-js true --input-dir wwwroot/ --output-dir wwwroot/
    

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /App
COPY --from=build /src/out .
COPY --from=build /src/PicArchiver.Web/picvoter.sh .
RUN chmod +x ./picvoter.sh


ENTRYPOINT ["./picvoter.sh"]

