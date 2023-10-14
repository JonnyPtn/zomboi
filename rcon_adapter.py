from discord.ext import tasks, commands
from discord.ext.commands import has_permissions
import os
from rcon.source import Client, rcon
import re
from datetime import datetime


class RCONAdapter(commands.Cog):
    def __init__(self, bot):
        self.bot = bot
        self.rconHost = (
            os.getenv("RCON_HOST") if os.getenv("RCON_HOST") else "localhost"
        )
        port = os.getenv("RCON_PORT")
        if port is None:
            self.rconPort = 27015
            self.bot.log.info("Using default port")
        else:
            self.rconPort = int(port)
        self.rconPassword = os.getenv("RCON_PASSWORD")
        self.syncplayers.start()

    @commands.command()
    @has_permissions(administrator=True)
    async def option(self, ctx, option: str, newValue: str = None):
        """Show or set the value of a server option"""
        if newValue is not None:
            with Client(
                self.rconHost, self.rconPort, passwd=self.rconPassword, timeout=5.0
            ) as client:
                result = client.run(f"changeoption {option} {newValue}")
            await ctx.send(f"`{result}`")
        else:
            with Client(
                self.rconHost, self.rconPort, passwd=self.rconPassword, timeout=5.0
            ) as client:
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

    @commands.command()
    @has_permissions(administrator=True)
    async def addxp(self, ctx, name: str = None, skill: str = None, amount: int = None):
        """Add xp for a skill"""
        if name is None or skill is None or amount is None:
            await ctx.reply("requires three values: Name, skill and amount")
            return
        with Client(
            self.rconHost, self.rconPort, passwd=self.rconPassword, timeout=5.0
        ) as client:
            result = client.run(f'addxp "{name}" {skill}={amount}')
            await ctx.send(result)

    @tasks.loop(minutes=5)
    async def syncplayers(self):
        """Syncs online players by checking number with rcon"""
        if not self.rconPassword:
            self.bot.log.warning("RCON password not set -- unable to syncplayers.")
            self.update.stop()
            return
        self.bot.log.info("Checking rcon to see if the player counter is correct")
        try:
            response = await rcon(
                "players",
                host=self.rconHost,
                port=self.rconPort,
                passwd=self.rconPassword,
            )
        except Exception as e:
            self.bot.log.error(e)
            self.bot.log.error(
                "Unable to run players command on rcon -- check rcon options"
            )
            self.syncplayers.stop()
            return

        # remove first line from response
        response = "".join(response.splitlines(keepends=True)[1:])

        # update user info too
        userHandler = self.bot.get_cog("UserHandler")
        for user in userHandler.users.values():
            if user.name in response:
                if user.online == False:
                    self.bot.log.info(
                        f"Player {user.name} out of sync, currently offline, should be online, fixing..."
                    )
                user.lastSeen = datetime.now()
                user.online = True
            else:
                if user.online == True:
                    self.bot.log.info(
                        f"Player {user.name} out of sync, currently online, should be offline, fixing..."
                    )
                user.online = False
        self.bot.log.info("Synced players successfully!")
