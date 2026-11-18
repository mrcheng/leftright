using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace LeftRight.Editor
{
    public static class WebBuildTools
    {
        public const string Output="Builds/WebGL";
        [MenuItem("LeftRight/Rebuild/Build GitHub Pages game")]
        public static void Build()=>BuildPages();
        public static string BuildPages()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play Mode first.");
            // Pages cannot supply custom Content-Encoding or isolation headers. Unity's
            // JS decompression fallback and a single-threaded build need neither.
            PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback=true;
            PlayerSettings.WebGL.threadsSupport=false;
            PlayerSettings.WebGL.initialMemorySize=256;
            PlayerSettings.WebGL.maximumMemorySize=1024;
            PlayerSettings.WebGL.dataCaching=true;
            PlayerSettings.WebGL.exceptionSupport=WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL,Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.runInBackground=true;
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{PrototypeTools.RebuildScene},locationPathName=Output,
                target=BuildTarget.WebGL,options=BuildOptions.None});
            if(report==null||report.summary.result!=BuildResult.Succeeded||report.summary.platform!=BuildTarget.WebGL||!Directory.Exists(Path.Combine(Output,"Build")))
                throw new System.Exception("Web build did not produce browser files. Check that Web Build Support is loaded.");
            WritePage();
            return Path.GetFullPath(Output);
        }
        public static void WritePage()
        {
            string build=Path.Combine(Output,"Build");
            // Unity can leave development intermediates beside a compressed rebuild.
            // Only the compressed versions are shipped when both versions exist.
            foreach(string compressed in Directory.GetFiles(build,"*.unityweb"))
            {
                string plain=compressed.Substring(0,compressed.Length-".unityweb".Length);
                if(File.Exists(plain))File.Delete(plain);
            }
            string FileMatching(string pattern)
            {
                string file=Directory.GetFiles(build,pattern).OrderByDescending(File.GetLastWriteTimeUtc).First();
                // Give each artifact a content address. Cached Pages clients can keep
                // an old game intact while a new audio/code/data bundle is deployed.
                using var hash=System.Security.Cryptography.SHA256.Create();
                using var stream=File.OpenRead(file);
                string revision=System.BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").Substring(0,16).ToLowerInvariant();
                stream.Close();
                string name=Path.GetFileName(file),suffix=name.Substring(name.IndexOf('.')+1);
                string versioned=Path.Combine(build,revision+"."+suffix);
                if(file!=versioned){File.Copy(file,versioned,true);File.Delete(file);}
                return "Build/"+Path.GetFileName(versioned);
            }
            string html=File.ReadAllText("Tools/Web/index.template.html")
                .Replace("__LOADER__",FileMatching("*.loader.js"))
                .Replace("__DATA__",FileMatching("*.data*"))
                .Replace("__FRAMEWORK__",FileMatching("*.framework.js*"))
                .Replace("__WASM__",FileMatching("*.wasm*"));
            File.WriteAllText(Path.Combine(Output,"index.html"),html);
            Directory.CreateDirectory(Path.Combine(Output,"style"));
            foreach(string name in new[]{"PressStart2P.ttf","Font-License.txt"})
                File.Copy("Assets/Rebuild/UI/Pixel/"+name,Path.Combine(Output,"style",name),true);
            File.WriteAllText(Path.Combine(Output,".nojekyll"),"");
        }
    }
}
