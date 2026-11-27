# Multiplayer Task


### What we created
For our mini game we created browser game in which players (2-8) can choose their team and test their vocabulary against each other. Players must type a word that begins with the last letter of their opponents word. Higher words are preferable, gaining more "pull" on the points tug of war. Teams start with 60 seconds to answer, that then decreases per round - creating a more challenging gameplay loop. Words are checked using a dictionary API: https://api.dictionaryapi.dev/api/v2/entries/en/. Incorrect words are blocked and repeated words. 





### Multiplayer + Setup

The game is hosted on a public sever using Firebase. The URL of the server is put in the GameManager. When the player loads the game, it verifies the link to the server and connects the player on their local machine whilst waiting for other players to join. 

To play the game, all players must just open the build on their Windows device and one player must host. Remaining players must input the lobby code given to them by the lobby host. 




### JOSIE CONTRIBUTIONS






### ZOE CONTRIBUTIONS

I implemented audio and refined the UI. When the scene starts ApplyKrillionTheme() runs and applies a unified colour palette onto every UI object. The screen flashes (when correct or incorrect) are made by creating a UI image FlashOverlay that is transparent (alpha set to 0). When TriggerFlash is called it then becomes visible. Colours can be set in the inspector. Audio shares a single AudioSource and uses PlayOneShot. 

I also setup the web hosting. I built the game for platform Web and then setup a Github Pages URL.