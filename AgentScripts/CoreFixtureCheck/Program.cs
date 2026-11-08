using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Spine;

internal static class Program {
    sealed class Loader : TextureLoader {
        public void Load(AtlasPage page, string path) {
            if (!File.Exists(path)) throw new FileNotFoundException(path);
            page.rendererObject = path;
        }
        public void Unload(object texture) { }
    }
    static int Main(string[] args) {
        var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
        var source = Path.Combine(root, "Assets", "SpineCompatibility", "Fixtures", "Spine3875");
        var jsonFolder = Path.Combine(source, "Json");
        var binaryFolder = Path.Combine(source, "Binary");
        var atlas = new Atlas(Path.Combine(jsonFolder, "hero-pro.atlas.txt"), new Loader());
        var json = new SkeletonJson(atlas) { Scale = .01f }.ReadSkeletonData(Path.Combine(jsonFolder, "hero-pro.json"));
        var binary = new SkeletonBinary(atlas) { Scale = .01f }.ReadSkeletonData(Path.Combine(binaryFolder, "hero-pro.skel.bytes"));
        var errors = new List<string>();
        var passed = new List<string>();
        var maxBoneDifference = 0.0;
        var maxVertexDifference = 0.0;
        var frames = 0;
        if (json.Version != "3.8.75" || binary.Version != "3.8.75") errors.Add("Unexpected source version");
        if (json.Bones.Count != binary.Bones.Count || json.Slots.Count != binary.Slots.Count) errors.Add("Structure differs");
        foreach(var animation in json.Animations) {
            try {
                var a = new Skeleton(json); var b = new Skeleton(binary);
                var ast = new AnimationState(new AnimationStateData(json));
                var bst = new AnimationState(new AnimationStateData(binary));
                ast.SetAnimation(0, animation.Name, true); bst.SetAnimation(0, animation.Name, true);
                for (int i = 0; i < 120; ++i) {
                    float dt = Math.Max(animation.Duration, .1f) / 60;
                    ast.Update(dt); bst.Update(dt); ast.Apply(a); bst.Apply(b);
                    a.UpdateWorldTransform(); b.UpdateWorldTransform();
                    for (int bone = 0; bone < a.Bones.Count; ++bone) {
                        double dx = a.Bones.Items[bone].WorldX - b.Bones.Items[bone].WorldX;
                        double dy = a.Bones.Items[bone].WorldY - b.Bones.Items[bone].WorldY;
                        maxBoneDifference = Math.Max(maxBoneDifference, Math.Sqrt(dx * dx + dy * dy));
                    }
                    for(int slot=0; slot<a.DrawOrder.Count; ++slot) {
                        var aa = a.DrawOrder.Items[slot]; var bb = b.DrawOrder.Items[slot];
                        if(aa.Data.Name != bb.Data.Name || aa.Attachment?.Name != bb.Attachment?.Name) throw new Exception("Slot/attachment mismatch");
                        var av=Vertices(aa); var bv=Vertices(bb);
                        if(av.Length!=bv.Length) throw new Exception("Vertex count mismatch");
                        for(int j=0;j<av.Length;++j) maxVertexDifference=Math.Max(maxVertexDifference,Math.Abs(av[j]-bv[j]));
                    }
                    ++frames;
                }
                passed.Add(animation.Name);
            } catch(Exception e) { errors.Add(animation.Name + ": " + e.Message); }
        }
        if(double.IsNaN(maxBoneDifference)||maxBoneDifference>.01) errors.Add("Bone difference exceeds 1 source pixel");
        if(double.IsNaN(maxVertexDifference)||maxVertexDifference>.01) errors.Add("Vertex difference exceeds 1 source pixel");
        var report = new { success=errors.Count==0, jsonVersion=json.Version, binaryVersion=binary.Version, bones=json.Bones.Count, slots=json.Slots.Count,
            skins=json.Skins.Count, animations=passed, comparedFrames=frames, maxBoneDifference, maxVertexDifference, errors,
            scope="Pure C# runtime, original 3.8.75 JSON and binary exports. See real-3.8.75.json for the separate Unity mesh comparison." };
        var text=JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true});
        Directory.CreateDirectory(Path.Combine(root,"Artifacts","SpineCompatibility"));
        File.WriteAllText(Path.Combine(root,"Artifacts","SpineCompatibility","real-3.8.75-core.json"),text);
        Console.WriteLine(text);
        atlas.Dispose();
        return errors.Count==0?0:1;
    }
    static float[] Vertices(Slot slot) {
        if(slot.Attachment is RegionAttachment region) { var v=new float[8]; region.ComputeWorldVertices(slot.Bone,v,0,2); return v; }
        if(slot.Attachment is VertexAttachment vertex) { var v=new float[vertex.WorldVerticesLength]; vertex.ComputeWorldVertices(slot,0,v.Length,v,0,2); return v; }
        return Array.Empty<float>();
    }
}
