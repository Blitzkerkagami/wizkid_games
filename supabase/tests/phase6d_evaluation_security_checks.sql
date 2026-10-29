-- Run after phase6d_evaluation.sql.

select
    has_table_privilege('anon', 'public.evaluation_responses', 'select')
        as anon_can_read,
    has_table_privilege('anon', 'public.evaluation_responses', 'insert')
        as anon_can_insert,
    has_table_privilege('authenticated', 'public.evaluation_responses', 'select')
        as authenticated_can_read,
    has_table_privilege('authenticated', 'public.evaluation_responses', 'insert')
        as authenticated_can_insert,
    has_table_privilege('authenticated', 'public.evaluation_responses', 'update')
        as authenticated_can_update,
    has_table_privilege('service_role', 'public.evaluation_responses', 'select')
        as service_can_read;
-- Expected: false, false, true, true, false, true.

select
    relname,
    relrowsecurity,
    relforcerowsecurity
from pg_class
where oid = 'public.evaluation_responses'::regclass;
-- Expected: relrowsecurity=true.

select c.relname, c.reloptions
from pg_class as c
where c.relnamespace = 'public'::regnamespace
  and c.relname = 'evaluation_category_summary';
-- Expected: reloptions includes security_invoker=true.

select
    has_table_privilege('authenticated', 'public.evaluation_category_summary', 'select')
        as game_client_can_read_aggregate,
    has_table_privilege('service_role', 'public.evaluation_category_summary', 'select')
        as researcher_service_can_read_aggregate;
-- Expected: false, true.
