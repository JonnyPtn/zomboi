# The main file for zomboi bot. Sets up and runs the discord client

from chat import ChatHandler
import discord
from discord.ext import commands
from dotenv import load_dotenv
import logging
from maps import MapHandler
import os
from pathlib import Path
from perks import PerkHandler
from users import UserHandler
from admin import AdminLogHandler
from rcon_adapter import RCONAdapter

load_dotenv(override=True)

# Verify the log path
logPath = os.getenv("LOGS_PATH")
if logPath is None or len(logPath) == 0:
    path = Path.home().joinpath("Zomboid/Logs")
    if path.exists():
        logPath = str(path)
    else:
        logging.error("Zomboid log path not set and unable to find default")
        exit()

# Our main bot object
intents = discord.Intents.default()
intents.members = True
intents.guilds = True
intents.message_content = True
zomboi = commands.bot.Bot("!", intents=intents)

# Redirect the discord log to a file
logFormat = logging.Formatter("%(asctime)s:%(levelname)s:%(name)s: %(message)s")
discordLogger = logging.getLogger("discord")
discordLogger.setLevel(logging.DEBUG)
handler = logging.FileHandler(filename="discord.log", encoding="utf-8", mode="w")
handler.setFormatter(logFormat)
discordLogger.addHandler(handler)

# set up our logging
zomboi.log = logging.getLogger("zomboi")
handler = logging.StreamHandler()
handler.setFormatter(logFormat)
handler.setLevel(logging.INFO)
zomboi.log.addHandler(handler)
handler = logging.FileHandler(filename="zomboi.log")
handler.setFormatter(logFormat)
handler.setLevel(logging.DEBUG)
zomboi.log.addHandler(handler)
zomboi.log.setLevel(logging.DEBUG)


@zomboi.event
async def on_ready():
    zomboi.log.info(f"We have logged in as {zomboi.user}")
    channel = os.getenv("CHANNEL")
    zomboi.channel = (  # Find by id
        zomboi.get_channel(int(channel)) if channel.isdigit() else None
    )
    if zomboi.channel is None:
        zomboi.channel = discord.utils.get(
            zomboi.get_all_channels(), name=channel
        )  # find by name
    if zomboi.channel is None:
        zomboi.log.warning("Unable to get channel, will not be enabled")
    else:
        zomboi.log.info("channel connected")
    await zomboi.add_cog(UserHandler(zomboi, logPath))
    await zomboi.add_cog(ChatHandler(zomboi, logPath))
    await zomboi.add_cog(PerkHandler(zomboi, logPath))
    await zomboi.add_cog(RCONAdapter(zomboi))
    await zomboi.add_cog(MapHandler(zomboi))
    await zomboi.add_cog(AdminLogHandler(zomboi, logPath))


# Always finally run the bot
token = os.getenv("DISCORD_TOKEN")
if token is None:
    zomboi.log.error("DISCORD_TOKEN environment variable not found")
    exit()

zomboi.run(os.getenv("DISCORD_TOKEN"))
