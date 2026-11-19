using System;
using System.Globalization;

namespace WPR.Engine.Graphics
{
    /// <summary>
    /// Extra image-quality options applied by the scaling graphics backend: multisample
    /// anti-aliasing of the game backbuffer and anisotropic texture filtering. 0 / 1 = off.
    /// </summary>
    public static class GraphicsQuality
    {
        public const int MaxMsaa = 4;
        public const int MaxAnisotropy = 16;

        private static int _msaa = FromEnv("WPR_MSAA", 0, MaxMsaa);
        private static int _anisotropy = FromEnv("WPR_ANISO", 1, MaxAnisotropy);

        /// <summary>Backbuffer sample count: 0 (off), 2 or 4.</summary>
        public static int Msaa
        {
            get { return _msaa; }
            set { _msaa = value < 2 ? 0 : (value >= MaxMsaa ? MaxMsaa : 2); }
        }

        /// <summary>Anisotropic filtering level: 1 (off), 2, 4, 8 or 16.</summary>
        public static int Anisotropy
        {
            get { return _anisotropy; }
            set { _anisotropy = value < 2 ? 1 : (value > MaxAnisotropy ? MaxAnisotropy : value); }
        }

        private static int FromEnv(string name, int off, int max)
        {
            string env = Environment.GetEnvironmentVariable(name);
            int parsed;
            if (!string.IsNullOrWhiteSpace(env)
                && int.TryParse(env.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed < 2 ? off : Math.Min(parsed, max);
            }
            return off;
        }
    }
}
