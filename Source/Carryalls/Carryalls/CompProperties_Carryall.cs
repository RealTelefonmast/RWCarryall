using RimWorld;

namespace Carryalls
{
    public class CompProperties_Carryall : CompProperties_Shuttle
    {
        public bool acceptColonists;
        public int maxColonistCount = -1;
        public int requiredColonistCount;
        public bool acceptColonyPrisoners;
        public bool onlyAcceptColonists;
    }
}