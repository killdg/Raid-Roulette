# Stormtide night hurricanes

Night hurricane effects are enabled by default, with a 30% chance slider in RR settings.
One roll is made per raid on first reaching 20:00–06:00. A failed roll is not retried.
Effects stop outside those hours, when disabled, or on raid cleanup.

The in-memory debris/wind implementation is compiled into RR from the original killdg
Return Of Thunderstorms implementation; no additional mod or asset download is required.
The cosmetic meshes have no loot, damage or collision components. Bounded roof, floor
and wall probes reject sheltered positions and stop pieces at obstacles. Outdoor debris
continues while the viewer is inside, allowing a view through windows; collider gaps can
still require map-specific fixes. Extra native eye/goggle droplets apply only under rain.

Automated native compatibility checks passed. Live RR hurricane testing remains pending.

Stormtide sound sliders now default to 70% outdoor rain, 40% indoor rain,
25% outdoor gameplay and 60% indoor gameplay, matching Return Of Thunderstorms.
