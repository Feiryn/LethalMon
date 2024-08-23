# LethalMon

LethalMon is a Lethal Company mod that includes the possibility to catch monsters and use them to help you.

This mod is still in alpha so expect bugs or not a lot of features.


## How to catch monsters?

You have to find balls in the buildings and then send them to a monster with left click. For now, not all monsters are implemented and catchable. Please take a look at [Implemented monsters and behaviours](#implemented-monsters-and-behaviours)

Each ball has a different probability of success depending on the type of ball and the monster's type.

The stronger the monster is, the harder to catch it is. And be careful, if the catch fails, the ball disappears and the monster will want to kill you.

![balls.png](https://raw.githubusercontent.com/Feiryn/LethalMon/master/Images/balls.png)


## Now that I caught one, what should I do?

You can throw the ball on the ground where you want the monster to appear.

To retrieve it, press P (configurable).

If a player dies or disconnects, the monster will be called in the ball at its location.


## Implemented monsters and behaviours

Here are the implementation status of monsters:

|     Monster     | Implemented |                               Behaviour                                |                     Capture failure                 |
|:---------------:|:-----------:|:----------------------------------------------------------------------:|:----------------------------------------------------:
|     Bracken     |     Yes     |                           Drags enemies away                           |                     Chasing thrower                 |
|     Spider      |     No      |                                  TBD                                   |                           TBD                       |
|     Butler      |     Yes     |                 Clean up dead enemies and spawn scraps                 |                      Stabs thrower                  |
|    Coil-head    |     No      |                                  TBD                                   |                           TBD                       |
|   Ghost Girl    |     Yes     |                   Teleports enemies and damages them                   |         Scaring thrower 3 times, followed by a hunt |
|  Hoarding Bug   |     Yes     |              Brings items in a line of sight to the owner              |                     Angry at thrower                |
|    Hygrodere    |     No      |                                  TBD                                   |                           TBD                       |
|     Jester      |     No      |                                  TBD                                   |                           TBD                       |
|     Masked      |     Yes     |      Can lend you its mask to make you see enemies through walls       |            Spawning 2 ghosts that deal damage       |
|   Nutcracker    |     Yes     |                 Shoots at any enemy in a line of sight                 |       Shooting while rotating, similiar to a turret |
|   Snare Flea    |     No      |                                  TBD                                   |                           TBD                       |
|  Spore Lizard   |     Yes     |                         Can be used as mount                           |                     Spawns a cloud                  |
|     Thumper     |     Yes     |   Can open all doors and disable turrets, but is not really obedient   |                     Target thrower                  |
|      Bees       |     Yes     |          Stun monsters and damage players that hurt the owner          |                Attacking thrower for 10s            |
|    Manticoil    |     No      |                                  TBD                                   |                           TBD                       |
| Roaming Locusts | Will not be |                                   -                                    |                            -                        |
|   Tulip Snake   |     Yes     |                        Allows the player to fly                        |  Clinging to player, during which it is uncatchable |
|   Baboon Hawk   |     No      |                                  TBD                                   |                           TBD                       |
| Earth Leviathan |     No      |                                  TBD                                   |                           TBD                       |
|   Eyeless Dog   |     Yes     | Run far from the owner and howls in order to draw other dogs attention |            Chasing player more aggressively         |
|  Forest Keeper  |     No      |                                  TBD                                   |                           TBD                       |
|    Old Bird     |     No      |                                  TBD                                   |                           TBD                       |
|  Mask Hornets   |     No      |                                  TBD                                   |                           TBD                       |
|     Barber      |     No      |                                  TBD                                   |                           TBD                       |
|  Kidnapper Fox  |     Yes     |             Shoots its tongue at enemies and can kill them             |       Chase player or shoot tongue if close enough  |
|    Maneater     |     No      |                                  TBD                                   |                           TBD                       |


## Capture probabilities

To get capture probabilities, use the command `lethaldex` in the terminal (the image may be outdated as enemies are regularly added and capture rates can be modified):

![lethaldex.png](https://raw.githubusercontent.com/Feiryn/LethalMon/master/Images/lethaldex.png)

## Bug reports

You can report bugs on github https://github.com/Feiryn/LethalMon/issues or on the LC discord modding server https://discord.com/invite/lcmod
