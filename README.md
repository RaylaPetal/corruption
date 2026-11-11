# Corruption

A Dalamud plugin for FINAL FANTASY XIV. Two nearby players with the plugin earn XP and arousal while each plays an animation from their own picked Penumbra mods, level up through corruption tiers, and climax into [Cummy](https://github.com/RaylaPetal/cummy) layers. **18+.**

Everything runs locally. There is no server and no pairing: clients find each other through a small hidden SimpleHeels tag, and progress is a file on your own PC.

## How a scene earns

Each client checks these for itself, once a second. A tick of XP and arousal lands after 10 seconds of them all holding:

1. You're looping an emote that one of your picked mods replaces, and Penumbra is serving that mod's file.
2. Someone nearby has Corruption, which you can tell from their tag.
3. Their tag says they're playing an animation from one of their own picked mods.
4. You can see them playing the emote they declared.
5. You're within 3 yalms of each other.

Your animations don't need to match, or even come from the same mod. Playing alone earns nothing, unless the mod is marked to count alone (see Solo scenes below). The HUD says which condition is failing. A quiet scene builds to a climax in about 11 minutes.

## Using it

- The first time you log in, a welcome window takes you through setup: difficulty and decay (locked afterward unless you reset your profile), climax mode and arousal gain (change them any time on **Profile**), then your anatomy, pregnancy, Cummy layering, the safeword, and what shows. You can go through it again from **Settings**. `/corr` opens the main window.
- **Personality and traits:** during setup you pick one personality and up to two traits. Each gives something and costs something: *Shy* earns more when nobody watches but counts watchers half, *Hypersexual* builds arousal faster but gets Pent Up after a day without a scene, *Thrifty* pays less in the shop but its items don't last as long. *Easygoing* does neither. They're locked with the rest of your profile until you reset it. Characters set up before this choose once when the welcome opens again. Some personalities have chemistry, such as Dominant with Submissive, and scenes between them pay a little more. Nearby shows other players' personalities, and `{personality}` puts yours on your nameplate.
- In **Acts**, tick your Penumbra folders, then tick the mods that count. Every looping animation in a ticked mod counts, except any you exclude. Click **Exclude some** on a mod to search its animations (try "cuddle") and untick them, or click **Exclude this animation** on the Profile scene card while it plays.
- **Goals** has your daily and weekly goals and your achievements. You're only given goals you can meet, so craving goals come only while pregnant and climax-together goals with a level 3 bond. Some achievements have higher tiers (Insatiable II and III, for example), and a few are hidden as "???" until you earn them.
- **Shop** sells items for Sins, which you earn from a daily check-in, scenes, levels, goals and achievements: faster or slower arousal, XP incense, a refractory cut, a streak charm and more, plus pregnancy items.
- **Titles:** reaching levels 10, 25, 50, 75 and 100, prestiging, and earning achievements each unlock a title, and the shop sells a few for Sins. Wear one from **Profile**: it shows over your nameplate and gives a small bonus that suits it. *Insatiable* shortens your refractory period, *Shameless* raises the watcher bonus, *Devout* adds to your check-in, and so on. Only the one you wear counts, and you can't change it during a scene or while you recover from a climax. Prestige keeps your titles. A profile reset clears them, bought ones too.
- **Scene summary:** when a scene of a minute or more ends, a popup sums it up: its length, XP, Sins, climaxes, the most watchers at once, and who it was with. The last one stays on **Profile**. Turn the popup off in **Settings**.
- **Solo scenes:** in **Acts**, tick **Counts alone** on a mod you want to count by yourself. Its animations then earn with nobody nearby, at half the XP for ticks and climaxes, for up to 45 minutes a day. After that, solo scenes earn nothing until the next UTC day. A partner with Corruption in an act still makes it a normal scene at the full rate. A climax alone layers you through your own Cummy: the groin with a vagina, otherwise the part nearest your genitals, usually a hand. Turn that off in **Settings → Cummy**. The shop's **Alone time** section sells a Toy, a Do Not Disturb Sign (20 more solo minutes today), a Vanity Mirror and an Afterglow Candle. There are solo goals, the *Self-Love*, *Hands-On*, *Self-Sufficient* and *Me Time* achievements (and a hidden one), each with a title that helps alone, and a *Lone Wolf* title in the shop.
- **Moon events** follow Eorzea's own moon, so everyone shares them with no server. During a Full Moon arousal builds 1.1x as fast, and during a New Moon scene time pays 1.5x Sins. Each phase lasts 4 hours 40 minutes of real time. The header and HUD show the moon and when the next event starts.
- **Nearby** lists who's around with Corruption, with their title and your history together (scenes, time together and when you were last together), plus a list of past partners. **Hide me** there, or `/corr hide`, stops other players seeing you have it. Scenes can't earn while you're hidden.
- `/corr hud` toggles the HUD. `/corr plate` toggles your nameplate text. `/corr climax` climaxes by hand at 70 arousal or more (40 with a Hair Trigger); at 100 it happens by itself.
- **Safeword:** Ctrl+Shift+End, or `/corr pause`, stops scene XP and arousal at once, and shows others you've paused. `/corr resume` continues. Change the key in **Settings**.

XP comes from your tier, your partner's tier if higher, people watching you, your streak, your bond, Cummy layers you're wearing, your prestige stars and your difficulty, not from which animation is playing.

- **Prestige:** at level 100, prestige from **Profile** or with `/corr prestige confirm`. You go back to level 1 for a star and +5% XP for good, up to +50% at 10 stars. Everything else stays.
- **Bonds:** at any level, click **Bond** on someone in **Nearby**. When they bond back, scenes together pay 1.25x. The bond levels up with time together (1, 3, 6 and 12 hours), adding faster arousal, a reward for climaxing together, better conception odds, and finally 1.35x XP and a shorter refractory period. Either of you can unbond.
- **Marked:** each Cummy layer stage you wear adds 3% XP, up to 1.3x. This needs Cummy's plugin API version 2. With API version 5, face layers count double, and a full coat (every part you don't block layered) counts 1.5x. **Your story** then shows what's layered now, part by part. With API version 6, each dripping hole counts as 3 more stages, and **Your story** shows it with the time it has left.
- **Licking clean (Cummy API 5):** for two minutes after you climax with a partner, a **Lick clean** button on the scene card and their Nearby card licks off the part your climax most likely landed on. If it comes off, you get a little XP, and bond time if they're your bond. Cummy's own rules decide whether the lick goes through: you need to be paired in Cummy, or in Public mode, and close enough. When a recent scene partner licks or kisses you clean, your arousal goes up a little. There are lick goals, and the *Cleanup Crew*, *Head to Toe* and *Glazed* achievements, each with a title. Parts you block in Cummy are never aimed at.
- **Creampies (Cummy API 6):** when your scene partner climaxes with the tip of their penis inside your pussy or ass (within 7 cm, read from both IVCS skeletons), your own Cummy creampies that hole instead of layering you outside: it drips for a while and steps up your groin or butt. You get 50 XP, and it counts for creampie goals and the *Filled*, *Both Ways*, *Seconds* and *Well Bred* achievements, each with a title. If you block that hole in Cummy, the climax lands outside as usual. A partner set to Vagina never creampies, and a pussy needs your anatomy to be Vagina, Both or not set. If you came inside your partner, **Lick clean** licks it out of them for 30 XP, and a recent partner licking you out gives you 10 arousal. While you drip, a Dripping Moodle shows which hole.
- **Profile** shows your status, how long since your last scene, and how long until decay starts, or until the next fade. **Your story** there counts everything: time in scenes and your longest, climaxes, loads taken and given, time watched, conceptions and babies (each birth with its date and father), Sins and progress.
- **Pregnancy** is off until you turn it on in **Settings**, as someone who can carry, breed or both. Carriers get a 7-day cycle with fertile days, and can conceive only from a breeder partner's creampie in their pussy, one roll each time, so it needs Cummy's plugin API 6. A shot outside, even on the groin, or a creampie in the ass never leads to conception; block "Creampie: pussy" in Cummy to stay safe. You can set your own conception chance. A pregnancy lasts 14 days, with daily cravings and a baby on **Profile** that grows as it goes, then a birth.

