-- Run after phase6c_forecast_validation.sql.

select
    has_table_privilege('anon', 'public.forecast_validation_details', 'select')
        as anon_can_read_validation,
    has_table_privilege('authenticated', 'public.forecast_validation_details', 'select')
        as authenticated_can_read_validation,
    has_table_privilege('service_role', 'public.forecast_validation_details', 'select')
        as service_can_read_validation;
-- Expected: false, true, true.

select c.relname, c.reloptions
from pg_class as c
where c.relnamespace = 'public'::regnamespace
  and c.relname = 'forecast_validation_details';
-- Expected: reloptions includes security_invoker=true.

select column_name, data_type
from information_schema.columns
where table_schema = 'public'
  and table_name = 'forecast_validation_details'
order by ordinal_position;
-- Expected: the report columns include absolute_error, squared_error,
-- baseline_absolute_error, baseline_squared_error, and has_baseline.
