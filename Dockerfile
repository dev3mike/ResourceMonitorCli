FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .

# Install required packages for Linux system monitoring
RUN apt-get update && \
    apt-get install -y \
    procps \
    sysstat \
    && rm -rf /var/lib/apt/lists/*

ENTRYPOINT ["dotnet", "ResourceMonitorCli.dll"] 