Under **Settings → Bridges**, all off until you turn them on:

- **Tier visuals:** pick a Glamourer design or a Penumbra mod option for each tier, and for each trimester if you can carry. It goes on when you reach that tier, and synced players see it.
- **Toys:** through [Intiface Central](https://intiface.com/central/). Toys follow your arousal during a scene, and a climax gives a 3 second burst. The safeword stops them at once.
- **Moodles:** Aroused, Marked, Watched, Bonded, Refractory and Fading statuses on your character, plus Fertile, Pregnant, Craving, Unsatisfied and Recovering with pregnancy on. Moodles needs "Allow other plugins to apply Moodles" turned on.

Requirements: Penumbra, SimpleHeels, and a sync service such as Lightless. **You can only find someone you're synced with**, because the tag travels through your sync service. Optional: Glamourer, Moodles and Intiface Central for the bridges, and Cummy (plugin API 3 or later), which layers you where your scene partner climaxes: the part of you nearest their genitals, read from both skeletons, then the next nearest if it's full. Set **Your anatomy** in **Settings** (Penis, Vagina or Both), so partners aim your climaxes from the right place. No pairing is needed, your own Cummy part blocks still apply, and you can turn it off in **Settings**.

**Nameplate text:** your tier and level show above your nameplate. Add `{prestige}` to show your stars, or `{title}` for your title. Your equipped title also gets its own line, above the text or below your name, or off, under **Nameplate**. It shows even when the text is turned off. During an act, or while you have any arousal, it cycles with your arousal, and after a climax with a climax line. Edit the texts, size, height and pace under **Nameplate**. Only you and players with Corruption who are synced with you can see it. Anyone else sees nothing.

Progress is local and your partner's plugin is trusted, so it's easy to cheat. That's accepted for a hobby plugin.

## Installing

In game, open `/xlsettings` → **Experimental**, add this URL under **Custom Plugin Repositories**, save, then install **Corruption** from `/xlplugins`:

```
https://raw.githubusercontent.com/RaylaPetal/corruption/main/repo.json
```

Your partner needs it too. For Cummy, add its own repository, `https://raw.githubusercontent.com/RaylaPetal/cummy/main/repo.json`.

To test alone, build it in Debug (see below) and load it as a dev plugin: it has a **Debug** tab with a simulated partner and detection diagnostics.

## Releasing

Bump `<Version>` in `Corruption/Corruption.csproj` (4 parts, e.g. `0.1.1.0`), commit it to `main`, then push a matching tag (`v0.1.1.0`). The release workflow builds and tests it, publishes a GitHub release with `Corruption-<version>.zip`, points `repo.json` on `main` at it and prunes old releases. A tag ending in `-testing` publishes a testing build only.

## Building

```
git clone --recursive <this repo>
dotnet build Corruption.slnx
dotnet test --project Corruption.Tests/Corruption.Tests.csproj
```

Requires the .NET 10 SDK and Dalamud installed at least once. Rules that don't need the game live in `Corruption.Core`, which has the tests. Balance constants ship in `Corruption/balance.json`.

## Credits and licence

Corruption is licensed under the **GNU AGPL v3** (see `LICENSE.md`).

- The mod scanning and pose reading are adapted from PoseKit (AGPL-3.0), and the SimpleHeels tag approach from CoPose.
- The nameplate overlay, and the Glamourer, Moodles and Intiface wrappers, are adapted from Oathbound. The overlay uses [KamiToolKit](https://github.com/MidoriKami/KamiToolKit).
- Toys via [Buttplug](https://github.com/buttplugio/buttplug), and Glamourer via [Glamourer.Api](https://github.com/Ottermandias/Glamourer.Api).
- Penumbra IPC via [Penumbra.Api](https://github.com/Ottermandias/Penumbra.Api).
