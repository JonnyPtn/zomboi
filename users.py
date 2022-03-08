import config
from dataclasses import dataclass, field
from datetime import datetime
from discord.ext import tasks, commands
from file_read_backwards import FileReadBackwards
import glob
import os
import re
from typing import List


empty_skillset = {
    "Cooking": 0,
    "Fitness": 0,
    "Strength": 0,
    "Blunt": 0,
    "Axe": 0,
    "Sprinting": 0,
    "Lightfoot": 0,
    "Nimble": 0,
    "Sneak": 0,
    "Woodwork": 0,
    "Aiming": 0,
    "Reloading": 0,
    "Farming": 0,
    "Fishing": 0,
    "Trapping": 0,
    "PlantScavenging": 0,
    "Doctor": 0,
    "Electricity": 0,
    "MetalWelding": 0,
    "Mechanics": 0,
    "Spear": 0,
    "Maintenance": 0,
    "SmallBlade": 0,
    "LongBlade": 0,
    "SmallBlunt": 0,
    "Tailoring": 0,
}


@dataclass
class User:
    """A class representing a user"""
    name: str
    hoursAlive: int = 0
    skills: dict = field(default_factory=lambda: empty_skillset)
    online: bool = False
    lastSeen: datetime = datetime(1, 1, 1)
    died: list[datetime] = field(default_factory=lambda: [])


class UserHandler(commands.Cog):
    """Handles all the info we get from the user log files"""
    async def setup(self, bot):
        self.bot = bot
        self.lastUpdateTimestamp = datetime.now()
        self.users = {}

        # Load current history
        await self.loadHistory()

    def getUser(self, name: str):
        """Get a user from a name, will create if it doesn't exist"""
        if not name in self.users:
            self.users[name] = User(name)
        return self.users[name]

    def splitLine(self, line: str):
        """Split a log line into a timestamp and the remaining message"""
        timestampStr, message = line.strip()[1:].split("]", 1)
        timestamp = datetime.strptime(timestampStr, '%d-%m-%y %H:%M:%S.%f')
        return timestamp, message

    async def update(self, file):
        """Update from the log file anything since the last update"""
        self.bot.log.info("User log file updated")
        with FileReadBackwards(file) as f:
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

    async def loadHistory(self):
        """Go through all log files and load the info"""
        self.bot.log.info("Loading user history...")
        files = glob.glob(config.logPath + '/**/*user.txt', recursive=True)
        files.sort(key=os.path.getmtime)
        for file in files:
            with open(file) as f:
                for line in f:
                    await self.handleLog(*self.splitLine(line))
        self.bot.log.info("User history loaded")

    async def handleLog(self, timestamp: datetime, message: str):
        """Parse the log message and extract any useful info from it"""
        # We only care about disconnects from the user file as we get login/deaths from the perklog
        if "disconnected" in message:
            name = re.search(r'\"(.*)\"', message).group(1)
            user = self.getUser(name)
            if timestamp > user.lastSeen:
                user.online = False
                user.lastSeen = timestamp
            if timestamp > self.lastUpdateTimestamp:
                self.bot.log.info(f"{user.name} disconnected")
                await self.bot.get_channel(config.notificationChannel).send(f"{user.name} has logged off, surviving {user.hoursAlive} so far...")
        else:
            # Ignore but mirror log if it's new
            if timestamp > self.lastUpdateTimestamp:
                self.bot.log.debug(f"Ignored: {message}")
