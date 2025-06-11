FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["MicroLedger.Api/MicroLedger.Api.csproj", "MicroLedger.Api/"]
COPY ["MicroLedger.Domain/MicroLedger.Domain.csproj", "MicroLedger.Domain/"]
COPY ["MicroLedger.Application/MicroLedger.Application.csproj", "MicroLedger.Application/"]
COPY ["MicroLedger.Infrastructure/MicroLedger.Infrastructure.csproj", "MicroLedger.Infrastructure/"]
RUN dotnet restore "MicroLedger.Api/MicroLedger.Api.csproj"

# Copy the rest of the code
COPY . .
RUN dotnet build "MicroLedger.Api/MicroLedger.Api.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "MicroLedger.Api/MicroLedger.Api.csproj" -c Release -o /app/publish

# Final image
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MicroLedger.Api.dll"] 