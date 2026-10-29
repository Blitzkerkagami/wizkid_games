# Phase 6D UAT and ISO 25010 Evaluation

## What is implemented

The Adventure Map now includes an **Evaluate Game** button. The evaluation uses
one question per screen so it remains readable on Android phones.

- Learner with adult assistance: 20-item UAT
- Parent: 20-item UAT
- Teacher: 20-item UAT
- IT evaluator: 28-item ISO/IEC 25010 evaluation
- Required five-point scale: Poor, Fair, Good, Very Good, Excellent
- Optional final comment
- Automatic overall mean and interpretation
- Offline-first SQLite storage and automatic Supabase synchronization

The scale interpretation follows the capstone instrument:

- 4.21–5.00: Excellent
- 3.41–4.20: Very Good
- 2.61–3.40: Good
- 1.81–2.60: Fair
- 1.00–1.80: Poor

## One-time Supabase setup

1. Open the Supabase SQL Editor.
2. Run `supabase/schema/phase6d_evaluation.sql`.
3. Run `supabase/tests/phase6d_evaluation_security_checks.sql`.
4. Confirm the expected privilege and RLS results in the test comments.

Unity uses only the publishable key and the anonymous authenticated session.
Each device can read only its own responses. The game cannot update or delete a
submitted response. Cross-device aggregate results are available only through
the Supabase SQL Editor or a server-side secret connection.

## Evaluation procedure

1. Let the participant test all relevant game functions first.
2. Open **Adventure Map > Evaluate Game**.
3. Select the correct respondent role.
4. Explain that no name, birthday, contact information, or password is collected.
5. For a learner, an adult reads each item aloud without suggesting an answer.
6. Complete every rating and optionally add a comment.
7. Keep the completion screen as evidence that the response was saved.
8. When online, wait until the header says responses are up to date.

Do not submit invented responses. Record the actual number and demographics of
participants in the final research documentation after data collection.

## Capstone result summary

After collecting responses, run
`supabase/reports/phase6d_evaluation_report.sql` in the Supabase SQL Editor. It
returns response counts, overall means, category means, and verbal
interpretations. Use those real values in the Results and Discussion chapter.

## Unity checks

- Run **WizKid > Phase 6D > Validate Evaluation Setup**.
- Test one complete UAT response and one complete ISO response.
- Test with airplane mode, restart the app, reconnect, and confirm the saved
  response synchronizes.
- Verify that long questions and the comments keyboard remain readable on the
  smallest supported Android screen.
