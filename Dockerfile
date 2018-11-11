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
COPY --from=builder /app .

# Define environment variables
# Application Insights
ENV APPINSIGHTS_KEY=

# Challenge Logging
ENV TEAMNAME=

ENV SERVICEBUSCONNSTRING=
ENV SERVICEBUSQUEUENAME=

ENTRYPOINT ["dotnet", "sblistener.dll"]