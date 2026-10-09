using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace JungleBooze.Editor.Build
{
    /// <summary>
    /// iOS bench builds only: turns on file sharing (UIFileSharingEnabled, LSSupportsOpeningDocumentsInPlace) in the
    /// exported Info.plist so the benchmark logs in Documents/bench appear in Finder (iPhone > Files) and the Files app.
    /// Game builds are never touched (<see cref="DeviceBenchBuild.Building"/>).
    /// </summary>
    public sealed class DeviceBenchPostprocess : IPostprocessBuildWithReport
    {
        private const string Keys = "\t<key>UIFileSharingEnabled</key>\n\t<true/>\n\t<key>LSSupportsOpeningDocumentsInPlace</key>\n\t<true/>\n";

        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!DeviceBenchBuild.Building || report.summary.platform != BuildTarget.iOS)
            {
                return;
            }

            string plist = Path.Combine(report.summary.outputPath, "Info.plist");
            if (!File.Exists(plist))
            {
                return;
            }

            string text = File.ReadAllText(plist);
            if (text.Contains("UIFileSharingEnabled"))
            {
                return;
            }

            int dict = text.IndexOf("<dict>", System.StringComparison.Ordinal);
            if (dict < 0)
            {
                return;
            }

            int insert = text.IndexOf('\n', dict) + 1;
            File.WriteAllText(plist, text.Insert(insert, Keys));
        }
    }
}
