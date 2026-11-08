using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only regression runner. Scene loading and pipeline settings use Unity APIs.
[InitializeOnLoad]
public static class SpineSceneSmoke {
    const string Key = "SpineCompatibility.SceneSmoke.";
    [Serializable] public class SceneResult {
        public string scene;
        public int frames, skeletons, graphics, renderedMeshes;
        public List<string> errors = new List<string>();
    }
    [Serializable] public class Report {
        public string pipeline, unityVersion;
        public bool complete;
        public List<SceneResult> scenes = new List<SceneResult>();
    }
    static Report report;
    static string[] paths;
    static SceneResult current;
    static double loadedAt;
    static int firstFrame;
    static bool exiting;

    static SpineSceneSmoke() {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        Application.logMessageReceived += OnLog;
    }

    public static string Start(string pipeline) {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Open scene has unsaved changes; smoke test did not start.");
        SessionState.SetString(Key + "originalScene", SceneManager.GetActiveScene().path);
        SessionState.SetString(Key + "graphics", AssetDatabase.GetAssetPath(UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline));
        SessionState.SetString(Key + "quality", AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
        SessionState.SetString(Key + "pipeline", pipeline);
        SessionState.SetBool(Key + "active", true);
        if (pipeline == "Built-in") {
            UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null;
            QualitySettings.renderPipeline = null;
        } else if (pipeline == "URP-2D") {
            QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/SpineCompatibility/Validation/URP2D.asset");
            if (QualitySettings.renderPipeline == null) throw new InvalidOperationException("Create the URP 2D validation asset first.");
        }
        EditorApplication.EnterPlaymode();
        return "Started " + pipeline + " scene regression";
    }

    static void Tick() {
        if (!SessionState.GetBool(Key + "active", false) || !EditorApplication.isPlaying || exiting) return;
        if (report == null) {
            var pipeline = SessionState.GetString(Key + "pipeline", "URP");
            var baseScenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Spine Examples" }).Select(AssetDatabase.GUIDToAssetPath);
            var urpScenes = AssetDatabase.FindAssets("t:Scene", new[] { "Packages/com.esotericsoftware.spine.urp-shaders" }).Select(AssetDatabase.GUIDToAssetPath);
            paths = (pipeline == "URP-2D" ? urpScenes.Where(x => x.Contains("/2D/"))
                : pipeline == "URP" ? baseScenes.Concat(urpScenes.Where(x => !x.Contains("/2D/"))) : baseScenes).OrderBy(x => x).ToArray();
            report = new Report { pipeline = pipeline, unityVersion = Application.unityVersion };
        }
        if (current != null && (Time.frameCount - firstFrame < 30 || EditorApplication.timeSinceStartup - loadedAt < 1.0)) return;
        if (current != null) {
            current.frames = Time.frameCount - firstFrame;
            current.skeletons = UnityEngine.Object.FindObjectsByType<Spine.Unity.SkeletonRenderer>(FindObjectsSortMode.None).Length;
            current.graphics = UnityEngine.Object.FindObjectsByType<Spine.Unity.SkeletonGraphic>(FindObjectsSortMode.None).Length;
            current.renderedMeshes = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Count(m => m.sharedMesh != null && m.sharedMesh.vertexCount > 0);
            report.scenes.Add(current);
            current = null;
            Save();
        }
        if (report.scenes.Count >= paths.Length) {
            report.complete = true;
            Save();
            exiting = true;
            EditorApplication.ExitPlaymode();
            return;
        }
        current = new SceneResult { scene = paths[report.scenes.Count] };
        try {
            EditorSceneManager.LoadSceneInPlayMode(current.scene, new LoadSceneParameters(LoadSceneMode.Single));
        } catch (Exception e) { current.errors.Add(e.ToString()); }
        firstFrame = Time.frameCount;
        loadedAt = EditorApplication.timeSinceStartup;
    }

    static void OnLog(string message, string stack, LogType type) {
        if (current == null || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
        var text = message + "\n" + stack;
        if (!current.errors.Contains(text)) current.errors.Add(text);
    }

    static void Save() {
        Directory.CreateDirectory("Artifacts/SpineCompatibility");
        File.WriteAllText("Artifacts/SpineCompatibility/scenes-" + report.pipeline + ".json", JsonUtility.ToJson(report, true));
    }

    static void OnPlayModeChanged(PlayModeStateChange state) {
        if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key + "active", false)) return;
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(SessionState.GetString(Key + "graphics", ""));
        QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(SessionState.GetString(Key + "quality", ""));
        var original = SessionState.GetString(Key + "originalScene", "");
        SessionState.SetBool(Key + "active", false);
        if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
        report = null;
        current = null;
        exiting = false;
    }
}
