using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace RaidRoulette.Quests
{
    [Injectable(TypePriority = OnLoadOrder.Routers + 1)]
    public sealed class FenceTrialRouter(JsonUtil json) : StaticRouter(
        json,
        [new RouteAction<FenceTrialRequest>(
            "/client/raidroulette/fence-trial",
            (_, request, session, _, _) =>
    {
        // Session authentication/addressing comes from native SPT. This
        // route only records this session's assigned event, with a nonce
        // binding completion to the generated raid. No rewards or profile
        // edits occur here; native end-raid processing owns the outcome.
        string? token = request.Action == "begin" ? FenceEvents.AttemptToken(session, request.EventId) : null;
        bool accepted = request.Action == "complete" && FenceEvents.ReportClear(session, request.EventId, request.Token);
        return new ValueTask<string>(json.Serialize(new { token, accepted })!);
    }
        )]
    )
    {
    }

    public sealed class FenceTrialRequest : IRequestData
    {
        public string Action { get; set; } = "";
        public string EventId { get; set; } = "";
        public string Token { get; set; } = "";
    }
}
