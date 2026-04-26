# Build context: HoneyDrunk.Notify/ (solution root)

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
# Run as a non-root user. The dotnet/runtime image does not define APP_UID (only dotnet/aspnet does),
# so pin the UID explicitly to a non-root value. Linux's USER directive accepts numeric UIDs without
# requiring a corresponding /etc/passwd entry.
USER 1654
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["HoneyDrunk.Notify.Worker/HoneyDrunk.Notify.Worker.csproj", "HoneyDrunk.Notify.Worker/"]
COPY ["HoneyDrunk.Notify/HoneyDrunk.Notify.csproj", "HoneyDrunk.Notify/"]
COPY ["HoneyDrunk.Notify.Abstractions/HoneyDrunk.Notify.Abstractions.csproj", "HoneyDrunk.Notify.Abstractions/"]
COPY ["HoneyDrunk.Notify.Hosting.AspNetCore/HoneyDrunk.Notify.Hosting.AspNetCore.csproj", "HoneyDrunk.Notify.Hosting.AspNetCore/"]
COPY ["HoneyDrunk.Notify.Queue.Abstractions/HoneyDrunk.Notify.Queue.Abstractions.csproj", "HoneyDrunk.Notify.Queue.Abstractions/"]
COPY ["HoneyDrunk.Notify.Queue.InMemory/HoneyDrunk.Notify.Queue.InMemory.csproj", "HoneyDrunk.Notify.Queue.InMemory/"]
COPY ["HoneyDrunk.Notify.Queue.AzureStorage/HoneyDrunk.Notify.Queue.AzureStorage.csproj", "HoneyDrunk.Notify.Queue.AzureStorage/"]
COPY ["HoneyDrunk.Notify.Providers.Email.Smtp/HoneyDrunk.Notify.Providers.Email.Smtp.csproj", "HoneyDrunk.Notify.Providers.Email.Smtp/"]
COPY ["HoneyDrunk.Notify.Providers.Email.Resend/HoneyDrunk.Notify.Providers.Email.Resend.csproj", "HoneyDrunk.Notify.Providers.Email.Resend/"]
COPY ["HoneyDrunk.Notify.Providers.Sms.Twilio/HoneyDrunk.Notify.Providers.Sms.Twilio.csproj", "HoneyDrunk.Notify.Providers.Sms.Twilio/"]
COPY ["NuGet.config", "."]
RUN dotnet restore "HoneyDrunk.Notify.Worker/HoneyDrunk.Notify.Worker.csproj"

COPY . .
WORKDIR "/src/HoneyDrunk.Notify.Worker"
RUN dotnet build "HoneyDrunk.Notify.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "HoneyDrunk.Notify.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HoneyDrunk.Notify.Worker.dll"]
