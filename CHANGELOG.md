# 0.6.3

- Load enemies AIs later to prevent conflicts with other mods (like LLL's moons mod)

# 0.6.2

- Fixed compatibility with EnemySkinRegistry that made the mod not working for players that didn't have the mod

# 0.6.1

- Added EnemySkinRegistry compatibility (requires version 1.4.6+)
- Added an option to make the PC saves per save and not global (the host decides)
- Trajectory of the balls has been slightly improved (correct calculation of the velocity at impact)
- Fixed a bug that made the bracken continue to drag enemies while the enemy was being captured
- Fixed a bug that made the HUD of the invoked monsters not disappearing when a player left and rejoined a game

# 0.6.0

- Added a PC where you can see the tutorial, browse the dex, scan balls and duplicate monsters
  - Thanks to [Foof](https://foof000.newgrounds.com/) for the PC model
  - Thanks to @tln_ on discord for the monsters descriptions
  - Thanks to @austinfrancisco on discord for the PC icons, background, and for testing

# 0.5.6

- Same as version 0.5.5, but with the correct DLL...

# 0.5.5

- Fixed incompatibility with an unknown mod that made the game not loading because of the hygrodere

# 0.5.4

- Hygrodere is now catchable (thanks to [@NiroDev](https://github.com/NiroDev))

# 0.5.3

- Spider is now catchable (thanks to [@NiroDev](https://github.com/NiroDev))
- Hoarder bug can hold items the owner gives him
- Improved and optimized the line of sight of a lot of tamed monsters, and most will work outside now
- Bees are working way better now
- Hoarder bug no longer brings the same item multiple times
- Fixed a bug that prevented the player from dying from some monsters while on a puffer (thanks to [@NiroDev](https://github.com/NiroDev))
- Fox is now disabled

# 0.5.2

- Fixed balls clipping for some players
- Fixed thumper not working in mines

# 0.5.1

- Added sounds to balls (thanks to [@NiroDev](https://github.com/NiroDev))
- Fixed masked ghosts being catchable (thanks to [@NiroDev](https://github.com/NiroDev))
- Fixed ghost girl escape behaviour synchronisation (thanks to [@NiroDev](https://github.com/NiroDev))

# 0.5.0

- Baboon hawk is now catchable (thanks to [@NiroDev](https://github.com/NiroDev))
- Throw algorithm has been greatly improved in order to prevent clipping and make it more natural
- Tamed monsters now have {owner}'s {monster} above them and parameters are configurable (thanks to [@NiroDev](https://github.com/NiroDev))
- Balls are now smaller
- Balls now fall correctly after a capture
- Masked xray distance is now configurable (thanks to [@NiroDev](https://github.com/NiroDev))
- Fixed tamed monsters owner's name in the scan node
- Fixed tulip snake being stuck in air when stopped controlling from too high (thanks to [@NiroDev](https://github.com/NiroDev))

# 0.4.10

- The less an enemy has health, the more it is easy to catch (thanks to [@NiroDev](https://github.com/NiroDev))
- The nutcracker's steps are now less loud
- Fixed a bug that didn't remove the mask if a player retrieved the masked while wearing the mask (thanks to [@NiroDev](https://github.com/NiroDev))
- Fixed a bug that made the balls collide with the cruiser and sometimes make it go upside down
- Mirage mod compatibility: prevent tamed monsters from mimicking the players voices
- SnatchinBracken mod compatibility: Make the bracken drop dragged players when captured
- Fixed bees behaviour

# 0.4.9

- Fixed a softlock on ship ladders if a player has a monster out
- Bees now stun on sight and have a cooldown

# 0.4.8

- v61 compatibility
- You can now find filled balls in the dungeon
- Nutcracker stops shooting at invisible enemies and its line of sight is now more accurate (thanks to [@NiroDev](https://github.com/NiroDev))
- Tooltip are now properly hidden when a monster is called back (thanks to [@NiroDev](https://github.com/NiroDev))
- Fixed a bug that could make the player go underground with the tulip snake (thanks to [@NiroDev](https://github.com/NiroDev))
- Big code refactoring and cleanup (thanks to [@NiroDev](https://github.com/NiroDev))

# 0.4.7

- Fixed a bug that made the host freeze if they died with a monster out

# 0.4.6

- Masked is now catchable (thanks to [@NiroDev](https://github.com/NiroDev))
- Fixed a bug that desynchronized the balls when a client died with a monster out
- Hoarding bug is a bit easier to catch

# 0.4.5

- Thumper is now catchable

# 0.4.4

- Added a lot of fields to the configuration file (host and clients are synced):
  - Balls spawn rarity
  - Balls prices in shop (and possibility to disable them)
  - Capture rate modifier to make the captures easier or harder
  - Possibility to disable some monsters
  - Choose if monsters react when a player fails to capture
  - Cooldowns for the bracken, dress girl, hoarding bug, fox and eyeless dog
  - Possibility to keep full balls or all balls if all the players are dead
- The fox will now attack if you fail to capture him (thanks to [@NiroDev](https://github.com/NiroDev))
- Fixed a bug that caused incompatibility with some mods like LethalEmotesAPI or EnemySkinRegistry

# 0.4.3

- Kidnapper fox is now catchable (thanks to [@NiroDev](https://github.com/NiroDev))
- Capture probabilities can be seen in the terminal with the command `lethaldex`
- Balls price has been greatly reduced
- Fixed a bug that allowed to capture already captured monsters
- Fixed a bug that showed the tip about scanning enemy for everyone and not just the player that took a ball

# 0.4.2

- Prevent tamed monsters from being hit or killed
- Eyeless dogs and butlers are now easier to catch
- Stop the butler's music when he is being captured

# 0.4.1

- Butler is now catchable (thanks to [@NiroDev](https://github.com/NiroDev))
- Fixed game lags

# 0.4.0

- Added an HUD for invoked monsters
- Bracken now has a 20s cooldown to drag enemies
- Hoarding bug now has 5s cooldown to bring items
- The ghost girl now has a 1min cooldown to attack enemies
- Bracken now goes back to the owner by walking and not teleporting
- Cooldowns now can't be exploited by calling a monster back to its ball and release it afterward
- Fixed a bug that made the bees spawn an hive each time they are called from their ball
- Fixed a bug that made the hoarding bug stuck if the owner was attacked
- Fixed a bug that sometimes made the nutcracker shoots at dead enemies
- Tamed monsters can no longer attack other tamed monsters
- The tip "You already have a monster out!" is now displaying correctly for clients

# 0.3.2

- Fixed a bug that made the content of balls not loaded properly
- Catchable scan node text is not added to modded enemies to avoid conflicts

# 0.3.1

- Fixed a bug that made the Nutcracker uncatchable
- Fixed a bug that allowed to capture dead monsters

# 0.3.0

- Ghost girl is now catchable (thanks to [@NiroDev](https://github.com/NiroDev))
- Nutcracker is now catchable
- Clients can now control the Spore Lizard (thanks to [@NiroDev](https://github.com/NiroDev))
- Added an 1/2 chance to not consume the ball when a capture fails
- Monsters can now be scanned in order to know if they are catchable (implemented) or not
- Tulip snake will now fight back when a capture fails (thanks to [@NiroDev](https://github.com/NiroDev))
- Improved the balls shakes algorithm to be more accurate
- Tamed monsters will no longer walk away when you invoke them and they will look at you directly
- The message that says "You already have a monster out" is now more visible
- Tulip snakes can no longer be captured by the player they are clinging to (thanks to [@NiroDev](https://github.com/NiroDev))
- Fixed a bug that could make the rider of a tulip snake go under the ground (thanks to [@NiroDev](https://github.com/NiroDev))

# 0.2.2

- Changed the key to unmount a monster to the crouch key
- Hoarding bug will now scan items more ofter (from every 5s to every 1s)
- Fixed a bug where the player could be softlocked if they retrieved a monster while riding it
- Fixed a bug that made clients unable to retrieve monsters
- Fixed a bug that could make hoarding bug softlocked

# 0.2.1

- Balls are now buyable
- Hoarder bug's flying sound has been changed to a less aggressive one (thanks to s1ckboy for the sound)

# 0.2.0

- Catchable eyeless dog
- Catchable tulip snake (thanks to [@NiroDev](https://github.com/NiroDev))
- Fixed a bug that could make some tamed monsters not working properly
- Fixed bees audio and size
- Fixed a bug that made the player's carry weight increased when balls were dropped

# 0.1.1

- Fixed a bug that can happen with mods that adds custom enemies

# 0.1.0

- Configurable key for retrieving tamed enemy (thanks to [@NiroDev](https://github.com/NiroDev))
- Catchable spore lizard that can be mounted (thanks to [@NiroDev](https://github.com/NiroDev))
- Catchable bracken that drags enemies away
- v55 compatibility (thanks to [@NiroDev](https://github.com/NiroDev))
- Big code refactoring and cleanup (thanks to [@NiroDev](https://github.com/NiroDev))

# 0.0.1

Items available :
- Pokeball
- Great ball
- Ultra ball
- Master ball

Catchable monsters
- Bracken (but doesn't do anything right now)
- Bees
- Hoarder Bug
