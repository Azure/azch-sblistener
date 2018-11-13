FROM microsoft/dotnet:2.1-sdk AS build-env
WORKDIR /source

# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet publish --output /app/ --configuration Release

# Build runtime image
FROM microsoft/dotnet:2.1-runtime-alpine
WORKDIR /app
COPY --from=build-env /app .

# Define environment variables
# Application Insights
ENV APPINSIGHTS_KEY=

# PLEASE DO NOT OVERRIDE UNLESS INSTRUCTED BY PROCTORS
ENV CHALLENGEAPPINSIGHTS_KEY=0e90ab6f-79ee-466b-a1e7-fe469a0767da

# Challenge Logging
ENV TEAMNAME=

ENV SERVICEBUSCONNSTRING=

ENTRYPOINT ["dotnet", "sblistener.dll"]