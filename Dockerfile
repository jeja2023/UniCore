# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["global.json", "./"]
COPY ["UniCore.slnx", "./"]
COPY ["src/", "src/"]
COPY ["tests/", "tests/"]

RUN dotnet restore "UniCore.slnx"
RUN dotnet publish "src/Platform.WebApi/Platform.WebApi.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    UseInMemoryDatabase=false

COPY --from=build /app/publish ./

EXPOSE 8080

ENTRYPOINT ["dotnet", "Platform.WebApi.dll"]
