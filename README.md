# Multiplayer Task


### What we created
For our mini game we created browser game in which players (2-8) can choose their team and test their vocabulary against each other. Players must type a word that begins with the last letter of their opponents word. Higher words are preferable, gaining more "pull" on the points tug of war. Teams start with 60 seconds to answer, that then decreases per round - creating a more challenging gameplay loop. Words are checked using a dictionary API: https://api.dictionaryapi.dev/api/v2/entries/en/. Incorrect words are blocked and repeated words. 





### Multiplayer + Setup

The game is hosted on a public sever using Firebase. The URL of the server is put in the GameManager. When the player loads the game, it verifies the link to the server and connects the player on their local machine whilst waiting for other players to join. 

To play the game, all players must just open the build on their Windows device and one player must host. Remaining players must input the lobby code given to them by the lobby host.
The build is named TugOfWorlds.exe




### JOSIE CONTRIBUTIONS

I implemented the base systems of the game. I setup the game manager which connects to the firebase server and then created the function that runs the game against the dictionary api in order to properly check and verify words that go in the box so that it ensures only valid words are in there. I then also created the starting word segment as after a few playtests we realised that whoever was player 1 had an advantage due to always going first which was unfair, so i created a 20s phase that forces players to type the longest word, with whoever has the longer word wins and starts the round first.




### ZOE CONTRIBUTIONS

I implemented audio and refined the UI. When the scene starts ApplyKrillionTheme() runs and applies a unified colour palette onto every UI object. The screen flashes (when correct or incorrect) are made by creating a UI image FlashOverlay that is transparent (alpha set to 0). When TriggerFlash is called it then becomes visible. Colours can be set in the inspector. Audio shares a single AudioSource and uses PlayOneShot. I also built the game and solved any issues when building as well as write the project description and setup.
