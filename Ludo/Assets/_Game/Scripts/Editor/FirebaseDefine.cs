using System.IO;
using UnityEditor;
using UnityEditor.Build;

namespace Ludo.EditorTools
{
    /// <summary>
    /// Keeps the LUDO_FIREBASE scripting symbol in step with the Firebase SDK: on while its Analytics plugin is in the project, off
    /// otherwise. GameAnalytics.cs only talks to Firebase when the symbol is on, so the game builds either way.
    /// </summary>
    [InitializeOnLoad]
    static class FirebaseDefine
    {
        const string Symbol = "LUDO_FIREBASE";

        static FirebaseDefine()
        {
            // a file named Library/ludo-no-firebase keeps the symbol off (to test a build without the analytics code)
            bool present = File.Exists("Assets/Firebase/Plugins/Firebase.Analytics.dll") && !File.Exists("Library/ludo-no-firebase");
            var target = NamedBuildTarget.Android;
            string current = PlayerSettings.GetScriptingDefineSymbols(target);
            var list = new System.Collections.Generic.List<string>(current.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries));
            bool has = list.Contains(Symbol);
            if (present == has) return;
            if (present) list.Add(Symbol); else list.Remove(Symbol);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", list));
        }
    }
}
