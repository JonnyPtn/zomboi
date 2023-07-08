from datetime import datetime
import discord
from discord.ext import tasks, commands
from file_read_backwards import FileReadBackwards
import glob
import os


class AdminLogHandler(commands.Cog):
    """
    Class which handles printing log files to the admin channel
    Reads from multiple log files
    The option needs to be set to True in the env variables and a channel should be set
    """

    def __init__(self, bot, logPath):
        self.was_turned_off = False
        self.bot = bot
        self.logPath = logPath
        self.lastUpdateTimestamp = datetime.now()
        self.sendLogs = os.getenv("ADMIN_LOGS", "True") == "True"
        # If the user has not enabled Admin logs, let's exit
        if not self.sendLogs:
            self.bot.log.info("Admin logs not enabled.")
            return 
        self.adminChannel = os.getenv("ADMIN_CHANNEL")
        self.text_channel = os.getenv("CHANNEL")
        if not self.adminChannel:
            self.bot.log.warning("Unable to get admin channel, setting to default channel...")
            self.adminChannel = self.bot.channel.name
        if self.adminChannel.isdigit():
            self.adminChannel = self.bot.get_channel(int(self.adminChannel))  # Find by id
        if self.text_channel.isdigit():
            self.text_channel = self.bot.get_channel(int(self.text_channel))  # Find by id
        if isinstance(self.adminChannel, str):
            self.adminChannel = discord.utils.get(
                self.bot.get_all_channels(), name=self.adminChannel
            )  # find by name
        self.bot.log.info("Admin logs enabled.")
        self.update.start()

    def splitLine(self, line: str):
        """Split a log line into a timestamp and the remaining message"""
        timestampStr, message = line.strip()[1:].split("]", 1)
        timestamp = datetime.strptime(timestampStr, "%d-%m-%y %H:%M:%S.%f")
        return timestamp, message

    @tasks.loop(seconds=10)
    async def update(self):
        # Don't really know how performant this will be since it's opening 4 files -- set it to 10 seconds loop for now
        files = glob.glob(self.logPath + "/*map.txt") + glob.glob(self.logPath + "/*cmd.txt") + glob.glob(self.logPath + "/*admin.txt") + glob.glob(self.logPath + "/*ClientActionLog.txt") + glob.glob(self.logPath + "/*user.txt") + glob.glob(self.logPath + "/*pvp.txt") + glob.glob(self.logPath + "/*PerkLog.txt")
        if len(files) > 0:
            newTimestamps = []
            for file in files:
                with FileReadBackwards(file) as f:
                    for line in f:
                        timestamp, message = self.splitLine(line)
                        if "closed server." in message and not self.was_turned_off:
                            await self.text_channel.send("Server was shutdown.")
                            self.bot.log.info("Server closed..")
                            self.was_turned_off = True
                        if timestamp > self.lastUpdateTimestamp:
                            newTimestamps.append(timestamp)
                            message = f'[{str(timestamp)}]: ' + message
                            if message is not None and self.adminChannel is not None:
                                await self.adminChannel.send(message)
                        else:
                            break
            if len(newTimestamps) > 0:
                self.lastUpdateTimestamp = max(newTimestamps)
        