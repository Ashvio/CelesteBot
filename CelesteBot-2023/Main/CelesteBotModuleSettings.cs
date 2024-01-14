using Celeste.Mod;
using CelesteBot_2023.SimplifiedGraphics;
using System.Linq;

namespace CelesteBot_2023
{
    public class CelesteBotModuleSettings : EverestModuleSettings
    {
        public bool SimplifiedGraphics { get; set; } = true;

        public bool Enabled { get; set; } = true;
        public bool TrainingEnabled { get; set; } = true;

        public bool DrawAlways { get; set; } = true;

        [SettingRange(1, 10)]
        public int UpdateTargetThreshold { get; set; } = 8;

        // Number of frames waiting between observation being sent and action being received
        [SettingRange(1, 10)]
        public int ActionCalculationFrames { get; set; } = 2;

        // MUST be a factor of 60
        [SettingRange(1, 30), SettingNeedsRelaunch()]
        public int CalculationsPerSecond { get; set; } = 4;

        [SettingRange(60, 3000), SettingNeedsRelaunch()]
        public int XMaxCacheSize { get; set; } = 1000;
        [SettingRange(60, 3000), SettingNeedsRelaunch()]
        public int YMaxCacheSize { get; set; } = 1000;
        [SettingRange(1, 60), SettingNeedsRelaunch()]
        public int EntityCacheUpdateFrames { get; set; } = 10;
        [SettingRange(2, 100), SettingNeedsRelaunch()]
        public int FastModeMultiplier { get; set; } = 10;
        [SettingRange(50, 100), SettingNeedsRelaunch()]
        public int ActionThreshold { get; set; } = 55;
        [SettingRange(1, 100), SettingNeedsRelaunch()]


        // simplified
        public int? SimplifiedLighting { get; set; } = 10;
        public int? SimplifiedBloomBase { get; set; } = 0;
        public int? SimplifiedBloomStrength { get; set; } = 1;
        private SimplifiedGraphicsFeature.SolidTilesStyle simplifiedSolidTilesStyle;

        public SimplifiedGraphicsFeature.SolidTilesStyle SimplifiedSolidTilesStyle
        {
            get => simplifiedSolidTilesStyle;
            set
            {
                if (simplifiedSolidTilesStyle != value && SimplifiedGraphicsFeature.SolidTilesStyle.All.Any(style => style.Value == value.Value))
                {
                    simplifiedSolidTilesStyle = value;
                    if (SimplifiedGraphics)
                    {
                        SimplifiedGraphicsFeature.ReplaceSolidTilesStyle();
                    }
                }
            }
        }
        public SimplifiedGraphicsFeature.SpinnerColor SimplifiedSpinnerColor { get; set; } = SimplifiedGraphicsFeature.SpinnerColor.All[1];

    }
}
