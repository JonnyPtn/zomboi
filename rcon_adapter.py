from discord.ext import commands
import os
from rcon.source import Client
import re

class RCONAdapter(commands.Cog):
    def __init__(self, bot):
        self.bot = bot
        self.rconHost = "localhost"
        self.rconPort = int(os.getenv("RCON_PORT"))
        self.rconPassword = os.getenv("RCON_PASSWORD")

    @commands.command()
    async def option(self, ctx, option: str, newValue: str = None):
        """Show or set the value of a server option"""
        if newValue is not None:
            with Client(self.rconHost, self.rconPort, passwd=self.rconPassword, timeout=5.0) as client:
                result = client.run(f"changeoption {option} {newValue}")
            await ctx.send(f"`{result}`")
        else:
            with Client(self.rconHost, self.rconPort, passwd=self.rconPassword, timeout=5.0) as client:
                message = client.run("showoptions")
            message = message.splitlines()
            regex = re.compile(f".*{option}.*", flags=re.IGNORECASE)
            message = list(filter(regex.match, message))
            message = "\n".join(message)
            try:
                if len(message):
                    await ctx.send(f"```\n{message}\n```")
                else:
                    await ctx.send("No matches found")
            except:
                await ctx.send("Unable to send message")
