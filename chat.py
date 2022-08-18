from datetime import datetime
from discord.ext import tasks, commands
from file_read_backwards import FileReadBackwards
import glob
import re


class ChatHandler(commands.Cog):
    """Class which handles the chat log files"""

    def __init__(self, bot, logPath):
        self.bot = bot
        self.logPath = logPath
        self.lastUpdateTimestamp = datetime.now()
        self.update.start()
        self.webhook = None

    def splitLine(self, line: str):
        """Split a log line into a timestamp and the remaining message"""
        timestampStr, message = line.strip()[1:].split("]", 1)
        timestamp = datetime.strptime(timestampStr, "%d-%m-%y %H:%M:%S.%f")
        return timestamp, message

    @tasks.loop(seconds=2)
    async def update(self):
        """Update the handler

        This will check the latest log file and update our data based on any
        new entries
        """
        files = glob.glob(self.logPath + "/*chat.txt")
        if len(files) > 0:
            with FileReadBackwards(files[0], encoding="latin-1") as f:
                newTimestamp = self.lastUpdateTimestamp
                for line in f:
                    timestamp, message = self.splitLine(line)
                    if timestamp > newTimestamp:
                        newTimestamp = timestamp
                    if timestamp > self.lastUpdateTimestamp:
                        await self.handleLog(timestamp, message)
                    else:
                        break
                self.lastUpdateTimestamp = newTimestamp

    async def handleLog(self, timestamp: datetime, message: str):
        """Parse the given line from the logfile and mirror chat message in
        discord if necessary"""

        # Ignore anything that's not "General" chat
        if "chat=General" not in message:
            return

        # Mirror any other received messages in the discord chat
        pattern = r"] Message.*author=\'(.*)\', text=\'(.*)\'"
        match = re.search(pattern, message)
        if match and self.bot.channel is not None:
            # Use a webhook to make it look like we're the discord member
            # God bless stack overflow
            if self.bot.channel:
                for webhook in await self.bot.channel.webhooks():
                    if webhook.user == self.bot.user:
                        self.webhook = webhook
            if self.webhook is None:
                self.webhook = await self.bot.channel.create_webhook(name="zomboi")
            name = match.group(1)
            avatar_url = None
            for member in self.bot.get_all_members():
                if match.group(1) in member.name:
                    avatar_url = member.avatar_url
            await self.webhook.send(
                match.group(2), username=name, avatar_url=avatar_url
            )
