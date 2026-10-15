FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/AutoArchive.Worker/AutoArchive.Worker.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# Numeric UID/GID 1000 (the base image may already own that ID under another name) so it matches
# `user: 1000:1000` in compose and a fresh /data volume inherits the right owner.
RUN mkdir -p /data && chown -R 1000:1000 /data /app
USER 1000:1000
EXPOSE 8080
ENTRYPOINT ["dotnet", "AutoArchive.Worker.dll"]
