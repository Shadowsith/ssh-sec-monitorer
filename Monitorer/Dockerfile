# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src
COPY Monitorer.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/release --no-restore

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble
# libldap: needed by System.DirectoryServices.Protocols for the optional LDAP / AD login
RUN apt-get update \
 && apt-get install -y --no-install-recommends libldap2 \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/release .
# Data Protection key ring (encrypts stored SSH keys / passwords) - mount as a volume
RUN mkdir -p /app/keys && chown app:app /app/keys \
 && mkdir -p /app/agents/local && chown 10001:10001 /app/agents/local
# /app/agents/<dir>: read-only mounts of local agents. /app/agents/local belongs to the agent UID 10001 so the
# shared named volume gets the right owner even if the Monitorer mounts it first (agent writes, Monitorer reads).

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DataProtection__KeyPath=/app/keys
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Monitorer.dll"]
