using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;
using HarmonyLib;

namespace SRTS;

[StaticConstructorOnStartup]
public class CompLaunchableSRTS : ThingComp
{
	public static readonly Texture2D TargeterMouseAttachment = ContentFinder<Texture2D>.Get("UI/Overlays/LaunchableMouseAttachment", true);
	private static readonly Texture2D LaunchCommandTex = ContentFinder<Texture2D>.Get("UI/Commands/LaunchShip", true);
	private CompTransporter cachedCompTransporter;
	private Caravan carr;

	readonly List<Thing> thingsInsideShip = new List<Thing>();
	readonly List<Pawn> tmpAllowedPawns = new List<Pawn>();
        
	public float BaseFuelPerTile => SRTSMod.GetStatFor<float>(this.parent.def.defName, StatName.fuelPerTile);

	public CompProperties_LaunchableSRTS SRTSProps => (CompProperties_LaunchableSRTS)this.props;
	public Building FuelingPortSource => (Building)parent;
	public bool ConnectedToFuelingPort => FuelingPortSource != null;
	public bool FuelingPortSourceHasAnyFuel => this.ConnectedToFuelingPort && this.FuelingPortSource.GetComp<CompRefuelable>().HasFuel;
	public bool LoadingInProgressOrReadyToLaunch => this.Transporter.LoadingInProgressOrReadyToLaunch;

	public bool AnythingLeftToLoad => this.Transporter.AnythingLeftToLoad;

	public Thing FirstThingLeftToLoad => this.Transporter.FirstThingLeftToLoad;

	public List<CompTransporter> TransportersInGroup => new (){parent.TryGetComp<CompTransporter>()};

	public bool AnyInGroupHasAnythingLeftToLoad => this.Transporter.AnyInGroupHasAnythingLeftToLoad;

	public Thing FirstThingLeftToLoadInGroup => this.Transporter.FirstThingLeftToLoadInGroup;

	public bool AnyInGroupIsUnderRoof
	{
		get
		{
			List<CompTransporter> transportersInGroup = this.TransportersInGroup;
			for (int index = 0; index < transportersInGroup.Count; ++index)
			{
				if (transportersInGroup[index].parent.Position.Roofed(this.parent.Map))
					return true;
			}
			return false;
		}
	}

	public CompTransporter Transporter
	{
		get
		{
			if (this.cachedCompTransporter == null)
				this.cachedCompTransporter = this.parent.GetComp<CompTransporter>();
			return this.cachedCompTransporter;
		}
	}

	public float FuelingPortSourceFuel
	{
		get
		{
			if (!this.ConnectedToFuelingPort)
				return 0.0f;
			return this.parent.GetComp<CompRefuelable>().Fuel;
		}
	}

	public bool AllInGroupConnectedToFuelingPort => true;

	public bool AllFuelingPortSourcesInGroupHaveAnyFuel => true;

	private float FuelInLeastFueledFuelingPortSource
	{
		get
		{
			float num = 0.0f;
			bool flag = false;
			float fuelingPortSourceFuel = this.FuelingPortSourceFuel;
			if (!flag || (double) fuelingPortSourceFuel < (double) num)
			{
				num = fuelingPortSourceFuel;
				flag = true;
			}
			if (!flag)
				return 0.0f;
			return num;
		}
	}

	public int MaxLaunchDistance
	{
		get
		{
			if (this.parent.Spawned && !this.LoadingInProgressOrReadyToLaunch)
				return 0;
			return CompLaunchableSRTS.MaxLaunchDistanceAtFuelLevel(this.FuelInLeastFueledFuelingPortSource, this.BaseFuelPerTile);
		}
	}

	public int MaxLaunchDistanceEverPossible
	{
		get
		{
			if (!this.LoadingInProgressOrReadyToLaunch)
				return 0;
			float num = 0.0f;
			Building fuelingPortSource = this.FuelingPortSource;
			if (fuelingPortSource != null)
				num = Mathf.Max(num, fuelingPortSource.GetComp<CompRefuelable>().Props.fuelCapacity);
			return CompLaunchableSRTS.MaxLaunchDistanceAtFuelLevel(num, this.BaseFuelPerTile);
		}
	}

	private bool PodsHaveAnyPotentialCaravanOwner
	{
		get
		{
			List<CompTransporter> transportersInGroup = this.TransportersInGroup;
			for (int index1 = 0; index1 < transportersInGroup.Count; ++index1)
			{
				ThingOwner innerContainer = transportersInGroup[index1].innerContainer;
				for (int index2 = 0; index2 < innerContainer.Count; ++index2)
				{
					Pawn pawn = innerContainer[index2] as Pawn;
					if (pawn != null && CaravanUtility.IsOwner(pawn, Faction.OfPlayer))
						return true;
				}
			}
			return false;
		}
	}
	    
