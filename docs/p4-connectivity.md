# P4 resident circulation contract

P4 treats the finished settlement as a graph, not as a rectangle with labels.
`PlannedNavigationGrid` is constructed from the immutable terrain snapshot, the
owned excavation footprint, all planned walls/doors, room walls, and blocking
furniture. It uses cardinal movement only. Natural rock outside the explicit
excavation ledger, water, occupied terrain, walls, and blocking placements are
blocked. Conduits and stock markers are not made blocking by default.

The graph has one resident-public root: the inside endpoint of the primary gate.
One flood fill of the final protected public mask must reach every operational gate
inside endpoint. A room cannot be used as public transit: each roofed room has a
separate local flood from its permitted door, and every declared interaction or
blocking-placement service target must be reached directly or by an adjacent legal
job-access cell. This prevents a disconnected logistics island, multi-source gate
union, center-cell, or bounding-box shortcut from passing validation.

Wall thickness is an explicit `PhysicalPlanningInput.WallLayers` value in the range
1–3. A planner may not claim a second or third ring unless it reserves a complete
gatehouse crossing—outside staging, one legal door per barrier, intervening
vestibules, and an inner endpoint in the same public component—and the final graph
passes. The earlier inset-ring experiment is deliberately rejected: the regression
creates that ring with no portals and the public root is blocked.

Approach routing is separate: edge-to-gate routes remain terrain-only exterior
assertions. They cannot satisfy internal connectivity by leaving the protected
circulation mask and re-entering elsewhere. Runtime validation additionally uses
resident `TraverseParms`/job semantics and must exercise gate-to-room service paths.
