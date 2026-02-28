# Terraria Server Modded
![Terraria Version](https://img.shields.io/badge/Terraria-v1.4.5.5-28a745)

A containerized, modded Terraria server with a custom management wrapper for enhanced server control.

## Features
- Server-side player data
  - Protects against using save editors (i.e. modifying .plr files). Does **not** protect against realtime cheating
    (i.e. memory editing).
  - Automatic character backup rotation
  - Forced difficulty setting
  - Persistent player team
  - Adds `/playtime` chat command for tracking playtime
  - Adds `/boosters` chat command for showing players' permanent boosters
- Graceful shutdown on SIGTERM. Saves world and character data before exiting.
- Includes socket at `/tmp/terraria.sock` (`/data/terraria.sock` when using docker) for external communication
  - `isidle` checks if there are any active players. Useful for shutting down the server when no players are connected 
    and your host charges by CPU time. Pairs well with [game-manager].
  - Any other string will be treated as a command to send to the server (e.g. `say`, `exit`, etc...).
  ```bash
  echo "isidle" | socat - UNIX-CONNECT:/tmp/terraria.sock
  ```

## Quick Start

### Using Docker Compose
1. Create a `docker-compose.yaml` file
    ```yaml
    services:
      terraria:
        image: ghcr.io/rfvgyhn/terraria-server:latest
        stdin_open: true
        tty: true
        ports:
          - "7777:7777"
        volumes:
          - ./data:/data
    ```
2. Start the server
    ```bash
    docker compose up -d
    ```

### Using Docker CLI
```bash
docker run --name terraria -it -p 7777:7777 -v $(pwd)/data:/data ghcr.io/rfvgyhn/terraria-server:latest
```

### Persistent Data
The container runs as non-root user 1654:1654 (the pre-defined user for dotnet runtime chiseled images). If you're using
bind mounts and want to edit files, create a group on the host that matches this ID and add your user to it.
```bash
sudo groupadd --gid 1654 docker-dotnet
sudo usermod -aG docker-dotnet $USER
mkdir data
chmod -R g+w data
sudo chgrp -R docker-dotnet data
```
The container expects a volume mounted at `/data`. Your world files and server-side character data will be stored here.

### Issuing commands
Attach to the container to issue server commands. Use `CTRL+P, CTRL+Q` to detach.
```bash
docker attach terraria
```

---

## Passing Extra Arguments

The entrypoint supports passing custom arguments to both the **Server Wrapper** and the **Terraria Server**. These are 
separated by a double dash `--`.

**Syntax:**
`[wrapper-args] -- [terraria-args]`

### With Docker Compose
Add a `command` section to your service

```yaml
services:
  terraria:
    # ... other config ... 
    command: ["--verbose", "--", "-players", "16"]
```

### With Docker CLI
Append the arguments to the end of your run command

```bash
docker run -it [options] terraria-server:latest --verbose -- -players 16
```

---

## Configuration Reference

### Server Wrapper Arguments
These arguments control the server wrapper (the code preceding the `--` separator).

| Argument         | Description                                                                  | Default                   |
|:-----------------|:-----------------------------------------------------------------------------|:--------------------------|
| `--data-path`    | Directory where server data and characters are stored.                       | `~/.local/share/Terraria` |
| `--verbose`      | Enable trace-level logging for the wrapper.                                  | `false`                   |
| `--difficulty`   | Difficulty to use for all players (Softcore: 0, Mediumcore: 1, Hardcore: 2). | `0`                       |
| `--no-compress`  | Disable compression for character save files.                                | `false`                   |
| `--backup-count` | Number of character backups to maintain per player.                          | `5`                       |
| `--no-team-save` | Disable saving the player's last active team.                                | `false`                   |
| `--version`      | Print the version of the server wrapper and exit.                            | `false`                   |
| `--dry-run`      | Do not start the server.                                                     | `false`                   |
| `--socket-dir`   | Directory to use for Unix domain socket.                                     | `/tmp`                    |
| `--help`         | Print usage information and exit.                                            | `false`                   |

### Terraria Server Arguments
Arguments placed **after** the `--` separator are passed directly to the Terraria Dedicated Server executable.

You can run the following command to extract the latest CLI params and server configuration:
```sh
TERRARIA_VERSION=1455; curl -L "https://terraria.org/api/download/pc-dedicated-server/terraria-server-$TERRARIA_VERSION.zip" \
  | bsdtar -xf - --to-stdout --strip-components=2 "$TERRARIA_VERSION/Windows/serverconfig.txt" > serverconfig.full.txt && \
sed -n 's/^#\(-.*\)/\1/p' serverconfig.full.txt > cli-reference.txt && \
awk '/^#-/ {found=NR} END {last=found} {lines[NR]=$0} END {for(i=last+1;i<=NR;i++) print lines[i]}' serverconfig.full.txt \
  | sed '/C:\\.*\\Terraria/ { s|C:\\.*\\Terraria|/data|; s|\\|/|g; }' \
  | sed 's|^#banlist=banlist.txt|banlist=/data/banlist.txt|' > serverconfig.txt && \
rm serverconfig.full.txt
```

---

[game-manager]: https://github.com/rfvgyhn/game-manager