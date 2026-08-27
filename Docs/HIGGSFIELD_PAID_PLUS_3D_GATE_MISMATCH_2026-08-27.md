# Higgsfield Paid Plus 3D Gate Mismatch — 2026-08-27

Status: `PAID_PLUS_CONFIRMED / SERVER_ROUTE_GATE_MISMATCH / ZERO_CHARGE`

## Confirmed account state

- Subscription plan: `plus` (paid)
- Credits before submission: `64`
- Credits after submission: `64`
- `free_trial.status=cancelled_by_user` is separate trial-lifecycle metadata. It does **not** mean the
  current subscription is a free-trial account.
- Unlimited generations: unavailable

## Reproduction

1. Open the paid Plus account's Higgsfield Supercomputer `Korean Father 3D` chat.
2. Request one Meshy `multi_image_to_3d` job with four image references, texture, rigging and
   animation enabled.
3. The final UI resolves the payload and displays `Approve 38`.
4. Approve exactly once.
5. After about 51 seconds, the gateway returns HTTP 403:
   `{"detail":{"error_type":"only_mcp_usage_on_trial_is_available"}}`
6. Response contains `job_ids: []`; no GLB is produced and the paid credit balance remains `64`.

## Correct interpretation

The error label must not be interpreted as evidence that this is a free-trial account. Direct
billing state says `plan=plus`. The observed failure is a server-side entitlement/routing mismatch:

- the paid web flow accepts and prices the 3D payload, then routes it into an MCP-only trial gate;
- the connected Higgsfield MCP surface in this Codex session exposes image/video/audio generation
  but no callable `generate_3d` tool.

The four reference images and all Meshy parameters were accepted before the gateway failure. This
is not an input-order, parameter-validation, insufficient-credit, or content-safety failure.

## Support-ready report

```text
My account is a paid Plus account with 64 credits. The Supercomputer successfully validates and
quotes 38 credits for a Meshy multi_image_to_3d request with texture, rigging and animation enabled.
After I approve the 38-credit card, the request fails before job creation with HTTP 403:
only_mcp_usage_on_trial_is_available, job_ids: []. My balance stays at 64. The free_trial field is
cancelled_by_user, but the active subscription_plan_type is plus. The connected MCP integration
does not expose generate_3d, only image/video/audio generation. Please correct the paid Plus 3D
entitlement/routing or expose generate_3d on the connected MCP surface.
```

Do not repeat the paid web submission until the routing/access state changes. No production asset
was created; `productionEligible=false`.

## Fresh authorized retry — 2026-08-27

- The user explicitly authorized one new retry after reconfirming the paid Plus account.
- Immediately before approval, the UI again showed `Approve 38` with Texture, Rigging and
  Animation enabled; the balance was `64`.
- `Approve 38` was clicked exactly once. No automatic retry, duplicate, branch, image, video or
  audio job was submitted.
- The gateway again returned HTTP 403 with
  `only_mcp_usage_on_trial_is_available` before job creation.
- Result: `job_ids: []`, no GLB URL, zero credits charged, final balance `64` and plan `plus`.
- This confirms that repeating the same validated payload does not bypass the server-side
  entitlement/routing mismatch. A further paid submission must not be attempted until Higgsfield
  changes or confirms the account's 3D access route.
