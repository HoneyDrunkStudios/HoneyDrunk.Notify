# Build context: repository root

FROM mcr.microsoft.com/dotnet/aspnet:11.0-resolute AS base
# Ubuntu 26.04 (Resolute), CVE-2026-109518: bootstrap Ubuntu's APT fix with retries disabled, as Canonical recommends.
RUN apt-get -o Acquire::Retries=0 update \
    && apt-get -o Acquire::Retries=0 install -y --no-install-recommends --only-upgrade '?source-package(^apt$)' \
    && dpkg --compare-versions "$(dpkg-query -W -f='${Version}' apt)" ge 3.2.1ubuntu0.1 \
    && dpkg --compare-versions "$(dpkg-query -W -f='${Version}' libapt-pkg7.0)" ge 3.2.1ubuntu0.1 \
    && rm -rf /var/lib/apt/lists/*
# ASP.NET Core is required by the worker hosting adapter. Pin a non-root UID explicitly
# rather than depending on the image's APP_UID environment variable.
USER 1654
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:11.0.100-resolute AS build
# Ubuntu 26.04 (Resolute), CVE-2026-109518: bootstrap Ubuntu's APT fix with retries disabled, as Canonical recommends.
RUN apt-get -o Acquire::Retries=0 update \
    && apt-get -o Acquire::Retries=0 install -y --no-install-recommends --only-upgrade '?source-package(^apt$)' \
    && dpkg --compare-versions "$(dpkg-query -W -f='${Version}' apt)" ge 3.2.1ubuntu0.1 \
    && dpkg --compare-versions "$(dpkg-query -W -f='${Version}' libapt-pkg7.0)" ge 3.2.1ubuntu0.1 \
    && rm -rf /var/lib/apt/lists/*
ARG BUILD_CONFIGURATION=Release
WORKDIR /workspace

COPY ["global.json", "."]

COPY ["src/HoneyDrunk.Notify.Worker/HoneyDrunk.Notify.Worker.csproj", "src/HoneyDrunk.Notify.Worker/"]
COPY ["src/HoneyDrunk.Notify/HoneyDrunk.Notify.csproj", "src/HoneyDrunk.Notify/"]
COPY ["src/HoneyDrunk.Notify.Abstractions/HoneyDrunk.Notify.Abstractions.csproj", "src/HoneyDrunk.Notify.Abstractions/"]
COPY ["src/HoneyDrunk.Notify.Hosting.AspNetCore/HoneyDrunk.Notify.Hosting.AspNetCore.csproj", "src/HoneyDrunk.Notify.Hosting.AspNetCore/"]
COPY ["src/HoneyDrunk.Notify.Queue.Abstractions/HoneyDrunk.Notify.Queue.Abstractions.csproj", "src/HoneyDrunk.Notify.Queue.Abstractions/"]
COPY ["src/HoneyDrunk.Notify.Queue.InMemory/HoneyDrunk.Notify.Queue.InMemory.csproj", "src/HoneyDrunk.Notify.Queue.InMemory/"]
COPY ["src/HoneyDrunk.Notify.Queue.AzureStorage/HoneyDrunk.Notify.Queue.AzureStorage.csproj", "src/HoneyDrunk.Notify.Queue.AzureStorage/"]
COPY ["src/HoneyDrunk.Notify.Providers.Email.Smtp/HoneyDrunk.Notify.Providers.Email.Smtp.csproj", "src/HoneyDrunk.Notify.Providers.Email.Smtp/"]
COPY ["src/HoneyDrunk.Notify.Providers.Email.Resend/HoneyDrunk.Notify.Providers.Email.Resend.csproj", "src/HoneyDrunk.Notify.Providers.Email.Resend/"]
COPY ["src/HoneyDrunk.Notify.Providers.Sms.Twilio/HoneyDrunk.Notify.Providers.Sms.Twilio.csproj", "src/HoneyDrunk.Notify.Providers.Sms.Twilio/"]
COPY ["NuGet.config", "."]
RUN dotnet restore "src/HoneyDrunk.Notify.Worker/HoneyDrunk.Notify.Worker.csproj"

# Recursive copy is bounded by .dockerignore at the build-context root, which
# excludes bin/, obj/, TestResults/, .git/, .vs/, .env*, secrets.json,
# appsettings.Development.json, *.pfx, *.snk, and test projects (Sonar S6470).
COPY . .
WORKDIR "/workspace/src/HoneyDrunk.Notify.Worker"
RUN dotnet build "HoneyDrunk.Notify.Worker.csproj" -c "$BUILD_CONFIGURATION" -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "HoneyDrunk.Notify.Worker.csproj" -c "$BUILD_CONFIGURATION" -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HoneyDrunk.Notify.Worker.dll"]
