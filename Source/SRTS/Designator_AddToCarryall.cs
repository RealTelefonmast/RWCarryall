﻿using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SRTS;

public class Designator_AddToCarryall : Designator
{
    public override int DraggableDimensions => 2;
    private CompLaunchableSRTS launchable;

    public Designator_AddToCarryall(CompLaunchableSRTS launchable)
    {
        this.launchable = launchable;
        this.defaultLabel = "CA_AddToCarryall".Translate();
        this.icon = ContentFinder<Texture2D>.Get("Misc/AddToCarryall", true);
        this.soundDragSustain = SoundDefOf.Designate_DragStandard;
        this.soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
        this.useMouseIcon = true;
        this.soundSucceeded = SoundDefOf.Checkbox_TurnedOff;
        this.hasDesignateAllFloatMenuOption = true;
    }
    
    public override AcceptanceReport CanDesignateCell(IntVec3 c)
    {
        if (!c.InBounds(base.Map) || c.Fogged(base.Map))
        {
            return false;
        }
        if (!c.GetThingList(base.Map).Any((Thing t) => this.CanDesignateThing(t).Accepted))
        {
            return false;
        }
        return true;
    }
    
    public override AcceptanceReport CanDesignateThing(Thing t)
    {
        Pawn pawn = t as Pawn;
        if (pawn != null)
        {
            if (pawn.health?.Downed ?? false) return true;
            if (pawn.IsColonist) return true;
            if (pawn.def.race.Animal && pawn.Faction == Faction.OfPlayer && !pawn.InAggroMentalState)
            {
                return true;
            }
        }

        if (t.def.category != ThingCategory.Item)
        {
            return false;
        }

        return !launchable.Transporter.LeftToLoadContains(t);
    }
    
    public override void DesignateSingleCell(IntVec3 c)
    {
        List<Thing> thingList = c.GetThingList(base.Map);
        for (int i = 0; i < thingList.Count; i++)
        {
            if (CanDesignateThing(thingList[i]).Accepted)
            {
                DesignateThing(thingList[i]);
            }
        }
    }

    public override void DesignateThing(Thing t)
    {
        if(!launchable.Transporter.LoadingInProgressOrReadyToLaunch)
            TransporterUtility.InitiateLoading(new List<CompTransporter>() {launchable.Transporter});

        var fobiddable = t.TryGetComp<CompForbiddable>();
        if (fobiddable is {Forbidden: true})
        {
            fobiddable.Forbidden = false;
        }

        var curMass = launchable?.Transporter?.leftToLoad?.Sum(tf => tf.things?.Sum(t => t.def.BaseMass));
        if (curMass + t.def.BaseMass > launchable?.Transporter?.MassCapacity)
        {
            Messages.Message("TooBigTransportersMassUsage".Translate(), MessageTypeDefOf.RejectInput, false);
            return;
        }

        launchable.Transporter.AddToTheToLoadList(new TransferableOneWay()
        {
            things = new List<Thing>(){t}
        }, t.stackCount);
    }
}