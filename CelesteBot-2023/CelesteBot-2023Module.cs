using System;
using Celeste.Mod;
using Microsoft.Xna.Framework;

namespace CelesteBot_2023 {
    public class CelesteBot_2023Module : EverestModule {
        public static CelesteBot_2023Module Instance { get; private set; }

        public override Type SettingsType => typeof(CelesteBot_2023ModuleSettings);
        public static CelesteBot_2023ModuleSettings Settings => (CelesteBot_2023ModuleSettings) Instance._Settings;

        public override Type SessionType => typeof(CelesteBot_2023ModuleSession);
        public static CelesteBot_2023ModuleSession Session => (CelesteBot_2023ModuleSession) Instance._Session;

        public CelesteBot_2023Module() {
            Instance = this;
#if DEBUG
            // debug builds use verbose logging
            Logger.SetLogLevel(nameof(CelesteBot_2023Module), LogLevel.Verbose);
#else
            // release builds use info logging to reduce spam in log files
            Logger.SetLogLevel(nameof(CelesteBot_2023Module), LogLevel.Info);
#endif
        }

        public override void Load() {
            // TODO: apply any hooks that should always be active
        }

        public override void Unload() {
            // TODO: unapply any hooks applied in Load()
        }
    }
}