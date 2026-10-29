-- WizKid Phase 6C: leakage-safe forecast validation for the in-game report.
-- Prerequisites: phase4_learning_sync.sql and phase5d_arima_forecasting.sql

begin;

create or replace view public.forecast_validation_details
with (security_invoker = true)
as
select
    forecast.forecast_id,
    forecast.owner_id,
    forecast.player_id,
    forecast.skill,
    forecast.model_version,
    forecast.generated_at,
    actual.session_id as actual_session_id,
    actual.ended_at_utc as actual_at,
    forecast.expected_score_percent::numeric(5, 2) as predicted_score_percent,
    actual.score_percent::numeric(5, 2) as actual_score_percent,
    round(
        (actual.score_percent - forecast.expected_score_percent)::numeric,
        2
    ) as signed_error,
    round(
        abs(actual.score_percent - forecast.expected_score_percent)::numeric,
        2
    ) as absolute_error,
    round(
        power(actual.score_percent - forecast.expected_score_percent, 2)::numeric,
        2
    ) as squared_error,
    coalesce(previous.score_percent, 0)::numeric(5, 2) as baseline_score_percent,
    coalesce(
        round(abs(actual.score_percent - previous.score_percent)::numeric, 2),
        0
    ) as baseline_absolute_error,
    coalesce(
        round(power(actual.score_percent - previous.score_percent, 2)::numeric, 2),
        0
    ) as baseline_squared_error,
    (previous.session_id is not null) as has_baseline
from public.learner_forecasts as forecast
cross join lateral (
    select
        session.session_id,
        session.ended_at_utc,
        session.score_percent
    from public.gameplay_sessions as session
    where session.owner_id = forecast.owner_id
      and session.player_id = forecast.player_id
      and session.skill = forecast.skill
      and session.completion_status in ('completed', 'failed')
      and session.ended_at_utc is not null
      and session.score_percent is not null
      -- Only a later result can validate a forecast. This boundary prevents
      -- training observations from leaking into the reported actual score.
      and session.ended_at_utc > forecast.generated_at
    order by session.ended_at_utc asc, session.session_id asc
    limit 1
) as actual
left join lateral (
    select
        session.session_id,
        session.score_percent
    from public.gameplay_sessions as session
    where session.owner_id = forecast.owner_id
      and session.player_id = forecast.player_id
      and session.skill = forecast.skill
      and session.completion_status in ('completed', 'failed')
      and session.ended_at_utc is not null
      and session.score_percent is not null
      and session.ended_at_utc <= forecast.generated_at
    order by session.ended_at_utc desc, session.session_id desc
    limit 1
) as previous on true;

-- Explicit Data API grants are required by Supabase's 2026 table/view
-- exposure rules. RLS on both source tables still limits rows to auth.uid().
revoke all on table public.forecast_validation_details from public;
revoke all on table public.forecast_validation_details from anon;
grant select on table public.forecast_validation_details to authenticated;
grant select on table public.forecast_validation_details to service_role;

commit;
