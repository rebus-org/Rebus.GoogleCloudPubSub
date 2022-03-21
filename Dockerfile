FROM mcr.microsoft.com/dotnet/sdk:6.0-bullseye-slim AS build-env
WORKDIR /app

COPY Rebus.GoogleCloudPubSub/*.csproj ./Rebus.GoogleCloudPubSub/
COPY Rebus.GoogleCloudPubSub.Tests/*.csproj ./Rebus.GoogleCloudPubSub.Tests/

COPY Rebus.GoogleCloudPubSub.sln .
RUN dotnet restore

COPY . ./
FROM build-env AS test
CMD dotnet test --collect:"XPlat Code Coverage" --test-adapter-path:. --logger:"junit;LogFilePath=/tmp/testresults.run/{assembly}-test-result.xml;MethodFormat==Class;FailureBodyFormat=Verbose" --results-directory /tmp/testresults.run ; mkdir -p /tmp/testresults.host/testreports && cp -r /tmp/testresults.run/* /tmp/testresults.host/testreports/