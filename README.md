# LethalMon

A group of scientists called Lethal Monsters Inc. conducted experiments on monsters and designed balls to catch them.

Caught monsters can be used to help you in your journey by defending you, attacking enemies, or supporting you in various ways.

## Equipment

### PC

A fresh new PC has been added to your ship!

This PC has a lot of useful features:
- A guide that explains how to use thd mod
- A detailed list of monsters with their capture probabilities and behaviours
- A scanner that allows you to scan balls and unlock monsters
- A duplicator that allows you to duplicate unlocked monsters

![PC](https://raw.githubusercontent.com/Feiryn/LethalMon/master/Images/pc.png)

<details><summary>PC screenshots</summary>

![Dex](https://raw.githubusercontent.com/Feiryn/LethalMon/master/Images/dex.png)

![Tutorial Page 1](https://raw.githubusercontent.com/Feiryn/LethalMon/master/Images/tutorial-page1.png)

![Tutorial Page 2](https://raw.githubusercontent.com/Feiryn/LethalMon/master/Images/tutorial-page2.png)

![Tutorial Page 3](https://raw.githubusercontent.com/Feiryn/LethalMon/master/Images/tutorial-page3.png)

</details>

### Balls

Balls can be found in the buildings or in the ship's store.

You'll need them to catch monsters!

Balls found in the buildings can be found with a monster already inside... Maybe someone lost them?

![balls.png](https://raw.githubusercontent.com/Feiryn/LethalMon/master/Images/balls.png)

![store.png](https://raw.githubusercontent.com/Feiryn/LethalMon/master/Images/store.png)

## How to catch monsters?

If you got some balls in your hands, you can left-click to throw them. If you hit a monster, the capture will start.

If the capture is successful, the monster will be caught in the ball. If not, the monster will be angry and will want to kill you.

The capture probability depends on the ball's strength, the monster's strength (based on their abilities once caught, not on their original strength), and their remaining HP.

Lethal Monsters Inc. scientists are still experimenting with the balls, So not all monsters are catchable. Please take a look at the section below.

## Catchable monsters

Here are the monsters that can be captured:
- Baboon Hawk
- Barber
- Bees
- Bracken
- Butler
- Eyeless Dog
- Ghost Girl
- Hoarding Bug
- Hygrodere
- Kidnapper Fox
- Masked
- Nutcracker
- Spider
- Spore Lizard
- Thumper
- Tulip snake

Here are the monsters that cannot be captured yet:
- Coil-head
- Earth Leviathan
- Forest Keeper
- Jester
- Maneater
- Manticoil
- Mask Hornets
- Old Bird
- Snare Flea

## Monsters are not working at the company building!

By default, there are no path in the company building for the monsters to move.
You can fix this by installing the mod [NavMeshInCompany](https://thunderstore.io/c/lethal-company/p/Kittenji/NavMeshInCompany/)

## Addons

We also got you covered with additional mods that add up on this one:
- [LethalMonReservedSlot](https://thunderstore.io/c/lethal-company/p/Niro/LethalMonReservedSlot/) - A reserved slot for the first pokeball you pick up!
- Kidnapper Fox can be captured if re-enabled with [YesFox](https://thunderstore.io/c/lethal-company/p/Dev1A3/YesFox/) or other mods that re-enable it

## API

If you are a modder and want to add your own monsters, you can use the API provided by this mod.

The complete documentation is not finished yet, but you can register your own monster by calling `LethalMon.Registry.RegisterEnemy`, and by look at the code of [ExampleTamedBehaviour.cs](https://github.com/Feiryn/LethalMon/blob/master/LethalMon/Behaviours/ExampleTamedBehaviour.cs) and other behaviours in the same folder.

All public methods and fields are documented in the code, so you can take a look at it to understand how to use the API.

If you need help with implementing your own monster, you can ask for help in the LethalMon thread on the LC discord modding server https://discord.com/invite/lcmod

## Bug reports

You can report bugs on GitHub https://github.com/Feiryn/LethalMon/issues or on the LC discord modding server https://discord.com/invite/lcmod
