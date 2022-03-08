from mailbox import Message
import config
from datetime import datetime
from discord.ext import commands
from file_read_backwards import FileReadBackwards
import glob
import os
import re

class SkillHandler():
    async def setup(self, bot):
        self.bot = bot
        self.lastUpdateTimestamp = datetime.now()
        
        # Load current history
        await self.loadHistory()

    # Split a log line into a timestamp and message
    def splitLine(self, line : str):
        timestampStr,message = line.strip()[1:].split("]",1) 
        timestamp = datetime.strptime(timestampStr, '%d-%m-%y %H:%M:%S.%f')
        return timestamp, message
    
    # Update from the latest log file
    async def update(self, file):
        self.bot.log.info("Skill log file updated")
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
        self.bot.log.info("Loading skill history...")

        # Go through each user file in the log folder and subfolders
        files = glob.glob(config.logPath + '/**/*PerkLog.txt', recursive=True)
        files.sort(key=os.path.getmtime)
        for file in files:
            with open(file) as f:
                for line in f:
                    await self.handleLog(*self.splitLine(line))
        self.bot.log.info("Skill history loaded")
    

    # Parse a line in the user log file and take appropriate action
    async def handleLog(self, timestamp : datetime, message : str):
        # Ignore the id at the start of the message, no idea what it's for
        message = message[message.find("[",2) + 1:]

        # Next is the name which we use to get the user
        name, message = message.split("]",1)
        user = self.bot.userHandler.getUser(name)

        # Then (I think) position? which we will handle at some point, for now ignore
        message = message[message.find("[",2) + 1:]

        # Then the message type, can be "Died", "Login", "Level Changed" or a list of skills
        type, message = message.split("]",1)

        # All these logs should include hours survived
        hours = re.search(r'Hours Survived: (\d+)',message).group(1)
        user.hoursAlive = hours

        match type:
            case "Died":
                user.died.append(timestamp)
            case "Login":
                if timestamp > user.lastSeen:
                    user.online = True
                    user.lastSeen = timestamp
            case "Level Changed":
                for skill in user.skills:
                    if skill in message:
                        match = re.search(r'\[(\d+)\]',message)
                        user.skills[skill] = match.group(1)
            case _:
                # Must be a list of skills following a login/player creation
                for skill in user.skills:
                    match = re.search(fr'{skill}=(\d+)',type)
                    if match is None:
                        self.bot.log.debug(f"Unexpected log message: {type}")
                    else:
                        user.skills[skill] = match.group(1)




