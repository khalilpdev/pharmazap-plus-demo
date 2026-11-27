FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["PharmazapPlus.Web/PharmazapPlus.Web.csproj", "PharmazapPlus.Web/"]
COPY ["PharmazapPlus.Shared/PharmazapPlus.Shared.csproj", "PharmazapPlus.Shared/"]
RUN dotnet restore "PharmazapPlus.Web/PharmazapPlus.Web.csproj"
COPY . .
WORKDIR "/src/PharmazapPlus.Web"
RUN dotnet build "PharmazapPlus.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PharmazapPlus.Web.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS="http://0.0.0.0:8080"
ENTRYPOINT ["dotnet", "PharmazapPlus.Web.dll"]
