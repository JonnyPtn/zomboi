import os

rconPassword = os.getenv('SERVER_RCON_PASSWORD')
channel = os.getenv('DISCORD_CHANNEL')
token = os.getenv('DISCORD_BOT_TOKEN')
mapsPath = os.getenv('ZOMBOI_MAPS_PATH', '')
logPath = os.getenv('ZOMBOI_LOG_PATH', '')
