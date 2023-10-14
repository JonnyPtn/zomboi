from datetime import datetime
import discord
from discord.ext import commands
import glob
import os
from pathlib import Path
import PIL.ImageDraw as ImageDraw
import PIL.Image as Image
import xml.etree.ElementTree as ET

# Taken from ISMapDefinitions.lua
colours = {
    "default": (219, 215, 192),
    "forest": (189, 197, 163),
    "river": (59, 141, 149),
    "trail": (185, 122, 87),
    "tertiary": (171, 158, 143),
    "secondary": (134, 125, 113),
    "primary": (134, 125, 113),
    "*": (200, 191, 231),
    "yes": (210, 158, 105),
    "Residential": (210, 158, 105),
    "CommunityServices": (139, 117, 235),
    "Hospitality": (127, 206, 225),
    "Industrial": (56, 54, 53),
    "Medical": (229, 128, 151),
    "RestaurantsAndEntertainment": (245, 225, 60),
    "RetailAndCommercial": (184, 205, 84),
}

pathsToTry = [
    "steam/steamapps/common/Project Zomboid Dedicated Server/media/maps",
    "steam/steamapps/common/ProjectZomboid/media/maps",
]


class MapHandler(commands.Cog):
    """Class which handles generation of maps"""

    def __init__(self, bot):
        self.bot = bot
        # Validate the maps path
        self.mapsPath = os.getenv("MAPS_PATH")
        if self.mapsPath is None or len(self.mapsPath) == 0:
            for path in pathsToTry:
                tryPath = Path.home().joinpath(path)
                if tryPath.exists():
                    self.mapsPath = str(tryPath)
                    break
        if (
            self.mapsPath is None
            or len(self.mapsPath) == 0
            or not Path(self.mapsPath).is_dir()
        ):
            self.bot.log.error(
                f"Map path {self.mapsPath} not found and/or no suitable default"
            )
        else:
            self.bot.log.info(f"Maps path: {self.mapsPath}")

    @commands.command()
    async def location(self, ctx, name=None):
        """Get the last known location of the given user"""
        if name is None:
            name = ctx.author.name
        user = self.bot.get_cog("UserHandler").getUser(name)
        x = int(user.lastLocation[0])
        y = int(user.lastLocation[1])
        chunkSize = 300
        cellx = x // chunkSize
        celly = y // chunkSize
        posX = x % chunkSize
        posY = y % chunkSize

        image = Image.new("RGB", (chunkSize, chunkSize), colours["default"])
        draw = ImageDraw.Draw(image)

        tree = ET.parse(self.mapsPath + ("/Muldraugh, KY/worldmap.xml"))
        root = tree.getroot()
        for cell in root.findall("cell"):
            if int(cell.get("x")) == cellx and int(cell.get("y")) == celly:
                for feature in cell.findall("feature"):
                    for geometry in feature.findall("geometry"):
                        if geometry.get("type") == "Polygon":
                            for coordinates in geometry.findall("coordinates"):
                                points = []
                                for point in coordinates.findall("point"):
                                    points.append(
                                        (int(point.get("x")), int(point.get("y")))
                                    )
                            for properties in feature.findall("properties"):
                                for property in properties.findall("property"):
                                    draw.polygon(
                                        points, fill=colours[property.get("value")]
                                    )

        draw.polygon(
            (
                (posX - 1, posY - 1),
                (posX + 1, posY - 1),
                (posX + 1, posY + 1),
                (posX - 1, posY + 1),
            ),
            (255, 0, 0),
        )

        image = image.rotate(270)
        image.save("map.png")
        sinceSeen = datetime.now() - user.lastSeen
        if sinceSeen.seconds < 60:
            timeString = "less than a minute"
        elif sinceSeen.seconds < 60 * 60:
            minutes = sinceSeen.seconds // 60
            timeString = f"{minutes} minute{'s' if minutes > 1 else ''}"
        else:
            hours = sinceSeen.seconds // (60 * 60)
            timeString = f"over {hours} hour{'s' if hours > 1 else ''}"

        await ctx.send(
            file=discord.File("map.png"),
            content=f"{name} was last seen {timeString} ago",
        )
