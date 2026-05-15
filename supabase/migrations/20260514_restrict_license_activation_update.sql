-- Restrict license activation updates to machine binding fields only.
-- Mitigates privilege escalation during first activation.

BEGIN;

REVOKE UPDATE ON TABLE public.licenses FROM anon, authenticated;
GRANT UPDATE (machine_id, activated_at) ON TABLE public.licenses TO anon, authenticated;

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
    AND activated_at IS NOT NULL
);

COMMIT;

