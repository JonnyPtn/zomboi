from discord import Embed, Colour
from datetime import datetime

# Message formatting and coloring for public-facing logs


def __embedify(timestamp: datetime, colour: Colour, message: str) -> Embed:
    return Embed(timestamp=timestamp, colour=colour, description=message)


def chat_message(timestamp: datetime, message: str) -> Embed:
    """Stock blurple embed to relay a user's message"""
    return __embedify(timestamp, Colour.og_blurple(), message)


def perk(timestamp: datetime, user: str, aka: str, perk: str, level: int) -> Embed:
    """Blue embed to indicate a user's level-up"""
    message = f":chart_with_upwards_trend: {user} {aka}reached {perk} level {level}"
    return __embedify(timestamp, Colour.blue(), message)


def join(timestamp: datetime, user: str, aka: str) -> Embed:
    """Green embed to indicate a new user/character joining"""
    message = f":person_raising_hand: {user} {aka}just woke up in the Apocalypse..."
    return __embedify(timestamp, Colour.green(), message)


def resume(timestamp: datetime, user: str, aka: str, hours: int) -> Embed:
    """Green embed to indicate a user/character resuming"""
    message = f":person_doing_cartwheel: {user} {aka}has arrived, survived for {hours} hours so far..."
    return __embedify(timestamp, Colour.green(), message)


def leave(timestamp: datetime, user: str) -> Embed:
    """Red embed to indicate a user disconnecting"""
    message = f":person_running: {user} has left"
    return __embedify(timestamp, Colour.red(), message)


def death(timestamp: datetime, user: str, aka: str, hours: int) -> Embed:
    """Dark embed to indicate a user's/character's death"""
    message = f":zombie: {user} {aka}died after surviving {hours} hours :dizzy_face:"
    return __embedify(timestamp, Colour.dark_red(), message)
