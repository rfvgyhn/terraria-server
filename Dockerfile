FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["TerrariaServerModded/TerrariaServerModded.csproj", "TerrariaServerModded/"]
RUN dotnet restore -r linux-x64 "TerrariaServerModded/TerrariaServerModded.csproj"
COPY . .
RUN dotnet build "./TerrariaServerModded/TerrariaServerModded.csproj" -c Release -o /app/build

FROM build AS publish
RUN mkdir -p /home/app/.local/share /data && \
    ln -s /data /home/app/.local/share/Terraria
RUN dotnet publish "./TerrariaServerModded/TerrariaServerModded.csproj" -r linux-x64 -c Release -o /app/publish --self-contained false

FROM mcr.microsoft.com/dotnet/runtime:9.0-noble-chiseled-extra AS final
WORKDIR /app
COPY --from=publish --chown=1654:1654 /home/app /home/app
COPY --from=publish --chown=1654:1654 /data /data
COPY --from=publish --chown=1654:1654 /app/publish .
ENTRYPOINT ["dotnet", "TerrariaServerModded.dll", "--data-path", "/data", "--socket-dir", "/data"]
