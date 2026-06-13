# Domain Model

## Entities

### User
```
id            : Guid
name          : string
email         : string
passwordHash  : string
role          : Dispatcher | ServiceRep | Requester | Simulator
tier          : Bronze | Silver | Gold | None   (Requester only; None for other roles)
dealerId      : Guid
```

### Vehicle
```
id              : Guid
dealerId        : Guid
claimedByRepId  : Guid?   (null = unclaimed)
claimedAt       : DateTime?
```

### VehicleEquipment
```
vehicleId       : Guid
equipmentType   : EquipmentType (enum, 10 values)
```
One row per equipment type a vehicle carries. A vehicle carries 6 of the 10 equipment types.

### DiagnosticTroubleCode (DTC)
```
id                    : Guid
code                  : string   (e.g. "DTC-001")
humanReadableTitle    : string   (e.g. "Hydraulic system fault")
requiredEquipmentType : EquipmentType
```

### ServiceRequest
```
id              : Guid
dealerId        : Guid
requesterId     : Guid   (FK → User)
dtcId           : Guid   (FK → DiagnosticTroubleCode)
latitude        : double
longitude       : double
status          : Pending | Assigned | InProgress | Completed
assignedRepId   : Guid?  (null until a rep accepts)
createdAt       : DateTime
```

### RepSession
```
id          : Guid
repId       : Guid      (FK → User, role = ServiceRep)
vehicleId   : Guid      (FK → Vehicle)
startedAt   : DateTime
endedAt     : DateTime? (null = session active)
```
Tracks which rep claimed which vehicle for the day. One active session per rep at a time. A take-over ends the prior session on the vehicle and opens a new one for the human operator (see `RepState.humanControlled`); the session itself records the claim regardless of whether the rep is simulator-operated or human-controlled.

### RepState
```
repId           : Guid         (FK → User, role = ServiceRep)
state           : Offline | Available | EnRoute | Within15Miles | OnSite
activeRequestId : Guid?        (null when Offline or Available)
lastRedirectedAt: DateTime?    (used for 5-minute cooldown calculation)
humanControlled : bool         (true when a human has taken over this rep/vehicle; false when simulator-operated)
lastHeartbeatAt : DateTime?    (last liveness ping from a human-controlled device; null when simulator-operated)
updatedAt       : DateTime
```
`humanControlled` is set on a successful take-over and cleared when the rep goes off-duty (logout/release or heartbeat timeout). `lastHeartbeatAt` is refreshed on every `POST /rep/heartbeat`; a stale value (no ping within the timeout) drives the rep `Offline`.

### JobOffer
```
id               : Guid
serviceRequestId : Guid      (FK → ServiceRequest)
repId            : Guid      (FK → User, role = ServiceRep)
offeredAt        : DateTime
expiresAt        : DateTime  (offeredAt + 60 seconds)
status           : Pending | Accepted | Declined | Expired
```

---

## Entity Relationships (ERD)

```
User (Requester) ──────────────► ServiceRequest ◄─── DiagnosticTroubleCode
                                       │                       │
                                       │               requiredEquipmentType
                                       │                       │
User (ServiceRep) ◄── RepState        │               VehicleEquipment ◄── Vehicle
       │                              │                                        │
       └──── RepSession ─────────────►│                                        │
                   └─── Vehicle ──────┘                                        │
                                                                               │
JobOffer ──────────► ServiceRequest                                            │
    └────────────► User (ServiceRep)                                           │
                                                                        claimedByRepId
                                                                               │
                                                                        User (ServiceRep)
```

All entities scoped to a dealer carry `dealerId`. Queries always filter by the authenticated user's `dealerId`.

---

## Seed Data Specification

### EquipmentType Enum (10 values)
| Value | Label |
|-------|-------|
| HydraulicTool | Hydraulic Tool |
| ElectricalDiagnosticKit | Electrical Diagnostic Kit |
| TransmissionKit | Transmission Kit |
| BrakingSystemKit | Braking System Kit |
| CoolingSystemKit | Cooling System Kit |
| FuelSystemKit | Fuel System Kit |
| ExhaustSystemKit | Exhaust System Kit |
| SuspensionKit | Suspension Kit |
| SteeringKit | Steering Kit |
| PowertrainKit | Powertrain Kit |

### DTCs (10)
| Code | Title | Required Equipment |
|------|-------|-------------------|
| DTC-001 | Hydraulic system fault | HydraulicTool |
| DTC-002 | Electrical system fault | ElectricalDiagnosticKit |
| DTC-003 | Transmission fault | TransmissionKit |
| DTC-004 | Braking system fault | BrakingSystemKit |
| DTC-005 | Cooling system overheating | CoolingSystemKit |
| DTC-006 | Fuel system fault | FuelSystemKit |
| DTC-007 | Exhaust system fault | ExhaustSystemKit |
| DTC-008 | Suspension fault | SuspensionKit |
| DTC-009 | Steering system fault | SteeringKit |
| DTC-010 | Powertrain fault | PowertrainKit |

