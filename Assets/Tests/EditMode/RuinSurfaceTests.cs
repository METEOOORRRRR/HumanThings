using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EarthRecovery.Tests
{
    public sealed class RuinSurfaceTests
    {
        [Test] public void BackgroundChangesAndRestoresWhilePoiAndGeometryStayIntact()
        {
            var world=new GameObject("world"); var chunk=new GameObject("chunk");chunk.transform.SetParent(world.transform);
            var bg=GameObject.CreatePrimitive(PrimitiveType.Cube);bg.transform.SetParent(chunk.transform);
            var poi=GameObject.CreatePrimitive(PrimitiveType.Cube);poi.transform.SetParent(world.transform);
            var source=new Material(Shader.Find("HumanThings/Weathered Environment")){name="HT_Concrete_Test"};
            bg.GetComponent<Renderer>().sharedMaterial=source;poi.GetComponent<Renderer>().sharedMaterial=source;
            var mesh=bg.GetComponent<MeshFilter>().sharedMesh;var collider=bg.GetComponent<Collider>();
            var json=EditorJsonUtility.ToJson(source);var fog=RenderSettings.fogColor;
            world.AddComponent<GeneratedWorldRoot>().result=new GeneratedWorldResult{root=world,chunks=new(){new GeneratedCityChunk{city=new GeneratedCityResult{root=chunk}}}};
            try
            {
                var overlay=world.AddComponent<HumanThingsRuinSurfaces>();overlay.ApplyDefault();
                Assert.That(overlay.IsApplied,Is.True);
                Assert.That(bg.GetComponent<Renderer>().sharedMaterial.shader.name,Is.EqualTo("HumanThings/Ruined Environment"));
                Assert.That(poi.GetComponent<Renderer>().sharedMaterial,Is.SameAs(source));
                Assert.That(bg.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(mesh));
                Assert.That(bg.GetComponent<Collider>(),Is.SameAs(collider));
                Assert.That(RenderSettings.fogColor,Is.EqualTo(fog));
                Assert.That(EditorJsonUtility.ToJson(source),Is.EqualTo(json));
                overlay.ApplyDefault();overlay.Restore();overlay.Restore();
                Assert.That(bg.GetComponent<Renderer>().sharedMaterial,Is.SameAs(source));
            }
            finally{Object.DestroyImmediate(world);Object.DestroyImmediate(source);}
        }
        [Test] public void SeparateProfileAndShaderAreAvailable()
        {
            var p=Resources.Load<HumanThingsRuinProfile>("HumanThingsRuinProfile");
            var baseline=Resources.Load<HumanThingsVisualProfile>("HumanThingsVisualProfile");
            Assert.That(p,Is.Not.Null); Assert.That(p.shader,Is.Not.Null);
            Assert.That(p.shader,Is.Not.EqualTo(baseline.environmentShader));
            Assert.That(ShaderUtil.ShaderHasError(p.shader),Is.False);
        }
        [Test] public void ObjectsOutsideGeneratedChunksRemainUntouched()
        {
            var root=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var renderer=root.GetComponent<Renderer>(); var material=renderer.sharedMaterial;
            try
            {
                var overlay=root.AddComponent<HumanThingsRuinSurfaces>();
                overlay.ApplyDefault();
                Assert.That(renderer.sharedMaterial,Is.SameAs(material));
                Assert.That(overlay.IsApplied,Is.False);
                overlay.Restore();overlay.Restore();
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
