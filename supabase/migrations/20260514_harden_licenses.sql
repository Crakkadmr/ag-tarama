-- Security hardening for public.licenses and public.licenses_view
-- Apply in Supabase SQL editor or migration pipeline.

BEGIN;

ALTER TABLE IF EXISTS public.licenses ENABLE ROW LEVEL SECURITY;

-- Keep table access minimal and explicit.
REVOKE INSERT, DELETE, TRUNCATE, REFERENCES, TRIGGER ON TABLE public.licenses FROM anon, authenticated;
GRANT SELECT, UPDATE ON TABLE public.licenses TO anon, authenticated;

-- Ensure machine_id uniqueness for one-license-per-device binding.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'licenses_machine_id_unique'
          AND conrelid = 'public.licenses'::regclass
    ) THEN
        ALTER TABLE public.licenses
            ADD CONSTRAINT licenses_machine_id_unique UNIQUE (machine_id);
    END IF;
END $$;

-- SELECT only when request header key matches row key.
DROP POLICY IF EXISTS license_validate ON public.licenses;
CREATE POLICY license_validate
ON public.licenses
FOR SELECT
TO anon, authenticated
USING (
    key = current_setting('request.headers', true)::json->>'x-license-key'
);

-- Allow first activation only for the matching key and when machine is not bound.
DROP POLICY IF EXISTS license_first_activation ON public.licenses;
CREATE POLICY license_first_activation
ON public.licenses
FOR UPDATE
TO anon, authenticated
USING (
    key = current_setting('request.headers', true)::json->>'x-license-key'
    AND machine_id IS NULL
)
WITH CHECK (
    key = current_setting('request.headers', true)::json->>'x-license-key'
    AND machine_id IS NOT NULL
);

-- Avoid view-based bypass; prefer invoker security where available.
DO $$
BEGIN
    IF to_regclass('public.licenses_view') IS NOT NULL THEN
        IF current_setting('server_version_num')::int >= 150000 THEN
            EXECUTE 'ALTER VIEW public.licenses_view SET (security_invoker = true)';
        END IF;
        EXECUTE 'REVOKE ALL ON public.licenses_view FROM anon, authenticated';
    END IF;
END $$;

COMMIT;
