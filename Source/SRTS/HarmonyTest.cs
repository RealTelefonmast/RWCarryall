using HarmonyLib;
using RimWorld;
using Verse;

namespace SRTS
{
    /* Akreedz original patch */
    [HarmonyPatch(typeof (DropPodUtility), "MakeDropPodAt")]
    public static class HarmonyTest
    {
        public static bool Prefix(IntVec3 c, Map map, ActiveDropPodInfo info)
        {
            for (int index = 0; index < info.innerContainer.Count; index++)
            {
                var comp = info.innerContainer[index].TryGetComp<CompLaunchableSRTS>();
                if (comp != null)
                {
                    Thing ship = info.innerContainer[index];
                    ActiveDropPod activeDropPod = (ActiveDropPod)ThingMaker.MakeThing(SRTSStatic.SkyfallerActiveDefByRot(comp), (ThingDef)null);
                    activeDropPod.Contents = info;
                    
                    EnsureInBoundsSRTS(ref c, info.innerContainer[index].def, map);
                    SRTSIncoming incomingSRTS = (SRTSIncoming)SkyfallerMaker.SpawnSkyfaller(SkyfallerIncomingDefByRot(info.innerContainer[index].TryGetComp<CompLaunchableSRTS>()), (Thing)activeDropPod, c, map);
                    incomingSRTS.SRTSRotation = ship.Rotation;
                    return false;
                }
                else if(DefDatabase<ThingDef>.GetNamedSilentFail(info.innerContainer[index].def.defName.Split('_')[0])?.GetCompProperties<CompProperties_BombsAway>() != null)
                {
                    ThingDef td = DefDatabase<ThingDef>.GetNamed(info.innerContainer[index].def.defName.Split('_')[0]);
                    ActiveDropPod activeDropPod = (ActiveDropPod)ThingMaker.MakeThing(info.innerContainer[index].def, null);
                    activeDropPod.Contents = info;
                    SRTSIncoming incomingSRTS = (SRTSIncoming)SkyfallerMaker.SpawnSkyfaller(SkyfallerIncomingDefByRot(info.innerContainer[index].TryGetComp<CompLaunchableSRTS>()), activeDropPod, c, map);
                    return false;
                }
            }
            return true;
        }
        
        private static ThingDef SkyfallerIncomingDefByRot(CompLaunchableSRTS comp)
        {
            if (comp.parent.Rotation == Rot4.East)
            {
                return comp.SRTSProps.eastSkyfallerIncoming;
            }
            else if (comp.parent.Rotation == Rot4.West)
            {
                return comp.SRTSProps.westSkyfallerIncoming;
            }

            return comp.SRTSProps.eastSkyfaller;
        }

        private static void EnsureInBoundsSRTS(ref IntVec3 c, ThingDef shipDef, Map map)
        {
            int x = (int)shipDef.graphicData.drawSize.x;
            int y = (int)shipDef.graphicData.drawSize.y;
            int offset = x > y ? x : y;

            if (c.x < offset)
            {
                c.x = offset;
            }
            else if (c.x >= (map.Size.x - offset))
            {
                c.x = (map.Size.x - offset);
            }
            if (c.z < offset)
            {
                c.z = offset;
            }
            else if (c.z > (map.Size.z - offset))
            {
                c.z = (map.Size.z - offset);
            }
        }
    }
}
