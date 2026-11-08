using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Spine;
using Spine.Unity;
using Spine.Unity.Playables;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.SceneManagement;

public static class SpineFeatureChecks {
    [Serializable] public class Report {
        public List<string> passed = new List<string>();
        public List<string> failed = new List<string>();
        public string limitation = "These synthetic format checks supplement the real exported-asset comparison in real-3.8.75.json.";
    }
    public static string Run() {
        var original = SceneManager.GetActiveScene();
        var temporary = Application.isPlaying ? SceneManager.CreateScene("Spine Feature Regression")
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(temporary);
        try { return RunInScene(); }
        finally {
            SceneManager.SetActiveScene(original);
            if (Application.isPlaying) SceneManager.UnloadSceneAsync(temporary);
            else EditorSceneManager.CloseScene(temporary, true);
        }
    }
    static string RunInScene() {
        var report = new Report();
        Check(report, "3.8.75 JSON: animation, events, mixing and completion", JsonFixture);
        Check(report, "3.8.75 binary: header and skeleton structure", BinaryFixture);
        Check(report, "Malformed binary remains rejected", InvalidBinary);
        Check(report, Application.isPlaying ? "Timeline 1.8: runtime track binding" : "Timeline 1.8: editor pose preview", Timeline);
        Check(report, "2D physics: dynamic and kinematic bone bodies", PhysicsBodies);
        Directory.CreateDirectory("Artifacts/SpineCompatibility");
        File.WriteAllText("Artifacts/SpineCompatibility/features-" + (Application.isPlaying ? "playmode" : "editmode") + ".json", JsonUtility.ToJson(report, true));
        return JsonUtility.ToJson(report);
    }
    static void Check(Report report, string name, Action test) {
        try { test(); report.passed.Add(name); }
        catch(Exception e) { report.failed.Add(name + " : " + e); }
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }

