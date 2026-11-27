FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5000
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["PharmazapPlus.API/PharmazapPlus.API.csproj", "PharmazapPlus.API/"]
COPY ["PharmazapPlus.Shared/PharmazapPlus.Shared.csproj", "PharmazapPlus.Shared/"]
RUN dotnet restore "PharmazapPlus.API/PharmazapPlus.API.csproj"
COPY . .
WORKDIR "/src/PharmazapPlus.API"
RUN dotnet build "PharmazapPlus.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PharmazapPlus.API.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS="http://0.0.0.0:5000;http://0.0.0.0:5001"
ENTRYPOINT ["dotnet", "PharmazapPlus.API.dll"]
