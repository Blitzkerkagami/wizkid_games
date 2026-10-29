-- Plain-Postgres Phase 6C smoke test. Leaves no test data.

begin;

insert into auth.users(id)
values ('66666666-6666-4666-8666-666666666666');

insert into public.learner_profiles (
    player_id, owner_id, display_name, age, wizard_index,
    created_at_utc, updated_at_utc, client_revision
) values (
    'aaaaaaaa-6666-4666-8666-aaaaaaaaaaaa',
    '66666666-6666-4666-8666-666666666666',
    'Validation Test Learner', 7, 1, now() - interval '10 days', now(), 1
);

insert into public.gameplay_sessions (
    session_id, player_id, owner_id, activity, skill, level,
    started_at_utc, ended_at_utc, total_questions, attempted_questions,
    correct_answers, score_percent, passed, lives_remaining,
    completion_status, installation_id, app_version, client_revision
) values
(
    'bbbbbbbb-6666-4666-8666-000000000001',
    'aaaaaaaa-6666-4666-8666-aaaaaaaaaaaa',
    '66666666-6666-4666-8666-666666666666',
    'Quiz', 'Counting', 1, now() - interval '3 days',
    now() - interval '3 days' + interval '5 minutes',
    5, 5, 4, 80, true, 2, 'completed',
    'cccccccc-6666-4666-8666-cccccccccccc', '0.1', 1
),
(
    'bbbbbbbb-6666-4666-8666-000000000002',
    'aaaaaaaa-6666-4666-8666-aaaaaaaaaaaa',
    '66666666-6666-4666-8666-666666666666',
    'Quiz', 'Counting', 2, now() - interval '1 day',
    now() - interval '1 day' + interval '5 minutes',
    5, 5, 5, 100, true, 3, 'completed',
    'cccccccc-6666-4666-8666-cccccccccccc', '0.1', 1
);

insert into public.learner_forecasts (
    forecast_id, owner_id, player_id, skill, model_version,
    generated_at, valid_until, source_attempt_count,
    expected_score_percent, mastery_probability, confidence,
    trend_label, risk_level, recommended_level, recommendation,
    feature_snapshot
) values (
    'dddddddd-6666-4666-8666-dddddddddddd',
    '66666666-6666-4666-8666-666666666666',
    'aaaaaaaa-6666-4666-8666-aaaaaaaaaaaa',
    'Counting', 'arima-session-v1', now() - interval '2 days',
    now() + interval '5 days', 10, 90, 0.8, 0.8,
    'improving', 'low', 2, 'Keep practicing!', '{}'::jsonb
);

do $$
declare
    validation record;
begin
    select * into validation
    from public.forecast_validation_details
    where forecast_id = 'dddddddd-6666-4666-8666-dddddddddddd';

    if validation.actual_score_percent <> 100
       or validation.predicted_score_percent <> 90
       or validation.absolute_error <> 10
       or validation.squared_error <> 100
       or validation.baseline_score_percent <> 80
       or validation.baseline_absolute_error <> 20
       or validation.has_baseline is not true then
        raise exception 'Unexpected Phase 6C output: %', row_to_json(validation);
    end if;
end;
$$;

rollback;
