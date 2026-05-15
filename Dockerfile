FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Ambiquality.Auth.Api/Ambiquality.Auth.Api.csproj", "src/Ambiquality.Auth.Api/"]
RUN dotnet restore "src/Ambiquality.Auth.Api/Ambiquality.Auth.Api.csproj"
COPY src/ src/
RUN dotnet publish "src/Ambiquality.Auth.Api/Ambiquality.Auth.Api.csproj" -c Release -o /app/publish

RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"
RUN dotnet ef migrations bundle \
    --project src/Ambiquality.Auth.Api/Ambiquality.Auth.Api.csproj \
    --output /app/efbundle \
    --self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrator
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/efbundle .
ENTRYPOINT ["/app/efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /app/efbundle .
EXPOSE 6100
ENTRYPOINT ["dotnet", "Ambiquality.Auth.Api.dll"]
