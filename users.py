import config
from datetime import datetime
from discord.ext import commands
from file_read_backwards import FileReadBackwards
import glob
import re

class UserHandler():
    async def setup(self, bot):
        self.bot = bot
        self.lastUpdateTimestamp = datetime.now()
        self.connections = {}
        self.disconnections = {}
        self.died = {}
        
        # Load current history
        await self.loadHistory()

    # Split a log line into a timestamp and message
    def splitLine(self, line : str):
        timestampStr,message = line.strip()[1:].split("]") 
        timestamp = datetime.strptime(timestampStr, '%d-%m-%y %H:%M:%S.%f')
        return timestamp, message
    
    # Update from the latest log file
    async def update(self, file):
        self.bot.log.info("User log file updated")
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

    # Load the history from the files up until the last update time
    async def loadHistory(self):
        self.bot.log.info("Loading user history...")

        # Go through each user file in the log folder and subfolders
        for file in glob.iglob(config.logPath + '/**/*user.txt',recursive=True):
            with open(file) as f:
                for line in f:
                    await self.handleLog(*self.splitLine(line))
        self.bot.log.info("User history loaded")
    

    # Parse a line in the user log file and take appropriate action
    async def handleLog(self, timestamp : datetime, message : str):
        # When a player connects...
        if "fully connected" in message:
            name = re.search(r'\"(.*)\"',message).group(1)
            if not name in self.connections:
                self.connections[name] = [timestamp]
            else:
                self.connections[name].append(timestamp)

        # When a player disconnects
        elif "disconnected" in message:
            name = re.search(r'\"(.*)\"',message).group(1)
            if not name in self.disconnections:
                self.disconnections[name] = [timestamp]
            else:
                self.disconnections[name].append(timestamp)

        # When a player dies
        elif "died" in message:
            name = re.search(r'user (.*) died',message).group(1)
            if not name in self.died:
                self.died[name] = [timestamp]
            else:
                self.died[name].append(timestamp)
            if timestamp > self.lastUpdateTimestamp:
                message = f":dizzy_face: {name} has died :skull:"
                await self.bot.get_channel(config.notificationChannel).send(message)

        else:
            # These log lines we currently don't care about, but print for visibility
            redundantLogs = ["added", "attempting", "allowed", "loading", "removed"]
            if any(x in message for x in redundantLogs):
                # Currently don't care about these, but log for visibility
                self.bot.log.debug(f'Ignored: {message}')
            else:
                # If we get here it's possibly some log line I haven't seen before so don't know how to handle
                self.bot.log.warning(f"Line unhandled: {message}")
                assert False, f"Unhandled line: {message}"

