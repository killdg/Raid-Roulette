# Fence event activation

Enable Raid Roulette and **Trader Event Activation** in **F1 → Raid Roulette**, then reopen Fence's Tasks page. Accept one **[RR: Event Name]** offer. A newly accepted RR offer replaces the prior queued selection; these events do not stack.

Gold **[RR]** badges mark only the eligible PMC locations for the accepted assignment. Hover for its name. An ineligible PMC map runs Normal and keeps the pending assignment. Scav raids do not activate these selectors.

While Fence activation is on, it owns event selection. Turn it off to use the weighted random presets. Story campaigns remain a separate package.

Ordinary assignments finish after the assigned raid regardless of survival. Trials variants require their clear receipt; incomplete attempts fail. Both outcomes re-offer the same stable assignment as new and unaccepted. Client startup also resets stale RR selector state from interrupted sessions. This reset is restricted to these reward-free RR tickets; it does not reset story or standard quests.

Gun Game and **Gun Game [Respawns]** have separate offers. The latter starts another pistol run after defeat, at full health, while the raid timer continues.

## Engine and data scope

Native CustomQuestService registers definitions; native quest controllers manage statuses. SHA-256-derived stable IDs use a dedicated namespace. The existing local `/client/raidroulette/testing` route carries settings and map notices; Trials completion uses its scoped local receipt route. No external service or telemetry is used.

Client UI filters hide only RR offers when the setting is off. Native location-button components display cached badges rather than scanning the world. See the source under `Quests` and client `Fence*` files for lifecycle gates and cleanup.

Live replacement and artwork were user-confirmed. Repeated and interrupted lifecycle paths still require final regression checks; automated Fence checks do not prove every native UI flow.
