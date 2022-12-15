using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace SRTS.ArrivalActions;

public class CarryallArrivalAction_LandInSpecificCell :  TransportPodsArrivalAction
{
    private MapParent mapParent;
    private IntVec3 cell;

    public CarryallArrivalAction_LandInSpecificCell(MapParent mapParent, IntVec3 shuttleBayPos)
    {
        this.mapParent = mapParent;
        this.cell = cell;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look<MapParent>(ref this.mapParent, "mapParent", false);
        Scribe_Values.Look<IntVec3>(ref this.cell, "cell", default(IntVec3), false);
    }

    public override FloatMenuAcceptanceReport StillValid(IEnumerable<IThingHolder> pods, int destinationTile)
    {
        FloatMenuAcceptanceReport floatMenuAcceptanceReport = base.StillValid(pods, destinationTile);
        if (!floatMenuAcceptanceReport)
        {
            return floatMenuAcceptanceReport;
        }
        if (this.mapParent != null && this.mapParent.Tile != destinationTile)
        {
            return false;
        }
        return TransportPodsArrivalAction_LandInSpecificCell.CanLandInSpecificCell(pods, this.mapParent);
    }

    public override void Arrived(List<ActiveDropPodInfo> pods, int tile)
    {
        Thing lookTarget = TransportPodsArrivalActionUtility.GetLookTarget(pods);
        Messages.Message("MessageTransportPodsArrived".Translate(), lookTarget, MessageTypeDefOf.TaskCompletion, true);
        
        //
        DropCarryall(pods, cell, mapParent.Map);
    }
    
    private void DropCarryall(List<ActiveDropPodInfo> dropPods, IntVec3 near, Map map)
    {
        TransportPodsArrivalActionUtility.RemovePawnsFromWorldPawns(dropPods);
        for (int i = 0; i < dropPods.Count; i++)
        {
            if (!near.IsValid)
            {
                near = DropCellFinder.GetBestShuttleLandingSpot(map, Faction.OfPlayer);
            }
            
            DropPodUtility.MakeDropPodAt(near, map, dropPods[i], null);
        }
    }
}