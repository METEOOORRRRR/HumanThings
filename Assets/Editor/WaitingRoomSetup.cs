using System.IO;
using System.Linq;
using UnityEditor;
namespace EarthRecovery.Editor
{
    public static class WaitingRoomSetup
    {
        public static void Prepare()
        {
            if (File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset")) return;
            var package = Directory.GetFiles("Library/PackageCache", "TMP Essential Resources.unitypackage", SearchOption.AllDirectories).FirstOrDefault();
            if (package == null) throw new FileNotFoundException("TextMeshPro essential resources package missing");
            AssetDatabase.ImportPackage(package, false);
            AssetDatabase.Refresh();
            if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
                throw new FileNotFoundException("Import TMP Essential Resources before building the waiting room");
        }
    }
}
