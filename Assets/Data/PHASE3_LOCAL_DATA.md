# Phase 3: Offline Learning Data

WizKid now stores detailed gameplay data in a local SQLite database named
`wizkid_learning.db`. The database is created under Unity's
`Application.persistentDataPath`, not inside the Assets folder.

## What is saved

- `learner_profiles`: local player identity, display name, age, birthday text,
  and selected wizard.
- `gameplay_sessions`: activity, skill, level, start/end time, score, pass/fail,
  lives, and completion status.
- `question_attempts`: question ID, competency, difficulty, selected answer,
  correct answer, correct/wrong result, timeout flag, and response time.
- `level_results`: one final summary for each completed or failed level run.

The standard quizzes are tracked question by question. Alphabet Order is also
tracked: each manual **Done** check is an attempt, and a perfect A-to-Z order
finishes its session successfully.

All records include a `sync_status` value. New and changed rows use `pending`,
which lets a future Supabase sync service upload them without changing the
offline gameplay flow.

## How to verify in Unity

1. Wait for Unity to finish compiling scripts.
2. Use **WizKid > Phase 3 > Initialize and Validate Local Database**.
3. Confirm the Console prints `WizKid Phase 3 local database is ready`.
4. Play one quiz level and answer at least one question.
5. Run the validation menu again. The session and attempt counts should rise.
6. Use **WizKid > Phase 3 > Open Local Database Folder** to locate the file.

The database is an analytics/history store. Existing PlayerPrefs remain the
source for unlocked levels and best scores, so Phase 3 does not reset or alter
current player progress.