	public override void PostSpawnSetup(bool respawningAfterLoad)
	{
		base.PostSpawnSetup(respawningAfterLoad);
		if (parent.Rotation == Rot4.North)
		{
			parent.Rotation = Rot4.East;
		}
		if (parent.Rotation == Rot4.South)
		{
			parent.Rotation = Rot4.West;
		}
		    
		//
		parent.Map.mapDrawer.MapMeshDirty(parent.Position, MapMeshFlagDefOf.Buildings | MapMeshFlagDefOf.Things);
	}

	public void AddThingsToSRTS(Thing thing)
	{
		thingsInsideShip.Add(thing);
	}

	private Designator_AddToCarryall designator;

	private Designator_AddToCarryall AddToCarryallDesignator => designator ??= new Designator_AddToCarryall(this);

	/*
	private void TryLaunch()
	{
		if (SRTSProps.needsConfirmation)
		{
			Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CA_ConfirmationScreen".Translate(), DoLaunchAction));
		}
		else
		{
			DoLaunchAction();
		}
	}
	*/
	    
	private void DoLaunchAction()
	{
		int num = 0;
		foreach(Thing t in this.Transporter.innerContainer)
		{
			if(t is Pawn && (t as Pawn).IsColonist)
			{
				num++;
			}
		}
		if(SRTSMod.mod.settings.passengerLimits)
		{
			if (num < SRTSMod.GetStatFor<int>(this.parent.def.defName, StatName.minPassengers))
			{
				Messages.Message("NotEnoughPilots".Translate(), MessageTypeDefOf.RejectInput, false);
				return;
			}
			else if (num > SRTSMod.GetStatFor<int>(this.parent.def.defName, StatName.maxPassengers))
			{
				Messages.Message("TooManyPilots".Translate(), MessageTypeDefOf.RejectInput, false);
				return;
			}
		}

		if (this.AnyInGroupHasAnythingLeftToLoad)
		{
			Find.WindowStack.Add((Window) Dialog_MessageBox.CreateConfirmation(
				"ConfirmSendNotCompletelyLoadedPods".Translate(this.FirstThingLeftToLoadInGroup.LabelCapNoCount),
				StartChoosingDestination));
		}
		else
		{
			StartChoosingDestination();
		}
	}
	    
	public override IEnumerable<Gizmo> CompGetGizmosExtra()
	{
		foreach (Gizmo gizmo in base.CompGetGizmosExtra())
		{
			Gizmo g = gizmo;
			yield return g;
			g = null;
		}

		yield return AddToCarryallDesignator;
	        
		yield return new Command_Action()
		{
			defaultLabel = "CA_Rotate".Translate(),
			defaultDesc = "CA_RotateDesc".Translate(),
			icon = ContentFinder<Texture2D>.Get("Misc/Rotate"),
			action = delegate
			{
				parent.Rotation = parent.Rotation.Opposite;
				parent.Map.mapDrawer.MapMeshDirty(parent.Position, MapMeshFlagDefOf.Buildings | MapMeshFlagDefOf.Things);
			}
		};

		if (SRTSProps.hasSelfDestruct)
		{
			yield return new Command_Action()
			{
				defaultLabel = "CA_SelfDestruct".Translate(),
				defaultDesc = "CA_SelfDestructDesc".Translate(),
				icon = ContentFinder<Texture2D>.Get("Misc/Detonate"),
				action = delegate
				{
					var exp = parent.TryGetComp<CompExplosive>();
					if (exp != null)
					{
						FleckMaker.ThrowMicroSparks(parent.DrawPos, parent.Map);
						FireUtility.TryStartFireIn(parent.Position, parent.Map, 2, null);
						parent.TryAttachFire(100, null);
					}
				}
			};
		}

		if (this.LoadingInProgressOrReadyToLaunch)
		{ 
			Command_Action launch = new Command_Action();
			launch.defaultLabel = "CommandLaunchGroup".Translate();
			launch.defaultDesc = "CommandLaunchGroupDesc".Translate();
			launch.icon = LaunchCommandTex;
			launch.alsoClickIfOtherInGroupClicked = false;
			launch.action = DoLaunchAction;
			if (!this.AllInGroupConnectedToFuelingPort)
				launch.Disable("CommandLaunchGroupFailNotConnectedToFuelingPort".Translate());
			else if (!this.AllFuelingPortSourcesInGroupHaveAnyFuel)
				launch.Disable("CommandLaunchGroupFailNoFuel".Translate());
			else if (this.AnyInGroupIsUnderRoof && !this.parent.Position.GetThingList(this.parent.Map).Any(x => x.def.defName == "ShipShuttleBay"))
				launch.Disable("CommandLaunchGroupFailUnderRoof".Translate());
			yield return launch;
		}
	}

