using Verse;

namespace SRTS
{
    public class CompProperties_LaunchableSRTS : CompProperties
    {
        public CompProperties_LaunchableSRTS()
        {
            this.compClass = typeof (CompLaunchableSRTS);
        }

        public ThingDef eastSkyfaller;
        public ThingDef westSkyfaller;
        
        public ThingDef eastSkyfallerIncoming;
        public ThingDef westSkyfallerIncoming;
        
        public ThingDef eastSkyfallerActive;
        public ThingDef westSkyfallerActive;

        public bool needsConfirmation;
        public bool hasSelfDestruct;
        
        public float travelSpeed = 25f;
        public int minPassengers = 1;
        public int maxPassengers = 2;

        public float fuelPerTile = 2.25f;

        /* SOS2 Compatibility */
        public bool spaceFaring;
        public bool shuttleBayLanding;
        /* ------------------ */
    }
}
