using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Carryalls.Properties
{
    public class Comp_Carryall : CompShuttle
    {
        public CompProperties_Carryall Props => (CompProperties_Carryall) base.props;
        
        public override void PostPostMake()
        {
            base.PostPostMake();
            this.acceptColonists = Props.acceptColonists;
            this.onlyAcceptColonists = Props.onlyAcceptColonists;
            this.acceptColonyPrisoners = Props.acceptColonyPrisoners;
            this.maxColonistCount = Props.maxColonistCount;
            this.requiredColonistCount = Props.requiredColonistCount;

        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
        }
    }
}