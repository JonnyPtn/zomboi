# The main file for zomboi bot. Sets up and runs the discord client

from chat import ChatHandler
import config
import discord
from discord.ext import tasks, commands
import logging
from maps import MapHandler
from pathlib import Path
from perks import PerkHandler
from users import UserHandler
from rcon_adapter import RCONAdapter

# Verify the log path
if len(config.logPath) == 0:
    path = Path.home().joinpath("Zomboid/Logs")
    if path.exists():
        config.logPath = str(path)
    else:
        logging.error("Zomboid log path not set and unable to find default")
        exit()

# Our main bot object
intents = discord.Intents.default()
intents.members = True
intents.guilds = True
zomboi = commands.bot.Bot("!", intents=intents)

# Redirect the discord log to a file
logFormat = logging.Formatter(
    '%(asctime)s:%(levelname)s:%(name)s: %(message)s')
discordLogger = logging.getLogger('discord')
discordLogger.setLevel(logging.DEBUG)
handler = logging.FileHandler(
    filename='discord.log', encoding='utf-8', mode='w')
handler.setFormatter(logFormat)
discordLogger.addHandler(handler)

# set up our logging
zomboi.log = logging.getLogger("zomboi")
handler = logging.StreamHandler()
handler.setFormatter(logFormat)
handler.setLevel(logging.INFO)
zomboi.log.addHandler(handler)
handler = logging.FileHandler(filename='zomboi.log')
handler.setFormatter(logFormat)
handler.setLevel(logging.DEBUG)
zomboi.log.addHandler(handler)
zomboi.log.setLevel(logging.DEBUG)

@zomboi.event
async def on_ready():
    zomboi.log.info(f'We have logged in as {zomboi.user}')
    zomboi.channel = zomboi.get_channel(config.channel)
    if zomboi.channel is None:
        zomboi.log.warning('Unable to get channel, will not be enabled')
    else:
        zomboi.log.info('channel connected')
    zomboi.add_cog(UserHandler(zomboi))
    zomboi.add_cog(ChatHandler(zomboi))
    zomboi.add_cog(PerkHandler(zomboi))
    zomboi.add_cog(RCONAdapter(zomboi))
    zomboi.add_cog(MapHandler(zomboi))

# Always finally run the bot
zomboi.run(config.token)
