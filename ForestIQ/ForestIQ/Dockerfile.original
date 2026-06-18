# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER root
#RUN apt-get update \
    #&& apt-get install -y --no-install-recommends wget ca-certificates gss-ntlmssp \
    #&& wget -q https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb \
    #&& dpkg -i packages-microsoft-prod.deb \
    #&& rm packages-microsoft-prod.deb \
    #&& apt-get update \
    #&& apt-get install -y --no-install-recommends powershell \
    #&& apt-get clean \
    #&& rm -rf /var/lib/apt/lists/*
#
## 2. Inject the missing WSMan binaries natively into PowerShell Core
##RUN pwsh -NoProfile -NonInteractive -Command "Install-Module -Name PSWSMan -Force -SkipPublisherCheck" \
    ##&& pwsh -NoProfile -NonInteractive -Command "Install-WSMan" 
        #
#RUN apt-get install -y --no-install-recommends curl iputils-ping telnet traceroute net-tools

#COPY ["ForestIQ/krb5.conf", "/etc/krb5.conf"]

RUN apt-get update \
    && apt-get install -y --no-install-recommends wget ca-certificates krb5-user libssl-dev curl iputils-ping telnet traceroute net-tools \
    && wget -q https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y --no-install-recommends powershell \
    && pwsh -NoProfile -NonInteractive -Command "Install-Module -Name PSWSMan -Force -SkipPublisherCheck" \
    && pwsh -NoProfile -NonInteractive -Command "Install-WSMan" \
    && apt-get clean \
    && apt-get update \
    && apt-get install -y --no-install-recommends dnsutils netcat-openbsd \
    && rm -rf /var/lib/apt/lists/*

#USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ForestIQ/ForestIQ.csproj", "ForestIQ/"]
COPY ["ForestIQ.Domain/ForestIQ.Domain.csproj", "ForestIQ.Domain/"]
COPY ["ForestIQ.Infrastructure.Data/ForestIQ.Infrastructure.Data.csproj", "ForestIQ.Infrastructure.Data/"]
COPY ["ForestIQ.Service/ForestIQ.Service.csproj", "ForestIQ.Service/"]
RUN dotnet restore "./ForestIQ/ForestIQ.csproj"
COPY . .
WORKDIR "/src/ForestIQ"
RUN dotnet build "./ForestIQ.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ForestIQ.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ForestIQ.dll"]
