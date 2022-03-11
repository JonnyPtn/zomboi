import config
from discord.ext import tasks, commands
from rcon.source import Client, rcon
import re

def run_rcon(command):
    """Run the given rcon command"""
    with Client("127.0.0.1",27015,passwd=config.rconPassword) as client:
        return client.run(command)

class RCONAdapter(commands.Cog):
    def __init__(self, bot):
        self.bot = bot
        bot.log.debug(run_rcon("help"))

    #@commands.command()
    #async def options(self,ctx):
    #    """Get the current server options"""
    #    message = run_rcon("showoptions")
    #    self.bot.log.debug(message)
    #    await ctx.send(f"> {message}")

    @commands.command()
    async def option(self,ctx, option : str):
        """Show the value of a server option"""
        message = run_rcon("showoptions")
        message = message.splitlines()
        regex = re.compile(f".*{option}.*",flags=re.IGNORECASE)
        message = list(filter(regex.match,message))
        message = "\n".join(message)
        try:
            if len(message):
                await ctx.send(f"```\n{message}\n```")
            else:
                await ctx.send("No matches found")
        except:
            await ctx.send("Unable to send message")

   