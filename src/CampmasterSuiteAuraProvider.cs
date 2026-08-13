using System;
using Lunaris;
using Lunaris.IPC;

namespace ErenshorCampmaster
{
    // Optional Aura transport over CampmasterControlApi. Campmaster remains observational: the only
    // mutable Hub setting is its own recognition toggle, and the only actions are explicit Relax
    // context start/stop. No native roles, Guard, Auto Pull, targets, movement or combat are touched.
    internal sealed class CampmasterSuiteAuraProvider
    {
        private const string Prefix = "forgetwhtuno.erenshor.suite.campmaster.v1.";
        private const int MaxFieldLength = 200;

        private readonly IAuraProvider<string> _describe;
        private readonly IAuraProvider<string> _basicSettings;
        private readonly IAuraProvider<string, string, string> _setSetting;
        private readonly IAuraProvider<string, string, string> _action;

        internal CampmasterSuiteAuraProvider(LunarisPlugin owner)
        {
            _describe = owner.IPCAuraProvider<string>(Prefix + "describe");
            _basicSettings = owner.IPCAuraProvider<string>(Prefix + "settings.basic");
            _setSetting = owner.IPCAuraProvider<string, string, string>(Prefix + "setting.set");
            _action = owner.IPCAuraProvider<string, string, string>(Prefix + "action");
        }

        internal void Register()
        {
            try
            {
                _describe.RegisterFunc(Describe);
                _basicSettings.RegisterFunc(BasicSettings);
                _setSetting.RegisterFunc(SetSetting);
                _action.RegisterFunc(InvokeAction);
            }
            catch
            {
                Unregister();
                throw;
            }
        }

        internal void Unregister()
        {
            try { if (_setSetting != null) _setSetting.UnregisterFunc(); } catch { }
            try { if (_action != null) _action.UnregisterFunc(); } catch { }
            try { if (_basicSettings != null) _basicSettings.UnregisterFunc(); } catch { }
            try { if (_describe != null) _describe.UnregisterFunc(); } catch { }
        }

        private static string Describe()
        {
            return CampmasterSuiteDescriptorPolicy.BuildDescribe(CampmasterPlugin.PluginVersion, CampmasterControlApi.GetStatus());
        }

        private static string BasicSettings()
        {
            return CampmasterSuiteDescriptorPolicy.BuildBasicSettings(CampmasterControlApi.AutoRecognitionEnabled);
        }

        private static string SetSetting(string settingId, string value)
        {
            string failure;
            bool ok = CampmasterControlApi.TrySetSetting(settingId, value, out failure);
            return ok ? "ok" : ("error: " + Bound(failure ?? "rejected", MaxFieldLength));
        }

        private static string InvokeAction(string actionId, string argument)
        {
            switch (actionId)
            {
                case "relaxHere":
                {
                    string failure;
                    return CampmasterControlApi.TryRelaxHere(out failure) ? "ok" : ("rejected: " + Bound(failure, MaxFieldLength));
                }
                case "relaxOff":
                {
                    string failure;
                    return CampmasterControlApi.TryRelaxOff(out failure) ? "ok" : ("rejected: " + Bound(failure, MaxFieldLength));
                }
                default:
                    return "unknown action";
            }
        }

        private static string Bound(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max) return value ?? string.Empty;
            return value.Substring(0, max);
        }
    }
}
