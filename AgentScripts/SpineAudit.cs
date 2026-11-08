using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Spine.Unity;

// Run through unity command script run, outside Assets to avoid shipping audit code.
public static class SpineAudit {
    [Serializable] public class Report {
        public string unityVersion;
        public string renderPipeline;
        public int skeletons, animations, skins, meshes;
        public bool normalsTangentsAndTwoColorTint;
        public List<string> passed = new List<string>();
        public List<string> failures = new List<string>();
        public List<string> shaderMessages = new List<string>();
        public List<string> scenes = new List<string>();
    }

    public static string Run() {
        var original = SceneManager.GetActiveScene();
        var temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(temporary);
        try { return RunInScene(); }
        finally {
            SceneManager.SetActiveScene(original);
            EditorSceneManager.CloseScene(temporary, true);
        }
    }
    static string RunInScene() {
        var report = new Report {
            unityVersion = Application.unityVersion,
            normalsTangentsAndTwoColorTint = true,
            renderPipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null
                ? "Built-in" : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name
        };
        foreach (var guid in AssetDatabase.FindAssets("t:SkeletonDataAsset")) {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = null;
            try {
                var asset = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(path);
                var data = asset.GetSkeletonData(false);
                if (data == null) throw new Exception("Skeleton data is null");
                ++report.skeletons;
                var component = SkeletonAnimation.NewSkeletonAnimationGameObject(asset, true);
                go = component.gameObject;
                go.hideFlags = HideFlags.HideAndDontSave;
                component.addNormals = true;
                component.calculateTangents = true;
                component.tintBlack = true;
                component.Initialize(true, true);
                foreach (var skin in data.Skins) {
                    component.Skeleton.SetSkin(skin);
                    component.Skeleton.SetSlotsToSetupPose();
                    component.Update(0);
                    component.LateUpdate();
                    ++report.skins;
                }
                component.Skeleton.SetSkin(data.DefaultSkin);
                component.Skeleton.SetToSetupPose();
                foreach (var animation in data.Animations) {
                    component.AnimationState.SetAnimation(0, animation, false);
                    for (int i = 0; i < 25; ++i) {
                        component.Update(Mathf.Max(animation.Duration, .1f) / 24f);
                        component.LateUpdate();
                        foreach (var bone in component.Skeleton.Bones)
                            if (float.IsNaN(bone.WorldX) || float.IsInfinity(bone.WorldX)
                                || float.IsNaN(bone.WorldY) || float.IsInfinity(bone.WorldY))
                                throw new Exception("Non-finite bone: " + bone.Data.Name + " / " + animation.Name);
                        var mesh = go.GetComponent<MeshFilter>().sharedMesh;
                        if (mesh == null) throw new Exception("Mesh not generated");
                        if (mesh.normals.Length != mesh.vertexCount || mesh.tangents.Length != mesh.vertexCount)
                            throw new Exception("Normal/tangent channel length mismatch");
                        if (mesh.uv2.Length != mesh.vertexCount || mesh.uv3.Length != mesh.vertexCount)
                            throw new Exception("Two-color tint channel length mismatch");
                        foreach (var v in mesh.vertices)
                            if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsInfinity(v.x) || float.IsInfinity(v.y))
                                throw new Exception("Non-finite vertex / " + animation.Name);
                        ++report.meshes;
                    }
                    ++report.animations;
                }
                report.passed.Add(path + " [" + data.Version + "]");
            } catch (Exception e) { report.failures.Add(path + " : " + e); }
            finally { if (go != null) UnityEngine.Object.DestroyImmediate(go); }
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Shader")) {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.IndexOf("spine", StringComparison.OrdinalIgnoreCase) < 0) continue;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            foreach (var message in ShaderUtil.GetShaderMessages(shader)) {
                var text = path + ":" + message.line + " " + message.severity + " " + message.message;
                report.shaderMessages.Add(text);
                if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    report.failures.Add(text);
            }
        }
        report.scenes.AddRange(AssetDatabase.FindAssets("t:Scene")
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.IndexOf("spine", StringComparison.OrdinalIgnoreCase) >= 0));
        Directory.CreateDirectory("Artifacts/SpineCompatibility");
        File.WriteAllText("Artifacts/SpineCompatibility/audit.json", JsonUtility.ToJson(report, true));
        return JsonUtility.ToJson(report);
    }
}
