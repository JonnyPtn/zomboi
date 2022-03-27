# Zomboi

A discord bot for Project Zomboid. You are welcome to [log](https://github.com/JonnyPtn/zomboi/issues) any issues, questions, or feature suggestions you have

## Features
- Mirror in-game chat messages to discord channel using linked discord name/avatar
- Notifications for logins and deaths
- Presence shows number of players currently online
- View and change server options
- Request a map showing a players location

## Commands (prefix: `!`):
```
MapHandler:
  location Get the last known location of the given user
RCONAdapter:
  option   Show or set the value of a server option
UserHandler:
  info     Get detailed user info
  users    Return a list of users on the server with basic info
No Category:
  help     Shows this message
```

## Requirements
Python 3.10 is used for development, but please do log any issues you have with lower versions as I'm open to supporting them

To install dependencies:
`pip install -r requirements.txt`

## Configuration
Configuration is loaded from a `config.py` file, a sample for which is provided (`sample_config.py`) so you can just rename that and set the values

## Running the bot
This bot works by monitoring the log files produced by the game, so must be run on the same machine as the server/host

To run:
`python zomboi.py`

It may be a good idea to run as a service, especially on a dedicated server

## Docker

You will have to mount the maps and logs directories of the Project Zomboid
server into the zomboi container at the expected paths.

To build and run zomboi in a Docker container:

```
docker build -t zomboi .
docker run -d -v /path/to/maps:~/steam/steamapps/common/ProjectZomboid/media/maps -v /path/to/logs:~/Zomboid/Logs -e DISCORD_BOT_TOKEN=my_bot_token -e DISCORD_CHANNEL=channel_name_or_id -e SERVER_RCON_PASSWORD=my_rcon_password zomboi
```

Or using docker-compose:

```
docker-compose build
docker-compose up -d
```
