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
        public int TimeStuckThreshold { get; set; } = 4;
        public bool ShowDetailedPlayerInfo { get; set; } = true;
        public bool ShowPlayerBrain { get; set; } = true;
        public bool ShowPlayerFitness { get; set; } = true;
        public bool ShowGraph { get; set; } = true;
        public bool ShowTarget { get; set; } = true;
        public bool ShowRewardGraph { get; set; } = true;
        [SettingRange(2, 25)]
        public int GenerationsToSaveForGraph { get; set; } = 5;
        [SettingRange(10, 100), SettingNeedsRelaunch()]
        public int OrganismsPerGeneration { get; set; } = 30;
        [SettingRange(1, 10), SettingNeedsRelaunch()]
        public int WeightMaximum { get; set; } = 5;
        [SettingRange(2, 50)]
        public int UpdateTargetThreshold { get; set; } = 8;
        [SettingRange(0, 20)]
        public int TargetReachedRewardFitness { get; set; } = 2;
        public bool ShowBestFitness { get; set; } = true;
        [SettingRange(1, 25)]
        public int CheckpointInterval { get; set; } = 3;
        [SettingRange(0, 500)]
        public int CheckpointToLoad { get; set; } = 20;
        [SettingRange(5, 50)]
        public int MaxTalkAttempts { get; set; } = 30;
        [SettingRange(60, 240)]
        public int TalkFrameBuffer { get; set; } = 100;
        [SettingRange(4, 30), SettingNeedsRelaunch()]

        public int CalculationsPerSecond { get; set; } = 8;
        [SettingRange(1, 10), SettingNeedsRelaunch()]

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
