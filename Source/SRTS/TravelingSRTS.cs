using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;

namespace SRTS
{
    public class TravelingSRTS : TravelingTransportPods
    {
        public Thing flyingThing;
        private Material material;
        private const float ExpandingResize = 35f;
        private const float TransitionTakeoff = 0.015f;
        private float transitionSize = 0f;

        private Material SRTSMaterial
        {
            get
            {
                if (flyingThing is null)
                    return this.Material;
                
                if(flyingThing.Rotation == Rot4.West)
                    return flyingThing.Graphic.MatEast;
                if(flyingThing.Rotation == Rot4.East)
                    return flyingThing.Graphic.MatWest;
                
                if(material is null)
                {
                    string texPath = flyingThing.def.graphicData.texPath;
                    GraphicRequest graphicRequest = new GraphicRequest(flyingThing.def.graphicData.graphicClass, flyingThing.def.graphicData.texPath, ShaderTypeDefOf.Cutout.Shader, flyingThing.def.graphic.drawSize,
                       Color.white, Color.white, flyingThing.def.graphicData, 0, null, null);
                    if(graphicRequest.graphicClass == typeof(Graphic_Multi))
                        texPath += "_north";
                    material = MaterialPool.MatFrom(texPath, ShaderDatabase.WorldOverlayTransparentLit, WorldMaterials.WorldObjectRenderQueue);
                }
                return ExpandableWorldObjectsUtility.TransitionPct > 0 ? flyingThing.Graphic.MatNorth : material;
            }
        }

        public override void SpawnSetup()
        {
            base.SpawnSetup();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref flyingThing, "flyingThing");
        }
        
        public override void Draw()
        {
            if(!SRTSMod.mod.settings.dynamicWorldDrawingSRTS)
            {
                base.Draw();
                return;
            }
            
            if (!this.HiddenBehindTerrainNow())
            {
                float averageTileSize = Find.WorldGrid.averageTileSize;
                float transitionPct = ExpandableWorldObjectsUtility.TransitionPct;
            
                if(transitionSize < 1)
                    transitionSize += TransitionTakeoff * (int)Find.TickManager.CurTimeSpeed;
                float drawPct = (1 + (transitionPct * Find.WorldCameraDriver.AltitudePercent * ExpandingResize)) * transitionSize;

                Vector3 normalized = this.DrawPos.normalized;
                //var rotation = flyingThing.Rotation == Rot4.West ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.Euler(0f, 180f, 0f);
                Quaternion quat = Quaternion.LookRotation(Vector3.Cross(normalized, Direction), normalized);// * rotation;
                
                Vector3 s = new Vector3(averageTileSize * 0.7f * drawPct, 5f, averageTileSize * 0.7f * drawPct);
                Matrix4x4 matrix = default;
                matrix.SetTRS(this.DrawPos + normalized * 0.015f, quat, s); //Direction.ToAngleFlat().ToQuat()

                Mesh mesh = MeshPool.plane10;
                if ((flyingThing.Rotation == Rot4.West && flyingThing.Graphic.WestFlipped) || (flyingThing.Rotation == Rot4.East && flyingThing.Graphic.EastFlipped))
                {
                    mesh = MeshPool.GridPlaneFlip(s);
                }
                
                Graphics.DrawMesh(mesh, matrix, SRTSMaterial, WorldCameraManager.WorldLayer);
            }
        }

        public Vector3 Direction => (DrawPos - Find.WorldGrid.GetTileCenter(this.destinationTile)).normalized;
    }
}