### DTC Coverage Distribution
**Common DTCs** (6–8 vehicles carry the required equipment):
- DTC-001 (Hydraulic) — 7 vehicles
- DTC-002 (Electrical) — 7 vehicles
- DTC-004 (Braking) — 6 vehicles
- DTC-005 (Cooling) — 6 vehicles

**Specialized DTCs** (2–3 vehicles carry the required equipment):
- DTC-003 (Transmission) — 3 vehicles
- DTC-006 (Fuel) — 3 vehicles
- DTC-007 (Exhaust) — 2 vehicles
- DTC-008 (Suspension) — 2 vehicles
- DTC-009 (Steering) — 2 vehicles
- DTC-010 (Powertrain) — 3 vehicles

Every DTC is covered by at least 2 vehicles. No DTC is unserviceable.

### Vehicles (8)
Each vehicle carries exactly 6 of the 10 equipment types. The combination is designed so common DTCs have broad coverage and specialized DTCs have limited but guaranteed coverage.

| Vehicle | Equipment Types |
|---------|----------------|
| V-001 | Hydraulic, Electrical, Braking, Cooling, Transmission, Fuel |
| V-002 | Hydraulic, Electrical, Braking, Cooling, Powertrain, Exhaust |
| V-003 | Hydraulic, Electrical, Braking, Cooling, Suspension, Steering |
| V-004 | Hydraulic, Electrical, Braking, Cooling, Transmission, Powertrain |
| V-005 | Hydraulic, Electrical, Cooling, Fuel, Transmission, Steering |
| V-006 | Hydraulic, Electrical, Braking, Exhaust, Suspension, Powertrain |
| V-007 | Hydraulic, Electrical, Cooling, Fuel, Suspension, Steering |
| V-008 | Braking, Cooling, Fuel, Exhaust, Suspension, Steering |

### Users
**Dispatchers (2)**
| Name | Email | Role |
|------|-------|------|
| Alex Dispatcher | alex@dealer.com | Dispatcher |
| Jordan Dispatcher | jordan@dealer.com | Dispatcher |

**Service Reps (8)**
| Name | Email | Role |
|------|-------|------|
| Rep One | rep1@dealer.com | ServiceRep |
| Rep Two | rep2@dealer.com | ServiceRep |
| Rep Three | rep3@dealer.com | ServiceRep |
| Rep Four | rep4@dealer.com | ServiceRep |
| Rep Five | rep5@dealer.com | ServiceRep |
| Rep Six | rep6@dealer.com | ServiceRep |
| Rep Seven | rep7@dealer.com | ServiceRep |
| Rep Eight | rep8@dealer.com | ServiceRep |

Each `rep1`…`rep8` account is operated **either by the simulator or by a human** — never both at once. By default the simulator logs in as each rep account, claims a vehicle, connects `RepHub`, and auto-responds to offers. A human can take over any one of these accounts on an idle rep/idle vehicle via `POST /vehicles/{id}/take-over`, at which point that rep is marked `human-controlled` and the simulator stops operating it (see business-rules.md, Human Takeover Rules).

**Requesters (10 — 6 Bronze, 3 Silver, 1 Gold)**
| Name | Email | Tier |
|------|-------|------|
| Bronze User 1 | bronze1@example.com | Bronze |
| Bronze User 2 | bronze2@example.com | Bronze |
| Bronze User 3 | bronze3@example.com | Bronze |
| Bronze User 4 | bronze4@example.com | Bronze |
| Bronze User 5 | bronze5@example.com | Bronze |
| Bronze User 6 | bronze6@example.com | Bronze |
| Silver User 1 | silver1@example.com | Silver |
| Silver User 2 | silver2@example.com | Silver |
| Silver User 3 | silver3@example.com | Silver |
| Gold User 1 | gold1@example.com | Gold |

**Simulator Service Account (1)**
| Name | Email | Role |
|------|-------|------|
| Simulator | simulator@system.internal | Simulator |

There is exactly **one** `Simulator`-role account, and its only job is positions: it posts vehicle positions for **all** vehicles via `POST /vehicles/{id}/position` and reads fleet job-state via `GET /simulator/fleet-state` to drive those positions (including for human-controlled trucks). It makes **no** job decisions. Job decisions (claim, accept/decline, arrive, complete) are made by the simulator process additionally logging in as the real `rep1`…`rep8` accounts — those are ordinary `ServiceRep` accounts, not the `Simulator` account. See central repo ADR-0009 (Human Takeover) for the rationale behind this split.

### Default Passwords

All seeded users share the default password **`Password123!`** for the POC. Use this value for `SimulatorPassword` in `appsettings.Local.json`.