	private bool CanAcceptNewPawnToLoad(Pawn pawn)
	{
		var boardingPawnCount = Transporter.leftToLoad?.Sum(t => t.things?.Count(t => t is Pawn)) ?? 0;
		var currentlyLoadedPawnCount = Transporter.innerContainer?.Count(t => t is Pawn) ?? 0;

		//Weight condition
		if (pawn != null)
		{
			var leftLoadList = Transporter.leftToLoad;
			var innerContainerList = Transporter.innerContainer;
			float currentMass = 0;
			float extraMass = 0;
			if (!leftLoadList.NullOrEmpty())
				currentMass = CollectionsMassCalculator.MassUsage(leftLoadList.SelectMany(t => t.things).ToList(), IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload, true);
				    
			if (!innerContainerList.NullOrEmpty())
				extraMass = CollectionsMassCalculator.MassUsage(innerContainerList.ToList(), IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload, true);

			if ((currentMass + extraMass + pawn.def.BaseMass) > Transporter?.MassCapacity)
			{
				return false;
			}
		}
		    
		if ((boardingPawnCount + currentlyLoadedPawnCount) < SRTSProps.maxPassengers)
		{
			return true;
		}
		return false;
	}

	private void TryInitPawnLoadingCommand(Pawn pawn)
	{
		if (!LoadingInProgressOrReadyToLaunch)
		{
			TransporterUtility.InitiateLoading(Gen.YieldSingle<CompTransporter>(this.Transporter));
		}

		Transporter.AddToTheToLoadList(new TransferableOneWay()
		{
			things = new List<Thing>() {pawn}
		}, 1);
	}

