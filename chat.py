import asyncio
import config
from datetime import datetime
from discord.ext import tasks, commands
from file_read_backwards import FileReadBackwards
import glob
import re

class ChatHandler():
    async def setup(self, bot):
        self.bot = bot
        self.lock = asyncio.Lock()
        self.lastUpdateTimestamp = datetime.now()

    # Split a log line into a timestamp and message
    def splitLine(self, line : str):
        timestampStr,message = line.strip()[1:].split("]",1) 
        timestamp = datetime.strptime(timestampStr, '%d-%m-%y %H:%M:%S.%f')
        return timestamp, message
    
    # Update from the latest log file
    async def update(self, file):
        async with self.lock:
            self.bot.log.info("Chat log file updated")
            with FileReadBackwards(file) as f:
                newTimestamp = self.lastUpdateTimestamp
                for line in f:
                    timestamp,message = self.splitLine(line)
                    if timestamp > newTimestamp:
                        newTimestamp = timestamp
                    if timestamp > self.lastUpdateTimestamp:
                        await self.handleLog(timestamp,message)
                    else:
                        break
                self.lastUpdateTimestamp = newTimestamp
    

    # Parse a line in the user log file and take appropriate action
    # returns the timestamp of the line
    async def handleLog(self, timestamp : datetime, message : str):
        # First ignore all the quickchat spam (jay). "id = 2" seems to be the best way to identify these
        if "id = 2" in message:
            return

        # Mirror any other received messages in the discord chat
        match = re.search(r'] Message.*author=\'(.*)\', text=\'(.*)\'',message)
        if match:
            await self.bot.get_channel(config.notificationChannel).send(f'{match.group(1)}:{match.group(2)}')
