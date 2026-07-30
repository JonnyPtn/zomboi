# Zomboi

A discord bot for dedicated project zomboid servers. Currently under heavy development, but any testing/feedback is welcome.

This is also an exercise for me to learn c# better so if you are experienced and wondering wtf is going on with this code, please share some knowledge with me :)

## Commands

The bot makes use of slash commands for most things, so if you start typing `/` discord will show the commands available.

Features include:
- Server management
- Mod management
- Player info

## Notifications
- Players joining or leaving
- Players levelling up
- Chat mirrored from Global in-game chat (Only game -> discord mirroring, not the other way round yet)

## Discord setup

Create an application in the [Discord developer portal](https://discord.com/developers/applications) and add a bot to it, then grab the bot token for `zomboi.ini`.

### Permissions

Invite the bot with both the `bot` and `applications.commands` scopes - the second one is what allows it to register the slash commands. The permissions it needs are:

| Permission | What it's needed for |
| --- | --- |
| View Channel | Finding the channels you list in `zomboi.ini` |
| Send Messages | Posting the join/leave and level up notifications |
| Embed Links | The player and server info commands reply with embeds |
| Manage Webhooks | Chat mirroring posts through a webhook so in-game names and avatars show up |

The easiest way to get all of that is to use this invite link, replacing `<YOUR_APP_ID>` with your application ID:

```
https://discord.com/api/oauth2/authorize?client_id=<YOUR_APP_ID>&permissions=536890368&scope=bot%20applications.commands
```

If you'd rather build the link yourself in the portal's OAuth2 URL generator, tick the four permissions above.

Bear in mind that channel specific permission overwrites can still deny these even when the bot's role allows them, so if a notification channel is private make sure the bot's role is granted access to it.

### Quick start

Copy the `zomboi.ini.sample` file and rename to `zomboi.ini` then populate with your desired values. Then run `dotnet run`
