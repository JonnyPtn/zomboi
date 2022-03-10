import config
from datetime import datetime
from discord.ext import tasks, commands
from file_read_backwards import FileReadBackwards
import glob
import re


class ChatHandler(commands.Cog):
    """Class which handles the chat log files"""

    def __init__(self, bot):
        self.bot = bot
        self.lastUpdateTimestamp = datetime.now()
        self.update.start()

    def splitLine(self, line: str):
        """Split a log line into a timestamp and the remaining message"""
        timestampStr, message = line.strip()[1:].split("]", 1)
        timestamp = datetime.strptime(timestampStr, '%d-%m-%y %H:%M:%S.%f')
        return timestamp, message

    @tasks.loop(seconds=2)
    async def update(self):
        """Update the handler

        This will check the latest log file and update our data based on any new entries
        """
        files = glob.glob(config.logPath + "/*chat.txt")
        if len(files) > 0:
            with FileReadBackwards(files[0]) as f:
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
        """Parse the given line from the logfile and mirror chat message in discord if necessary"""

        # First ignore all the quickchat spam (jay...). "id = 2" seems to be the best way to identify these
        if "id = 2" in message:
            return

        # Mirror any other received messages in the discord chat
        pattern = r'] Message.*author=\'(.*)\', text=\'(.*)\''
        match = re.search(pattern, message)
        if match:
            # Use a webhook to make it look like we're the discord member
            # God bless stack overflow
            name = match.group(1)
            avatar_url = None
            channel = self.bot.get_channel(config.notificationChannel)
            for member in self.bot.get_all_members():
                if match.group(1) in member.name:
                    avatar_url = member.avatar_url
            webhook = await channel.create_webhook(name=name)
            await webhook.send(
                str(match.group(2)), username=name, avatar_url=avatar_url)

            webhooks = await channel.webhooks()
            for webhook in webhooks:
                    await webhook.delete()
