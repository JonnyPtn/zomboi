# The token for your discord bot
token = "DISCORD_BOT_TOKEN"

# Path to Project Zomboid Logs folder
# leave empty if it's the default location:
# - /home/<user/Zomboid/Logs on Linux
# - C:/Users/<user>/Zomboid/Logs on Windows
logPath = ""

# The id for the channel you want notifications and chat
# If this is a channel name string it will use the first channel it finds
# matching that name, or it can be an integer for the discord channel ID which
# you can get by right clicking on the channel -> copy ID
# or from the channel link, e.g: https://discord.com/channels/<server_id>/<channel_id>
channel = None

# The password for rcon on the server (used to relay chat from discord to game)
rconPassword = None

# The port to use for the rcon connection, default is 27015
rconPort = 27015

# Path to the project zomboid maps folder
# leave empty to try the default location, which will look for both
# dedicated server and regular game files
mapsPath = ""
