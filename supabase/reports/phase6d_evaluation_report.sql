-- Run in the Supabase SQL Editor after collecting Phase 6D responses.
-- This is a read-only capstone summary; it does not change any responses.

select
    instrument,
    respondent_role,
    count(*)::integer as response_count,
    round(avg(overall_mean), 2) as overall_mean,
    case
        when avg(overall_mean) >= 4.21 then 'Excellent'
        when avg(overall_mean) >= 3.41 then 'Very Good'
        when avg(overall_mean) >= 2.61 then 'Good'
        when avg(overall_mean) >= 1.81 then 'Fair'
        else 'Poor'
    end as interpretation
from public.evaluation_responses
group by instrument, respondent_role
order by instrument, respondent_role;

select
    instrument,
    respondent_role,
    category,
    response_count,
    mean_score,
    case
        when mean_score >= 4.21 then 'Excellent'
        when mean_score >= 3.41 then 'Very Good'
        when mean_score >= 2.61 then 'Good'
        when mean_score >= 1.81 then 'Fair'
        else 'Poor'
    end as interpretation
from public.evaluation_category_summary
order by instrument, respondent_role, category;
