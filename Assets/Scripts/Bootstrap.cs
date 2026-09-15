using UnityEngine;

namespace EarthRecovery
{
    public sealed class Bootstrap : MonoBehaviour
    {
        void Awake()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            var root = new GameObject("Earth Recovery Session");
            root.AddComponent<DisplayPreferences>();
            var session = root.AddComponent<NetworkSession>();
            var world = root.AddComponent<WorldView>();
            var hud = root.AddComponent<GameHud>();
            var voice = root.AddComponent<RadioVoice>();
            session.world = world; session.radio = voice; world.session = session; world.hud = hud;
            hud.session = session; hud.world = world; voice.session = session;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--terminal-qa") >= 0)
            { root.AddComponent<TerminalSmoke>(); return; }
#endif
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--qa-role") < 0)
            {
                var title = root.AddComponent<MainMenu>(); title.Initialize(session, hud);
                var lobby = root.AddComponent<ExpeditionLobby>(); lobby.Initialize(session, hud, title);
                root.AddComponent<WaitingRoom>().Initialize(session, hud, lobby);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--display-qa") >= 0) root.AddComponent<DisplaySmoke>();
#endif
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--qa-role") >= 0)
            {
                world.Automated = true;
                root.AddComponent<NetworkSmoke>().session = session;
            }
#endif
        }
    }
}
