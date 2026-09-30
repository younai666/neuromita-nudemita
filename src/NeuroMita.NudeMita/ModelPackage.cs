using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NeuroMita.NudeMita
{
    /// <summary>One replaceable mesh inside a model pack, with the rig and material it came with.</summary>
    public sealed class ModelPart
    {
        public string Name;                 // mesh name, e.g. Body / Hair / Sweater
        public Mesh Mesh;                   // the Unity Mesh that was built from the container
        public string[] BoneNames;          // one per Bindposes entry
        public Matrix4x4[] Bindposes;
        public long SourceMaterialPathId;    // the source renderer's material reference
        public string SourceFile;
        public List<ModelBlendShape> BlendShapes = new List<ModelBlendShape>();

        /// <summary>bone name to that bone's bind-pose position, for AutoAlign.</summary>
        public AutoAlign.Sample SampleBindposes()
        {
            var s = new AutoAlign.Sample();
            if (BoneNames == null || Bindposes == null) return s;
            int n = Math.Min(BoneNames.Length, Bindposes.Length);
            for (int i = 0; i < n; i++)
            {
                if (string.IsNullOrEmpty(BoneNames[i])) continue;
                var inv = Bindposes[i].inverse;
                s.Add(BoneNames[i], inv.GetColumn(3));
            }
            return s;
        }
    }

    public sealed class ModelBlendShape
    {
        public string Name;
        public readonly List<ModelBlendShapeFrame> Frames = new List<ModelBlendShapeFrame>();
    }

    public sealed class ModelBlendShapeFrame
    {
        public float Weight = 100f;
        public Vector3[] DeltaVertices;
        public Vector3[] DeltaNormals;
        public Vector3[] DeltaTangents;
    }

    /// <summary>
    /// A model pack. This plugin only reads UnityFS containers, which is the only format the nude
    /// mod ships in and the only one that has to be parsed by hand: the game never initialises
    /// Unity's AssetBundle subsystem, so every AssetBundle.LoadFrom* overload is dead here.
    /// </summary>
    public abstract class ModelPackage : IDisposable
    {
        public string RootPath;
        public abstract bool Open();
        public abstract List<ModelPart> Parts { get; }
        public virtual void Dispose() { }

        /// <summary>Is this file a UnityFS container?</summary>
        public static bool LooksLikeBundle(string file)
        {
            try
            {
                if (!File.Exists(file)) return false;
                using (var fs = File.OpenRead(file))
                {
                    var buf = new byte[8];
                    int n = fs.Read(buf, 0, 8);
                    return n >= 7 && buf[0] == 'U' && buf[1] == 'n' && buf[2] == 'i' && buf[3] == 't' &&
                           buf[4] == 'y' && buf[5] == 'F' && buf[6] == 'S';
                }
            }
            catch { return false; }
        }

        /// <summary>
        /// Open a pack at a file path or a folder. A folder is accepted so the pack can simply be
        /// dropped in as downloaded; the first UnityFS file inside it is used.
        /// </summary>
        public static ModelPackage Open(string path)
        {
            string file = path;
            try
            {
                if (Directory.Exists(path))
                {
                    file = null;
                    foreach (var f in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                        if (LooksLikeBundle(f)) { file = f; break; }
                }
            }
            catch { }

            if (file == null || !LooksLikeBundle(file))
            {
                Logging.Error($"[Pkg] no UnityFS container found at: {path}");
                return null;
            }
            return new BundlePackage { RootPath = file };
        }
    }
}
