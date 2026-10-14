# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src
COPY ["KestrelHelpdesk.Web/KestrelHelpdesk.Web.csproj", "KestrelHelpdesk.Web/"]
COPY ["KestrelHelpdesk.Core/KestrelHelpdesk.Core.csproj", "KestrelHelpdesk.Core/"]
COPY ["KestrelHelpdesk.Infrastructure/KestrelHelpdesk.Infrastructure.csproj", "KestrelHelpdesk.Infrastructure/"]

RUN dotnet restore "KestrelHelpdesk.Web/KestrelHelpdesk.Web.csproj"

COPY . .
WORKDIR "/src/KestrelHelpdesk.Web"
RUN dotnet publish "KestrelHelpdesk.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# PDF/UA exports must embed a real font program. Keep the same font family available
# in the Linux container as on Windows development machines.
RUN apt-get update \
    && apt-get install -y --no-install-recommends fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "KestrelHelpdesk.Web.dll"]
