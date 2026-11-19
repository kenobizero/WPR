using System;
using System.Globalization;

namespace WPR.Engine.Graphics
{
    /// <summary>Supersampling factor for the game backbuffer. 1.0 = off.</summary>
    public static class RenderScale
    {
        /// <summary>Set to 1.0f to switch the feature off.</summary>
        public const float DefaultFactor = 1.5f;
        public const float MinFactor = 1.0f;
        public const float MaxFactor = 4.0f;

        private static float _factor = Initial();

        public static float Factor
        {
            get { return _factor; }
            set { _factor = Clamp(value); }
        }

        /// <summary>
        /// When true, screen-sized render targets the game creates are enlarged by <see cref="Factor"/>
        /// too, not just the backbuffer. Many games draw their 3D scene into their own render target,
        /// which backbuffer scaling alone never touches. Off unless a head turns it on (the Android head
        /// does when the player picks a factor) or WPR_RENDER_SCALE_RT is set to anything but 0.
        /// </summary>
        public static bool ScaleRenderTargets { get; set; } = EnvFlag("WPR_RENDER_SCALE_RT");

        /// <summary>
        /// When true, the backbuffer is rendered at the display's native resolution instead of a fixed
        /// <see cref="Factor"/>: the backend works the factor out from the window's drawable size.
        /// </summary>
        public static bool UseNative { get; set; }

        /// <summary>
        /// Which of a game's own render targets the supersampling reaches (see
        /// <see cref="TargetsAll"/> and friends). A switch for isolating a driver-specific glitch.
        /// </summary>
        public static int TargetMode { get; set; }

        /// <summary>Every screen-sized target is enlarged.</summary>
        public const int TargetsAll = 0;
        /// <summary>Only targets that are almost exactly screen-sized are enlarged.</summary>
        public const int TargetsFullScreenOnly = 1;
        /// <summary>Every screen-sized target is enlarged, but sampling it stays as the game asked.</summary>
        public const int TargetsNoAnisotropy = 2;
        /// <summary>No render target is enlarged; only the backbuffer is.</summary>
        public const int TargetsNone = 3;

        private static bool EnvFlag(string name)
        {
            string env = Environment.GetEnvironmentVariable(name);
            return !string.IsNullOrWhiteSpace(env) && env.Trim() != "0";
        }

        private static float Initial()
        {
            string env = Environment.GetEnvironmentVariable("WPR_RENDER_SCALE");
            float parsed;
            if (!string.IsNullOrWhiteSpace(env)
                && float.TryParse(env.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return Clamp(parsed);
            }
            return Clamp(DefaultFactor);
        }

        private static float Clamp(float value)
        {
            if (float.IsNaN(value) || value < MinFactor) return MinFactor;
            if (value > MaxFactor) return MaxFactor;
            return value;
        }
    }
}
