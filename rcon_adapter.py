import config
from discord.ext import tasks, commands
from rcon.source import Client, rcon

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
        lower = [o for o in message if option.lower() in o]
        upper = [o for o in message if option.upper() in o]
        cap = [o for o in message if option.capitalize() in o]
        message = "\n".join(lower + upper + cap)
        await ctx.send(f"```\n{message}\n```")
   