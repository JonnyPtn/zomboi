# Zomboi

Zomboi runs on the same machine as a dedicated server and does two jobs. It drives the server - installing, creating, starting and stopping it on command - and it tails the logs the server writes so it can report what's happening in game back to discord, without anyone needing access to the box it runs on.

## Commands

Everything is a slash command, so start typing `/` and discord will list them. Replies are only visible to whoever ran the command. Anything that changes the server is limited to administrators.

### `/zomboi` - the server

| Command | Admin only | What it does |
| --- | --- | --- |
| `/zomboi status` | | Up time, the address and port to connect on, and how many players are online |
| `/zomboi start` | Yes | Starts the server, or attaches to one that's already running |
| `/zomboi stop` | Yes | Shuts the server down |
| `/zomboi install` | Yes | Fetches steamcmd and installs the server with it, or updates an existing install |
| `/zomboi create <password>` | Yes | Runs a fresh install through its first time setup with that admin password. Several minutes, see [Server installation](#server-installation) |

### `/mod` - mods

| Command | Admin only | What it does |
| --- | --- | --- |
| `/mod add <id>` | Yes | Adds the workshop item with that ID - the number on the end of its workshop URL - to the server's mod and workshop lists. The bot looks the name up on steam, so a bad ID is caught rather than written out. Needs a restart to take effect if the server is running |

### `/player` - players

| Command | Admin only | What it does |
| --- | --- | --- |
| `/player list` | | Who's currently connected, when they were last seen, a map link to where they were, and how long their character has survived |
| `/player skills <name>` | | Every skill the bot has seen that player level, and what level they're at |

Player info comes from the logs, so it covers anyone the bot has watched play rather than everyone who has ever been on the server.

## Notifications

Each of these posts to its own channel, set by ID in `zomboi.ini`. Leave one blank and that notification is simply skipped, so you can have chat mirroring without the level ups, or any other mix.

| Notification | Channel | What you get |
| --- | --- | --- |
| Joins and leaves | `users channel` | A line whenever someone connects or disconnects |
| Deaths | `users channel` | A line when a player's character dies |
| Level ups | `skill channel` | A line when a player reaches a new level in a skill |
| Chat | `chat channel` | Global in-game chat, posted through a webhook so each message shows the speaker's name and, if their in-game name matches a discord user, that user's avatar |
| Server up and down | `server channel` | A line when the server starts or stops |

Where possible notifications will include the location and hours survived

Chat is mirrored game -> discord only, not the other way round yet.

## Getting started

### 1. Get the bot

Download the build for your platform from the [releases page](../../releases) and unpack it on the machine that runs, or is going to run, the server.

To build from source instead, install the [.NET 8 SDK](https://dotnet.microsoft.com/download) and run `dotnet run` in a clone of this repo.

### 2. Make a discord bot

Create an application in the [Discord developer portal](https://discord.com/developers/applications) and add a bot to it, then copy the token from the Bot page.

### 3. Fill in the settings

Copy `zomboi.ini.sample` to `zomboi.ini` and put the token in it.

The same file is where you say which channels the notifications go to. Right click a channel in discord and copy a link to it - the ID is the number on the end, as in `https://discord.com/channels/<server_id>/<channel_id>` - and put that against the matching setting in the `[bot]` section. Leave any of them blank and that notification is simply skipped, see [Notifications](#notifications).

### 4. Run it

From the folder you unpacked it into:

| Platform | Command |
| --- | --- |
| Windows | `.\zomboi.exe`, or just double click it |
| Linux and macOS | `./zomboi` |
| From source | `dotnet run` in the clone |

The bot prints an invite link on its first run, since it isn't in a discord server yet. Follow that link to invite it to your server

## Running as a service on linux

Put this in `/etc/systemd/system/zomboi.service`:

```ini
[Unit]
Description=Zomboi
Wants=network-online.target
After=network-online.target

[Service]
ExecStart=/opt/zomboi/zomboi
WorkingDirectory=/opt/zomboi
User=pzuser
Restart=on-failure
KillMode=process

[Install]
WantedBy=multi-user.target
```

Point the paths at wherever you unpacked it and `User` at an account that owns that folder, then:

```
sudo systemctl enable --now zomboi
journalctl -u zomboi -f
```

The bot logs to the console, so the journal is where its output ends up.

## Server installation

If you don't have a server yet, run `/zomboi install` then `/zomboi create <password>`. The bot looks for an existing install in order:

1. `path` in the `[server]` section of `zomboi.ini`
2. A `server` folder next to the bot, where `/zomboi install` puts it
3. Steam's [default library location](https://pzwiki.net/wiki/Dedicated_server) for your platform, e.g. `C:\Program Files (x86)\Steam\steamapps\common\Project Zomboid Dedicated Server`

Set `path` if your server is somewhere else - that's also where `/zomboi install` will put it.

`/zomboi create` runs the server through its first time setup, which means answering the password prompt and then waiting for it to generate the world and write its config out. That takes several minutes, and the bot waits for the config file to appear before reporting back, so don't take the delay as a hang.

If your server doesn't use the game's default name of `servertest`, set `name` in the `[server]` section too - that's how the bot finds the config file it reads the port from and writes mods into, and it's passed on to the server when the bot starts it.

## Development

```
dotnet build
dotnet test tests/zomboi.tests/zomboi.tests.csproj
```

The tests cover the string handling that reads the server's logs and edits its config, which is the part most worth pinning down - the log formats aren't documented anywhere and a line in an unexpected shape used to be enough to take the bot down.

