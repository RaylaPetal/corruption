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
- **Pregnancy** is off until you turn it on in **Settings**, as someone who can carry, breed or both. Carriers get a 7-day cycle with fertile days, and can conceive only from a breeder partner's creampie in their pussy, one roll each time, so it needs Cummy's plugin API 6. A shot outside, even on the groin, or a creampie in the ass never leads to conception; block "Creampie: pussy" in Cummy to stay safe. You can set your own conception chance. A conception is secret: nothing tells you, and your cycle shows as before. Take a **Pregnancy Test** from the shop to find out, or wait until your belly starts to show on day 5. Until then there's no card, status, look or craving, and the father isn't told. A pregnancy lasts 14 days, with daily cravings and a baby on **Profile** that grows as it goes, then a birth.

Under **Settings → Bridges**, all off until you turn them on:

- **Tier visuals:** pick a Glamourer design or a Penumbra mod option for each tier, and for each trimester if you can carry. It goes on when you reach that tier, and synced players see it.
- **Toys:** through [Intiface Central](https://intiface.com/central/). Toys follow your arousal during a scene, and a climax gives a 3 second burst. The safeword stops them at once.
- **Moodles:** Aroused, Marked, Watched, Bonded, Refractory and Fading statuses on your character, plus Fertile, Pregnant, Craving, Unsatisfied and Recovering with pregnancy on. Moodles needs "Allow other plugins to apply Moodles" turned on.

Requirements: Penumbra, SimpleHeels, and a sync service such as Lightless. **You can only find someone you're synced with**, because the tag travels through your sync service. Optional: Glamourer, Moodles and Intiface Central for the bridges, and Cummy (plugin API 3 or later), which layers you where your scene partner climaxes: the part of you nearest their genitals, read from both skeletons, then the next nearest if it's full. Set **Your anatomy** in **Settings** (Penis, Vagina or Both), so partners aim your climaxes from the right place. No pairing is needed, your own Cummy part blocks still apply, and you can turn it off in **Settings**.

**Nameplate text:** your tier and level show above your nameplate. Add `{prestige}` to show your stars, or `{title}` for your title. Your equipped title also gets its own line, above the text or below your name, or off, under **Nameplate**. It shows even when the text is turned off. During an act, or while you have any arousal, it cycles with your arousal, and after a climax with a climax line. Edit the texts, size, height and pace under **Nameplate**. Only you and players with Corruption who are synced with you can see it. Anyone else sees nothing.

Progress is local and your partner's plugin is trusted, so it's easy to cheat. That's accepted for a hobby plugin.

## Plugin API

Other Dalamud plugins can read Corruption's state for the local character, hear when things happen, and, where the player allows it, breed, change arousal or ask for a climax. Copy [`Corruption.Api/CorruptionIpc.cs`](Corruption.Api/CorruptionIpc.cs) into your plugin for typed calls, records, named outcome strings (`CorruptionStrings`) and events that survive Corruption reloading. Or call the gates directly by name.

- **Threads:** call everything from the framework thread (`Framework.Update`, `RunOnFrameworkThread`, or your UI's Draw). From any other thread, reads return their empty value and writes return `wrong-thread`. Events are raised on the framework thread.
- **Before setup:** reads have `Ready = false` and empty values, and writes return `not-set-up`.
- **Formats:**
  - enums cross as lower-case strings (`"ovulation"`, `"second"`, `"earning"`);
  - times are UTC `DateTime`, with `default` for none;
  - names and worlds are separate strings.
- **Stability:** version 2 changed `Breed` and `BreedChance` (see below), and is now frozen. Later versions only add gates.
- **Secret pregnancies (version 2):** a conception stays secret until the player takes a Pregnancy Test or it shows at day 5. Until then `GetPregnancy` reads `none`, `GetCycle` and `BreedChance` read as if nothing happened, `Breed` answers `bred`, and `OnConceived` hasn't fired. Plugins see exactly what the player knows.

### Lifecycle

| Gate | Signature | Notes |
| --- | --- | --- |
| `Corruption.ApiVersion` | `() -> int` | 2 |
| `Corruption.Ready` | event `()` | Gates are registered. Re-read your state. |
| `Corruption.Disposing` | event `()` | Corruption is unloading. |

### Reads

| Gate | Returns |
| --- | --- |
| `Corruption.GetProfile` | `(bool Ready, string Name, string World, int Level, string Tier, double Xp, double XpToNext, int Prestige, string Title, string Personality, string Anatomy, long Sins, int StreakDays, bool Paused, bool Hidden)` |
| `Corruption.GetArousal` | `(bool Ready, int Arousal, double RefractorySeconds, bool Edging, string ClimaxMode)`. Arousal is 0 to 100 as shown. Edging is held at 100 outside refractory. ClimaxMode is `auto` or `manual`. |
| `Corruption.GetScene` | `(bool Ready, string State, bool Solo, string PartnerName, string PartnerWorld, int Watchers, double Seconds)`. State is `idle`, `waiting`, `earning` or `paused`. |
| `Corruption.GetBond` | `(bool Ready, string Name, string World, bool Formed, int Level)`. This is the bond or bond request; Level is 0 until formed. |
| `Corruption.GetCycle` | `(bool Ready, bool Enabled, bool CanCarry, bool CanBreed, bool Running, int Day, string Phase, double Chance, bool Protected, DateTime NextPhaseUtc)`. Phase is `period`, `follicular`, `fertile`, `ovulation` or `luteal`. Chance is what a breeding would have now from this character's side (cycle, items, chance setting, own titles). Day and Chance are 0 without a running cycle. A pregnancy that isn't confirmed yet reads as the cycle running on. Protected means a Contraceptive Pill is active. |
| `Corruption.GetPregnancy` | `(bool Ready, string Stage, double DaysPregnant, DateTime ConceivedUtc, DateTime DueUtc, string FatherName, string FatherWorld, DateTime RecoveryUntilUtc, string Craving, bool CravingMet, bool Unsatisfied, int Conceptions, int Births, int Fathered)`. Stage is `none`, `first`, `second`, `third` or `recovering`. A pregnancy reads as `none` until it's confirmed. |
| `Corruption.GetBirths` | `List<(DateTime BornUtc, DateTime ConceivedUtc, string FatherName, string FatherWorld)>`, oldest first |
| `Corruption.GetForceCreampie` | `() -> bool`: whether forced creampies are in effect (some plugin turned them on, and the player still allows breeding). |
| `Corruption.GetNearby` | `List<(string Name, string World, double Distance, int Level, int Arousal, bool InAct, int ClimaxCount, bool HasBodyTag, bool CanCarry, bool CanBreed, bool Virile, string Anatomy, string Stage)>`. This is only what each player's tags publish to everyone, never their cycle or father. The body fields need `HasBodyTag`. |
| `Corruption.BreedChance` | `(string breederName, string breederWorld, double chanceFactor) -> double`. This is the chance a `Breed` by that player with that factor would have now, without rolling, or 0 when `Breed` would refuse. |

### Events

Every event is raised at most once per transition. `OnArousalChanged` and `OnStateChanged` are raised at most once a second.

| Gate | Arguments |
| --- | --- |
| `Corruption.OnClimax` | `()`: the local character climaxed. |
| `Corruption.OnPartnerClimax` | `(string name, string world)`: a scene partner's climax was credited. |
| `Corruption.OnSceneStarted` | `(string partnerName, string partnerWorld, bool solo)` |
| `Corruption.OnSceneEnded` | `(double verifiedSeconds, double xp)` |
| `Corruption.OnLevelUp` | `(int level)` |
| `Corruption.OnCyclePhaseChanged` | `(string phase)` |
| `Corruption.OnConceived` | `(string fatherName, string fatherWorld, string source)`: a pregnancy was confirmed, by a test or by showing at day 5, not when it was conceived. Source is `scene` or the plugin whose `Breed` started it. |
| `Corruption.OnTrimesterChanged` | `(string stage)`: `first`, `second` or `third`. |
| `Corruption.OnBirth` | `(string fatherName, string fatherWorld)` |
| `Corruption.OnRecoveryEnded` | `()` |
| `Corruption.OnArousalChanged` | `(int arousal)`: the shown whole number changed. |
| `Corruption.OnStateChanged` | `()`: something a read returns changed. Values that tick every second alone don't count: scene seconds, refractory left, days pregnant, distance. |

### Writes

Each write needs the player's consent, the one "Let other plugins control Corruption" switch under **Settings → Other plugins**, for that character. It's off by default, and without consent a write returns `not-allowed`. Every call is listed there with its plugin name, time and outcome. No write gives XP, Sins or items.

| Gate | Signature | Outcomes |
| --- | --- | --- |
| `Corruption.Breed` | `(string breederName, string breederWorld, string pluginName, double chanceFactor) -> string` | `bred`, `not-allowed`, `not-set-up`, `pregnancy-off`, `cant-carry`, `pregnant`, `recovering`, `protected`, `paused`, `hidden`, `not-a-breeder`, `too-soon`, `no-character` |
| `Corruption.SetForceCreampie` | `(string pluginName, bool on) -> string` | `ok`, `not-allowed`, `not-set-up` |
| `Corruption.AddArousal` | `(double amount, string pluginName) -> string` | `ok`, `limited`, `not-allowed`, `not-set-up`, `paused`, `refractory` |
| `Corruption.Climax` | `(string pluginName) -> string` | `climaxed`, `not-allowed`, `not-set-up`, `paused`, `no-scene`, `refractory`, `too-low` |

- **Breed:**
  - rolls conception once for the local character, as if that player bred them, with the same odds as a scene: cycle phase, Fertility Charm, the chance setting, the bond, both players' titles and traits where known, and the breeder's Virility Tonic from their tag;
  - a breeder whose Corruption body tag is seen must have "can breed" (`not-a-breeder` otherwise);
  - a breeder without Corruption rolls with no virility factor;
  - `chanceFactor` multiplies those odds, for example by the bull's level. It's kept to ×0.5–×2 (anything that isn't a number counts as ×1), and the 90% cap and the player's chance setting still apply after it. Settings lists the factor used;
  - the same breeder can breed once a minute (`too-soon`);
  - `bred` means it rolled, whether or not it took. A conception records the breeder as the father and stays secret until confirmed;
  - while a pregnancy is secret, `Breed` answers `bred` without rolling, so a caller can't tell; once it's confirmed it answers `pregnant`.
- **SetForceCreampie:** while on, a scene partner's credited climax creampies the local character's pussy even when the bones aren't lined up, so a breeder's climax rolls for conception:
  - it needs the same consent to turn on (`not-allowed` otherwise), and turning off always works;
  - only the carrier's own client decides, so it does nothing on the bull's side. Both players need Corruption, since only their climaxes are credited;
  - a hole the bones do find still wins, a character set to Penis isn't forced, and Cummy's blocks ("Creampie: pussy") still apply;
  - it lasts until turned off or Corruption reloads, so set it again on `Ready`. Settings shows which plugin has it on.
- **AddArousal:** at most 25 either way per call and 60 per plugin per minute (`limited` when part was cut). A rise isn't taken during refractory. Reaching 100 behaves as in a scene: in automatic mode the scene's next tick climaxes.
- **Climax:** the same rules and effects as `/corr climax`. It needs a verified scene and enough arousal, and not refractory.

### A breeding session

A plugin with a bull/breeding role might:
1. watch `OnArousalChanged`, and start its breeding timer once arousal rises;
2. end it on `OnClimax` (the carrier) or `OnPartnerClimax` (the bull, if both have Corruption);
3. check `GetPregnancy().Stage` is `none`, or call `BreedChance(bullName, bullWorld, factor)` to show the odds;
4. call `Breed(bullName, bullWorld, "Your Plugin", factor)`; `bred` means the roll happened;
5. listen for `OnConceived` to hear when the carrier finds out, by a test or at day 5.

For breeding through real scenes instead, a "climaxes are always creampies" checkbox on the carrier's side can call `SetForceCreampie("Your Plugin", true)`, and `false` when it's unticked or the session ends.

The carrier's own pregnancy settings decide, and a confirmed pregnancy answers `pregnant`.

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
