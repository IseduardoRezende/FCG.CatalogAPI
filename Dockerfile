FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["global.json", "./"]
COPY ["Contracts/", "Contracts/"]
COPY ["01- API/FCG.Catalog.Api/FCG.Catalog.Api.csproj", "01- API/FCG.Catalog.Api/"]
COPY ["02- Core/FCG.Catalog.Application/FCG.Catalog.Application.csproj", "02- Core/FCG.Catalog.Application/"]
COPY ["02- Core/FCG.Catalog.Domain/FCG.Catalog.Domain.csproj", "02- Core/FCG.Catalog.Domain/"]
COPY ["03- Infrastructure/FCG.Catalog.Infrastructure/FCG.Catalog.Infrastructure.csproj", "03- Infrastructure/FCG.Catalog.Infrastructure/"]
RUN dotnet restore "01- API/FCG.Catalog.Api/FCG.Catalog.Api.csproj"

COPY . .
RUN dotnet publish "01- API/FCG.Catalog.Api/FCG.Catalog.Api.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "FCG.Catalog.Api.dll"]
