# EventFlow

This repository contains the backend source code for EventFlow.

## Building

You will need the [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) to build
the code.

To build the project:

```sh
dotnet build -c Release -o out EventFlow.slnx
```

## Running

Before your first run, you will need to prepare the application's runtime environment.

### Secrets

The application relies on various external components. The secrets file provides information on how
to access these components.

In the `out` folder, create a `appsettings.Secrets.json` file, which should at least contain these
lines:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=<HostName>; Database=<DatabaseName>; Username=<UserName>; Password=<UserPassword>;"
  },
  "ApiKeys": {
    "Postmark": "<Postmark Server Token>",
    "Cloudinary": {
      "CloudName": "<Cloudinary Cloud Name>",
      "ApiKey": "<Cloudinary API Key>",
      "ApiSecret": "<Cloudinary API Secret>"
    }
  },
  "Authentication": {
    "Google": {
      "ClientId": "<Google Cloud Client ID>",
      "ClientSecret": "<Google Cloud Client Secret>"
    },
    "Microsoft": {
      "ClientId": "<Microsoft Azure Client ID>",
      "ClientSecret": "<Microsoft Azure Client Secret>"
    }
  }
}
```

### PostgreSQL Database

This application uses the PostgreSQL 16 DBMS.

Running this app requires a dedicated user and database, which may be set up by running this in
a query window:

```sql
CREATE USER <UserName> WITH PASSWORD '<UserPassword>';
CREATE DATABASE <DatabaseName>;
GRANT ALL ON DATABASE <DatabaseName> TO <UserName>;
ALTER DATABASE <DatabaseName> OWNER TO <UserName>;
```

Replace `<UserName>`, `<UserPassword>`, and `<DatabaseName>` with your chosen values.

Then, we should register the connection string in the `ConnectionStrings:DefaultConnection` field in
the secrets file.

The database must be properly configured for the application to function.

### API Keys

Some of the steps below are optional. A placeholder value in the secrets file may be sufficient for
the application to start but will hinder certain functionality.

#### Postmark

We use [Postmark](https://postmarkapp.com/) for sending emails.
For this to work, we need a
[Server Token](https://postmarkapp.com/developer/api/overview#authentication) from Postmark.

After getting one, we should register this in the `ApiKeys:Postmark` field in the secrets file.

Without a Postmark API key, user account creation may fail.

#### Cloudinary

We use [Cloudinary](https://cloudinary.com/) for storing images.
For this to work, we need a set of
[credentials](https://cloudinary.com/documentation/dev_kickstart_acct_setup) from Cloudinary.

After getting these credentials, we should fill in the corresponding fields in the
`ApiKeys:Cloudinary` object in the secrets file.

Without Cloudinary credentials, features that use image upload may fail.

#### Google/Microsoft OAuth

For external authentication providers, we need to acquire client IDs and client secrets from the
corresponding portal.

You only need to get the client ID and secret then store it in the secrets file. The
other steps in the tutorials below are for app developers and are not necessary for configuration.

For Google, please follow [these instructions](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/google-logins?view=aspnetcore-8.0#create-the-google-oauth-20-client-id-and-secret).

For Microsoft, please follow [these instructions](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/microsoft-logins?view=aspnetcore-8.0#create-the-app-in-microsoft-developer-portal).

### Database Initialization

The application will need the database to have been populated with the required tables.

To do this, run:

```sh
dotnet tools install --global dotnet-ef
dotnet ef database update --project EventFlow/EventFlow.csproj
```

### Executing

After the database and API keys are ready, you can run the app using:

```sh
cd out
dotnet EventFlow.dll
```

## Testing

To run unit tests for the application, run:

```sh
dotnet test
```

## Seeding

The database can optionally be seeded with random sample data by running the `EventFlow.Seeding`
project.

To do this, run:

```sh
cd out
dotnet EventFlow.Seeding.dll
```

The sample data contains at least 100 accounts, 10 organizers, 50 events, and 1000 tickets, ensuring
the robustness of the system.

### Sample Account Passwords

The sample accounts all have a password of:
```
Passw0rd_<Case-Sensitive Email Address>
```

For example, the password for `TestUser1234@example.com` will be:
```
Passw0rd_TestUser1234@example.com
```

## Deployment

To deploy the application, first publish the backend codebase:

```sh
dotnet publish -c Release -o out
```

For the deployed artifacts to work, place the secrets file created using the steps
[above](#secrets) in the `out` folder.

### UI Components

For a full application with UI components included, the
[frontend static assets](https://github.com/EvntFlow/EventFlow_Web?tab=readme-ov-file#deployment)
are required.

After exporting the frontend codebase as a static website, copy the `out` folder to the `wwwroot`
folder relative to the application binary.

```sh
cp -r /path/to/EventFlow_Web/frontend/build out/wwwroot
```

### Pre-built Deployments

Pre-built deployed artifacts are available at the
[EventFlow CI](https://github.com/EvntFlow/EventFlow_CI/releases) repository.

### Public Deployments

EventFlow is deployed at https://ef.trungnt2910.com and https://csit314.trungnt2910.com.
