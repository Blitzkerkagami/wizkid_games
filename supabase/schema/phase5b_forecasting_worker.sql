-- WizKid Phase 5B: idempotent writes for the forecasting worker.
-- Prerequisite: phase5a_forecasting_foundation.sql

begin;

create unique index if not exists learner_forecasts_source_version_uidx
    on public.learner_forecasts(
        owner_id,
        player_id,
        skill,
        model_version,
        source_attempt_count
    );

-- Reassert the least-privilege boundary for deployments upgraded from Phase 5A.
revoke all on table public.learner_forecasts from public;
revoke all on table public.learner_forecasts from anon;
revoke insert, update, delete on table public.learner_forecasts from authenticated;
grant select on table public.learner_forecasts to authenticated;
grant all on table public.learner_forecasts to service_role;

revoke all on table public.learner_skill_features from public;
revoke all on table public.learner_skill_features from anon;
grant select on table public.learner_skill_features to authenticated;
grant select on table public.learner_skill_features to service_role;

commit;

