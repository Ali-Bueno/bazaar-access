# BazaarAccess Progress Log

## January 16, 2026

### Combat Announcement System Overhaul

Changed from wave-based accumulated summaries to immediate per-card announcements for better real-time feedback.

**Before:** Effects were accumulated and announced as summaries when activity paused.
- Format: "You: [damage] ([top item]). Enemy: [damage] ([top item]), [status]."

**After:** Each card trigger is announced immediately as it happens.
- Format: "[ItemName]: [amount] [effect]"
- Player items: "Sword: 10 damage"
- Enemy items: "Enemy Dagger: 5 damage"

**Changes in CombatDescriber.cs:**

1. **Disabled periodic health announcements**
   - Removed automatic health announcement loop during combat
   - Users can press H for summary instead
   - Health threshold warnings (low/critical) remain active

2. **Immediate effect announcements**
   - Replaced wave accumulation system with instant per-effect announcements
   - Each effect is spoken immediately via `TolkWrapper.Speak()`

3. **New `FormatEffectAnnouncement()` method**
   - Formats effects as "[ItemName]: [amount] [effect]"
   - Prefixes enemy items with "Enemy "
   - Handles damage, heal, shield, burn, poison, slow, freeze
   - Adds "crit" suffix for critical hits
   - Special "Frozen!" alert for enemy freeze effects

4. **Simplified `OnEffectTriggered()` handler**
   - Removed wave accumulation logic
   - Now builds and announces each effect immediately
   - Still tracks damage totals for H key summary

---

### Combat Quick Stats Keys (1-4)

Added number keys 1-4 to announce individual combat stats with numbers only for quick reference during combat.

**New Keybindings:**
- **1** - Player health (number only)
- **2** - Enemy health (number only)
- **3** - Damage dealt (number only)
- **4** - Damage taken (number only)

**Files Modified:**

1. **BazaarAccess/Accessibility/AccessibleMenu.cs**
   - Added 4 new enum values to `AccessibleKey`: `PlayerHealth`, `EnemyHealth`, `DamageDealt`, `DamageTaken`

2. **BazaarAccess/Core/KeyboardNavigator.cs**
   - Added key mappings for `KeyCode.Alpha1` through `KeyCode.Alpha4` in the `MapKey()` method

3. **BazaarAccess/Gameplay/GameplayScreen.cs**
   - Added handlers in the combat switch block for the 4 new keys, calling the corresponding `CombatDescriber` methods

4. **BazaarAccess/Gameplay/CombatDescriber.cs**
   - Added `GetPlayerHealth()` - returns player health as string
   - Added `GetEnemyHealth()` - returns enemy health as string
   - Added `GetDamageDealt()` - returns total damage dealt as string
   - Added `GetDamageTaken()` - returns total damage taken as string

**Testing:**
- Build succeeds with no errors or warnings
- During combat, press 1-4 for quick stats, H for full summary

---

### Day/Hour Announcement Feature

Added automatic announcements when the day or hour changes during gameplay, helping blind players track run progression.

**Features:**
- Announces "Hour X" when progressing to a new hour
- Announces "Day X" when progressing to a new day (takes priority over hour)
- Resets tracking on new run

**Implementation Journey:**

1. **Initial Implementation**
   - Added `_lastDay` and `_lastHour` tracking variables
   - Added `CheckAndAnnounceDayHourChanges()` method to detect and announce changes
   - Called it from `OnStateChanged()` and transition events

2. **Diagnosis Phase**
   - Hour 1 announced correctly on new game
   - Subsequent hours NOT announced after shop choices
   - Added logging to investigate
   - User tested and reported logs showing the issue

3. **Root Cause Discovery**
   - `OnStateChanged` fires BEFORE `Data.Run.Hour` is updated by game engine
   - When checking for hour changes, the hour hadn't actually changed yet
   - Comparison (1 != 1) failed because hour still showed old value

4. **Fix: Delayed Coroutine**
   - Replaced immediate check with `DelayedCheckDayHourChanges()` coroutine
   - Waits 0.5 seconds for game data to update before checking
   - Now hour changes announce correctly after shop choices

**Files Modified:**
- `BazaarAccess/Patches/StateChangePatch.cs`
  - Added `_lastDay` and `_lastHour` tracking fields
  - Added `CheckAndAnnounceDayHourChanges()` method
  - Added `DelayedCheckDayHourChanges()` coroutine (0.5s delay)
  - Called from `OnStateChanged()`, `OnBoardTransitionFinished()`, `OnNewDayTransitionFinished()`
  - Reset tracking on `NewRun` state