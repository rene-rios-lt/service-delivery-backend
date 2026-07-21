# Business Rules

## Matching Algorithm

When a service request is submitted (or re-queued after a decline/redirect), the backend finds the best available rep using the following steps:

1. **Filter by dealer** — only consider reps belonging to the same dealer as the request
2. **Filter by equipment** — only consider reps whose current vehicle carries the equipment type required by the request's DTC
3. **Filter by state** — only consider reps in `Available` state. Automatic matching offers jobs only to free reps; reassigning an `EnRoute` rep to a higher-priority request is a dispatcher-only action via redirect (see Priority and Redirect Rules below), which enforces the tier, cooldown, and proximity protections
4. **Exclude skipped reps** — exclude any rep who previously **explicitly declined** this specific request; a rep whose offer merely expired remains eligible and may receive a new offer on the next matching run
5. **Sort by distance** — calculate Haversine distance from each rep's last known position to the requester's location; sort ascending
6. **Tiebreaker** — if two reps are equidistant, the rep who has been in `Available` state longest wins

The top result receives a job offer. If they decline or expire, repeat from step 4 with the next candidate.

If no candidates remain, the request stays in `Pending` and the dispatcher is notified.

These candidate rules are **unchanged** by Human Takeover. Simulator-operated reps are real `Available` reps backed by real rep sessions, so the simulator and a human are indistinguishable to the matcher: candidates are always reps with an active `RepSession`, in `Available` state, with an equipment match, in the same dealer, sorted by Haversine distance. A `human-controlled` rep is matched on exactly the same terms as a simulator-operated one.

---

## Priority and Redirect Rules

### Tier Hierarchy
`Bronze` < `Silver` < `Gold`

A higher-tier requester's request can trigger a dispatcher redirect of an `EnRoute` rep currently serving a lower-tier request.

### Redirect Eligibility
A rep can only be redirected if ALL of the following are true:
- Rep is in `EnRoute` state (not `Within15Miles`, `OnSite`, `Available`, or `Offline`)
- The incoming request's requester tier is higher than the displaced request's requester tier
- The 5-minute cooldown has elapsed since the rep was last redirected, **unless** the incoming request is Gold tier

### Cooldown Rule
- After a rep is redirected, a 5-minute cooldown begins (`RepState.lastRedirectedAt`)
- `Silver` and `Bronze` requests must respect the cooldown — cannot redirect a rep in cooldown
- `Gold` requests override the cooldown — can redirect a rep regardless of cooldown status
- The cooldown does **not** override `Within15Miles` or `OnSite` protection — those are absolute

### Proximity Protection (Absolute — No Exceptions)
| State | Redirect Allowed? |
|-------|------------------|
| Within 15 Miles | Never — not by any tier |
| On Site | Never — not by any tier |

The 15-mile threshold is calculated by the backend on every position update using the Haversine formula between the rep's current position and their active request's location.

### Redirect Outcome
1. Displaced request → `Pending`
2. Displaced rep is given a new destination (the higher-tier request)
3. Displaced rep's cooldown timer starts
4. System runs matching algorithm for the displaced request
5. Once a new rep accepts the displaced request, the displaced requester is notified: *"Our apologies, we needed to redirect [rep name]. [new rep name] is heading your way."* with updated ETA

Redirect applies **uniformly** to simulator-operated and human-controlled reps. The same `EnRoute`-only, tier, cooldown, and proximity protections apply regardless of who is operating the truck. On redirect, the simulator re-navigates the truck to the new destination; for a human-controlled rep, the redirect surfaces on the human's device (`RedirectReceived`), and the simulator drives the truck to follow it.

---

## Human Takeover Rules

A human operator can log in as one of the seeded rep accounts (`rep1`…`rep8`) and assume control of a vehicle the simulator is currently operating (via `POST /vehicles/{id}/take-over`).

### Takeover Eligibility
Takeover is allowed only when **both** of the following hold:
- The rep is **idle** — `Available` with no active job (not `EnRoute`, `Within15Miles`, or `OnSite`)
- The target vehicle is **idle** — its claiming rep has no active job (not `EnRoute`, `Within15Miles`, or `OnSite`)

On takeover the backend releases the prior simulator claim, ends the prior session, claims the vehicle for the human, opens a new session, and marks the rep `human-controlled`. The human's device then sends a ~15-second heartbeat.

### Sticky — No Re-assume
When a human-controlled rep goes off-duty — explicit logout/release, or heartbeat timeout — the rep transitions to `Offline`, the vehicle is parked (claim released), and `human-controlled` is cleared. The simulator does **not** re-assume that rep or vehicle for the remainder of the run; both stay out of the automated fleet until the run restarts.

### Abandoned Job Re-match
If a human-controlled rep goes off-duty while mid-job, the abandoned request returns to `Pending` and is **re-matched** to another available rep using the standard matching algorithm (this is the existing `Offline` mid-job behavior, applied to human-controlled reps as well).

---

## Vehicle Claim Rules

- First rep to select a vehicle locks it — enforced with optimistic concurrency or a database-level unique constraint on active sessions
- A rep can only have one active vehicle session at a time
- Vehicle stays claimed for the entire day until the rep explicitly releases it on logout
- If a rep's session ends unexpectedly (crash), the vehicle remains claimed until:
  - The rep logs back in and releases it, or
  - A dispatcher force-releases it
- Dispatcher can force-release any vehicle at any time via the dispatcher view

---

## Job Offer Rules

- One offer is active at a time per request — the next offer is sent only after the current one resolves
- Offer expires after **60 seconds** with no response — the rep is **not** permanently skipped; they become re-eligible for that specific request on the next matching run
- A rep who explicitly **declines** an offer is permanently skipped for that specific request (even if the request re-queues after all reps are exhausted)
- A rep cannot receive a new offer while they have an active pending offer

---

## Rep State Transition Rules

| Transition | Trigger | Owner |
|-----------|---------|-------|
| Offline → Available | Rep claims a vehicle | Rep action via API |
| Available → EnRoute | Rep accepts a job offer | System (on offer acceptance) |
| EnRoute → Within15Miles | Haversine distance to destination < 15 miles | Backend (on each position update) |
| Within15Miles → OnSite | Rep taps "I've Arrived" | Rep action via API |
| OnSite → Available | Rep taps "Mark Complete" | Rep action via API |
| Any → Offline (mid-job) | Unexpected session end | System detects disconnection |

When a rep transitions to `OnSite → Available` via Mark Complete, the associated `ServiceRequest` simultaneously transitions `InProgress → Completed`.

When a rep goes `Offline` mid-job, the associated request returns to `Pending` and the dispatcher is notified.

---

## ETA Calculation

```
distance_miles = haversine(rep_lat, rep_lng, requester_lat, requester_lng)
eta_hours      = distance_miles / 60.0    // assumed average speed: 60 mph
eta_minutes    = eta_hours * 60
```

ETA is recalculated by the backend on every position update and broadcast to the requester via SignalR. The assumed average speed is **60 mph**.

---

## No Rep Available / All Declined

When any of the following occur, the request returns to (or stays in) `Pending` and the dispatcher is notified:
- No qualified rep is found by the matching algorithm
- All qualified reps have explicitly declined this request (expired offers do not permanently block re-matching)

The system will automatically re-run the matching algorithm when a rep transitions to `Available` state (on job completion or vehicle claim), checking whether any pending requests now have a match.