	public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn pawn)
	{
		if (!pawn.Dead && !pawn.InMentalState && pawn.Faction == Faction.OfPlayerSilentFail)
		{
			if (!CanAcceptNewPawnToLoad(pawn))
			{
				yield break;
			}
			if (pawn.CanReach(this.parent, PathEndMode.Touch, Danger.Deadly, false, mode: TraverseMode.ByPawn))
			{
				yield return new FloatMenuOption("BoardSRTS".Translate(this.parent.Label), delegate()
				{
					TryInitPawnLoadingCommand(pawn);
					Job job = new Job(JobDefOf.EnterTransporter, this.parent);
					pawn.jobs.TryTakeOrderedJob(job);
				}, MenuOptionPriority.Default, null, null, 0f, null, null);
			}
		}
	}
        
        
	public override IEnumerable<FloatMenuOption> CompMultiSelectFloatMenuOptions(List<Pawn> selPawns)
	{
		for (int i = 0; i < selPawns.Count; i++)
		{
			if (selPawns[i].CanReach(this.parent, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn))
			{
				tmpAllowedPawns.Add(selPawns[i]);
			}
		}
		if (!tmpAllowedPawns.Any<Pawn>())
		{
			yield return new FloatMenuOption("BoardSRTS".Translate(this.parent.Label) + " (" + "NoPath".Translate() + ")", null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
			yield break;
		}
	        
		if (!CanAcceptNewPawnToLoad(null))
		{
			yield break;
		}

		yield return new FloatMenuOption("BoardSRTS".Translate(this.parent.Label), delegate()
		{
			for (int k = 0; k < tmpAllowedPawns.Count; k++)
			{
				Pawn pawn = tmpAllowedPawns[k];
			        
				if (pawn.CanReach(this.parent, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn) && !pawn.Downed && !pawn.Dead && pawn.Spawned)
				{
					if (!CanAcceptNewPawnToLoad(pawn))
					{
						Messages.Message("CA_WarningSeatsFull".Translate(pawn.NameFullColored), MessageTypeDefOf.RejectInput, false);
						continue;
					}

					TryInitPawnLoadingCommand(pawn);
				        
					Job job = JobMaker.MakeJob(JobDefOf.EnterTransporter, this.parent);
					tmpAllowedPawns[k].jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.Misc), false);
				}
			}
		}, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
		yield break;
	}
        

	public override string CompInspectStringExtra()
	{
		StringBuilder sb = new StringBuilder();
		if (!this.LoadingInProgressOrReadyToLaunch) { }
		else
		if (!this.AllInGroupConnectedToFuelingPort)
		{
			sb.AppendLine("NotReadyForLaunch".Translate() + ": " +
			              "NotAllInGroupConnectedToFuelingPort".Translate() + ".");
		}
		else
		if (!this.AllFuelingPortSourcesInGroupHaveAnyFuel)
		{
			sb.AppendLine("NotReadyForLaunch".Translate() + ": " +
			              "NotAllFuelingPortSourcesInGroupHaveAnyFuel".Translate() + ".");
		}
		else
		if (this.AnyInGroupHasAnythingLeftToLoad)
		{
			sb.AppendLine("NotReadyForLaunch".Translate() + ": " +
			              "TransportPodInGroupHasSomethingLeftToLoad".Translate() + ".");
		}

		sb.AppendLine("ReadyForLaunch".Translate());
		var container = Transporter.innerContainer.ToList();
		var pawnCount = container.Count(t => t is Pawn);
		var val = CollectionsMassCalculator.MassUsage(container, IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload, true);
		sb.AppendLine("CA_PassengerCapacity".Translate($"{pawnCount}/{SRTSProps.maxPassengers}"));
		sb.AppendLine("CA_Storage".Translate($"{Math.Round(val, 1)}/{Transporter.MassCapacity}"));
		return sb.ToString().TrimEndNewlines();
	}

	private void StartChoosingDestination()
	{
		CameraJumper.TryJump(CameraJumper.GetWorldTarget(parent));
		Find.WorldSelector.ClearSelection();
		int tile = this.parent.Map.Tile;
		carr = null;

		/* SOS2 Compatibility Section */
		if(SRTSHelper.SOS2ModLoaded)
		{
			if (this.parent.Map.Parent.def.defName == "ShipOrbiting")
			{
				Find.WorldTargeter.BeginTargeting(new Func<GlobalTargetInfo, bool>(this.ChoseWorldTarget), true, TargeterMouseAttachment, true, null, delegate (GlobalTargetInfo target)
				{
					if (!target.IsValid || this.parent.TryGetComp<CompRefuelable>() == null || this.parent.TryGetComp<CompRefuelable>().FuelPercentOfMax == 1.0f)
					{
						return null;
					}

					if (target.WorldObject != null && target.WorldObject.GetType().IsAssignableFrom(SRTSHelper.SpaceSiteType))
					{
						/*if (this.parent.TryGetComp<CompRefuelable>().FuelPercentOfMax >= ((SRTSHelper.SpaceSite.worldObjectClass)target.WorldObject).fuelCost / 100f)
						    return null;
						return "MessageShuttleNeedsMoreFuel".Translate(((SpaceSite)target.WorldObject).fuelCost);*/
						return null;
					}
					return "MessageShuttleMustBeFullyFueled".Translate();
				});
			}
			else if (this.parent.Map.Parent.GetType().IsAssignableFrom(SRTSHelper.SpaceSiteType))
			{
				Find.WorldTargeter.BeginTargeting(new Func<GlobalTargetInfo, bool>(this.ChoseWorldTarget), true, TargeterMouseAttachment, true, null, delegate (GlobalTargetInfo target)
				{
					if (target.WorldObject == null || (!(target.WorldObject.def == SRTSHelper.SpaceSite) && !(target.WorldObject.def.defName == "ShipOrbiting")))
					{
						return "MessageOnlyOtherSpaceSites".Translate();
					}
					return null;
					/*if (this.parent.TryGetComp<CompRefuelable>().FuelPercentOfMax >= ((SpaceSite)this.parent.Map.Parent).fuelCost / 100f)
					    return null;
					return "MessageShuttleNeedsMoreFuel".Translate(((SpaceSite)this.parent.Map.Parent).fuelCost);*/
				});
			}
		}
		/* -------------------------- */
		Find.WorldTargeter.BeginTargeting(new Func<GlobalTargetInfo, bool>(this.ChoseWorldTarget), true, TargeterMouseAttachment, true, (() => GenDraw.DrawWorldRadiusRing(tile, this.MaxLaunchDistance)), (target =>
		{
			if (!target.IsValid)
				return null;
			int num = Find.WorldGrid.TraversalDistanceBetween(tile, target.Tile);
			if (num > this.MaxLaunchDistance)
			{
				GUI.color = Color.red;
				if (num > this.MaxLaunchDistanceEverPossible)
					return "TransportPodDestinationBeyondMaximumRange".Translate();
				return "TransportPodNotEnoughFuel".Translate();
			}

			if( target.WorldObject?.def?.defName == "ShipOrbiting" || (target.WorldObject?.GetType()?.IsAssignableFrom(SRTSHelper.SpaceSiteType) ?? false))
			{
				return null;
			}

			IEnumerable<FloatMenuOption> floatMenuOptionsAt = this.GetTransportPodsFloatMenuOptionsAt(target.Tile, (Caravan) null);
			if (!floatMenuOptionsAt.Any<FloatMenuOption>())
			{
				if (Find.WorldGrid[target.Tile].biome.impassable || Find.World.Impassable(target.Tile))
					return "MessageTransportPodsDestinationIsInvalid".Translate();
				return string.Empty;
			}
			if (floatMenuOptionsAt.Count<FloatMenuOption>() == 1)
			{
				if (floatMenuOptionsAt.First<FloatMenuOption>().Disabled)
					GUI.color = Color.red;
				return floatMenuOptionsAt.First<FloatMenuOption>().Label;
			}
			MapParent worldObject = target.WorldObject as MapParent;
			if (worldObject == null)
				return "ClickToSeeAvailableOrders_Empty".Translate();
			return "ClickToSeeAvailableOrders_WorldObject".Translate(worldObject.LabelCap);
		}));
	}

	public void WorldStartChoosingDestination(Caravan car)
	{
		CameraJumper.TryJump(CameraJumper.GetWorldTarget((GlobalTargetInfo) ((WorldObject) car)));
		Find.WorldSelector.ClearSelection();
		int tile = car.Tile;
		this.carr = car;
		Find.WorldTargeter.BeginTargeting(new Func<GlobalTargetInfo, bool>(this.ChoseWorldTarget), true, CompLaunchableSRTS.TargeterMouseAttachment, false, (Action) (() => GenDraw.DrawWorldRadiusRing(car.Tile, this.MaxLaunchDistance)), (Func<GlobalTargetInfo, string>) (target =>
		{
			if (!target.IsValid)
				return (string) null;
			int num = Find.WorldGrid.TraversalDistanceBetween(tile, target.Tile, true, int.MaxValue);
			if (num > this.MaxLaunchDistance)
			{
				GUI.color = Color.red;
				if (num > this.MaxLaunchDistanceEverPossible)
					return "TransportPodDestinationBeyondMaximumRange".Translate();
				return "TransportPodNotEnoughFuel".Translate();
			}
			IEnumerable<FloatMenuOption> floatMenuOptionsAt = this.GetTransportPodsFloatMenuOptionsAt(target.Tile, car);
			if (!floatMenuOptionsAt.Any<FloatMenuOption>())
			{
				if (Find.WorldGrid[target.Tile].biome.impassable || Find.World.Impassable(target.Tile))
					return "MessageTransportPodsDestinationIsInvalid".Translate();
				return string.Empty;
			}
			if (floatMenuOptionsAt.Count<FloatMenuOption>() == 1)
			{
				if (floatMenuOptionsAt.First<FloatMenuOption>().Disabled)
					GUI.color = Color.red;
				return floatMenuOptionsAt.First<FloatMenuOption>().Label;
			}
			MapParent worldObject = target.WorldObject as MapParent;
			if (worldObject == null)
				return "ClickToSeeAvailableOrders_Empty".Translate();
			return "ClickToSeeAvailableOrders_WorldObject".Translate(worldObject.LabelCap);
		}));
	}

	private bool ChoseWorldTarget(GlobalTargetInfo target)
	{
		if (this.carr == null && !this.LoadingInProgressOrReadyToLaunch)
			return true;
		if (!target.IsValid)
		{
			Messages.Message("MessageTransportPodsDestinationIsInvalid".Translate(), MessageTypeDefOf.RejectInput, false);
			return false;
		}
            
		int num = Find.WorldGrid.TraversalDistanceBetween(this.carr != null ? this.carr.Tile : this.parent.Map.Tile, target.Tile, true, int.MaxValue);
		if (num > this.MaxLaunchDistance)
		{
			Messages.Message("MessageTransportPodsDestinationIsTooFar".Translate(CompLaunchableSRTS.FuelNeededToLaunchAtDist((float) num, this.BaseFuelPerTile).ToString("0.#")), MessageTypeDefOf.RejectInput, false);
			return false;
		}
		if( (Find.WorldGrid[target.Tile].biome.impassable || Find.World.Impassable(target.Tile)) && (!SRTSHelper.SOS2ModLoaded || target.WorldObject?.def?.defName != "ShipOrbiting"))
		{
			Messages.Message("MessageTransportPodsDestinationIsInvalid".Translate(), MessageTypeDefOf.RejectInput, false);
			return false;
		}
		if(SRTSHelper.SOS2ModLoaded && target.WorldObject?.def?.defName == "ShipOrbiting")
		{
			if (!SRTSMod.GetStatFor<bool>(this.parent.def.defName, StatName.spaceFaring))
			{
				Messages.Message("NonSpaceFaringSRTS".Translate(parent.def.defName), MessageTypeDefOf.RejectInput, false);
				return false;
			}
			if(SRTSMod.GetStatFor<bool>(parent.def.defName, StatName.shuttleBayLanding))
			{ 
				IntVec3 shuttleBayPos = (IntVec3)AccessTools.Method(type: SRTSHelper.SOS2LaunchableType, "FirstShuttleBayOpen").Invoke(null, new object[] { (target.WorldObject as MapParent).Map });
				if (shuttleBayPos == IntVec3.Zero)
				{
					Messages.Message("NeedOpenShuttleBay".Translate(), MessageTypeDefOf.RejectInput);
					return false;
				}
				ConfirmLaunch(target.Tile, new TransportPodsArrivalAction_LandInSpecificCell((target.WorldObject as MapParent).Map.Parent, shuttleBayPos));
				return true;
			}
		}
            
		IEnumerable<FloatMenuOption> floatMenuOptionsAt = this.GetTransportPodsFloatMenuOptionsAt(target.Tile, this.carr);
		if (!floatMenuOptionsAt.Any<FloatMenuOption>())
		{
			if(Find.WorldGrid[target.Tile].biome.impassable || Find.World.Impassable(target.Tile))
			{
				Messages.Message("MessageTransportPodsDestinationIsInvalid".Translate(), MessageTypeDefOf.RejectInput, false);
				return false;
			}
			ConfirmLaunch(target.Tile, (TransportPodsArrivalAction) null, (Caravan) null);
			return true;
		}
		if (floatMenuOptionsAt.Count<FloatMenuOption>() == 1)
		{
			if (!floatMenuOptionsAt.First<FloatMenuOption>().Disabled)
				floatMenuOptionsAt.First<FloatMenuOption>().action();
			return false;
		}
		Find.WindowStack.Add((Window) new FloatMenu(floatMenuOptionsAt.ToList<FloatMenuOption>()));
		return false;
	}

	private void ConfirmLaunch(int destinationTile, TransportPodsArrivalAction arrivalAction, Caravan cafr = null)
	{
		var map = this.parent?.MapHeld;
		if (map != null)
		{
			var dist = Find.WorldGrid.TraversalDistanceBetween(map.Tile, destinationTile);
			if (dist >= SRTSMod.mod.settings.confirmDistance)
			{
				Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CA_ConfirmationScreen".Translate(), () => TryLaunch(destinationTile, arrivalAction, cafr)));
				return;
			}
		}
		    
		//
		TryLaunch(destinationTile, arrivalAction, cafr);
	}

	public void TryLaunch(int destinationTile, TransportPodsArrivalAction arrivalAction, Caravan cafr = null)
	{
		if (cafr == null && !this.parent.Spawned)
		{
			Log.Error("Tried to launch " + (object) this.parent + ", but it's unspawned.");
		}
		else
		{
			if (this.parent.Spawned && !this.LoadingInProgressOrReadyToLaunch || (!this.AllInGroupConnectedToFuelingPort || !this.AllFuelingPortSourcesInGroupHaveAnyFuel))
				return;
			if(cafr == null)
			{
				Map map = this.parent.Map;
				int num = Find.WorldGrid.TraversalDistanceBetween(map.Tile, destinationTile, true, int.MaxValue);
				if (num > this.MaxLaunchDistance)
					return;
				this.Transporter.TryRemoveLord(map);
				int groupId = this.Transporter.groupID;
				float amount = Mathf.Max(CompLaunchableSRTS.FuelNeededToLaunchAtDist((float) num, this.BaseFuelPerTile), 1f);
				CompTransporter comp1 = this.FuelingPortSource.TryGetComp<CompTransporter>();
				Building fuelingPortSource = this.FuelingPortSource;
				if (fuelingPortSource != null)
					fuelingPortSource.TryGetComp<CompRefuelable>().ConsumeFuel(amount);
				ThingOwner directlyHeldThings = comp1.GetDirectlyHeldThings();

				// Neceros Edit
				Thing thing = ThingMaker.MakeThing(ThingDef.Named(parent.def.defName), (ThingDef) null);
				thing.SetFactionDirect(Faction.OfPlayer);
				thing.Rotation = this.FuelingPortSource.Rotation;
				CompRefuelable comp2 = thing.TryGetComp<CompRefuelable>();
				comp2.GetType().GetField("fuel", BindingFlags.Instance | BindingFlags.NonPublic).SetValue((object) comp2, (object) fuelingPortSource.TryGetComp<CompRefuelable>().Fuel);
				comp2.TargetFuelLevel = fuelingPortSource.TryGetComp<CompRefuelable>().TargetFuelLevel;
				thing.stackCount = 1;
				thing.HitPoints = parent.HitPoints;
				directlyHeldThings.TryAddOrTransfer(thing, true);

				// Neceros Edit
				ActiveDropPod activeDropPod = (ActiveDropPod) ThingMaker.MakeThing(SRTSStatic.SkyfallerActiveDefByRot(this), null);
				activeDropPod.HitPoints = parent.HitPoints;
				activeDropPod.Contents = new ActiveDropPodInfo();
				activeDropPod.Contents.innerContainer.TryAddRangeOrTransfer((IEnumerable<Thing>) directlyHeldThings, true, true);

				// Neceros Edit
				SRTSLeaving srtsLeaving = (SRTSLeaving) SkyfallerMaker.MakeSkyfaller(SkyfallerLeavingDefByRot(), activeDropPod);
				srtsLeaving.rotation = this.FuelingPortSource.Rotation;
				srtsLeaving.groupID = groupId;
				srtsLeaving.destinationTile = destinationTile;
				srtsLeaving.arrivalAction = arrivalAction;
				comp1.CleanUpLoadingVars(map);
				IntVec3 position = fuelingPortSource.Position;
				SRTSStatic.SRTSDestroy((Thing) fuelingPortSource, DestroyMode.Vanish);
				GenSpawn.Spawn((Thing) srtsLeaving, position, map, WipeMode.Vanish);
				CameraJumper.TryHideWorld();
			}
			else
			{
				int num = Find.WorldGrid.TraversalDistanceBetween(this.carr.Tile, destinationTile, true, int.MaxValue);
				if (num > this.MaxLaunchDistance)
					return;
				float amount = Mathf.Max(CompLaunchableSRTS.FuelNeededToLaunchAtDist((float) num, this.BaseFuelPerTile), 1f);
				if (this.FuelingPortSource != null)
					this.FuelingPortSource.TryGetComp<CompRefuelable>().ConsumeFuel(amount);
				ThingOwner<Pawn> directlyHeldThings = (ThingOwner<Pawn>) cafr.GetDirectlyHeldThings();
				Thing thing = null;
				foreach (Pawn pawn in directlyHeldThings.InnerListForReading)
				{
					Pawn_InventoryTracker inventory = pawn.inventory;
					for (int index = 0; index < inventory.innerContainer.Count; ++index)
					{
						// Neceros Edit
						if (inventory.innerContainer[index].TryGetComp<CompLaunchableSRTS>() != null)
						{
							thing = inventory.innerContainer[index];
							inventory.innerContainer[index].holdingOwner.Remove(inventory.innerContainer[index]);
							break;
						}
					}
				}
				/*Add caravan items to SRTS - SmashPhil */
				foreach(Pawn p in directlyHeldThings.InnerListForReading)
				{
					p.inventory.innerContainer.InnerListForReading.ForEach(AddThingsToSRTS);
					p.inventory.innerContainer.Clear();
				}

				ThingOwner<Thing> thingOwner = new ThingOwner<Thing>();
				foreach (Pawn pawn in directlyHeldThings.AsEnumerable<Pawn>().ToList<Pawn>())
					thingOwner.TryAddOrTransfer((Thing) pawn, true);
				if (thing != null && thing.holdingOwner == null)
					thingOwner.TryAddOrTransfer(thing, false);

				// Neceros Edit
				ActiveDropPod activeDropPod = (ActiveDropPod) ThingMaker.MakeThing(SRTSStatic.SkyfallerActiveDefByRot(this), (ThingDef) null);
				activeDropPod.Contents = new ActiveDropPodInfo();
				activeDropPod.Contents.innerContainer.TryAddRangeOrTransfer((IEnumerable<Thing>) thingOwner, true, true);
				activeDropPod.Contents.innerContainer.TryAddRangeOrTransfer((IEnumerable<Thing>) thingsInsideShip, true, true);
				thingsInsideShip.Clear();

				cafr.RemoveAllPawns();
				if(!cafr.Destroyed)
					cafr.Destroy();
				TravelingSRTS travelingTransportPods = (TravelingSRTS) WorldObjectMaker.MakeWorldObject(StaticDefOf.TravelingSRTS_Carryall);
				travelingTransportPods.Tile = cafr.Tile;
				travelingTransportPods.SetFaction(Faction.OfPlayer);
				travelingTransportPods.destinationTile = destinationTile;
				travelingTransportPods.arrivalAction = arrivalAction;
				travelingTransportPods.flyingThing = this.parent;
				Find.WorldObjects.Add((WorldObject) travelingTransportPods);
				travelingTransportPods.AddPod(activeDropPod.Contents, true);
				activeDropPod.Contents = (ActiveDropPodInfo) null;
				activeDropPod.Destroy(DestroyMode.Vanish);
				Find.WorldTargeter.StopTargeting();
			}
		}
	}

	private ThingDef SkyfallerLeavingDefByRot()
	{
		if (parent.Rotation == Rot4.East)
		{
			return SRTSProps.eastSkyfaller;
		}
		else if (parent.Rotation == Rot4.West)
		{
			return SRTSProps.westSkyfaller;
		}

		return SRTSProps.eastSkyfaller;
	}
	    
	public void Notify_FuelingPortSourceDeSpawned()
	{
		if (!this.Transporter.CancelLoad())
			return;
		Messages.Message("MessageTransportersLoadCanceled_FuelingPortGiverDeSpawned".Translate(), (LookTargets) ((Thing) this.parent), MessageTypeDefOf.NegativeEvent, true);
	}

	public static int MaxLaunchDistanceAtFuelLevel(float fuelLevel, float costPerTile)
	{
		return Mathf.FloorToInt(fuelLevel / costPerTile);
	}

	public static float FuelNeededToLaunchAtDist(float dist, float cost)
	{
		return cost * dist;
	}

	public IEnumerable<FloatMenuOption> GetTransportPodsFloatMenuOptionsAt(int tile, Caravan car = null)
	{
		bool anything = false;
		IEnumerable<IThingHolder> pods = this.TransportersInGroup.Cast<IThingHolder>();
		if (car != null)
		{
			List<Caravan> rliss = new List<Caravan>();
			rliss.Add(car);
			pods = rliss.Cast<IThingHolder>();
			rliss = (List<Caravan>) null;
		}
		if (car == null)
		{
			if(TransportPodsArrivalAction_FormCaravan.CanFormCaravanAt(pods, tile) && !Find.WorldObjects.AnySettlementBaseAt(tile) && !Find.WorldObjects.AnySiteAt(tile))
			{
				anything = true;
				yield return new FloatMenuOption("FormCaravanHere".Translate(), (() => this.ConfirmLaunch(tile, new TransportPodsArrivalAction_FormCaravan(), car)));
			}
		}
		else if (!Find.WorldObjects.AnySettlementBaseAt(tile) && !Find.WorldObjects.AnySiteAt(tile) && !Find.World.Impassable(tile))
		{
			anything = true;
			yield return new FloatMenuOption("FormCaravanHere".Translate(), (() => this.ConfirmLaunch(tile, new TransportPodsArrivalAction_FormCaravan(), car)));
		}
		List<WorldObject> worldObjects = Find.WorldObjects.AllWorldObjects;
		for (int i = 0; i < worldObjects.Count; ++i)
		{
			if (worldObjects[i].Tile == tile)
			{
				IEnumerable<FloatMenuOption> nowre = SRTSStatic.getFM(worldObjects[i], pods, this, car);
				if (nowre.ToList<FloatMenuOption>().Count < 1)
				{
					yield return new FloatMenuOption("FormCaravanHere".Translate(), (() => this.ConfirmLaunch(tile, (TransportPodsArrivalAction) new TransportPodsArrivalAction_FormCaravan(), car)));
				}
				else
				{
					foreach (FloatMenuOption floatMenuOption in nowre)
					{
						FloatMenuOption o = floatMenuOption;
						anything = true;
						yield return o;
						o = (FloatMenuOption) null;
					}
				}
				nowre = (IEnumerable<FloatMenuOption>) null;
			}
		}

		if (!anything && !Find.World.Impassable(tile))
			yield return new FloatMenuOption("TransportPodsContentsWillBeLost".Translate(),
				(() => ConfirmLaunch(tile, (TransportPodsArrivalAction) null)));
	}
	    
}