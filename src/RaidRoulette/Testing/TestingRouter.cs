using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace RaidRoulette.Testing
{
    [Injectable(TypePriority = OnLoadOrder.Routers + 1)]
    public sealed class TestingRouter(JsonUtil jsonUtil) : StaticRouter(
        jsonUtil,
        [new RouteAction<TestingRequest>(
            "/client/raidroulette/testing",
            (_, request, session, _, _) => new ValueTask<string>(System.Text.Json.JsonSerializer.Serialize(TestingControl
                .Update(request, session)))
        )]
    )
    {
    }
}
