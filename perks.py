import config
from datetime import datetime
from discord.ext import tasks, commands
from file_read_backwards import FileReadBackwards
import glob
import os
import re


class PerkHandler(commands.Cog):
    """Class which handles the Perk log files"""

    def __init__(self, bot):
        self.bot = bot
        self.lastUpdateTimestamp = datetime.now()
        self.loadHistory()
        self.update.start()

    def splitLine(self, line: str):
        """Split a log line into a timestamp and the remaining message"""
        timestampStr, message = line.strip()[1:].split("]", 1)
        timestamp = datetime.strptime(timestampStr, '%d-%m-%y %H:%M:%S.%f')
        return timestamp, message

    @tasks.loop(seconds=2)
    async def update(self):
        files = glob.glob(config.logPath + "/*PerkLog.txt")
        if len(files) > 0:
            with FileReadBackwards(files[0]) as f:
                newTimestamp = self.lastUpdateTimestamp
                for line in f:
                    timestamp, message = self.splitLine(line)
                    if timestamp > newTimestamp:
                        newTimestamp = timestamp
                    if timestamp > self.lastUpdateTimestamp:
                        message = self.handleLog(timestamp, message)
                        if message is not None and self.bot.channel is not None:
                            await self.bot.channel.send(message)
                    else:
                        break
                self.lastUpdateTimestamp = newTimestamp

    # Load the history from the files up until the last update time
    def loadHistory(self):
        self.bot.log.info("Loading Perk history...")

        # Go through each user file in the log folder and subfolders
        files = glob.glob(config.logPath + '/**/*PerkLog.txt', recursive=True)
        files.sort(key=os.path.getmtime)
        for file in files:
            with open(file) as f:
                for line in f:
                    self.handleLog(*self.splitLine(line))
        self.bot.log.info("Perk history loaded")

    # Parse a line in the user log file and take appropriate action

    def handleLog(self, timestamp: datetime, message: str):
        # Ignore the id at the start of the message, no idea what it's for
        message = message[message.find("[", 2) + 1:]

        # Next is the name which we use to get the user
        name, message = message.split("]", 1)
        user = self.bot.get_cog('UserHandler').getUser(name)

        # Then position which we set if it's more recent
        x = message[1:message.find(",")]
        y = message[message.find(
            ",") + 1:message.find(",", message.find(",") + 1)]
        message = message[message.find("[", 2) + 1:]

        if timestamp > user.lastSeen:
            user.lastSeen = timestamp
            user.lastLocation = (x, y)

        # Then the message type, can be "Died", "Login", "Level Changed" or a list of perks
        type, message = message.split("]", 1)

        # All these logs should include hours survived
        hours = re.search(r'Hours Survived: (\d+)', message).group(1)
        user.hoursAlive = hours
        if int(hours) > int(user.recordHoursAlive):
            user.recordHoursAlive = hours

        if type == "Died":
            user.died.append(timestamp)
            if timestamp > self.lastUpdateTimestamp:
                self.bot.log.info(f"{user.name} died")
                return f":zombie: {user.name} died after surviving {user.hoursAlive} hours :dizzy_face:"
        elif type == "Login":
            if timestamp > self.lastUpdateTimestamp:
                user.online = True
                self.bot.log.info(f"{user.name} login")
                return f":zombie: {user.name} has arrived, survived for {user.hoursAlive} hours so far..."
        elif type == "Level Changed":
            for perk in user.perks:
                if perk in message:
                    match = re.search(r'\[(\d+)\]', message)
                    level = match.group(1)
                    user.perks[perk] = level
                    if timestamp > self.lastUpdateTimestamp:
                        self.bot.log.info(
                            f"{user.name} {perk} changed to {level}")
                        return f":chart_with_upwards_trend: {user.name} reached {perk} level {level}"
        else:
            # Must be a list of perks following a login/player creation
            for perk in user.perks:
                match = re.search(fr'{perk}=(\d+)', type)
                if match is not None:
                    user.perks[perk] = match.group(1)
