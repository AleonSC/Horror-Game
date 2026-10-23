using System.Collections.Generic;
using Game.Core;
using Game.Player;
using Game.Rendering;
using Game.World;
using Game.World.Build;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Editor
{
    // The test scenes' lamp, scour stand-in, camera and player.
    public static partial class LookTestSceneBuilder
    {
        static void BuildLamp(Dictionary<string, Material> mats)
        {
            Vector2 at = Clearing + new Vector2(2f, 2f);
            float y = GroundHeight(at.x, at.y);
            var root = new GameObject("Lamp Post") { layer = Layers.Props }.transform;
            root.position = new Vector3(at.x, y, at.y);

            Primitive(PrimitiveType.Cylinder, "Post", root, new Vector3(0f, 1.6f, 0f), new Vector3(0.12f, 1.6f, 0.12f), mats["metal"], Layers.Props, keepCollider: true);
            Primitive(PrimitiveType.Cube, "Head", root, new Vector3(0f, 3.35f, 0f), new Vector3(0.45f, 0.12f, 0.45f), mats["metal"], Layers.Props);
            Primitive(PrimitiveType.Cube, "Glass", root, new Vector3(0f, 3.12f, 0f), new Vector3(0.32f, 0.34f, 0.32f), mats["glass"], Layers.Props);

            var lightGo = new GameObject("Lamp Light");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, 3.0f, 0f);
            lightGo.AddHDLight(LightType.Point);
            var light = lightGo.GetComponent<Light>();
            light.color = new Color(1f, 0.75f, 0.45f);
            light.intensity = 25f; // candela
            light.range = 13f;
            light.shadows = LightShadows.Soft;
            lightGo.AddComponent<FogGlowLight>();
        }

        // A lamp to crank (click it): dark, with a blinking red marker, until it's lit.
        static void BuildPlayableLamp(Dictionary<string, Material> mats)
        {
            Vector2 at = Clearing + new Vector2(2f, 2f);
            float y = GroundHeight(at.x, at.y);
            var root = new GameObject("Lamp 0") { layer = Layers.Props };
            root.transform.position = new Vector3(at.x, y, at.y);

            Primitive(PrimitiveType.Cylinder, "Post", root.transform, new Vector3(0f, 1.6f, 0f), new Vector3(0.12f, 1.6f, 0.12f), mats["metal"], Layers.Props, keepCollider: true);
            Primitive(PrimitiveType.Cube, "Head", root.transform, new Vector3(0f, 3.35f, 0f), new Vector3(0.45f, 0.12f, 0.45f), mats["metal"], Layers.Props);
            var glass = Primitive(PrimitiveType.Cube, "Glass", root.transform, new Vector3(0f, 3.12f, 0f), new Vector3(0.32f, 0.34f, 0.32f), mats["glass"], Layers.Props);
            var marker = Primitive(PrimitiveType.Sphere, "Marker", root.transform, new Vector3(0f, 3.5f, 0f), Vector3.one * 0.1f, mats["marker"], Layers.Props);
            var crank = Primitive(PrimitiveType.Cylinder, "Crank", root.transform, new Vector3(0.18f, 1.1f, 0f), new Vector3(0.08f, 0.12f, 0.08f), mats["crank"], Layers.Props, keepCollider: true);
            crank.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            var lightGo = new GameObject("Lamp Light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 3.0f, 0f);
            lightGo.AddHDLight(LightType.Point);
            var light = lightGo.GetComponent<Light>();
            light.shadows = LightShadows.Soft;
            light.enabled = false;
            lightGo.AddComponent<FogGlowLight>();

            var lamp = root.AddComponent<Lamp>();
            lamp.index = 0;
            lamp.lampLight = light;
            lamp.marker = marker.GetComponent<Renderer>();
            lamp.glass = glass.GetComponent<Renderer>();
            lamp.crank = crank.transform;
        }

        /// <summary>
        /// Game > Build Forest Scene: the real game scene (Assets/Scenes/Forest.unity). It holds only the world
        /// builder and the player; the forest itself is generated from the run's seed when Play starts.
        /// </summary>
        [MenuItem("Game/Build Forest Scene")]
        public static void BuildForest()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            const string path = "Assets/Scenes/Forest.unity";
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("World Builder").AddComponent<WorldBuilder>();
            BuildPlayer();
            EditorSceneManager.SaveScene(scene, path);

            // First in the build, so a build starts in the forest.
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(path, true) };
            foreach (var s in EditorBuildSettings.scenes) if (s.path != path) scenes.Add(s);
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"Forest scene built: {path}. Press Play: the world is generated from the run's seed.");
        }

        // The player: a body that walks (on the PlayerBody layer), with the camera and flashlight at eye height.
        static void BuildPlayer()
        {
            Vector2 at = PathPoints[0] + new Vector2(0f, 12f);
            var go = new GameObject("Player") { layer = Layers.PlayerBody };
            go.transform.position = new Vector3(at.x, GroundHeight(at.x, at.y) + 0.05f, at.y);
            go.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var body = go.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.35f;
            body.center = new Vector3(0f, 0.9f, 0f);
            body.stepOffset = 0.4f;
            body.slopeLimit = 50f;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(go.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 600f;
            cam.cullingMask = ~Layers.Mask(Layers.PlayerBody);
            camGo.AddComponent<HDAdditionalCameraData>();
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<Flashlight>();
            go.AddComponent<PlayerController>();
        }

        // A stand-in scour on the path ahead: a too-thin black figure with burning red eyes, 2.4 m tall.
        static void BuildScour(Dictionary<string, Material> mats)
        {
            Vector2 at = new Vector2(0.4f, -18f);
            var root = new GameObject("Scour Stand-in") { layer = Layers.Entities }.transform;
            root.position = new Vector3(at.x, GroundHeight(at.x, at.y), at.y);
            root.rotation = Quaternion.LookRotation(Vector3.back);
            var body = mats["scour"];

            Primitive(PrimitiveType.Capsule, "Body", root, new Vector3(0f, 1.45f, 0f), new Vector3(0.22f, 0.45f, 0.14f), body, Layers.Entities);
            Primitive(PrimitiveType.Capsule, "Neck", root, new Vector3(0f, 2.0f, 0.06f), new Vector3(0.06f, 0.14f, 0.06f), body, Layers.Entities);
            Primitive(PrimitiveType.Capsule, "Head", root, new Vector3(0f, 2.24f, 0.1f), new Vector3(0.17f, 0.15f, 0.17f), body, Layers.Entities);
            foreach (float side in new[] { -1f, 1f })
            {
                Primitive(PrimitiveType.Capsule, "Upper Arm", root, new Vector3(0.2f * side, 1.55f, 0f), new Vector3(0.05f, 0.32f, 0.05f), body, Layers.Entities);
                Primitive(PrimitiveType.Capsule, "Forearm", root, new Vector3(0.24f * side, 0.95f, 0.04f), new Vector3(0.045f, 0.32f, 0.045f), body, Layers.Entities);
                Primitive(PrimitiveType.Capsule, "Hand", root, new Vector3(0.26f * side, 0.55f, 0.08f), new Vector3(0.04f, 0.12f, 0.04f), body, Layers.Entities);
                Primitive(PrimitiveType.Capsule, "Thigh", root, new Vector3(0.09f * side, 0.85f, 0f), new Vector3(0.06f, 0.3f, 0.06f), body, Layers.Entities);
                Primitive(PrimitiveType.Capsule, "Shin", root, new Vector3(0.09f * side, 0.3f, 0.02f), new Vector3(0.045f, 0.3f, 0.045f), body, Layers.Entities);
                Primitive(PrimitiveType.Sphere, "Eye", root, new Vector3(0.055f * side, 2.27f, 0.25f), Vector3.one * 0.045f, mats["eyes"], Layers.Entities);
            }

            var glow = new GameObject("Eye Glow");
            glow.transform.SetParent(root, false);
            glow.transform.localPosition = new Vector3(0f, 2.27f, 0.4f);
            glow.AddHDLight(LightType.Point);
            var light = glow.GetComponent<Light>();
            light.color = new Color(1f, 0.12f, 0.05f);
            light.intensity = 0.6f; // candela
            light.range = 3f;
            light.shadows = LightShadows.None;
            glow.AddComponent<FogGlowLight>().strength = 1.5f; // (a red haze round the head in the mist)
        }

        static void BuildCamera()
        {
            Vector2 at = PathPoints[0] + new Vector2(0f, 12f);
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(at.x, GroundHeight(at.x, at.y) + 1.65f, at.y);
            go.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 600f;
            go.AddComponent<HDAdditionalCameraData>();
            go.AddComponent<AudioListener>();
            go.AddComponent<LookWalker>();
            go.AddComponent<Flashlight>();
        }
    }
}
