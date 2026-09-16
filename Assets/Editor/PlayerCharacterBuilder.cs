using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EarthRecovery.Editor
{
    public static class PlayerCharacterBuilder
    {
        public const string Source = "Assets/Character/Meshy_AI_Neon_Vanguard_All_Animations.glb";
        public const string Output = "Assets/Character/Generated";
        public const string PrefabPath = "Assets/Resources/PlayerCharacter.prefab";

        [MenuItem("Earth Recovery/Rebuild Player Character")]
        public static void Build() => BuildCharacter(Source, Output, PrefabPath, "Neon Vanguard");

        public static void BuildCharacter(string sourcePath, string output, string prefabPath, string modelName)
        {
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("Player character GLB is missing", sourcePath);
            AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(sourcePath);
            var serializedImporter = new SerializedObject(importer);
            var animationMethod = serializedImporter.FindProperty("importSettings.animationMethod");
            if (animationMethod == null) throw new InvalidOperationException("Unity glTFast importer settings are missing.");
            int mecanim = Array.IndexOf(animationMethod.enumNames, "Mecanim");
            if (animationMethod.enumValueIndex != mecanim)
            {
                animationMethod.enumValueIndex = mecanim;
                serializedImporter.ApplyModifiedPropertiesWithoutUndo();
                importer.SaveAndReimport();
            }
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null) throw new InvalidOperationException("GLB import failed. Install Unity glTFast and enable the Animation module.");
            var clips = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>().ToArray();
            foreach (string required in new[] { "restpose", "Walking", "Running" })
                if (clips.Count(c => c.name == required) != 1) throw new InvalidOperationException("Missing or ambiguous animation: " + required);
            Directory.CreateDirectory(output);
            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath));
            AssetDatabase.Refresh();
            var root = new GameObject("Player Character");
            try
            {
                var facing = new GameObject("Facing").transform; facing.SetParent(root.transform, false);
                var model = Object.Instantiate(source, facing, false); model.name = modelName;
                foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
                foreach (var old in model.GetComponentsInChildren<Animation>(true)) Object.DestroyImmediate(old);
                foreach (var old in model.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(old);
                var animator = model.AddComponent<Animator>();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
                if (renderers.Length == 0) throw new InvalidOperationException("The character has no skinned mesh.");
                clips.Single(c => c.name == "restpose").SampleAnimation(model, 0);
                var bounds = RestBounds(renderers);
                foreach (var renderer in renderers)
                {
                    renderer.updateWhenOffscreen = true;
                    if (renderer.sharedMaterial == null || renderer.sharedMaterial.shader == null)
                        throw new InvalidOperationException("Missing character material.");
                }
                if (bounds.size.y < .1f) throw new InvalidOperationException("Invalid character bounds.");
                float scale = 1.8f / bounds.size.y;
                model.transform.localScale = Vector3.one * scale;
                model.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * scale;
                var hips = model.GetComponentsInChildren<Transform>().Single(t => t.name == "mixamorig:Hips");
                string hipsPath = AnimationUtility.CalculateTransformPath(hips, model.transform);
                var idle = MakeClip(clips.Single(c => c.name == "restpose"), "Idle", hipsPath, hips.localPosition, true, output);
                var walk = MakeClip(clips.Single(c => c.name == "Walking"), "Walk", hipsPath, hips.localPosition, false, output);
                var run = MakeClip(clips.Single(c => c.name == "Running"), "Run", hipsPath, hips.localPosition, false, output);
                var fast = clips.FirstOrDefault(c => c.name == "RunFast");
                var sprint = MakeClip(fast != null ? fast : clips.Single(c => c.name == "Running"), "Sprint", hipsPath, hips.localPosition, false, output);
                animator.runtimeAnimatorController = MakeController(output, fast != null ? 1 : 1.2f, idle, walk, run, sprint);
                var avatar = root.AddComponent<PlayerAvatar>(); avatar.animator = animator; avatar.facing = facing;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                foreach (var clip in clips) Debug.Log($"CHARACTER_CLIP {clip.name} length={clip.length:F3} legacy={clip.legacy}");
                Debug.Log($"CHARACTER_BUILD_PASS mesh={renderers[0].sharedMesh.vertexCount} height={bounds.size.y:F3} scale={scale:F3} hips={hipsPath}");
            }
            finally { Object.DestroyImmediate(root); }
        }

        static AnimationClip MakeClip(AnimationClip source, string name, string hipsPath, Vector3 origin, bool still, string output)
        {
            var clip = Object.Instantiate(source); clip.name = name; clip.legacy = false;
            var bindings = AnimationUtility.GetCurveBindings(clip);
            float start = bindings.Min(b => AnimationUtility.GetEditorCurve(clip, b).keys[0].time);
            float duration = still ? 1 : source.length - start;
            foreach (var binding in bindings)
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (still) curve = AnimationCurve.Constant(0, duration, curve.Evaluate(start));
                else
                {
                    var keys = curve.keys;
                    for (int i = 0; i < keys.Length; i++) keys[i].time -= start;
                    curve.keys = keys;
                }
                AnimationUtility.SetEditorCurve(clip, binding, curve);
                // Keep vertical hip bob, but remove authored travel from the visual rig.
                if (binding.path == hipsPath && binding.propertyName == "m_LocalPosition.x")
                    AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0, duration, origin.x));
                if (binding.path == hipsPath && binding.propertyName == "m_LocalPosition.z")
                    AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0, duration, origin.z));
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true; settings.loopBlend = true;
            settings.startTime = 0; settings.stopTime = duration;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            clip.EnsureQuaternionContinuity();
            string path = output + "/" + name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null) { AssetDatabase.CreateAsset(clip, path); return clip; }
            EditorUtility.CopySerialized(clip, existing); Object.DestroyImmediate(clip);
            EditorUtility.SetDirty(existing); return existing;
        }

        static Bounds RestBounds(SkinnedMeshRenderer[] renderers)
        {
            var bounds = new Bounds(); bool first = true;
            var baked = new Mesh();
            try
            {
                foreach (var renderer in renderers)
                {
                    renderer.BakeMesh(baked, true);
                    foreach (var vertex in baked.vertices)
                    {
                        var point = renderer.transform.TransformPoint(vertex);
                        if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                        else bounds.Encapsulate(point);
                    }
                }
            }
            finally { Object.DestroyImmediate(baked); }
            return bounds;
        }

        [MenuItem("Earth Recovery/Preview Player Character")]
        public static void Preview() => PreviewCharacter(PrefabPath, Output, "QA/Character");

        public static void PreviewCharacter(string prefabPath, string clipsFolder, string output)
        {
            Directory.CreateDirectory(output);
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            var camera = new GameObject("Character preview camera").AddComponent<Camera>();
            var light = new GameObject("Character preview light").AddComponent<Light>();
            var target = new RenderTexture(600, 720, 24);
            var image = new Texture2D(600, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var ambient = RenderSettings.ambientLight;
            bool fog = RenderSettings.fog;
            try
            {
                var avatar = root.GetComponent<PlayerAvatar>(); avatar.animator.enabled = false;
                camera.orthographic = true; camera.orthographicSize = 1.08f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.19f, .22f, .24f);
                camera.targetTexture = target;
                light.type = LightType.Directional; light.intensity = 1.8f; light.transform.rotation = Quaternion.Euler(35, 160, 0);
                RenderSettings.ambientLight = new Color(.7f, .7f, .7f); RenderSettings.fog = false;
                foreach (var name in new[] { "Idle", "Back", "Walk", "Sprint" })
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipsFolder + "/" + (name == "Back" ? "Idle" : name) + ".anim");
                    clip.SampleAnimation(avatar.animator.gameObject, name == "Idle" || name == "Back" ? 0 : clip.length * .3f);
                    camera.transform.position = new Vector3(0, 1, name == "Back" ? -4 : 4);
                    camera.transform.LookAt(new Vector3(0, .9f, 0));
                    camera.Render(); camera.Render(); RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 600, 720), 0, 0); image.Apply();
                    File.WriteAllBytes(output + "/" + name + ".png", image.EncodeToPNG());
                }
            }
            finally
            {
                RenderSettings.ambientLight = ambient; RenderSettings.fog = fog;
                camera.targetTexture = null; RenderTexture.active = previous; target.Release();
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                Object.DestroyImmediate(root); Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(light.gameObject);
            }
        }

        static AnimatorController MakeController(string output, float sprintRate, params AnimationClip[] clips)
        {
            string path = output + "/PlayerCharacter.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            if (!controller.parameters.Any(p => p.name == "Gait")) controller.AddParameter("Gait", AnimatorControllerParameterType.Float);
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Locomotion") ?? machine.AddState("Locomotion");
            var tree = state.motion as BlendTree;
            if (tree == null)
            {
                tree = new BlendTree { name = "Locomotion", blendType = BlendTreeType.Simple1D, blendParameter = "Gait", useAutomaticThresholds = false };
                AssetDatabase.AddObjectToAsset(tree, controller); state.motion = tree;
            }
            tree.children = clips.Select((clip, i) => new ChildMotion { motion = clip, threshold = i, timeScale = i == 3 ? sprintRate : 1 }).ToArray();
            machine.defaultState = state;
            EditorUtility.SetDirty(tree); EditorUtility.SetDirty(state); EditorUtility.SetDirty(machine); EditorUtility.SetDirty(controller);
            return controller;
        }
    }
}
