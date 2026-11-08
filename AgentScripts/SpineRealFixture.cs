using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Spine.Unity;
using Spine.Unity.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SpineRealFixture {
    public const string Root = "Assets/SpineCompatibility/Fixtures/Spine3875";
    [Serializable] public class Report {
        public string jsonVersion, binaryVersion;
        public int bones, slots, skins, animations, comparedFrames;
        public float maxBoneDifference, maxVertexDifference;
        public List<string> passedAnimations = new List<string>();
        public List<string> errors = new List<string>();
        public bool success;
    }

    public static async Task<string> Import() {
        Folder("Assets", "SpineCompatibility");
        Folder("Assets/SpineCompatibility", "Fixtures");
        Folder("Assets/SpineCompatibility/Fixtures", "Spine3875");
        Folder(Root, "Json"); Folder(Root, "Binary");
        var preferences = SpineEditorUtilities.Preferences;
        var oldShader = preferences.defaultShader;
        var oldPreset = preferences.textureSettingsReference;
        var oldScale = preferences.defaultScale;
        try {
            preferences.defaultShader = "Universal Render Pipeline/Spine/Skeleton";
            preferences.textureSettingsReference = AssetDatabase.FindAssets("StraightAlphaPreset t:Preset").Select(AssetDatabase.GUIDToAssetPath).First();
            preferences.defaultScale = .01f;
            AssetDatabase.StartAssetEditing();
            try {
                CopyExport(".tmp/测试/测试json", Root + "/Json");
                CopyExport(".tmp/测试/测试二进制", Root + "/Binary");
            } finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            await Task.Delay(500);
        } finally {
            preferences.defaultShader = oldShader;
            preferences.textureSettingsReference = oldPreset;
            preferences.defaultScale = oldScale;
            EditorUtility.SetDirty(preferences);
            AssetDatabase.SaveAssetIfDirty(preferences);
        }
        return string.Join("\n", AssetDatabase.FindAssets("t:SkeletonDataAsset", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath));
    }
    static void CopyExport(string source, string target) {
        foreach (var path in Directory.GetFiles(source)) {
            if (!(path.EndsWith(".json") || path.EndsWith(".txt") || path.EndsWith(".bytes") || path.EndsWith(".png"))) continue;
            var destination = target + "/" + Path.GetFileName(path);
            if (File.Exists(destination)) throw new IOException("Refusing to overwrite existing fixture: " + destination);
            FileUtil.CopyFileOrDirectory(Path.GetFullPath(path), destination);
        }
    }
    static void Folder(string parent, string child) {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
    }
    static SkeletonDataAsset Data(string kind) {
        return AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(AssetDatabase.FindAssets("t:SkeletonDataAsset", new[] { Root + "/" + kind }).Select(AssetDatabase.GUIDToAssetPath).Single());
    }

    public static string Compare() {
        var original = SceneManager.GetActiveScene();
        var temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(temporary);
        var report = new Report();
        try {
            var jsonAsset = Data("Json"); var binaryAsset = Data("Binary");
            var a = SkeletonAnimation.NewSkeletonAnimationGameObject(jsonAsset, true);
            var b = SkeletonAnimation.NewSkeletonAnimationGameObject(binaryAsset, true);
            var ad = a.Skeleton.Data; var bd = b.Skeleton.Data;
            report.jsonVersion = ad.Version; report.binaryVersion = bd.Version;
            report.bones = ad.Bones.Count; report.slots = ad.Slots.Count; report.skins = ad.Skins.Count;
            if (ad.Version != "3.8.75" || bd.Version != "3.8.75") throw new Exception("Expected original 3.8.75 headers");
            if (ad.Bones.Count != bd.Bones.Count || ad.Slots.Count != bd.Slots.Count || ad.Animations.Count != bd.Animations.Count)
                throw new Exception("JSON/binary structure differs");
            foreach (var animation in ad.Animations) {
                a.Initialize(true); b.Initialize(true);
                a.AnimationState.SetAnimation(0, animation.Name, true);
                b.AnimationState.SetAnimation(0, animation.Name, true);
                for (int frame = 0; frame < 120; ++frame) {
                    float delta = Math.Max(animation.Duration, .1f) / 60;
                    a.Update(delta); b.Update(delta); a.LateUpdate(); b.LateUpdate();
                    for (int bone = 0; bone < ad.Bones.Count; ++bone) {
                        var aa = a.Skeleton.Bones.Items[bone]; var bb = b.Skeleton.Bones.Items[bone];
                        var difference = Vector2.Distance(new Vector2(aa.WorldX, aa.WorldY), new Vector2(bb.WorldX, bb.WorldY));
                        report.maxBoneDifference = Math.Max(report.maxBoneDifference, difference);
                        if (float.IsNaN(difference) || difference > .01f) throw new Exception(animation.Name + ": bone mismatch " + aa.Data.Name + " (" + difference + ")");
                    }
                    var av = a.GetComponent<MeshFilter>().sharedMesh.vertices;
                    var bv = b.GetComponent<MeshFilter>().sharedMesh.vertices;
                    if (av.Length != bv.Length) throw new Exception(animation.Name + ": mesh vertex count differs");
                    for (int vertex = 0; vertex < av.Length; ++vertex) {
                        var difference = Vector3.Distance(av[vertex], bv[vertex]);
                        report.maxVertexDifference = Math.Max(report.maxVertexDifference, difference);
                        if (float.IsNaN(difference) || difference > .01f) throw new Exception(animation.Name + ": mesh mismatch (" + difference + ")");
                    }
                    ++report.comparedFrames;
                }
                ++report.animations;
                report.passedAnimations.Add(animation.Name);
            }
            report.success = true;
        } catch (Exception e) { report.errors.Add(e.ToString()); }
        finally {
            SceneManager.SetActiveScene(original);
            EditorSceneManager.CloseScene(temporary, true);
        }
        Directory.CreateDirectory("Artifacts/SpineCompatibility");
        File.WriteAllText("Artifacts/SpineCompatibility/real-3.8.75.json", JsonUtility.ToJson(report, true));
        return JsonUtility.ToJson(report);
    }

    public static string CreateComparisonScene() {
        if (SceneManager.GetActiveScene().isDirty) throw new Exception("Save the current scene first.");
        var originalPath = SceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        try {
            var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 3.3f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.14f, .16f, .2f);
            camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var json = SkeletonAnimation.NewSkeletonAnimationGameObject(Data("Json"), true);
            json.name = "JSON - Spine 3.8.75"; json.transform.position = new Vector3(-3, -1.5f, 0);
            json.AnimationName = "run"; json.loop = true;
            var binary = SkeletonAnimation.NewSkeletonAnimationGameObject(Data("Binary"), true);
            binary.name = "Binary - Spine 3.8.75"; binary.transform.position = new Vector3(2, -1.5f, 0);
            binary.AnimationName = "run"; binary.loop = true;
            var path = "Assets/SpineCompatibility/Validation/Spine3875Comparison.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        } finally { EditorSceneManager.OpenScene(originalPath); }
    }
}
