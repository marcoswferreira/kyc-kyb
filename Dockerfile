FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY MiniKycKyb.sln ./
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Application/Application.csproj src/Application/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/MiniKycKyb.API/MiniKycKyb.API.csproj src/MiniKycKyb.API/

RUN dotnet restore src/MiniKycKyb.API/MiniKycKyb.API.csproj

COPY src/ src/
WORKDIR /src/src/MiniKycKyb.API
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MiniKycKyb.API.dll"]
