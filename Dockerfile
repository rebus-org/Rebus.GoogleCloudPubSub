FROM mcr.microsoft.com/dotnet/sdk:7.0-bullseye-slim AS build-env
WORKDIR /app

COPY Rebus.GoogleCloudPubSub/*.csproj ./Rebus.GoogleCloudPubSub/
COPY Rebus.GoogleCloudPubSub.Tests/*.csproj ./Rebus.GoogleCloudPubSub.Tests/

COPY Rebus.GoogleCloudPubSub.sln .
RUN dotnet restore

COPY . ./

RUN apt-get update && apt-get install -y curl && \
    curl -o /usr/local/bin/wait-for-it.sh https://raw.githubusercontent.com/vishnubob/wait-for-it/master/wait-for-it.sh && \
    chmod +x /usr/local/bin/wait-for-it.sh 

FROM build-env AS test