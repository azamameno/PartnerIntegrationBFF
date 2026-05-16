FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["PartnerIntegrationBFF/PartnerIntegrationBFF.csproj", "PartnerIntegrationBFF/"]
RUN dotnet restore "PartnerIntegrationBFF/PartnerIntegrationBFF.csproj"
COPY PartnerIntegrationBFF/ PartnerIntegrationBFF/
WORKDIR "/src/PartnerIntegrationBFF"
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "PartnerIntegrationBFF.dll"]
