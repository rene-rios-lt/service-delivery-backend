# API Design

## Authentication

All endpoints require a valid JWT Bearer token. Tokens are issued by the `/auth/login` endpoint. The JWT payload includes:
- `sub` — user ID
- `role` — Dispatcher | ServiceRep | Requester | Simulator
- `tier` — Bronze | Silver | Gold | None
- `dealerId` — the dealer this user belongs to

All queries are automatically scoped to the authenticated user's `dealerId`.

---

## REST Endpoints

### Auth
| Method | Path | Description | Roles |
|--------|------|-------------|-------|
| POST | `/auth/login` | Exchange credentials for JWT | All |

### Users
| Method | Path | Description | Roles |
|--------|------|-------------|-------|
| GET | `/users/me` | Get current user profile | All |

### Vehicles
| Method | Path | Description | Roles |
|--------|------|-------------|-------|
| GET | `/vehicles` | List all vehicles with claim status | Dispatcher, ServiceRep |
| GET | `/vehicles/available` | List unclaimed vehicles | ServiceRep |
| POST | `/vehicles/{id}/claim` | Claim a vehicle for today's session | ServiceRep |
| POST | `/vehicles/{id}/take-over` | Human assumes control of an idle vehicle from the simulator | ServiceRep |
| POST | `/vehicles/{id}/release` | Release vehicle at end of day | ServiceRep |
| POST | `/vehicles/{id}/force-release` | Force-release a claimed vehicle | Dispatcher |
| POST | `/vehicles/{id}/position` | Push a position update (drives **all** vehicles, including human-controlled ones) | Simulator |

`POST /vehicles/{id}/take-over` lets a human operator log in as one of the seeded rep accounts (`rep1`…`rep8`) and assume control of a vehicle the simulator is currently operating.

**Preconditions** (both must hold, else `409`):
- The caller's rep is **idle** — `Available` with no active request (not `EnRoute`, `Within15Miles`, or `OnSite`)
- The target vehicle is **idle** — its claiming rep has no active job (not `EnRoute`, `Within15Miles`, or `OnSite`)

**Behavior:**
1. Release the prior (simulator-held) claim on the vehicle
2. End the prior rep session on that vehicle
3. Claim the vehicle for the caller and open a new rep session
4. Mark the caller's rep `human-controlled` (`RepState.humanControlled = true`)

**Responses:**
- `200 OK` — control transferred; body echoes the new claim and `humanControlled: true`
- `409 Conflict` — caller's rep is not idle, or the target vehicle is not idle

Vehicle position remains **simulator-pushed**, never backend-derived. After takeover the simulator keeps posting positions for that vehicle via `POST /vehicles/{id}/position`: it reads the human rep's job-state from the backend (see `GET /simulator/fleet-state`) and navigates the truck to the requester after the human Accepts, then **holds** at the requester until the human taps Arrived/Complete. The human's device never posts GPS.

### Diagnostic Trouble Codes
| Method | Path | Description | Roles |
|--------|------|-------------|-------|
| GET | `/dtcs` | List all DTCs with human-readable titles | Requester, Dispatcher |

### Service Requests
| Method | Path | Description | Roles |
|--------|------|-------------|-------|
| POST | `/service-requests` | Submit a new service request | Requester |
| GET | `/service-requests` | List active requests | Dispatcher |
| GET | `/service-requests/{id}` | Get request details (own-only: Dispatcher → any in their dealer, Requester → own, ServiceRep → assigned; out-of-scope/not-found → 404). Response: `requestId`, `requesterName`, `tier`, `dtcTitle`, nested `requesterLocation { lat, lng }`, `status`, `assignedRep` (null when unassigned), `createdAt`, `offerHistory[]` (ascending by `offeredAt`). | Dispatcher (any in dealer), Requester (own), ServiceRep (assigned) |
| GET | `/service-requests/my-active` | Get requester's current active request | Requester |

### Job Offers
| Method | Path | Description | Roles |
|--------|------|-------------|-------|
| GET | `/job-offers/pending` | Get current pending offer (if any) | ServiceRep |
| POST | `/job-offers/{id}/accept` | Accept a job offer | ServiceRep |
| POST | `/job-offers/{id}/decline` | Decline a job offer | ServiceRep |

### Rep Actions
| Method | Path | Description | Roles |
|--------|------|-------------|-------|
| POST | `/rep/arrive` | Mark "I've Arrived" — transitions to On Site | ServiceRep |
| POST | `/rep/complete` | Mark job complete — transitions to Available | ServiceRep |
| POST | `/rep/heartbeat` | Liveness ping from a human-controlled device (~15s interval) | ServiceRep |

`accept`, `decline`, `arrive`, and `complete` behave identically for simulator-operated and human-controlled reps. A `humanControlled` boolean travels on every rep/fleet-state payload (`GET /dispatcher/fleet`, `GET /simulator/fleet-state`, `RepStateChanged`) so clients and the simulator can tell which reps are under human control.

`POST /rep/heartbeat` is sent by a human-controlled device roughly every 15 seconds to prove liveness. If no heartbeat arrives within the timeout, the backend treats it as a logout: the rep goes `Offline`, its vehicle is parked (claim released), and `humanControlled` is cleared. Going off-duty explicitly (logout / `POST /vehicles/{id}/release`) has the same effect — the rep is marked `Offline` and `humanControlled` is cleared. Per the sticky rule (see business-rules.md) the simulator does **not** re-assume that rep or vehicle for the remainder of the run.

