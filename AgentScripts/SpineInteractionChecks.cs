using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Spine.Unity;
using Spine.Unity.Examples;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class SpineInteractionChecks {
    [Serializable] public class Report {
        public List<string> passed = new List<string>();
        public List<string> failed = new List<string>();
    }
    public static async Task<string> Run() {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        var report = new Report();
        await Check(report, "Pointer release launches the 2D ragdoll and physics advances", async () => {
            await Load("SkeletonUtility Ragdoll");
            var target = UnityEngine.Object.FindObjectsByType<RaggedySpineboy>(FindObjectsSortMode.None)
                .First(x => !x.GetComponent<SkeletonRagdoll2D>().pinStartBone);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            Require(ExecuteEvents.GetEventHandler<IPointerDownHandler>(target.gameObject) == target.gameObject, "Ragdoll cannot capture pointer press");
            ExecuteEvents.Execute(target.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            var ragdoll = target.GetComponent<SkeletonRagdoll2D>();
            Require(ragdoll.IsActive && ragdoll.RigidbodyArray.Length > 1, "Ragdoll bodies were not created");
            Require(ragdoll.RootRigidbody.bodyType == RigidbodyType2D.Dynamic, "Ragdoll root is not dynamic");
            var position = ragdoll.RootRigidbody.position;
            await Task.Delay(150);
            Require((ragdoll.RootRigidbody.position - position).sqrMagnitude > .001f, "Physics did not advance");
        });
        await Check(report, "Pointer click switches Goblins skin", async () => {
            await Load("Goblins");
            var goblin = UnityEngine.Object.FindFirstObjectByType<Goblins>();
            var skeleton = goblin.GetComponent<SkeletonAnimation>().Skeleton;
            var before = skeleton.Skin;
            ExecuteEvents.Execute(goblin.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerDownHandler);
            Require(skeleton.Skin != before, "Skin did not change");
        });
        await Check(report, "Pointer drag moves the bone target", async () => {
            await Load("SkeletonUtility Eyes");
            var target = UnityEngine.Object.FindFirstObjectByType<DraggableTransform>();
            var before = target.transform.position;
            ExecuteEvents.Execute(target.gameObject, new PointerEventData(EventSystem.current) {
                position = new Vector2(Screen.width * .5f, Screen.height * .5f), delta = new Vector2(25, 15)
            }, ExecuteEvents.dragHandler);
            Require((target.transform.position - before).sqrMagnitude > .0001f, "Drag target did not move");
        });
        Directory.CreateDirectory("Artifacts/SpineCompatibility");
        File.WriteAllText("Artifacts/SpineCompatibility/interactions.json", JsonUtility.ToJson(report, true));
        return JsonUtility.ToJson(report);
    }
    static async Task Load(string name) {
        EditorSceneManager.LoadSceneInPlayMode("Assets/Spine Examples/Other Examples/" + name + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
        await Task.Delay(150);
    }
    static async Task Check(Report report, string label, Func<Task> run) {
        try { await run(); report.passed.Add(label); }
        catch (Exception e) { report.failed.Add(label + ": " + e); }
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
