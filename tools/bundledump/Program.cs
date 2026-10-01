using System;
using System.Collections.Generic;
using System.Globalization;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

// 离线转储一个 UnityFS 包的内容，用来回答"这个包到底带了什么"：
//   网格（名字 / 子网格数 / bindpose 数 / blend shape 数）、
//   材质（shader + 每个贴图属性指到哪张贴图）、贴图（尺寸 / 格式）、
//   以及 SkinnedMeshRenderer 的层级路径和它的材质槽。
//
// 数组访问方式与 NeuroMita.CustomModels 的 BundlePackage.cs 保持一致：
// vector<T> 的元素在名为 "Array" 的子字段下（不是 AsArray）。
// 不启动游戏，不碰正式安装。
class BundleDump
{
    // ---- 字段访问 ----
    static AssetTypeValueField F(AssetTypeValueField p, string n)
    {
        if (p == null || p.Children == null) return null;
        foreach (var c in p.Children) if (c != null && c.FieldName == n) return c;
        return null;
    }
    static string S(AssetTypeValueField p, string n)
    { try { var f = F(p, n); return f != null ? f.AsString : null; } catch { return null; } }
    static long L(AssetTypeValueField p, string n)
    { try { var f = F(p, n); return f != null ? f.AsLong : 0; } catch { return 0; } }
    static int I(AssetTypeValueField p, string n)
    { try { var f = F(p, n); return f != null ? f.AsInt : 0; } catch { return 0; } }

    /// <summary>vector&lt;T&gt; 的元素列表（元素在 "Array" 子字段下）。</summary>
    static List<AssetTypeValueField> ElemList(AssetTypeValueField vectorField)
    {
        var r = new List<AssetTypeValueField>();
        var arr = F(vectorField, "Array");
        if (arr == null) return r;
        if (arr.Children != null) foreach (var c in arr.Children) if (c != null) r.Add(c);
        return r;
    }

    static long PathId(AssetTypeValueField pptr)
    {
        if (pptr == null) return 0;
        long file = L(pptr, "m_FileID"), path = L(pptr, "m_PathID");
        return file == 0 ? path : 0;
    }

    class Rec
    {
        public long PathId;
        public int TypeId;
        public string Name;
        public AssetTypeValueField Field;
    }

    static void Main(string[] args)
    {
        if (args.Length == 0) { Console.WriteLine("usage: bundledump <bundle> [bundle...]"); return; }
        foreach (var path in args)
        {
            try { Dump(path); }
            catch (Exception e) { Console.WriteLine("FAILED " + path + ": " + e.GetType().Name + ": " + e.Message); }
        }
    }

    static void Dump(string path)
    {
        Console.WriteLine("==================================================================");
        Console.WriteLine("BUNDLE: " + System.IO.Path.GetFileName(path));
        Console.WriteLine("==================================================================");

        var am = new AssetsManager();
        var bun = am.LoadBundleFile(path, true);
        var af = am.LoadAssetsFileFromBundle(bun, 0, true);

        var byId = new Dictionary<long, Rec>();
        foreach (var info in af.file.AssetInfos)
        {
            Rec r;
            try { r = new Rec { PathId = info.PathId, TypeId = info.TypeId, Field = am.GetBaseField(af, info) }; }
            catch { continue; }
            if (r.Field == null) continue;
            r.Name = S(r.Field, "m_Name");
            byId[info.PathId] = r;
        }

        string Name(long id)
        {
            Rec r;
            if (id != 0 && byId.TryGetValue(id, out r) && !string.IsNullOrEmpty(r.Name)) return "'" + r.Name + "'";
            return id == 0 ? "(none)" : "?(" + id + ")";
        }

        var counts = new Dictionary<int, int>();
        foreach (var r in byId.Values) { counts.TryGetValue(r.TypeId, out var c); counts[r.TypeId] = c + 1; }
        Console.WriteLine("assets: " + byId.Count);
        var keys = new List<int>(counts.Keys); keys.Sort();
        foreach (var k in keys) Console.WriteLine(string.Format("   type {0,4} : {1}", k, counts[k]));
        Console.WriteLine();

        // ---- 贴图 ----
        Console.WriteLine("--- Texture2D (28) ---");
        foreach (var r in byId.Values)
        {
            if (r.TypeId != 28) continue;
            Console.WriteLine(string.Format("   {0,-30} {1,5}x{2,-5} fmt={3}",
                r.Name, I(r.Field, "m_Width"), I(r.Field, "m_Height"), I(r.Field, "m_TextureFormat")));
        }
        Console.WriteLine();

        // ---- 网格 ----
        Console.WriteLine("--- Mesh (43) ---");
        foreach (var r in byId.Values)
        {
            if (r.TypeId != 43) continue;
            var sub = ElemList(F(r.Field, "m_SubMeshes"));
            var bp = ElemList(F(r.Field, "m_BindPose"));
            var shapes = F(r.Field, "m_Shapes");
            var shapeList = ElemList(F(shapes, "shapes"));
            int verts = 0;
            try { verts = I(F(r.Field, "m_VertexData"), "m_VertexCount"); } catch { }
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "   {0,-30} subMeshes={1,-3} bindposes={2,-4} shapes={3,-3} verts={4}",
                r.Name, sub.Count, bp.Count, shapeList.Count, verts));

