using BepInEx.Configuration;
using RaidRoulette.TestingClient;
var folder=Path.Combine(Path.GetTempPath(),"rr-menu-tests-"+Guid.NewGuid());Directory.CreateDirectory(folder);
int checks=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
try
{
    var migrationFolder=Path.Combine(folder,"migration");Directory.CreateDirectory(migrationFolder);
    var previousConfig=Path.Combine(migrationFolder,"com.previous.raidroulette.testing.cfg");
    File.WriteAllText(previousConfig,"[2. Raid chances]\nAny event chance (%) = 17\n");
    var previousBytes=File.ReadAllBytes(previousConfig);
    var migratedConfig=new ConfigFile(Path.Combine(migrationFolder,"com.killdg.raidroulette.testing.cfg"),false);
    Check(ConfigurationImport.Import(migratedConfig)&&migratedConfig.Bind("2. Raid chances","Any event chance (%)",40f).Value==17,"Publisher-ID migration imports existing F12 chance settings");
    Check(File.ReadAllBytes(previousConfig).SequenceEqual(previousBytes),"Preference import keeps the original file unchanged");
    migratedConfig.Bind("2. Raid chances","Any event chance (%)",40f).Value=21;migratedConfig.Save();
    Check(!ConfigurationImport.Import(migratedConfig)&&migratedConfig.Bind("2. Raid chances","Any event chance (%)",40f).Value==21,"Existing new-publisher settings are never overwritten");
    var ambiguousFolder=Path.Combine(folder,"ambiguous");Directory.CreateDirectory(ambiguousFolder);
    File.WriteAllText(Path.Combine(ambiguousFolder,"com.one.raidroulette.testing.cfg"),"[Menu]\nValue = 1\n");
    File.WriteAllText(Path.Combine(ambiguousFolder,"com.two.raidroulette.testing.cfg"),"[Menu]\nValue = 2\n");
    var ambiguousConfig=new ConfigFile(Path.Combine(ambiguousFolder,"com.killdg.raidroulette.testing.cfg"),false);
    Check(!ConfigurationImport.Import(ambiguousConfig),"Ambiguous preference files are not imported arbitrarily");
    var freshConfig=new ConfigFile(Path.Combine(folder,"fresh","com.killdg.raidroulette.testing.cfg"),false);
    Check(!ConfigurationImport.Import(freshConfig),"Fresh installations without legacy preferences keep defaults");
    var path=Path.Combine(folder,"test.cfg");
    var config=new ConfigFile(path,false);int calls=0;
    var action=MenuAction.Bind(config,"menu","apply","test",()=>calls++);
    Check(!action.Value&&calls==0,"No request during binding");
    action.Value=true;Check(calls==1&&!action.Value,"Checkbox sends once and resets");
    action.Value=true;Check(calls==2&&!action.Value,"Repeated checkbox clicks work");
    action.Value=false;Check(calls==2,"Reset does not send another request");
    config.Bind("menu","stale",true).Value=true;
    int staleCalls=0;var stale=MenuAction.Bind(config,"menu","stale","test",()=>staleCalls++);
    Check(!stale.Value&&staleCalls==0,"Previously saved Enabled flag cleared without sending");
    int draws=0;var tag=new ConfigurationManagerAttributes{CustomDrawer=_=>draws++,ReadOnly=true,HideDefaultButton=true};
    Check(typeof(ConfigurationManagerAttributes).GetField("CustomDrawer")?.FieldType==typeof(Action<ConfigEntryBase>),"Configuration Manager can discover CustomDrawer field");
    Check(typeof(ConfigurationManagerAttributes).GetField("ReadOnly")?.FieldType==typeof(bool?),"ReadOnly metadata uses nullable field");
    var custom=MenuAction.Bind(config,"menu","custom","test",()=>{},tag);
    var found=custom.Description.Tags.Single();((Action<ConfigEntryBase>)found.GetType().GetField("CustomDrawer")!.GetValue(found)!)(custom);
    Check(draws==1,"Manager-style field lookup invokes drawer");
    config.Save();Check(File.ReadAllText(path).Contains("apply = false"),"Action persists as false");
    var auto=new AutoApplyTracker();
    Check(!auto.Observe(0)&&!auto.Ready(1,false),"Startup does not silently force a stored event");
    var selection=config.Bind("menu","event","random");
    selection.SettingChanged+=(_,_)=>auto.Changed();
    selection.Value="cultist_invasion";
    Check(auto.Observe(1)&&!auto.Ready(1,false),"Dropdown edit queues automatic apply");
    Check(auto.Ready(1.5f,false)&&!auto.Ready(1.5f,true),"Automatic apply waits for debounce and in-flight request");
    auto.Dispatched(auto.Revision);
    Check(!auto.Ready(2,false),"Automatic apply sends a revision only once");
    selection.Value="get_handy";auto.Observe(3);selection.Value="marksman";auto.Observe(3.1f);
    Check(!auto.Ready(3.2f,false)&&auto.Ready(3.5f,false),"Rapid edits coalesce into the latest selection");
    Check(EventSelection.EventId(true,"Cultist_Invasion")=="cultist_invasion", "Force ON sends selected event ID");
    Check(EventSelection.EventId(false,"Cultist_Invasion")=="random", "Force OFF restores weighted random selection");
    Check(EventSelection.EventId(true,"Fake_Cult")=="fake_cult", "Clothing event and real cultist event have different IDs");
    Check(EventSelection.Matches("cultist_invasion","Queued: cultist_invasion (repeat)."), "Accept exact server selection acknowledgement");
    Check(!EventSelection.Matches("cultist_invasion","Using config.json selection."), "Do not accept config-file mode as confirmed cultist selection");
    Check(!EventSelection.Matches("cultist_invasion","Queued: fake_cult (repeat)."), "Do not accept clothing event as confirmed real cultist event");
    Check(EventSelection.Matches("random","Queued: Random (repeat)."), "Random acknowledgement comparison ignores display case");
    var rosterRows=new[]{new RosterEntry("aaa","Danny","pmcUSEC","Usec",42,true,500),new RosterEntry("bbb","Danny","pmcBEAR","Bear",42,false,10),new RosterEntry("ccc","Loading","Unknown","?",0,null,null)};
    Check(RosterView.Summary(rosterRows).Contains("Registered: 3 | Alive: 1 | Dead: 1 | Loading: 1"),"Roster separates registered actors, living AI, dead actors and pending health");
    Check(RosterView.Matches(rosterRows[0],"DANNY",true)&&!RosterView.Matches(rosterRows[1],"Danny",true),"Roster filter is case-insensitive and alive-only excludes dead clones");
    Check(RosterView.Matches(rosterRows[1],"bbb",false)&&RosterView.Matches(rosterRows[0],"pmcUSEC",false),"Identically named clones remain filterable by profile ID or role");
    Check(RosterView.Matches(rosterRows[0],"",false),"Distant AI are not excluded from roster");
    Check(RosterView.Summary(Array.Empty<RosterEntry>()).Contains("Registered: 0"),"Empty raid does not retain an old roster count");
    var many=Enumerable.Range(0,27).Select(i=>new RosterEntry(i.ToString(),"AI "+i,"assault","Savage",1,true,5000)).ToArray();
    Check(RosterView.Page(many,"",false,1,10).Rows.Length==10 && RosterView.Page(many,"",false,3,10).Rows.Length==7,"Roster paginates all AI including the final partial page");
    Check(Enumerable.Range(1,3).SelectMany(n=>RosterView.Page(many,"",false,n,10).Rows).Select(e=>e.Id).Distinct().Count()==27,"Paging covers every registered AI without dropping distant actors");
    Check(RosterView.Page(many,"does not exist",false,999,10).Number==1 && RosterView.Page(many,"",false,-9,10).Number==1,"Empty and out-of-range pages are clamped safely");
    int menuRefreshes=0;var display=new RosterSettings(config,()=>menuRefreshes++);
    var detailTags=config.Where(pair=>pair.Key.Section=="9. Advanced / Live raid information"&&pair.Value!=display.Expanded).SelectMany(pair=>pair.Value.Description.Tags).OfType<ConfigurationManagerAttributes>().ToArray();
    Check(!display.Expanded.Value&&detailTags.Length>20&&detailTags.All(t=>t.Browsable==false),"Live raid details start collapsed while the separate expand control remains available");
    display.Expanded.Value=true;Check(menuRefreshes==1&&detailTags.All(t=>t.Browsable==true),"Expanding reveals all roster controls and requests one settings-list refresh");
    display.Expanded.Value=false;Check(menuRefreshes==2&&detailTags.All(t=>t.Browsable==false),"Collapsing hides all roster rows again");
    Check(typeof(ConfigurationManagerAttributes).Name=="ConfigurationManagerAttributes"&&display.Overview.Description.Tags.OfType<ConfigurationManagerAttributes>().All(t=>t.ReadOnly==true&&t.HideDefaultButton==true),"Roster metadata uses the installed manager's required tag name and hides read-only reset buttons");
    display.Page.Value=3;
    string diskBefore=File.ReadAllText(path);display.Publish(many,"factory4_day: overrun","Wave 2","",true);
    Check(display.Overview.Value.Contains("Registered: 27")&&display.Identity[0].Value.Contains("AI 20"),"Ordinary F12 config fields receive current counts and roster rows");
    Check(display.Identity[7].Value=="—"&&display.State[7].Value=="—","Unused page slots are cleared rather than showing previous AI");
    Check(File.ReadAllText(path)==diskBefore && config.SaveOnConfigSet,"Display refresh does not write to disk and restores autosave");
    Check(display.Overview.Description.Tags.All(t=>!(t is ConfigurationManagerAttributes attrs)||attrs.CustomDrawer==null),"Roster display does not depend on a custom drawer");
    display.Publish(Array.Empty<RosterEntry>(),"","","",false);
    Check(display.Overview.Value=="No active raid"&&display.Identity.All(e=>e.Value=="—"),"Leaving a raid clears all visible roster rows");
    Check(GiftActivationPolicy.Allow(true,true,true,true),"Unarmed Gift can pass pre-activation after its hands controller is ready");
    Check(!GiftActivationPolicy.Allow(false,true,true,true),"Readiness override excludes every non-Gift actor");
    Check(!GiftActivationPolicy.Allow(true,false,true,true),"Readiness override does not fake an active actor's weapon readiness");
    Check(!GiftActivationPolicy.Allow(true,true,false,true)&&!GiftActivationPolicy.Allow(true,true,true,false),"Dead or incompletely initialized runners cannot bypass readiness");
    Check(GiftRoamPolicy.Score(100,100)>GiftRoamPolicy.Score(30,100),"Roaming prefers a farther equally fresh map destination");
    Check(GiftRoamPolicy.Score(100,100)>GiftRoamPolicy.Score(120,0),"Roaming favors an unvisited region over returning to a recent destination");
    Check(float.IsNegativeInfinity(GiftRoamPolicy.Score(10,200)),"Roaming rejects nearby anchors");
    Check(!GiftRoamPolicy.NeedsNewDestination(true,100,5),"An unfinished progressing route stays stable");
    Check(GiftRoamPolicy.NeedsNewDestination(true,2,0)&&GiftRoamPolicy.NeedsNewDestination(true,100,20)&&GiftRoamPolicy.NeedsNewDestination(false,100,0),"Arrival, missing destination and stalled progress each select a new route");
    Check(StartupFillPolicy.CanSubmit(3,0,0,15,20,5),"Startup fill adds only a shortfall after the initial spawn grace period");
    Check(!StartupFillPolicy.CanSubmit(3,12,12,15,20,15)&&!StartupFillPolicy.CanSubmit(15,0,0,15,20,15),"Live and pending AI both reserve startup target capacity");
    Check(!StartupFillPolicy.CanSubmit(3,0,0,15,20,91)&&!StartupFillPolicy.CanSubmit(3,0,15,15,20,20),"Startup window and total submission budget prevent endless replacements");
    int startupAlive=3,startupPending=0,startupSubmitted=0;bool populationBounded=true;
    for(int tick=5;tick<90;tick++)
    {
        if(tick%4==0&&startupPending>0){startupPending--;startupAlive++;}
        if(StartupFillPolicy.CanSubmit(startupAlive,startupPending,startupSubmitted,15,20,tick)){startupPending++;startupSubmitted++;}
        populationBounded&=startupAlive+startupPending<=15;
    }
    Check(populationBounded&&startupAlive==15&&startupSubmitted==12,"Delayed native registrations fill the missing 12 slots without duplicate requests");
    Check(!OverwatchPolicy.BaselineReady(30,20)&&!OverwatchPolicy.BaselineReady(65,5)&&OverwatchPolicy.BaselineReady(70,10)&&OverwatchPolicy.BaselineReady(90,0),"Rooftop extras wait for baseline loading and a quiet period, bounded at 90 seconds");
    Check(OverwatchPolicy.ExtraCount(13,180)==26&&OverwatchPolicy.ExtraCount(0,180)==0,"Thirteen rooftop points produce 26 extras, independent of ordinary population");
    Check(OverwatchPolicy.ExtraCount(13,20)==20&&OverwatchPolicy.ExtraCount(int.MaxValue,200)==200,"Only the separate extra-spawn budget limits rooftop requests");
    Check(Math.Abs(TimeWarpPolicy.Rate(50*60)-172.8)<0.000001,"Fifty-minute Streets Time Warp uses 172.8 world seconds per real second");
    Check(Math.Abs(TimeWarpPolicy.GameSeconds(500,50*60)-86400)<0.000001,"A complete Streets day/night cycle takes eight minutes twenty seconds");
    Check(new[]{1200d,1800d,2400d,3000d,3600d}.All(seconds=>Math.Abs(TimeWarpPolicy.GameSeconds(seconds,seconds)-6*86400)<0.000001),"Different actual raid durations all complete exactly six world days");
    Check(TimeWarpPolicy.GameSeconds(-1,3000)==0&&Math.Abs(TimeWarpPolicy.GameSeconds(4000,3000)-518400)<0.000001,"Clock progress is bounded to the current raid interval");
    Check(TimeWarpPolicy.Rate(0)==0&&TimeWarpPolicy.Rate(double.NaN)==0&&TimeWarpPolicy.Rate(double.PositiveInfinity)==0,"Invalid raid limits cannot produce a time speed override");
    var notice=new RaidEventNotice();notice.Begin();notice.Observe("factory4_day: normal");
    Check(notice.Consume()=="Raid Event : Normal","Normal raid uses the exact requested notification text");
    Check(notice.Consume()==null,"Repeated post-raid screen callbacks cannot duplicate a notification");
    notice.Begin();notice.Observe("TarkovStreets: time_warp");Check(notice.Consume()=="Raid Event : Time Warp","Actual event ID becomes a readable raid notification");
    notice.Begin();notice.Observe("factory4_day: normal — forced event unavailable");Check(notice.Consume()=="Raid Event : Normal","Unavailable forced events report the Normal raid that actually ran");
    notice.Begin();notice.Observe("Not reported");Check(notice.Consume()==null,"Menu-only and unconfirmed sessions cannot announce stale events");
    var screenFit=MonitorLayout.Clamp(1900,1000,650,420,1920,1080);
    Check(screenFit.X+screenFit.Width<=1920&&screenFit.Y+screenFit.Height<=1080,"Standalone monitor stays on screen after dragging beyond an edge");
    var tinyFit=MonitorLayout.Clamp(-50,-50,900,700,250,120);
    Check(tinyFit.X==0&&tinyFit.Y==0&&tinyFit.Width==250&&tinyFit.Height==120,"Monitor handles screens smaller than its usual minimum");
    var shrinkFit=MonitorLayout.Clamp(20,20,5,5,1920,1080);
    Check(shrinkFit.Width==320&&shrinkFit.Height==180,"Resizing preserves a usable minimum window size");
    var hugeFit=MonitorLayout.Clamp(0,0,5000,5000,1280,720);
    Check(hugeFit.Width==1280&&hugeFit.Height==720,"Oversized monitor dimensions are bounded by current resolution");
    var clock=new ReinforcementClock(300);
    Check(!clock.Advance(299.99)&&clock.Advance(300)&&clock.Waves==1,"First reinforcement due at five minutes");
    Check(!clock.Advance(300)&&!clock.Advance(599.99)&&clock.Advance(600),"No duplicate wave and second wave at ten minutes");
    Check(clock.Advance(1900)&&clock.Waves==3&&clock.NextDeadline==2100&&!clock.Advance(1900),"A stalled frame queues one wave and skips catch-up burst");
    Check(!clock.Advance(double.NaN)&&!clock.Advance(double.PositiveInfinity),"Invalid timer input cannot enqueue reinforcements");
    Check(ReinforcementClock.HasCapacity(20,4,25)&&!ReinforcementClock.HasCapacity(20,5,25),"Pending creations reserve alive capacity");
    Check(!ReinforcementClock.HasCapacity(int.MaxValue,int.MaxValue,35),"Capacity arithmetic cannot overflow");
    var fullRaid=new ReinforcementClock(300);for(int second=0;second<2400;second++)fullRaid.Advance(second);
    Check(fullRaid.Waves==7,"Forty-minute raid has seven useful five-minute waves before expiry");
    var limiter=new TraceLimiter();
    Check(limiter.Allow("first")&&!limiter.Allow("first"),"Native trace suppresses repeated identical gates");
    for(int i=0;i<239;i++)if(!limiter.Allow("entry"+i))throw new Exception("early trace cap");
    Check(!limiter.Allow("over-limit"),"Native trace caps records per scenario");
    limiter.Reset();Check(limiter.Allow("first"),"New scenario resets native trace budget");
    bool Gate(bool confirmed=true,bool forced=true,bool ignore=true,string boss="sectantPriest",string escort="sectantWarrior",double time=15)=>CultistGatePolicy.IsEventWave(confirmed,forced,ignore,boss,escort,time);
    Check(Gate()&&Gate(time:30),"Recognize confirmed preview.4 event waves");
    Check(!Gate(confirmed:false),"No zone workaround without confirmed cultist raid");
    Check(!Gate(forced:false)&&!Gate(ignore:false),"Unforced natural cultist waves excluded");
    Check(!Gate(boss:"bossTagilla")&&!Gate(escort:"followerBully"),"Other boss wave roles excluded");
    Check(!Gate(time:-1)&&!Gate(time:1),"Unrelated cultist schedules excluded");
    Check(CultistGatePolicy.IsCultistRole("sectantPriest")&&CultistGatePolicy.IsCultistRole("sectantWarrior")&&!CultistGatePolicy.IsCultistRole("assault")&&!CultistGatePolicy.IsCultistRole("pmcUSEC"),"Zone workaround role filter excludes ordinary AI");
    var hallucinationNotice=new RaidEventNotice();hallucinationNotice.Begin();hallucinationNotice.Observe("laboratory: schizophrenia");
    Check(hallucinationNotice.Consume()=="Raid Event : S̴̜͆c̷̯̈h̴̗̃ï̸͉z̶̻͊o̶̖͝p̷͇͘h̷̢̃r̴̦̂ê̶̯n̴͝ͅi̴̭̚a̵͊ͅ","New hallucination event keeps its requested post-raid display name");
    Check(StormtidePolicy.Progress(0,2400)==0&&StormtidePolicy.Progress(150,2400)==0.5f&&StormtidePolicy.Progress(300,2400)==1&&StormtidePolicy.Progress(2000,2400)==1,"Stormtide reaches full intensity five minutes after raid start and remains there");
    Check(StormtidePolicy.Progress(300,600)==1&&StormtidePolicy.Progress(300,3600)==1,"Stormtide buildup is five minutes on both short and long raids");
    var stormDay=new DateTime(2026,10,28,12,0,0);Check(StormtidePolicy.ClockAt(stormDay,0)==stormDay&&StormtidePolicy.ClockAt(stormDay,1)==stormDay.Date.AddHours(23),"Daylight Stormtide is dark at the five-minute deadline");
    foreach(int nightHour in new[]{0,3,5,21,23}){var night=stormDay.Date.AddHours(nightHour);Check(StormtidePolicy.ClockAt(night,0)==night&&StormtidePolicy.ClockAt(night,0.5f)==night&&StormtidePolicy.ClockAt(night,1)==night,"Night Stormtide stays dark without accelerating through daytime: "+nightHour);}
    Check(StormtidePolicy.OtherSoundGain(0)==1&&StormtidePolicy.OtherSoundGain(1)==0.25f,"Stormtide attenuates other audio to 25 percent");
    Check(StormtidePolicy.RainSoundGain(0)==1&&StormtidePolicy.RainSoundGain(0.5f)==1.5f&&StormtidePolicy.RainSoundGain(1)==2&&StormtidePolicy.RainSoundGain(2)==2,"Stormtide ramps native rain gain to double by five minutes without exceeding the requested multiplier");
    Check(StormtidePolicy.Progress(20,0)==0&&StormtidePolicy.Progress(double.NaN,200)==0,"Invalid raid timing cannot produce a storm override");
    Check(GunGameSpawnPolicy.CanSubmit(true,false,true,120,0,0,0,8),"An empty Factory Gun Game can request replacement opponents");
    Check(!GunGameSpawnPolicy.CanSubmit(true,true,true,120,0,0,0,8)&&!GunGameSpawnPolicy.CanSubmit(true,false,false,120,0,0,0,8),"Gun Game replenishment stops on player victory or death");
    Check(!GunGameSpawnPolicy.CanSubmit(true,false,true,120,4,0,4,8)&&!GunGameSpawnPolicy.CanSubmit(true,false,true,120,0,4,0,8)&&!GunGameSpawnPolicy.CanSubmit(true,false,true,120,1,0,8,8),"Replenishment respects the four-opponent target, pending reservations and total alive cap");
    Check(GunGameSpawnPolicy.CanSubmit(true,false,true,3500,1,0,1,8),"Gun Game replacements remain available late in the raid until victory");
    Check(GunGameSpawnPolicy.MinimumPopulation(20)==10&&GunGameSpawnPolicy.MinimumPopulation(15)==8&&GunGameSpawnPolicy.MinimumPopulation(6)==3,"Gun Game uses at least half the native map population, rounding up");
    Check(GunGameSpawnPolicy.CanSubmit(true,false,true,20,7,0,7,20,10)&&!GunGameSpawnPolicy.CanSubmit(true,false,true,20,10,0,10,20,10),"Replacement spawning follows the map-specific floor instead of a fixed four actors");
    Check(!GunGameSpawnPolicy.CanSubmit(true,false,true,20,8,2,8,20,10),"Pending native spawns count toward the floor and prevent a refill burst");
    Check(!GunGameFlowPolicy.RefillRocket(7,7,0,9.99)&&GunGameFlowPolicy.RefillRocket(7,7,0,10),"An empty rocket launcher waits ten seconds before replacement at its new stage");
    Check(!GunGameFlowPolicy.RefillRocket(7,8,0,12)&&!GunGameFlowPolicy.RefillRocket(7,7,1,12)&&!GunGameFlowPolicy.RefillRocket(6,6,0,12),"A recorded or pending rocket kill prevents supplying the old launcher again; LMG is not treated as a rocket");
    var still=new StillnessScare();
    Check(!still.Tick(0,0,0,0,true,0)&&!still.Tick(9.99,0,0,0,true,0)&&still.Tick(10,0,0,0,true,0.1),"Stillness grenade scare waits ten seconds and accepts a successful chance roll");
    Check(!still.Tick(150,0,0,0,true,0),"Remaining stationary cannot repeat the scare even after the cooldown");
    still.Tick(151,1,0,0,true,0);Check(!still.Tick(160,1,0,0,true,0)&&!still.Tick(161,1,0,0,true,0.9)&&!still.Tick(180,1,0,0,true,0),"Movement resets the timer, and a failed roll is not retried during that stop");
    still.Reset();still.Tick(0,0,0,0,true,0);still.Tick(10,0,0,0,true,0);still.Tick(11,1,0,0,true,0);
    Check(!still.Tick(21,1,0,0,true,0)&&still.Tick(130,1,0,0,true,0),"Separate stops respect the two-minute successful-scare cooldown");
    still.Reset();still.Tick(0,0,0,0,true,0);Check(!still.Tick(10,0,0,0,false,0)&&still.Tick(11,0,0,0,true,0),"An active effect defers the stillness roll until it can play without overlap");
    still.Reset();still.Tick(0,0,0,0,true,0);still.Tick(5,0.1,0,0,true,0);Check(still.Tick(10,0.1,0,0,true,0),"Minor position jitter does not prevent the stationary scare");
    var stormSchedule=new ThunderSchedule();
    Check(!stormSchedule.Tick(299,true,0)&&!stormSchedule.Tick(300,true,0)&&!stormSchedule.Tick(479.99,true,0)&&stormSchedule.Tick(480,true,0.329),"Thunder waits for five-minute darkness plus a three-minute interval before its first 33-percent roll");
    Check(!stormSchedule.Tick(480.1,true,0)&&!stormSchedule.Tick(659.99,true,0)&&!stormSchedule.Tick(660,true,0.33)&&!stormSchedule.Tick(661,true,0)&&stormSchedule.Tick(840,true,0),"Failed thunder rolls are consumed once, with no per-frame retries between three-minute checks");
    var lateStorm=new ThunderSchedule();Check(lateStorm.Tick(1000,true,0)&&!lateStorm.Tick(1001,true,0)&&!lateStorm.Tick(1179,true,0)&&lateStorm.Tick(1180,true,0),"Late thunder checks skip missed intervals instead of producing a catch-up burst");
    var occupiedStorm=new ThunderSchedule();Check(!occupiedStorm.Tick(480,false,0)&&!occupiedStorm.Tick(481,true,0)&&occupiedStorm.Tick(660,true,0),"A busy thunder source consumes its interval rather than rerolling as soon as it becomes free");
    occupiedStorm.Reset();Check(occupiedStorm.NextRollAt==480&&occupiedStorm.Rolls==0&&!occupiedStorm.Tick(double.NaN,true,0),"New raids reset the thunder schedule and invalid elapsed times cannot trigger a roll");
    Check(ThunderPolicy.Delay(0)==0.8&&Math.Abs(ThunderPolicy.Delay(1)-3)<0.000001,"Thunder sound follows the flash with a bounded distance delay");
    Check(ThunderPolicy.Flash(-1)==0&&ThunderPolicy.Flash(0)==1&&ThunderPolicy.Flash(0.1)==0&&Math.Abs(ThunderPolicy.Flash(0.13)-0.55)<0.000001&&ThunderPolicy.Flash(0.2)==0,"Lightning consists of two bounded brief pulses and ends completely");
    Check(Math.Abs(StormAudioPolicy.NativeRain(0)-1)<1e-6&&Math.Abs(StormAudioPolicy.NativeRain(1))<1e-6&&StormAudioPolicy.RecordedRain(0)==0&&Math.Abs(StormAudioPolicy.RecordedRain(1)-1)<1e-6,"Recorded rain crossfade starts native-only and ends recording-only");
    Check(Enumerable.Range(0,101).All(i=>Math.Abs(Math.Pow(StormAudioPolicy.NativeRain(i/100d),2)+Math.Pow(StormAudioPolicy.RecordedRain(i/100d),2)-1)<1e-6),"Rain crossfade maintains equal-power gains without two full-volume tracks");
    Check(Enumerable.Range(0,2).All(previous=>Enumerable.Range(0,101).All(i=>StormAudioPolicy.ChooseThunder(previous,2,i/100d)!=previous)),"Two-recording thunder pool avoids immediately repeating the last recording");
    Check(Enumerable.Range(0,101).Select(i=>StormAudioPolicy.ChooseThunder(-1,2,i/100d)).ToHashSet().SetEquals(new[]{0,1})&&StormAudioPolicy.ChooseThunder(0,1,1)==0&&StormAudioPolicy.ChooseThunder(0,0,0)==-1,"Both retained recordings can be selected and smaller fallback pools stay valid");
    string testWave=Path.Combine(folder,"pcm-test.wav");
    using(var writer=new BinaryWriter(File.Create(testWave)))
    {
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(56);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)2);writer.Write(32000);writer.Write(128000);writer.Write((short)4);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("JUNK"));writer.Write(4);writer.Write(123);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(8);foreach(short value in new short[]{short.MinValue,0,short.MaxValue,-16384})writer.Write(value);
    }
    var decoded=WavePcm.Read(testWave);Check(decoded.Rate==32000&&decoded.Channels==2&&decoded.Samples.Length==4&&decoded.Samples[0]==-1&&decoded.Samples[1]==0&&Math.Abs(decoded.Samples[2]-32767/32768f)<1e-7&&decoded.Samples[3]==-0.5f,"WAV reader correctly decodes signed stereo PCM and skips optional metadata chunks");
    byte[] truncated=File.ReadAllBytes(testWave);File.WriteAllBytes(testWave,truncated.Take(truncated.Length-1).ToArray());bool rejected=false;try{WavePcm.Read(testWave);}catch(InvalidDataException){rejected=true;}Check(rejected,"WAV reader rejects truncated recordings before upload");
    truncated[34]=24;File.WriteAllBytes(testWave,truncated);rejected=false;try{WavePcm.Read(testWave);}catch(InvalidDataException){rejected=true;}Check(rejected,"WAV reader rejects unsupported sample formats");
    int audioArg=Array.IndexOf(args,"--storm-audio");
    if(audioArg>=0)
    {
        foreach(string name in new[]{"Thunder1.wav","ThunderReplacement.wav","HeavyRain.wav"}){var asset=WavePcm.Read(Path.Combine(args[audioArg+1],name));Check(asset.Rate==32000&&asset.Channels==2&&asset.Samples.Length>32000&&asset.Samples.All(v=>Math.Abs(v)<=0.851f),"Packaged recording decodes correctly without clipping: "+name);}
        var loop=WavePcm.Read(Path.Combine(args[audioArg+1],"HeavyRain.wav"));Check(loop.Samples.Length==32000*2*30&&Math.Abs(loop.Samples[0]-loop.Samples[loop.Samples.Length-2])<0.05f&&Math.Abs(loop.Samples[1]-loop.Samples[loop.Samples.Length-1])<0.05f,"Heavy rain loop has the intended thirty-second length and continuous stereo seam");
    }
    var phantomDay=new DateTime(2026,10,28,13,0,0);var phantomNight=new DateTime(2026,10,28,23,30,0);
    Check(SchizophreniaClockPolicy.Shift(phantomDay)==new DateTime(2026,10,29,1,0,0)&&SchizophreniaClockPolicy.Shift(phantomNight)==new DateTime(2026,10,29,11,30,0),"Schizophrenia's twelve-hour jump handles both day-to-night and night-to-day across midnight");
    Check(SchizophreniaClockPolicy.Duration==60&&SchizophreniaClockPolicy.ScheduledAt(600,0)==60&&SchizophreniaClockPolicy.ScheduledAt(600,1)==530,"Time-jump scheduling leaves room for a complete sixty-second effect before raid end");
    Check(double.IsPositiveInfinity(SchizophreniaClockPolicy.ScheduledAt(100,0.5))&&double.IsPositiveInfinity(SchizophreniaClockPolicy.ScheduledAt(double.NaN,0.5)),"Too-short or invalid raids cannot start a time jump that would outlast the raid");
    Check(LabStaffPolicy.DefaultPenalty==1000,"Civilian kill penalty defaults to the requested 1,000 final raid XP");
    Check(LabStaffPolicy.Award(5000,1000)==4000&&LabStaffPolicy.Award(5000,3000)==2000,"Each civilian costs its full penalty from the final award after native bonuses");
    Check(LabStaffPolicy.Award(900,3000)==0&&LabStaffPolicy.Award(0,1000)==0,"Civilian penalties cannot create negative raid XP");
    Check(LabStaffPolicy.Award(5000,0)==5000,"Setting the penalty to zero preserves earned XP");
    Check(!LabStaffPolicy.IsStaff("gifter","The Gift")&&LabStaffPolicy.IsStaff("gifter","Lab Staff")&&!LabStaffPolicy.IsStaff("bossTagilla","Lab Staff"),"Civilian rules distinguish staff from Santa and real bosses");
    Check(LabStaffPolicy.Panic(20,10,35)&&!LabStaffPolicy.Panic(40,10,35),"Nearby gunfire causes a temporary panic that expires after quiet");
    Check(!LabStaffPolicy.Panic(20,10,100)&&!LabStaffPolicy.Panic(5,10,20),"Distant or future threats do not trigger civilian panic");
    
