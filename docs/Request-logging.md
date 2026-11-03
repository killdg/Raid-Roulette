# Request descriptions

The server appends a short explanation to existing SPT request lines for this
mod's exact API routes. This changes display text inside
`SPTarkov.Server.Middleware.SptLoggerMiddleware.LogRequest` only. It does not
change request URLs, route matching, bodies, responses or request frequency.

Each mod owns its own descriptions and needs no additional mod dependency for
this feature. The hooks compose when the packages are installed together.
Native SPT routes, unknown routes and third-party routes remain unchanged.
SPT's existing `LogRequests` option controls whether these lines are printed.
No player or profile data is appended, and no extra network calls or log lines
are created. Restart SPT.Server after updating the server DLL.
