# Steamworks.NET-DownloadItem-Bug
This repository contains an example of a bug that freezes the game and Steam client using the Steamworks.NET wrapper

Short issue description: When downloading an item that was previously removed from the hard-drive, the Steam client and game freeze for a couple of seconds when initializing the download process.
Unity version: 5.4.0f3
Steamworks.NET wrapper: 10.0.0 

How to use:
  - In `steam_appid.txt`, replace the `480` appId with an appId of your choosing.
  - Make sure you've got a number of subscribed items for that game
  - Run the `example` scene, then for items that are installed, click the delete button
  - Now click the download button and if lastTimeJumpLength ever exceeds 0, the issue is reproduced
