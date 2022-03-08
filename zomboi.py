# The main file for zomboi bot. Sets up and runs the discord client

import asyncio
from chat import ChatHandler
import config
from discord.ext import tasks, commands
import logging
from pathlib import Path
from skills import SkillHandler
from tabulate import tabulate
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
handler = logging.FileHandler(
    filename='zomboi.log', encoding='utf-8', mode='w')
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
        elif event.src_path.endswith("PerkLog.txt"):
            zomboi.loop.create_task(zomboi.userHandler.update(event.src_path))


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

    zomboi.skillHandler = SkillHandler()
    await zomboi.skillHandler.setup(zomboi)

    observer.start()

@zomboi.command()
async def users(ctx):
    """Return a list of users on the server"""
    table = [["Name", "Online", "Last Seen", "Hours survived"]]
    for user in ctx.bot.userHandler.users.values():
        table.append([user.name, "Yes" if user.online else "No", user.lastSeen.strftime("%d/%m at %H:%M"), user.hoursAlive])
    await ctx.send(f'>>> ```\n{tabulate(table,headers="firstrow", tablefmt="fancy_grid")}\n```')

@zomboi.command()
async def info(ctx, name = None):
    """Get your user info"""
    if name is None:
        name = ctx.author.name
    if name in ctx.bot.userHandler.users:
        user = ctx.bot.userHandler.users[name]
        table = []
        table.append(["Name", user.name])
        table.append(["Hours survived", user.hoursAlive])
        table.append(["Online", "Yes" if user.online else "No"])
        table.append(["Last Seen", user.lastSeen.strftime("%d/%m at %H:%M")])
        table.append(["Deaths", len(user.died)])
        for skill in user.skills:
            if user.skills[skill] != '0':
                table.append([skill, user.skills[skill]])
        await ctx.send(f'>>> ```\n{tabulate(table, tablefmt="fancy_grid")}\n```')

# Always finally run the bot
zomboi.run(config.token)
