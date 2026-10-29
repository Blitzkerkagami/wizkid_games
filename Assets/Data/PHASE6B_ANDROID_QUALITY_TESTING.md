# Phase 6B Android Quality Testing

This checkpoint verifies the real Android build before forecast-accuracy evaluation.
Use a physical Android phone and keep the same package name throughout the test.

## Prepare the project

1. Exit Play Mode.
2. Set a permanent package name in **Player Settings > Android**. Do not use a
   `com.DefaultCompany` identifier.
3. Select **WizKid > Phase 3A > Configure Android IL2CPP + ARM64**.
4. Select **WizKid > Phase 6B > Validate Android Quality Test Setup**.
5. Continue only when the Console reports `Phase 6B setup PASS`.

## Initial APK and online baseline

1. Select **WizKid > Phase 6B > Build Initial Quality Test APK**.
2. Install the APK on a physical phone and open it while online.
3. Tap **QA 6B** at the lower-left corner. This button is present only in a
   Development Build.
4. Complete at least one game session and use **Refresh cloud now**.
5. Confirm SQLite is `PASS`, the cloud state becomes `UpToDate`, and pending
   records return to zero.

## Offline, restart, and recovery

1. Download at least one learning forecast while online.
2. Enable airplane mode and wait for the QA panel to record offline mode.
3. Complete a full game session. Confirm **Offline gameplay queued** becomes
   `PASS`; local data must remain playable and saved.
4. Fully close the app, then reopen it while airplane mode remains enabled.
   Confirm **Restarted while offline** and **Cached forecast readable offline**
   become `PASS`.
5. Disable airplane mode, return to the app, and tap **Refresh cloud now**.
   Confirm pending records return to zero and **Reconnect uploaded pending data**
   becomes `PASS`.

## App update preservation

1. Do not uninstall the initial APK and do not clear app data.
2. Select **WizKid > Phase 6B > Build Update Quality Test APK**. This increments
   the Android version code automatically.
3. Install the update APK over the initial APK.
4. Open the updated app and confirm existing scores, sessions, attempts, and the
   forecast cache are still present.
5. Open **QA 6B** and confirm **App update preserved data** becomes `PASS`.

## Performance and error recovery

- Play every activity for at least one minute on the target phone.
- Average performance must remain at or above 30 FPS.
- The test should finish without an Android low-memory warning or runtime error.
- Temporarily interrupt the network during a sync. Local data must remain pending;
  after reconnecting, the sync must recover without duplicate or lost records.

## Save evidence

Tap **Save and print report** in the QA panel. The JSON report is saved as:

`Application.persistentDataPath/phase6b_android_quality_report.json`

Use Android Logcat to capture the printed report, or retrieve it with Android
Debug Bridge from the app-specific files directory. Include the report, device
model, Android version, screenshots, APK version codes, and test date in the
capstone evidence. A final Phase 6B pass requires all nine QA checks to show
`PASS` on at least one representative physical phone.

Anonymous Supabase accounts are device-bound. Installing an update preserves the
account and local data, while uninstalling or clearing app data can remove them.
