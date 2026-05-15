// Supabase Edge Function: validate-license
// This function is designed to be called by desktop client.
// It verifies HMAC, reads/writes licenses via service role, and returns a minimal response.

import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const SUPABASE_URL = Deno.env.get("SUPABASE_URL") ?? "";
const SUPABASE_SERVICE_ROLE_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY") ?? "";
const CLIENT_HMAC_SECRET = Deno.env.get("CLIENT_HMAC_SECRET") ?? "";

function json(status: number, body: Record<string, unknown>) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json; charset=utf-8" },
  });
}

function hex(bytes: Uint8Array) {
  return Array.from(bytes).map((b) => b.toString(16).padStart(2, "0")).join("");
}

async function hmacSha256(message: string, secret: string) {
  const key = await crypto.subtle.importKey(
    "raw",
    new TextEncoder().encode(secret),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );
  const sig = await crypto.subtle.sign("HMAC", key, new TextEncoder().encode(message));
  return hex(new Uint8Array(sig));
}

function isFresh(tsHeader: string | null) {
  if (!tsHeader) return false;
  const ts = Number(tsHeader);
  if (!Number.isFinite(ts)) return false;
  const now = Math.floor(Date.now() / 1000);
  return Math.abs(now - ts) <= 120;
}

Deno.serve(async (req) => {
  if (req.method !== "POST") return json(405, { status: "invalid", message: "method_not_allowed" });

  if (!SUPABASE_URL || !SUPABASE_SERVICE_ROLE_KEY || !CLIENT_HMAC_SECRET) {
    return json(500, { status: "invalid", message: "server_not_configured" });
  }

  const ts = req.headers.get("x-ts");
  const nonce = req.headers.get("x-nonce") ?? "";
  const signature = (req.headers.get("x-signature") ?? "").toLowerCase();
  if (!isFresh(ts) || !nonce || !signature) {
    return json(401, { status: "invalid", message: "bad_auth_headers" });
  }

  const body = await req.text();
  const expected = await hmacSha256(`${ts}.${nonce}.${body}`, CLIENT_HMAC_SECRET);
  if (expected !== signature) {
    return json(401, { status: "invalid", message: "bad_signature" });
  }

  let payload: { key?: string; machine_id?: string };
  try {
    payload = JSON.parse(body);
  } catch {
    return json(400, { status: "invalid", message: "bad_json" });
  }

  const key = (payload.key ?? "").trim();
  const machineId = (payload.machine_id ?? "").trim();
  if (!key || !machineId) return json(400, { status: "invalid", message: "missing_fields" });

  const sb = createClient(SUPABASE_URL, SUPABASE_SERVICE_ROLE_KEY);

  const { data: rows, error: readErr } = await sb
    .from("licenses")
    .select("key,type,is_active,machine_id,expires_at")
    .eq("key", key)
    .limit(1);

  if (readErr) return json(500, { status: "invalid", message: "db_read_failed" });
  if (!rows || rows.length === 0) return json(200, { status: "invalid", message: "license_not_found" });

  const row = rows[0];
  if (!row.is_active) return json(200, { status: "invalid", message: "license_disabled" });

  if (row.type === "subscription" && row.expires_at) {
    const expires = new Date(row.expires_at).getTime();
    if (Number.isFinite(expires) && expires < Date.now()) {
      return json(200, {
        status: "expired",
        message: "license_expired",
        type: row.type,
        expires_at: row.expires_at,
        server_time: new Date().toISOString(),
      });
    }
  }

  if (row.machine_id === null) {
    const { data: updated, error: updErr } = await sb
      .from("licenses")
      .update({ machine_id: machineId, activated_at: new Date().toISOString() })
      .eq("key", key)
      .is("machine_id", null)
      .select("key,machine_id")
      .limit(1);

    if (updErr) return json(500, { status: "invalid", message: "db_update_failed" });
    if (!updated || updated.length !== 1) {
      return json(200, { status: "machine_conflict", message: "already_bound" });
    }
  } else if ((row.machine_id ?? "").toLowerCase() !== machineId.toLowerCase()) {
    return json(200, { status: "machine_conflict", message: "bound_to_other_machine" });
  }

  return json(200, {
    status: "valid",
    message: "license_valid",
    type: row.type,
    machine_id: machineId,
    expires_at: row.expires_at,
    server_time: new Date().toISOString(),
  });
});
