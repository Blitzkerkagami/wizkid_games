-- Plain-Postgres Phase 6D smoke test. Leaves no test data.

begin;

insert into auth.users(id)
values ('77777777-7777-4777-8777-777777777777');

set local role authenticated;
select set_config(
    'request.jwt.claim.sub',
    '77777777-7777-4777-8777-777777777777',
    true
);

insert into public.evaluation_responses (
    response_id, instrument, respondent_role, ratings, category_scores,
    overall_mean, interpretation, comments, completed_at_utc,
    app_version, platform
) values (
    'aaaaaaaa-7777-4777-8777-aaaaaaaaaaaa',
    'uat_tam',
    'teacher',
    '{"items":[{"question_id":"UAT-UI-01","category":"Usability and Interface","rating":5}]}'::jsonb,
    '{"items":[{"category":"Usability and Interface","mean":5.0}]}'::jsonb,
    5.00,
    'Excellent',
    'Clear and child-friendly.',
    now(),
    '0.1',
    'Android'
);

do $$
declare
    saved_owner uuid;
    saved_count integer;
begin
    select owner_id into saved_owner
    from public.evaluation_responses
    where response_id = 'aaaaaaaa-7777-4777-8777-aaaaaaaaaaaa';

    select count(*) into saved_count
    from public.evaluation_responses
    where response_id = 'aaaaaaaa-7777-4777-8777-aaaaaaaaaaaa';

    if saved_owner <> '77777777-7777-4777-8777-777777777777'
       or saved_count <> 1 then
        raise exception 'Unexpected Phase 6D response owner/count: %, %',
            saved_owner, saved_count;
    end if;
end;
$$;

reset role;
rollback;
