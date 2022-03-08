# The main file for zomboi bot. Sets up and runs the discord client

import asyncio
from chat import ChatHandler
import config
from discord.ext import tasks, commands
import logging
from pathlib import Path
from users import UserHandler
from watchdog.observers import Observer
from watchdog.events import FileSystemEventHandler

# Setup stuff from config
if len(config.logPath) == 0:
    path = Path.home().joinpath("Zomboid/Logs")
    if path.exists():
        config.logPath = str(path)
    else:
        logging.error("Zomboid log path not set and unable to find default")
        exit()

# Our main bot object
zomboi = commands.bot.Bot("!")

# Redirect the discord log to a file
logFormat = logging.Formatter('%(asctime)s:%(levelname)s:%(name)s: %(message)s')
discordLogger = logging.getLogger('discord')
discordLogger.setLevel(logging.DEBUG)
handler = logging.FileHandler(filename='discord.log', encoding='utf-8', mode='w')
handler.setFormatter(logFormat)
discordLogger.addHandler(handler)

# set up our logging
zomboi.log = logging.getLogger("zomboi")
handler = logging.StreamHandler()
handler.setFormatter(logFormat)
handler.setLevel(logging.INFO)
zomboi.log.addHandler(handler)
handler = logging.FileHandler(filename='zomboi.log', encoding='utf-8', mode='w')
handler.setFormatter(logFormat)
handler.setLevel(logging.DEBUG)
zomboi.log.addHandler(handler)
zomboi.log.setLevel(logging.DEBUG)

# A class to watch for changes to the zomboid log files
class LogFileWatcher(FileSystemEventHandler):
    def on_modified(self, event):
        if event.src_path.endswith("user.txt"):
            zomboi.loop.create_task(zomboi.userHandler.update(event.src_path))
        elif event.src_path.endswith("chat.txt"):
            zomboi.loop.create_task(zomboi.chatHandler.update(event.src_path))

watcher = LogFileWatcher()
observer = Observer()
observer.schedule(watcher, path=config.logPath)


@zomboi.event
async def on_ready():
    zomboi.log.info(f'We have logged in as {zomboi.user}')
    zomboi.log.setLevel(logging.DEBUG)

    zomboi.userHandler = UserHandler()
    await zomboi.userHandler.setup(zomboi)

    zomboi.chatHandler = ChatHandler()
    await zomboi.chatHandler.setup(zomboi)

    observer.start()

# Always finally run the bot
zomboi.run(config.token)