FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/AutoArchive.Worker/AutoArchive.Worker.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN useradd -m appuser && mkdir -p /data && chown -R appuser /data /app
USER appuser
EXPOSE 8080
ENTRYPOINT ["dotnet", "AutoArchive.Worker.dll"]
