FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/AgentFarm.API/AgentFarm.API.csproj", "src/AgentFarm.API/"]
COPY ["src/AgentFarm.Bot/AgentFarm.Bot.csproj", "src/AgentFarm.Bot/"]
COPY ["src/AgentFarm.Agents/AgentFarm.Agents.csproj", "src/AgentFarm.Agents/"]
COPY ["src/AgentFarm.Core/AgentFarm.Core.csproj", "src/AgentFarm.Core/"]
RUN dotnet restore "src/AgentFarm.API/AgentFarm.API.csproj"
COPY . .
RUN dotnet build "src/AgentFarm.API/AgentFarm.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/AgentFarm.API/AgentFarm.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AgentFarm.API.dll"]