    static void JsonFixture() {
        const string json = "{\"skeleton\":{\"hash\":\"fixture\",\"spine\":\"3.8.75\"},\"bones\":[{\"name\":\"root\"}],\"events\":{\"step\":{\"int\":7}},\"animations\":{\"move\":{\"bones\":{\"root\":{\"translate\":[{\"time\":0,\"x\":0},{\"time\":1,\"x\":10}]}},\"events\":[{\"time\":0.25,\"name\":\"step\"}]},\"idle\":{}}}";
        var data = new SkeletonJson(new RegionlessAttachmentLoader()).ReadSkeletonData(new StringReader(json));
        Require(data.Version == "3.8.75", "Version was not preserved");
        var skeleton = new Skeleton(data);
        var state = new Spine.AnimationState(new AnimationStateData(data) { DefaultMix = .2f });
        var eventCount = 0;
        var completions = 0;
        state.Event += (entry, ev) => { if (ev.Data.Name == "step" && ev.Int == 7) ++eventCount; };
        state.Complete += entry => ++completions;
        state.SetAnimation(0, "move", false);
        state.Update(.5f); state.Apply(skeleton); skeleton.UpdateWorldTransform();
        Require(Mathf.Abs(skeleton.RootBone.X - 5f) < .001f, "Translation interpolation changed");
        Require(eventCount == 1, "Event was not delivered exactly once");
        state.Update(.6f); state.Apply(skeleton);
        Require(completions == 1, "Completion callback missing");
        state.SetAnimation(0, "idle", true);
        state.Update(.1f); state.Apply(skeleton);
        Require(state.GetCurrent(0).MixingFrom != null, "Crossfade lost the outgoing track");
    }
    static byte[] MinimalBinary() {
        using (var stream = new MemoryStream()) {
            WriteString(stream, "fixture"); WriteString(stream, "3.8.75");
            for (var i = 0; i < 4; ++i) WriteFloat(stream, 0);
            stream.WriteByte(0); // nonessential
            stream.WriteByte(0); // shared strings
            stream.WriteByte(1); // bones
            WriteString(stream, "root");
            foreach (var value in new float[] { 0, 2, 3, 1, 1, 0, 0, 0 }) WriteFloat(stream, value);
            stream.WriteByte(0); stream.WriteByte(0); // transform mode / skin required
            for (var i = 0; i < 8; ++i) stream.WriteByte(0); // slots, constraints, skins, events, animations
            return stream.ToArray();
        }
    }
    static void WriteString(Stream stream, string value) {
        var bytes = Encoding.UTF8.GetBytes(value);
        stream.WriteByte((byte)(bytes.Length + 1));
        stream.Write(bytes, 0, bytes.Length);
    }
    static void WriteFloat(Stream stream, float value) {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        stream.Write(bytes, 0, 4);
    }
    static void BinaryFixture() {
        var data = new SkeletonBinary(new RegionlessAttachmentLoader()).ReadSkeletonData(new MemoryStream(MinimalBinary()));
        Require(data.Version == "3.8.75" && data.Bones.Count == 1, "Header/bone count mismatch");
        var skeleton = new Skeleton(data); skeleton.UpdateWorldTransform();
        Require(skeleton.RootBone.WorldX == 2 && skeleton.RootBone.WorldY == 3, "Binary bone transform mismatch");
    }
    static void InvalidBinary() {
        bool rejected = false;
        try { new SkeletonBinary(new RegionlessAttachmentLoader()).ReadSkeletonData(new MemoryStream(MinimalBinary().Take(20).ToArray())); }
        catch { rejected = true; }
        Require(rejected, "Truncated data unexpectedly loaded");
    }
    static void Timeline() {
        var path = AssetDatabase.FindAssets("t:SkeletonDataAsset", new[] { "Assets/Spine Examples/Spine Skeletons/spineboy-pro" }).Select(AssetDatabase.GUIDToAssetPath).First();
        var data = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(path);
        var skeleton = SkeletonAnimation.NewSkeletonAnimationGameObject(data, true);
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        var reference = ScriptableObject.CreateInstance<AnimationReferenceAsset>();
        try {
            var serializedReference = new SerializedObject(reference);
            serializedReference.FindProperty("skeletonDataAsset").objectReferenceValue = data;
            serializedReference.FindProperty("animationName").stringValue = data.GetSkeletonData(false).Animations.First(a => a.Duration > 0).Name;
            serializedReference.ApplyModifiedPropertiesWithoutUndo();
            var track = timeline.CreateTrack<SpineAnimationStateTrack>(null, "Spine Regression");
            var clip = track.CreateClip<SpineAnimationStateClip>();
            ((SpineAnimationStateClip)clip.asset).template.animationReference = reference;
            clip.start = 0; clip.duration = 2;
            var director = skeleton.gameObject.AddComponent<PlayableDirector>();
            director.playableAsset = timeline;
            director.SetGenericBinding(track, skeleton);
            director.time = .5; director.Evaluate();
            if (Application.isPlaying) {
                Require(skeleton.AnimationState.GetCurrent(0) != null, "Timeline did not set an animation track");
            } else {
                // In edit mode Timeline previews the pose directly, without changing tracks.
                var expected = new Skeleton(data.GetSkeletonData(false));
                reference.Animation.Apply(expected, 0, .5f, false, null, 1f, MixBlend.Setup, MixDirection.In);
                expected.UpdateWorldTransform();
                for (int i = 0; i < expected.Bones.Count; ++i) {
                    Require(Mathf.Abs(expected.Bones.Items[i].WorldX - skeleton.Skeleton.Bones.Items[i].WorldX) < .001f
                        && Mathf.Abs(expected.Bones.Items[i].WorldY - skeleton.Skeleton.Bones.Items[i].WorldY) < .001f,
                        "Timeline preview pose mismatch at bone " + expected.Bones.Items[i].Data.Name);
                }
            }
            director.Stop();
        } finally {
            UnityEngine.Object.DestroyImmediate(skeleton.gameObject);
            foreach (var track in timeline.GetOutputTracks()) {
                foreach (var clip in track.GetClips()) UnityEngine.Object.DestroyImmediate(clip.asset);
                UnityEngine.Object.DestroyImmediate(track);
            }
            UnityEngine.Object.DestroyImmediate(timeline);
            UnityEngine.Object.DestroyImmediate(reference);
        }
    }
    static void PhysicsBodies() {
        var dynamicBone = new GameObject("Dynamic regression");
        var pinnedBone = new GameObject("Pinned regression");
        try {
            Require(SkeletonUtility.AddBoneRigidbody2D(dynamicBone, false).bodyType == RigidbodyType2D.Dynamic, "Dynamic body mismatch");
            Require(SkeletonUtility.AddBoneRigidbody2D(pinnedBone, true).bodyType == RigidbodyType2D.Kinematic, "Pinned body mismatch");
        } finally {
            UnityEngine.Object.DestroyImmediate(dynamicBone);
            UnityEngine.Object.DestroyImmediate(pinnedBone);
        }
    }
}
