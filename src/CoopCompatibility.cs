using System;
using System.Reflection;
using UnityEngine;

namespace ErenshorCampmaster
{
    // Optional reflection-only compatibility with Erenshor COOP. Campmaster takes no
    // compile-time COOP reference and never invents a networking protocol of its own.
    internal static class CoopCompatibility
    {
        // COOP 2.3.1 names observed by the current Deep Sims compatibility layer.
        private const string NetworkedPlayerTypeName = "ErenshorCoop.NetworkedPlayer";
        private const string LegacyNetworkedPlayerTypeName = "ErenshorCoop.Client.NetworkedPlayer";
        private const string NetworkedSimTypeName = "ErenshorCoop.NetworkedSim";

        private static readonly object ResolveLock = new object();
        private static volatile bool _resolved;
        private static Type _networkedPlayerType;
        private static Type _legacyNetworkedPlayerType;
        private static Type _networkedSimType;

        static CoopCompatibility()
        {
            try { AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad; }
            catch { }
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            _resolved = false;
        }

        // Lunaris can unload this assembly at runtime. The AppDomain event outlives the plugin
        // GameObject, so an unremoved handler would retain a delegate into the old assembly.
        // Clear both the handler and the reflected cross-mod type cache during teardown; a
        // reloaded assembly gets fresh statics.
        internal static void Shutdown()
        {
            try { AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad; }
            catch { }
            lock (ResolveLock)
            {
                _resolved = false;
                _networkedPlayerType = null;
                _legacyNetworkedPlayerType = null;
                _networkedSimType = null;
            }
        }

        internal static bool IsRemoteCoopHuman(SimPlayer sim)
        {
            if (sim == null) return false;
            EnsureResolved();
            return HasComponent(sim, _networkedPlayerType) || HasComponent(sim, _legacyNetworkedPlayerType);
        }

        internal static bool IsRemoteCoopSim(SimPlayer sim)
        {
            if (sim == null) return false;
            EnsureResolved();
            return HasComponent(sim, _networkedSimType);
        }

        private static void EnsureResolved()
        {
            if (_resolved) return;
            lock (ResolveLock)
            {
                if (_resolved) return;
                try
                {
                    Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                    for (int i = 0; i < assemblies.Length; i++)
                    {
                        Assembly assembly = assemblies[i];
                        if (assembly == null) continue;
                        if (_networkedPlayerType == null)
                            _networkedPlayerType = assembly.GetType(NetworkedPlayerTypeName, false);
                        if (_legacyNetworkedPlayerType == null)
                            _legacyNetworkedPlayerType = assembly.GetType(LegacyNetworkedPlayerTypeName, false);
                        if (_networkedSimType == null)
                            _networkedSimType = assembly.GetType(NetworkedSimTypeName, false);
                    }
                }
                catch { }
                _resolved = true;
            }
        }

        private static bool HasComponent(SimPlayer sim, Type componentType)
        {
            if (sim == null || componentType == null) return false;
            GameObject go = null;
            try { go = sim.gameObject; }
            catch { }
            if (go == null) return false;
            try { return go.GetComponent(componentType) != null; }
            catch { return false; }
        }
    }
}
