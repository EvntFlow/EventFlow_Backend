FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /App

ENV PATH="${PATH}:/root/.dotnet/tools"

# Install dotnet ef
RUN dotnet tool install --global dotnet-ef

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -c Release -o out
# Load development secrets & copy for publish
RUN --mount=type=secret,id=appsettings_Secrets_json,dst=/etc/secrets/appsettings.Secrets.json \
    cp /etc/secrets/appsettings.Secrets.json ./out
RUN cat ./out/appsettings.Secrets.json | \
    dotnet user-secrets set --project EventFlow/EventFlow.csproj
# Synchronize database state
RUN dotnet ef database update --project EventFlow/EventFlow.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /App
COPY --from=build-env /App/out .
ENTRYPOINT ["dotnet", "EventFlow.dll"]
