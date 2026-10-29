# Phase 3A: Android Compatibility Checkpoint

## Project configuration

- Android scripting backend: IL2CPP
- Android CPU architecture: ARM64
- Build system: Gradle
- SQLite: official 3.54.0 amalgamation compiled into the IL2CPP player
- Runtime database path: `Application.persistentDataPath/wizkid_learning.db`

The Android player initializes SQLite before the first scene loads. Logcat
prints `WizKid Android SQLite checkpoint PASS` together with a persistent launch
number and current record counts.

## Unity validation

1. Install Android Build Support for Unity 2022.3.62f3, including the SDK, NDK,
   and OpenJDK modules.
2. Set a permanent package name in Player Settings. Do not change it between
   the initial APK and the update APK.
3. Run **WizKid > Phase 3A > Configure Android IL2CPP + ARM64**.
4. Run **WizKid > Phase 3A > Validate Android Checkpoint**.
5. The Console must print the Phase 3A `PASS` message before building.
6. Use **WizKid > Phase 3A > Build Development APK**, or connect a phone with
   USB debugging and use **Build and Run on Connected Device**.

## Device tests

1. Install and open the development APK. Confirm Logcat reports launch `#1`.
2. Complete at least one quiz and verify the session/attempt counts increase.
3. Enable airplane mode, complete another quiz, then fully close the app.
4. Reopen it while still offline. Confirm Logcat reports a larger launch number
   and the earlier counts are still present.
5. Build a higher version code and install it over the existing app without
   uninstalling. Confirm the launch number and record counts remain.

Uninstalling the app or clearing its app data removes the local database. The
future Supabase phase will protect already-synced records from that loss.