var chanceIds=new[]{"normal","clone_wars","stormtide","gun_game"};
var exactChances=new RaidRoulette.Shared.RaidChanceProfile{Mode="IndividualPercentages",Percentages=new(){["normal"]=10,["clone_wars"]=10,["stormtide"]=80,["gun_game"]=0}};
Check(RaidRoulette.Shared.RaidChances.Valid(exactChances,chanceIds,out _),"User's 10/10/80 profile is valid");
var outdoorChances=RaidRoulette.Shared.RaidChances.Allocate(exactChances,chanceIds,_=>true);
Check(outdoorChances["normal"]==10&&outdoorChances["clone_wars"]==10&&outdoorChances["stormtide"]==80,"Outdoor allocation preserves exact percentages");
var labsChances=RaidRoulette.Shared.RaidChances.Allocate(exactChances,chanceIds,id=>id!="stormtide");
Check(labsChances["normal"]==90&&labsChances["clone_wars"]==10&&labsChances["stormtide"]==0,"Labs moves Stormtide's 80 percent to Normal without increasing Clone Wars");
Check(RaidRoulette.Shared.RaidChances.Pick(labsChances,0)=="clone_wars"&&RaidRoulette.Shared.RaidChances.Pick(labsChances,9.999)=="clone_wars"&&RaidRoulette.Shared.RaidChances.Pick(labsChances,10)=="normal"&&RaidRoulette.Shared.RaidChances.Pick(labsChances,99.99)=="normal","Selection boundaries match the actual allocation");
Check(Enumerable.Range(0,10000).All(i=>RaidRoulette.Shared.RaidChances.Pick(outdoorChances,i/100d)!="gun_game"),"Zero-percent events cannot be selected automatically");
var overallChances=new RaidRoulette.Shared.RaidChanceProfile{Mode="OverallChance",EventChance=40,Percentages=new(){["normal"]=60,["clone_wars"]=1,["stormtide"]=1}};
var overallOutdoor=RaidRoulette.Shared.RaidChances.Allocate(overallChances,chanceIds,_=>true);
var overallLabs=RaidRoulette.Shared.RaidChances.Allocate(overallChances,chanceIds,id=>id!="stormtide");
Check(overallOutdoor["normal"]==60&&overallOutdoor["clone_wars"]==20&&overallOutdoor["stormtide"]==20,"Overall mode splits the 40 percent pool across enabled event shares");
Check(overallLabs["normal"]==80&&overallLabs["clone_wars"]==20,"Unavailable overall shares also become Normal rather than increasing other events");
Check(RaidRoulette.Shared.RaidChances.Allocate(exactChances,chanceIds,_=>false)["normal"]==100,"No supported event produces a Normal raid");
exactChances.Percentages["stormtide"]=81;
Check(!RaidRoulette.Shared.RaidChances.Valid(exactChances,chanceIds,out _),"Percentages above 100 are rejected");
exactChances.Percentages["stormtide"]=double.NaN;
Check(!RaidRoulette.Shared.RaidChances.Valid(exactChances,chanceIds,out _),"Non-finite chances are rejected");
exactChances.Percentages["stormtide"]=20;
Check(RaidRoulette.Shared.RaidChances.Allocate(exactChances,chanceIds,_=>true)["normal"]==70,"Unallocated percentages become Normal");
exactChances.Percentages["unknown"]=1;
Check(!RaidRoulette.Shared.RaidChances.Valid(exactChances,chanceIds,out _),"Unknown event IDs cannot alter the distribution");
Console.WriteLine("TOTAL PASS "+checks);
}
finally { Directory.Delete(folder,true); }






