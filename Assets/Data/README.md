# WizKid Question Banks

The quiz content is stored in `QuestionBanks` as one reusable asset per learning skill and level. The catalog currently includes six five-level activities: Counting, Shapes, Colors, Math, Patterns, and Object Matching.

Each question contains:

- a stable `questionId` for future attempt tracking and forecasting;
- a prompt and optional image;
- exactly four text or visual answer choices and one correct-answer index;
- a difficulty from 1 to 5;
- the competency measured by the question.

To edit content, select a question-bank asset in Unity's Project window. Keep existing IDs unchanged after learner attempts have been recorded, because those IDs will connect future SQLite and Supabase records to the correct question.

Use **WizKid > Phase 2 > Validate Question Banks** in the Unity menu after changing content. The validator checks missing fields, answer counts, invalid correct-answer indexes, and duplicate IDs.

Pattern questions use the existing sequence artwork where available. Object Matching shows one round animal and asks the learner to select the same animal from four square visual choices.
