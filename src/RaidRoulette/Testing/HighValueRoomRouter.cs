using RaidRoulette.Runtime;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace RaidRoulette.Testing
{
    [Injectable(TypePriority = OnLoadOrder.Routers + 1)]
    public sealed class HighValueRoomRouter(JsonUtil json) : StaticRouter(
        json,
        [new RouteAction<HighValueRoomRequest>(
            "/client/raidroulette/keyed-room-kits",
            (_, _, session, _, _) => new ValueTask<string>(json.Serialize(HighValueRoomRuntime.GetKits(session))!)
        )]
    )
    {
    }

    public sealed class HighValueRoomRequest : IRequestData
    {
    }
}
