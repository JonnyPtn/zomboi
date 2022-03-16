import config
from dataclasses import dataclass, field
from datetime import datetime
import discord
from discord.ext import tasks, commands
from file_read_backwards import FileReadBackwards
import glob
import os
import re
from tabulate import tabulate


empty_perkset = {
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
    recordHoursAlive: int = 0
    perks: dict = field(default_factory=lambda: empty_perkset)
    online: bool = False
    lastSeen: datetime = datetime(1, 1, 1)
    lastLocation: tuple = (0,0)
    died: list[datetime] = field(default_factory=lambda: [])


class UserHandler(commands.Cog):
    """Handles all the info we get from the user log files"""
    def __init__(self, bot):
        self.bot = bot
        self.lastUpdateTimestamp = datetime.now()
        self.users = {}
        self.loadHistory()
        self.update.start()
        self.onlineCount = None

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

    @tasks.loop(seconds=2)
    async def update(self):
        """Update from the log file anything since the last update"""
        files = glob.glob(config.logPath + "/*user.txt")
        if len(files) > 0:
            with FileReadBackwards(files[0]) as f:
                newTimestamp = self.lastUpdateTimestamp
                for line in f:
                    timestamp, message = self.splitLine(line)
                    if timestamp > newTimestamp:
                        newTimestamp = timestamp
                    if timestamp > self.lastUpdateTimestamp:
                        self.handleLog(timestamp, message)
                    else:
                        break
                self.lastUpdateTimestamp = newTimestamp

        # Also update the bot activity here
        onlineCount = len([user for user in self.users if self.users[user].online])
        if onlineCount != self.onlineCount:
            playerString = "nobody" if onlineCount == 0 else f"{onlineCount} survivors"
            await self.bot.change_presence(activity=discord.Game(f"PZ with {playerString}")) # have to abbreviate or it gets truncated
            self.onlineCount = onlineCount

    def loadHistory(self):
        """Go through all log files and load the info"""
        self.bot.log.info("Loading user history...")
        files = glob.glob(config.logPath + '/**/*user.txt', recursive=True)
        files.sort(key=os.path.getmtime)
        for file in files:
            with open(file) as f:
                for line in f:
                    self.handleLog(*self.splitLine(line))
        self.bot.log.info("User history loaded")

    def handleLog(self, timestamp: datetime, message: str):
        """Parse the log message and store any useful info. Returns a message string if relevant"""
        # We only care about disconnects from the user file as we get login/deaths from the perklog
        if "disconnected" in message:
            name = re.search(r'\"(.*)\"', message).group(1)
            user = self.getUser(name)
            if timestamp > user.lastSeen:
                user.online = False
                user.lastSeen = timestamp
            if timestamp > self.lastUpdateTimestamp:
                self.bot.log.info(f"{user.name} disconnected")
        elif "fully connected" in message:
            matches = re.search(r'\"(.*)\".*\((\d+),(\d+)', message)
            name = matches.group(1)
            user = self.getUser(name)
            if timestamp > user.lastSeen:
                user.online = True
                user.lastSeen = timestamp
                user.lastLocation = (matches.group(2),matches.group(3))
            if timestamp > self.lastUpdateTimestamp:
                self.bot.log.info(f"{user.name} connected")
        else:
            # Ignore but mirror log if it's new
            if timestamp > self.lastUpdateTimestamp:
                self.bot.log.debug(f"Ignored: {message}")

    @commands.command()
    async def users(self,ctx):
        """Return a list of users on the server with basic info"""
        table = [["Name", "Online", "Last Seen", "Hours survived"]]
        for user in self.users.values():
            table.append([user.name, "Yes" if user.online else "No",
                        user.lastSeen.strftime("%d/%m at %H:%M"), user.hoursAlive])
        await ctx.send(f'```\n{tabulate(table,headers="firstrow", tablefmt="fancy_grid")}\n```')

    @commands.command()
    async def info(self, ctx, name=None):
        """Get detailed user info
        
        Provide a username, or leave blank to show the user matching your discord name
        """
        if name is None:
            name = ctx.author.name
        if name in self.users:
            user = self.users[name]
            table = []
            table.append(["Name", user.name])
            table.append(["Hours survived", f"{user.hoursAlive} (record: {user.recordHoursAlive})"])
            table.append(["Online", "Yes" if user.online else "No"])
            table.append(["Last Seen", user.lastSeen.strftime("%d/%m at %H:%M")])
            table.append(["Deaths", len(user.died)])
            for perk in user.perks:
                if int(user.perks[perk]) != 0:
                    table.append([perk, user.perks[perk]])
            await ctx.send(f'```\n{tabulate(table, tablefmt="fancy_grid")}\n```')