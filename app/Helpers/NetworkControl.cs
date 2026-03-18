using System.Net.NetworkInformation;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GHelper.Helpers
{
    public static class NetworkControl
    {
        // ── Public state ──────────────────────────────────────────────────────
        public static string  WifiSSID      = string.Empty;
        public static bool    IsOnline       = false;
        public static float   DownloadSpeed  = 0f;   // KB/s
        public static float   UploadSpeed    = 0f;   // KB/s

        // ── Private state ─────────────────────────────────────────────────────
        private static long     _lastBytesRecv  = -1;
        private static long     _lastBytesSent  = -1;
        private static DateTime _lastSample     = DateTime.MinValue;

        private static string   _cachedSSID     = string.Empty;
        private static DateTime _lastSSIDCheck   = DateTime.MinValue;

        // Simple exponential smoothing to stop jumpy numbers (α = 0.4)
        private const float ALPHA = 0.4f;
        private static float _smoothDl = 0f;
        private static float _smoothUl = 0f;

        // Safety cap: if a single delta exceeds this it's probably a counter
        // reset or a virtual adapter spike – discard it.
        private const long MAX_BYTES_PER_SEC = 1_250_000_000L;  // 10 Gbps

        // ── Main entry point (called every 1 s from HardwareControl) ──────────
        public static void Refresh()
        {
            try
            {
                RefreshSSID();
                RefreshOnlineStatus();
                RefreshSpeeds();
            }
            catch (Exception ex)
            {
                Logger.WriteLine("NetworkControl error: " + ex.Message);
            }
        }

        // ── SSID – cache for 10 s to avoid spawning netsh every second ────────
        private static void RefreshSSID()
        {
            if ((DateTime.Now - _lastSSIDCheck).TotalSeconds < 10) return;
            _lastSSIDCheck = DateTime.Now;

            try
            {
                var psi = new ProcessStartInfo("netsh")
                {
                    Arguments            = "wlan show interfaces",
                    RedirectStandardOutput = true,
                    UseShellExecute      = false,
                    CreateNoWindow       = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) { _cachedSSID = string.Empty; WifiSSID = _cachedSSID; return; }

                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(2000);

                // Line like:  "    SSID                   : MyNetwork"
                var m = Regex.Match(output, @"^\s+SSID\s+:\s(.+)$", RegexOptions.Multiline);
                _cachedSSID = m.Success ? m.Groups[1].Value.Trim() : string.Empty;
            }
            catch { _cachedSSID = string.Empty; }

            WifiSSID = _cachedSSID;
        }

        // ── Online: fast-path using NetworkInterface ──────────────────────────
        private static void RefreshOnlineStatus()
        {
            IsOnline = NetworkInterface.GetIsNetworkAvailable()
                       && !string.IsNullOrEmpty(WifiSSID);
        }

        // ── Speed: use only the WiFi adapter matching current SSID name ───────
        // Falls back to summing all physical (non-loopback, non-virtual) adapters.
        private static void RefreshSpeeds()
        {
            long totalRecv = 0, totalSent = 0;
            bool found = false;

            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (!IsPhysical(ni)) continue;

                try
                {
                    var stats = ni.GetIPv4Statistics();
                    totalRecv += stats.BytesReceived;
                    totalSent += stats.BytesSent;
                    found = true;
                }
                catch { /* adapter may throw on some systems */ }
            }

            if (!found) { DownloadSpeed = 0; UploadSpeed = 0; return; }

            var now = DateTime.Now;

            if (_lastBytesRecv >= 0 && _lastSample != DateTime.MinValue)
            {
                double secs = (now - _lastSample).TotalSeconds;
                if (secs > 0.1)
                {
                    long  dRecv = totalRecv - _lastBytesRecv;
                    long  dSent = totalSent - _lastBytesSent;

                    // Discard negative or unrealistically large deltas (counter reset)
                    if (dRecv >= 0 && dSent >= 0 &&
                        dRecv < MAX_BYTES_PER_SEC && dSent < MAX_BYTES_PER_SEC)
                    {
                        float rawDl = (float)(dRecv / secs / 1024);  // KB/s
                        float rawUl = (float)(dSent / secs / 1024);

                        // Exponential smoothing
                        _smoothDl = ALPHA * rawDl + (1 - ALPHA) * _smoothDl;
                        _smoothUl = ALPHA * rawUl + (1 - ALPHA) * _smoothUl;

                        DownloadSpeed = _smoothDl;
                        UploadSpeed   = _smoothUl;
                    }
                    // else: keep previous value (don't spike)
                }
            }

            _lastBytesRecv = totalRecv;
            _lastBytesSent = totalSent;
            _lastSample    = now;
        }

        /// <summary>
        /// Returns true for physical / real adapters only.
        /// Excludes loopback, tunnel, VPN virtual adapters that spike randomly.
        /// </summary>
        private static bool IsPhysical(NetworkInterface ni)
        {
            if (ni.OperationalStatus != OperationalStatus.Up) return false;

            switch (ni.NetworkInterfaceType)
            {
                case NetworkInterfaceType.Loopback:
                case NetworkInterfaceType.Tunnel:
                    return false;
            }

            // Skip known virtual/software adapter names
            string name = ni.Description.ToLowerInvariant();
            if (name.Contains("virtual") ||
                name.Contains("vmware") ||
                name.Contains("hyper-v") ||
                name.Contains("miniport") ||
                name.Contains("pseudo") ||
                name.Contains("microsoft wi-fi direct") ||
                name.Contains("bluetooth"))
                return false;

            return true;
        }

        /// <summary>
        /// Format KB/s → human-readable string (no arrow, just number + unit).
        /// </summary>
        public static string FormatSpeed(float kbps)
        {
            if (kbps >= 1024f)
                return $"{kbps / 1024f:F1} MB/s";
            if (kbps >= 0.5f)
                return $"{kbps:F0} KB/s";
            return "0 KB/s";
        }
    }
}