### Dispatcher Actions
| Method | Path | Description | Roles |
|--------|------|-------------|-------|
| GET | `/dispatcher/fleet` | Get all reps with state, position, active request | Dispatcher |
| POST | `/dispatcher/redirect` | Redirect an En Route rep to a different request | Dispatcher |

`POST /dispatcher/redirect` request body:
```json
{ "repId": "guid", "toRequestId": "guid" }
```
The `fromRequestId` is derived from the rep's current active request — the backend looks it up. Only the rep to redirect and the destination request are required.

A redirect applies **uniformly** to simulator-operated and human-controlled reps — same `EnRoute`-only, tier, and cooldown rules. On redirect the simulator re-navigates the truck to the new destination, and a human-controlled rep's device shows the redirect via the existing `RedirectReceived` event.

### Simulator
| Method | Path | Description | Roles |
|--------|------|-------------|-------|
| GET | `/simulator/fleet-state` | Read job-state for every vehicle so the simulator can drive positions | Simulator |

`GET /simulator/fleet-state` exists because **vehicle position is simulator-pushed, not backend-derived** — the simulator must drive positions for *all* trucks, including human-controlled ones. To navigate a truck correctly the simulator needs each vehicle's current job-state (where to head, when to hold). It returns one row per vehicle:

```json
[
  {
    "vehicleId": "guid",
    "claimingRepId": "guid",
    "repState": "Offline | Available | EnRoute | Within15Miles | OnSite",
    "humanControlled": true,
    "activeRequestLocation": { "lat": 0.0, "lng": 0.0 }
  }
]
```

`activeRequestLocation` is `null` when the rep has no active request. For a human-controlled rep the simulator reads this state and navigates to the requester after the human Accepts, then holds at the requester until the human taps Arrived/Complete. This is the single `Simulator`-role account, used only for reading fleet state and posting positions — it never makes job decisions. (Job decisions are made by the simulator logging in as the real `rep1`…`rep8` accounts; see domain-model.md.)

---

## SignalR Hubs

### `VehiclePositionHub` — `/hubs/position`
**Publishers:** Backend (receives position updates from Simulator via `POST /vehicles/{id}/position`, then fans out)
**Subscribers:** Dispatchers (all), Requester (only for their assigned rep)

| Event (server → client) | Payload |
|--------------------------|---------|
| `VehiclePositionUpdated` | `{ repId, vehicleId, latitude, longitude, state }` |

The simulator pushes positions via REST — it is not a SignalR publisher. The backend receives the REST call, runs business logic (15-mile threshold check, ETA recalculation), then broadcasts to subscribed clients over this hub.

---

### `DispatchHub` — `/hubs/dispatch`
**Publishers:** Backend  
**Subscribers:** Dispatchers

| Event (server → client) | Payload | Trigger |
|--------------------------|---------|---------|
| `ServiceRequestPending` | `{ requestId, requesterTier, dtcTitle, location }` | New request with no rep |
| `ServiceRequestAssigned` | `{ requestId, repId, repName, eta }` | Rep accepted offer |
| `ServiceRequestCompleted` | `{ requestId }` | Rep marked complete |
| `RepStateChanged` | `{ repId, oldState, newState }` | Any rep state transition |
| `RepOfflineMidJob` | `{ repId, requestId }` | Rep disconnected while active |
| `FleetPositionUpdate` | `{ repId, latitude, longitude, state }` | Forwarded from position hub |

---

### `RepHub` — `/hubs/rep`
**Publishers:** Backend  
**Subscribers:** Individual service rep (scoped to their connection)

| Event (server → client) | Payload | Trigger |
|--------------------------|---------|---------|
| `JobOfferReceived` | `{ offerId, requestId, requesterName, requesterTier, dtcTitle, latitude, longitude, distanceMiles, etaMinutes }` | New offer sent to rep |
| `JobOfferExpired` | `{ offerId }` | 60-second timeout reached |
| `RedirectReceived` | `{ newRequestId, requesterName, requesterTier, dtcTitle, latitude, longitude, distanceMiles, etaMinutes }` | Dispatcher hard-redirected this rep |

---

### `RequesterHub` — `/hubs/requester`
**Publishers:** Backend  
**Subscribers:** Individual requester (scoped to their connection)

| Event (server → client) | Payload | Trigger |
|--------------------------|---------|---------|
| `RepAssigned` | `{ repId, repName, etaMinutes, latitude, longitude }` | Rep accepted request |
| `RepPositionUpdated` | `{ latitude, longitude, etaMinutes, state }` | Position update for assigned rep |
| `RepRedirected` | `{ oldRepName, newRepName, newEtaMinutes }` | Sent after new rep accepts displaced job |
| `ServiceCompleted` | `{}` | Rep marked complete |
| `RepArrived` | `{ repId, requestId }` | Rep marked arrived on site |

---

## Backend-Owned Calculations on Position Update

Every time the simulator posts a position update, the backend:
1. Persists the new position
2. Broadcasts `VehiclePositionUpdated` to dispatcher clients
3. If the rep has an active request in `Assigned` state:
   a. Recalculates Haversine distance to the requester's location
   b. If distance < 15 miles and rep state is `EnRoute` → transitions to `Within15Miles`
   c. Recalculates ETA and broadcasts `RepPositionUpdated` to the requester
   d. Broadcasts updated `FleetPositionUpdate` to dispatchers
