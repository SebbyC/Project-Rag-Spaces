FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Install git for GitHub repo cloning
RUN apt-get update \
    && apt-get install -y git curl \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY ["backend/src/RagWorkspace.Api/RagWorkspace.Api.csproj", "RagWorkspace.Api/"]
RUN dotnet restore "RagWorkspace.Api/RagWorkspace.Api.csproj"

# Copy the rest of the source code and build the project
COPY backend/src/ ./
WORKDIR "/src/RagWorkspace.Api"
RUN dotnet build "RagWorkspace.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "RagWorkspace.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RagWorkspace.Api.dll"]