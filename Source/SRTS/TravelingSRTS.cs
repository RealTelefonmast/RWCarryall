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
        //
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

        public Vector3 Direction => (DrawPos - Find.WorldGrid.GetTileCenter(this.destinationTile)).normalized;
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref flyingThing, "flyingThing");
        }
        
        public override void Draw()
        {
            var pos = DrawPos;
            var normalized = pos.normalized;
            var size = Mathf.Lerp(0.5f, 1f, ExpandableWorldObjectsUtility.TransitionPct);
            size = Mathf.Lerp(size, 1.5f, ExpandableWorldObjectsUtility.ExpandMoreTransitionPct); 
            
            Quaternion q = Quaternion.LookRotation(Vector3.Cross(normalized, Vector3.up), normalized) * Quaternion.Euler(0, Direction.AngleFlat(), 0);
            Vector3 s = new Vector3(size, 1f, size);
            Matrix4x4 matrix = default(Matrix4x4);
            matrix.SetTRS(pos + normalized * 0.015f, q, s);
            Graphics.DrawMesh(MeshPool.plane10, matrix, Material, WorldCameraManager.WorldLayer);
        }
    }
}
