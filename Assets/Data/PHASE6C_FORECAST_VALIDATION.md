# Phase 6C — Forecast Validation Report

## One-time Supabase setup

1. Open the Supabase **SQL Editor**.
2. Run `supabase/schema/phase6c_forecast_validation.sql`.
3. Run `supabase/tests/phase6c_forecast_validation_security_checks.sql`.
4. Confirm the privilege result is `false, true, true` and the view has
   `security_invoker=true`.

The validation view pairs each forecast with only the first completed or failed
scored session whose end time is later than the forecast generation time. This
prevents a training score from being reused as the forecast's actual result.

## Create the first real sample

1. Make sure a forecast already appears in **Learning Forecast**.
2. After that forecast was generated, complete another scored activity for the
   same skill.
3. Let cloud sync finish.
4. Run the protected forecasting job or wait for the scheduled job.
5. In the game, open **Adventure Map > Learning Forecast** and select
   **View Forecast Accuracy**.

If there is no later scored session, the game correctly shows
**No validated forecast samples yet**. It never creates sample data.

## What the report shows

- **Predictions Checked** — number of forecasts with a later real score.
- **Average Score Difference** — MAE in score points.
- **Forecast Accuracy** — child-friendly display calculated as `100 - MAE`,
  clamped to 0–100%.
- **Learning Journey** — Improving, Steady, or Keep Practicing.
- **Prediction Journey** — up to six recent forecast-vs-actual score pairs.
- **How It Is Measured** — MAE, RMSE, sample count, ARIMA model version, and
  comparison with the previous-score baseline.

The report is saved in SQLite schema version 5 for offline viewing. There is no
CSV, JSON, or other report export; capstone evidence should use screenshots of
the in-game scene.

## Unity checks

- Use **WizKid > Phase 6C > Validate Forecast Report Setup**.
- Use **WizKid > Phase 6C > Open Forecast Report Scene** for visual inspection.
- Test once online, then enable airplane mode, reopen the scene, and confirm the
  latest saved report still appears.
