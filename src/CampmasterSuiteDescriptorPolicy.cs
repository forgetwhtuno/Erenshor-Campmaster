using System;

namespace ErenshorCampmaster
{
    // Unity-free Suite Hub descriptor/mutation policy. Campmaster deliberately exposes only the
    // existing recognition enable toggle; recognition thresholds remain normal config rather than
    // turning the Hub into a tuning console for internal heuristics.
    internal static class CampmasterSuiteDescriptorPolicy
    {
        internal const int MaxHubText = 200;

        internal static string BuildDescribe(string version, string status)
        {
            return "protocol=1"
                + "&module=campmaster"
                + "&display=" + Escape("Campmaster")
                + "&version=" + Escape(Bound(version, 32))
                + "&summary=" + Escape("Read-only Hunt Camp and explicit Relax context")
                + "&status=" + Escape(Bound(status, MaxHubText))
                + "&actions=openPanel,closePanel,relaxHere,relaxOff";
        }

        internal static string BuildBasicSettings(bool autoRecognition)
        {
            return "id=autoRecognition"
                + "&label=" + Escape("Automatic Hunt Camp recognition")
                + "&tier=basic&type=bool&value=" + (autoRecognition ? "true" : "false")
                + "&mutable=true";
        }

        internal static bool TryNormalizeSettingValue(string settingId, string value, out string normalized)
        {
            if (!string.Equals((settingId ?? string.Empty).Trim(), "autoRecognition", StringComparison.OrdinalIgnoreCase))
            {
                normalized = null;
                return false;
            }
            bool parsed;
            if (!TryParseWireBool(value, out parsed))
            {
                normalized = null;
                return false;
            }
            normalized = parsed ? "true" : "false";
            return true;
        }

        internal static bool TryParseWireBool(string value, out bool parsed)
        {
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)) { parsed = true; return true; }
            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)) { parsed = false; return true; }
            parsed = false;
            return false;
        }

        internal static bool ContainsSensitiveFieldName(string payload)
        {
            string lower = Uri.UnescapeDataString(payload ?? string.Empty).ToLowerInvariant();
            string[] forbidden = { "apikey", "api key", "endpoint", "filepath", "filesystem", "memory", "conversation", "prompt", "windows username" };
            for (int i = 0; i < forbidden.Length; i++)
                if (lower.IndexOf(forbidden[i], StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        private static string Bound(string value, int max)
        {
            string safe = value ?? string.Empty;
            return safe.Length <= max ? safe : safe.Substring(0, max);
        }

        private static string Escape(string value)
        {
            return Uri.EscapeDataString(value ?? string.Empty);
        }
    }
}
