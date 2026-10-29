-- Plain-Postgres feature and isolation smoke test.
-- The transaction is rolled back, leaving no test data.

begin;

insert into auth.users(id) values
    ('33333333-3333-3333-3333-333333333333'),
    ('44444444-4444-4444-4444-444444444444');

insert into public.learner_profiles (
    player_id, owner_id, display_name, age, wizard_index,
    created_at_utc, updated_at_utc, client_revision
) values (
    'aaaaaaaa-3333-3333-3333-aaaaaaaaaaaa',
    '33333333-3333-3333-3333-333333333333',
    'Forecast Test Learner', 7, 1, now(), now(), 1
);

insert into public.gameplay_sessions (
    session_id, player_id, owner_id, activity, skill, level,
    started_at_utc, ended_at_utc, total_questions, attempted_questions,
    correct_answers, score_percent, passed, lives_remaining,
    completion_status, installation_id, app_version, client_revision
) values
(
    'bbbbbbbb-3333-3333-3333-bbbbbbbbbbb1',
    'aaaaaaaa-3333-3333-3333-aaaaaaaaaaaa',
    '33333333-3333-3333-3333-333333333333',
    'Quiz', 'Counting', 1, now() - interval '2 days', now() - interval '2 days',
    8, 8, 4, 50, false, 0, 'failed',
    'cccccccc-3333-3333-3333-cccccccccccc', '0.1', 1
),
(
    'bbbbbbbb-3333-3333-3333-bbbbbbbbbbb2',
    'aaaaaaaa-3333-3333-3333-aaaaaaaaaaaa',
    '33333333-3333-3333-3333-333333333333',
    'Quiz', 'Counting', 2, now() - interval '1 day', now() - interval '1 day',
    7, 7, 7, 100, true, 3, 'completed',
    'cccccccc-3333-3333-3333-cccccccccccc', '0.1', 1
);

insert into public.question_attempts (
    attempt_id, session_id, player_id, owner_id, question_id,
    activity, skill, level, difficulty, competency, attempt_number,
    selected_answer_index, correct_answer_index, is_correct, timed_out,
    response_time_ms, attempted_at_utc, client_revision
)
select
    gen_random_uuid(),
    case when sample_number <= 8
         then 'bbbbbbbb-3333-3333-3333-bbbbbbbbbbb1'::uuid
         else 'bbbbbbbb-3333-3333-3333-bbbbbbbbbbb2'::uuid end,
    'aaaaaaaa-3333-3333-3333-aaaaaaaaaaaa'::uuid,
    '33333333-3333-3333-3333-333333333333'::uuid,
    'forecast-question-' || sample_number,
    'Quiz', 'Counting', case when sample_number <= 8 then 1 else 2 end,
    case when sample_number <= 8 then 1 else 2 end,
    'number-sense', sample_number, 0, 0,
    sample_number > 5, false,
    5000 - (sample_number * 100),
    now() - ((16 - sample_number) || ' hours')::interval,
    1
from generate_series(1, 15) as samples(sample_number);

set local role authenticated;
select set_config('request.jwt.claim.sub', '33333333-3333-3333-3333-333333333333', true);

do $$
declare
    feature_row record;
begin
    select * into feature_row
    from public.learner_skill_features
    where skill = 'Counting';

    if feature_row.total_attempts <> 15
       or feature_row.total_sessions <> 2
       or feature_row.is_forecast_eligible is not true
       or feature_row.data_quality_status <> 'ready'
       or feature_row.observed_trend <> 'improving' then
        raise exception 'Unexpected Phase 5A feature output: %', row_to_json(feature_row);
    end if;
end;
$$;

reset role;
insert into public.learner_forecasts (
    owner_id, player_id, skill, model_version, valid_until,
    source_attempt_count, expected_score_percent, mastery_probability,
    confidence, trend_label, risk_level, recommended_level,
    recommendation, feature_snapshot
) values (
    '33333333-3333-3333-3333-333333333333',
    'aaaaaaaa-3333-3333-3333-aaaaaaaaaaaa',
    'Counting', 'smoke-test-v1', now() + interval '7 days',
    15, 82.50, 0.8250, 0.7500, 'improving', 'low', 2,
    'Continue Counting Level 2.', '{"test": true}'::jsonb
);

set local role authenticated;
select set_config('request.jwt.claim.sub', '33333333-3333-3333-3333-333333333333', true);

do $$
begin
    if (select count(*) from public.current_learner_forecasts) <> 1 then
        raise exception 'Owner could not read the current forecast.';
    end if;
end;
$$;

select set_config('request.jwt.claim.sub', '44444444-4444-4444-4444-444444444444', true);

do $$
begin
    if (select count(*) from public.learner_skill_features) <> 0
       or (select count(*) from public.current_learner_forecasts) <> 0 then
        raise exception 'RLS exposed another owner''s forecasting data.';
    end if;
end;
$$;

rollback;
