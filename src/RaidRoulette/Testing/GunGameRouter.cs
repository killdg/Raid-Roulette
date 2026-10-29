using RaidRoulette.Runtime;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Utils;
namespace RaidRoulette.Testing;
[Injectable(TypePriority=OnLoadOrder.Routers+1)]
public sealed class GunGameRouter(JsonUtil json):StaticRouter(json,[new RouteAction<GunGameRequest>("/client/raidroulette/gungame",async (_,request,session,_,_)=>
{
 try{return json.Serialize(await GunGameRuntime.Handle(session,request))!;}
 catch(Exception e){return json.Serialize(new{ok=false,error=e.GetBaseException().Message})!;}
})]){}

