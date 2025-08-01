# استخدم صورة Microsoft الرسمية
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["LicenseServerApi/LicenseServerApi.csproj", "LicenseServerApi/"]
RUN dotnet restore "LicenseServerApi/LicenseServerApi.csproj"
COPY . .
WORKDIR "/src/LicenseServerApi"
RUN dotnet build "LicenseServerApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "LicenseServerApi.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LicenseServerApi.dll"]
