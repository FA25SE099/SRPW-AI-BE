FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["RiceProduction.API/RiceProduction.API.csproj", "RiceProduction.API/"]
COPY ["RiceProduction.Application/RiceProduction.Application.csproj", "RiceProduction.Application/"]
COPY ["RiceProduction.Domain/RiceProduction.Domain.csproj", "RiceProduction.Domain/"]
COPY ["RiceProduction.Infrastructure/RiceProduction.Infrastructure.csproj", "RiceProduction.Infrastructure/"]

RUN dotnet restore "RiceProduction.API/RiceProduction.API.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/RiceProduction.API"
RUN dotnet build "RiceProduction.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "RiceProduction.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

# Create log directory
RUN mkdir -p /var/log/riceproduction && chmod 777 /var/log/riceproduction

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "RiceProduction.API.dll"]