// ============================================================
// DESK 42 — Desk42 CLI Bootstrap
//
// Registers every CLI tool with Desk42CLI at static-init time.
// Whatever console UI exists just needs to call Desk42CLI.Execute().
// ============================================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using UnityEngine;

namespace Desk42.Debugging
{
    public static class Desk42CliBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterTools()
        {
            Desk42CLI.Register("bsm", BsmCliTool.Run);
            Desk42CLI.Register("entropy", EntropyCliTool.Run);
        }
    }
}

#endif