            // 每个子网格的索引数。安装器会把这些累加成一个扁平数组，装完就再也分不出来了，
            // 所以要在这里量，供 HideSlots 的 MeshSplit 把它们拆回去。
            int cum = 0;
            for (int i = 0; i < sub.Count; i++)
            {
                int idx = I(sub[i], "indexCount");
                int vc = I(sub[i], "vertexCount");
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "        sub{0}: indexCount={1,-8} tris={2,-7} firstByte={3,-8} firstVertex={4,-7} vertexCount={5,-7} cumTriStart={6}",
                    i, idx, idx / 3, I(sub[i], "firstByte"), I(sub[i], "firstVertex"), vc, cum / 3));
                cum += idx;
            }
            if (sub.Count > 0)
                Console.WriteLine("        total indices=" + cum + "  (安装后运行时的 triangles 长度)");
        }
        Console.WriteLine();

        // ---- 材质 + 贴图指向 ----
        Console.WriteLine("--- Material (21) + 贴图槽 ---");
        foreach (var r in byId.Values)
        {
            if (r.TypeId != 21) continue;
            Console.WriteLine("   [" + r.PathId + "] " + Name(r.PathId) + "  shader=" + Name(PathId(F(r.Field, "m_Shader"))));

            var texEnvs = ElemList(F(F(r.Field, "m_SavedProperties"), "m_TexEnvs"));
            if (texEnvs.Count == 0) Console.WriteLine("        (m_TexEnvs 为空 —— 材质没有序列化任何贴图指向)");
            foreach (var entry in texEnvs)
            {
                string prop = S(entry, "first");
                long texId = PathId(F(F(entry, "second"), "m_Texture"));
                Console.WriteLine(string.Format("        {0,-24} -> {1}", prop, Name(texId)));
            }
        }
        Console.WriteLine();

        // ---- 层级 ----
        var tfGo = new Dictionary<long, long>();
        var tfFather = new Dictionary<long, long>();
        foreach (var r in byId.Values)
        {
            if (r.TypeId != 4) continue;
            tfGo[r.PathId] = PathId(F(r.Field, "m_GameObject"));
            tfFather[r.PathId] = PathId(F(r.Field, "m_Father"));
        }
        var goTf = new Dictionary<long, long>();
        foreach (var kv in tfGo) if (!goTf.ContainsKey(kv.Value)) goTf[kv.Value] = kv.Key;

        string PathOfTf(long tf)
        {
            var parts = new List<string>();
            long cur = tf;
            for (int i = 0; i < 64 && cur != 0; i++)
            {
                long go; tfGo.TryGetValue(cur, out go);
                parts.Add(Name(go).Trim('\''));
                long f; if (!tfFather.TryGetValue(cur, out f)) break;
                cur = f;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        // ---- SkinnedMeshRenderer ----
        Console.WriteLine("--- SkinnedMeshRenderer (137) ---");
        foreach (var r in byId.Values)
        {
            if (r.TypeId != 137) continue;
            long go = PathId(F(r.Field, "m_GameObject"));
            long tf = 0; goTf.TryGetValue(go, out tf);

            var mats = ElemList(F(r.Field, "m_Materials"));
            var names = new List<string>();
            foreach (var m in mats) names.Add(Name(PathId(m)));

            var bones = ElemList(F(r.Field, "m_Bones"));
            Console.WriteLine("   [" + r.PathId + "] go=" + Name(go));
            Console.WriteLine("        path  : " + PathOfTf(tf));
            Console.WriteLine(string.Format("        mesh  : {0}   bones={1}   rootBone={2}",
                Name(PathId(F(r.Field, "m_Mesh"))), bones.Count, Name(PathId(F(r.Field, "m_RootBone")))));
            Console.WriteLine("        mats  : " + (names.Count == 0 ? "(空)" : string.Join(", ", names)));
        }
        Console.WriteLine();
    }
}
