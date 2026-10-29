-- WizKid Phase 6D: secure UAT and ISO/IEC 25010 evaluation responses.
-- Prerequisite: phase4_learning_sync.sql

begin;

create table if not exists public.evaluation_responses (
    response_id uuid primary key,
    owner_id uuid not null default auth.uid()
        references auth.users(id) on delete cascade,
    instrument text not null
        check (instrument in ('uat_tam', 'iso_25010')),
    respondent_role text not null
        check (respondent_role in ('learner', 'parent', 'teacher', 'it_evaluator')),
    ratings jsonb not null
        check (
            jsonb_typeof(ratings) = 'object'
            and jsonb_typeof(ratings -> 'items') = 'array'
            and jsonb_array_length(ratings -> 'items') between 1 and 40
        ),
    category_scores jsonb not null
        check (
            jsonb_typeof(category_scores) = 'object'
            and jsonb_typeof(category_scores -> 'items') = 'array'
            and jsonb_array_length(category_scores -> 'items') between 1 and 10
        ),
    overall_mean numeric(3, 2) not null check (overall_mean between 1 and 5),
    interpretation text not null
        check (interpretation in ('Excellent', 'Very Good', 'Good', 'Fair', 'Poor')),
    comments text not null default '' check (char_length(comments) <= 1000),
    completed_at_utc timestamptz not null,
    app_version text not null check (char_length(app_version) between 1 and 40),
    platform text not null check (char_length(platform) between 1 and 80),
    received_at timestamptz not null default now()
);

create index if not exists evaluation_responses_owner_time_idx
    on public.evaluation_responses(owner_id, completed_at_utc desc);
create index if not exists evaluation_responses_instrument_role_idx
    on public.evaluation_responses(instrument, respondent_role);

alter table public.evaluation_responses enable row level security;

revoke all on table public.evaluation_responses from public;
revoke all on table public.evaluation_responses from anon;
revoke update, delete on table public.evaluation_responses from authenticated;
grant select, insert on table public.evaluation_responses to authenticated;
grant all on table public.evaluation_responses to service_role;

drop policy if exists evaluation_responses_select_own
    on public.evaluation_responses;
create policy evaluation_responses_select_own
on public.evaluation_responses
for select
to authenticated
using ((select auth.uid()) = owner_id);

drop policy if exists evaluation_responses_insert_own
    on public.evaluation_responses;
create policy evaluation_responses_insert_own
on public.evaluation_responses
for insert
to authenticated
with check (
    (select auth.uid()) is not null
    and (select auth.uid()) = owner_id
);

-- Researchers can query this aggregate from the SQL Editor or a server-side
-- secret connection. It is deliberately unavailable to the Unity client.
create or replace view public.evaluation_category_summary
with (security_invoker = true)
as
select
    response.instrument,
    response.respondent_role,
    score.item ->> 'category' as category,
    count(*)::integer as response_count,
    round(avg((score.item ->> 'mean')::numeric), 2) as mean_score
from public.evaluation_responses as response
cross join lateral jsonb_array_elements(
    response.category_scores -> 'items'
) as score(item)
group by
    response.instrument,
    response.respondent_role,
    score.item ->> 'category';

revoke all on table public.evaluation_category_summary from public;
revoke all on table public.evaluation_category_summary from anon;
revoke all on table public.evaluation_category_summary from authenticated;
grant select on table public.evaluation_category_summary to service_role;

commit;
