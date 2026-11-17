using UnityEditor;
using UnityEditor.Build.Reporting;

namespace LeftRight.Editor
{
    public static class OnlineBuildTools
    {
        [MenuItem("LeftRight/Rebuild/Build online test player")]
        public static void Build() => BuildTestPlayer();
        public static string BuildTestPlayer()
        {
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{PrototypeTools.RebuildScene},locationPathName="Builds/OnlineTest/LeftRight.exe",
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new System.Exception("Online test build failed: "+report.summary.result);
            return report.summary.outputPath;
        }
    }
}
