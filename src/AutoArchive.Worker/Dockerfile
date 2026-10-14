FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/AutoArchive.Worker/AutoArchive.Worker.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# Fixed UID/GID 1000 so it matches `user: 1000:1000` in compose and a fresh /data volume inherits the right owner.
RUN groupadd -g 1000 appuser && useradd -m -u 1000 -g 1000 appuser && mkdir -p /data && chown -R appuser:appuser /data /app
USER appuser
EXPOSE 8080
ENTRYPOINT ["dotnet", "AutoArchive.Worker.dll"]
