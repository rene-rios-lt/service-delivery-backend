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
| POST | `/vehicles/{id}/release` | Release vehicle at end of day | ServiceRep |
| POST | `/vehicles/{id}/force-release` | Force-release a claimed vehicle | Dispatcher |
| POST | `/vehicles/{id}/position` | Push a position update | Simulator |

### Diagnostic Trouble Codes
| Method | Path | Description | Roles |
|--------|------|-------------|-------|
| GET | `/dtcs` | List all DTCs with human-readable titles | Requester, Dispatcher |

### Service Requests
| Method | Path | Description | Roles |
|--------|------|-------------|-------|
| POST | `/service-requests` | Submit a new service request | Requester |
| GET | `/service-requests` | List active requests | Dispatcher |
| GET | `/service-requests/{id}` | Get request details | Dispatcher, Requester (own) |
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

### Dispatcher Actions
| Method | Path | Description | Roles |
|--------|------|-------------|-------|
| GET | `/dispatcher/fleet` | Get all reps with state, position, active request | Dispatcher |
| POST | `/dispatcher/redirect` | Redirect an En Route rep to a different request | Dispatcher |

---

## SignalR Hubs

### `VehiclePositionHub` — `/hubs/position`
**Publishers:** Simulator  
**Subscribers:** Dispatchers (all), Requester (only for their assigned rep)

| Event (server → client) | Payload |
|--------------------------|---------|
| `VehiclePositionUpdated` | `{ repId, vehicleId, latitude, longitude, state }` |

The simulator connects and calls a server-side method to push position updates. The backend fans out to subscribed clients and runs business logic (15-mile check, ETA recalculation).

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
| `RedirectReceived` | `{ newRequestId, requesterName, requesterTier, dtcTitle, latitude, longitude }` | Dispatcher hard-redirected this rep |

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
