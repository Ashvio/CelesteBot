using Celeste.Mod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using System;
using System.Collections.Generic;
using Logger = Celeste.Mod.Logger;
using Celeste;
using System.IO;

namespace CelesteBot_2023
{
    public class CelesteBotMain : EverestModule
    {
        public static CelesteBotMain Instance;

        public override Type SettingsType => typeof(CelesteBotModuleSettings);
        public static CelesteBotModuleSettings Settings => (CelesteBotModuleSettings)Instance._Settings;

        public static string ModLogKey = "celeste-bot";

        public static CelesteBotRunner BotRunner;

        public static readonly int VISION_2D_X_SIZE = 20; // X Size of the Vision array
        public static readonly int VISION_2D_Y_SIZE = 20; // Y Size of the Vision array
        public static int TILE_2D_X_CACHE_SIZE = 1000;
        public static int TILE_2D_Y_CACHE_SIZE = 1000;
        public static int ENTITY_CACHE_UPDATE_FRAMES = 10;
        public static int FAST_MODE_MULTIPLIER { get => Settings.FastModeMultiplier;  }

        public static float UPDATE_TARGET_THRESHOLD = 25; // Pixels in distance between the fitness target and the current position before considering it "reached"

        public static string ActionRetrievalStatus { get; internal set; }
        public static bool IsTransitioning { get; private set; }

        public static bool FitnessAppendMode = false;

        // Learning
        public static bool AIEnabled = false;

        public static int FrameLoops;
        internal static State BotState = State.None;

        public static bool IsWorker { get; set; }

        [Flags]
        public enum State
        {
            None = 0,
            Running = 1,
            Disabled = 2
        }
        private static KeyboardState kbState; // For handling the bot enabling/disabling (state changes)
        private static int timer = 0; // Timer for Double-presses for fitness collection
        private static readonly int ResetTime = 60;
        public static InputPlayer inputPlayer;

        private static Dictionary<string, int> times;
        private static bool IsKeyDown(Keys key)
        {
            return kbState.IsKeyDown(key);
        }

        public CelesteBotMain()
        {
            Instance = this;
            AttributeUtils.CollectMethods<LoadAttribute>();
            AttributeUtils.CollectMethods<UnloadAttribute>();
            AttributeUtils.CollectMethods<InitializeAttribute>();
        }
        public override void Load()
        {
            AttributeUtils.Invoke<LoadAttribute>();
            On.Monocle.Engine.Draw += Engine_Draw;
            On.Monocle.Engine.Update += Engine_Update;
            On.Monocle.MInput.Update += MInput_Update;
            On.Celeste.Celeste.OnSceneTransition += OnScene_Transition;
            On.Celeste.Player.ClimbCheck += Player_ClimbCheck;

            Logger.Log(ModLogKey, "Load successful");
        }

        static void InitializeData()
        {
            Logger.Log(ModLogKey, "CELESTEBOT Initializing");

            string currentDir = Directory.GetCurrentDirectory();
            if (currentDir.Contains("CelesteWorkers"))
            {
                IsWorker = true;
                Logger.SetLogLevel(ModLogKey, LogLevel.Info);
                Settings.TrainingEnabled = true;
            }
            else
            {
                IsWorker = false;
                Logger.SetLogLevel(ModLogKey, LogLevel.Verbose);

            }

            TILE_2D_X_CACHE_SIZE = Settings.XMaxCacheSize; // X Size of max cache size
            TILE_2D_Y_CACHE_SIZE = Settings.YMaxCacheSize; // Y Size of max cache size
            ENTITY_CACHE_UPDATE_FRAMES = Settings.EntityCacheUpdateFrames; // Frames between updating entity cache


            UPDATE_TARGET_THRESHOLD = Settings.UpdateTargetThreshold;

            Log("Finished Initializing CelesteBot");
        }

        public static void Log(string message, LogLevel level = LogLevel.Verbose)
        {
            Logger.Log(level, ModLogKey, message);
        }
        public override void Initialize()
        {
            AttributeUtils.Invoke<InitializeAttribute>();
            BotRunner = new CelesteBotRunner();
            InitializeData();
            FrameLoops = IsWorker ? FAST_MODE_MULTIPLIER : 1;
        }

        public override void Unload()
        {
            AttributeUtils.Invoke<UnloadAttribute>();

            On.Monocle.Engine.Draw -= Engine_Draw;
            On.Monocle.Engine.Update -= Engine_Update;
            On.Monocle.MInput.Update -= MInput_Update;
            On.Celeste.Celeste.OnSceneTransition -= OnScene_Transition;
            On.Celeste.Player.ClimbCheck -= Player_ClimbCheck;
            On.Celeste.Level.TransitionTo -= Level_TransitionTo;

            Logger.Log(ModLogKey, "Unload successful");
        }

        private bool Player_ClimbCheck(On.Celeste.Player.orig_ClimbCheck orig, Player self, int dir, int yAdd)
        {
            bool result = orig(self, dir, yAdd);
            BotRunner.Episode.IsClimbing = result;
            return result;
        }



        public static void Engine_Draw(On.Monocle.Engine.orig_Draw original, Engine self, GameTime time)
        {

            original(self, time);
            if (BotState == State.Running && Settings.DrawAlways)
            {
                Draw.SpriteBatch.Begin();
                DrawMetrics.Draw(BotRunner);
                Draw.SpriteBatch.End();
            }
        }


        //public static void StartRandomizerGame()
        //{
        //    string sequence = "MenuConfirm";
        //    CurrentActionSequence = ActionSequence.GenerateActionSequence(sequence);
        //    AIPlayer.NeedToRestartGame = false;
        //}
        public static void MInput_Update(On.Monocle.MInput.orig_Update original)
        {
            if (!Settings.Enabled)
            {
                original();
                return;
            }
            InputData nextInput = new();

            HandleKB();

            bool actioned = BotRunner.HandleEdgeCases(nextInput);
            if (actioned)
            {
                // Handled an edge case so move on
                original();
                return;
            }
            Level level = TileFinder.GetCelesteLevel();

            if (level != null && !level.Paused) {
                BotRunner.TryUpdateGameState(nextInput);
            }
            original();
        }
        static bool HandleKB()
        {
            kbState = Keyboard.GetState();
            if (IsKeyDown(Keys.A))
            {
                FitnessAppendMode = !FitnessAppendMode;
            }
            if (IsKeyDown(Keys.F) && IsKeyDown(Keys.LeftShift))
            {
                FrameLoops = 1;
            }
            else if (IsKeyDown(Keys.F))
            {
                FrameLoops = FAST_MODE_MULTIPLIER;
            }
            else if (IsKeyDown(Keys.T))
            {
                Settings.TrainingEnabled = !Settings.TrainingEnabled;
            }
            else if (FrameLoops > 1 && FrameLoops < FAST_MODE_MULTIPLIER && IsKeyDown(Keys.H))
            {

                FrameLoops += 1;
            }
            else if (FrameLoops > 1 && IsKeyDown(Keys.G))
            {

                FrameLoops += 1;
            }
            if (FitnessAppendMode)
            {
                if (IsKeyDown(Keys.Space) && timer <= 0)
                {
                    // Add it to the file
                    try
                    {

                        Player player = Engine.Scene.Tracker.GetEntity<Player>();
                        Level level = (Level)Engine.Scene;
                        if (times.ContainsKey(level.Session.MapData.Filename + "_" + level.Session.Level))
                        {
                            if (IsKeyDown(Keys.LeftShift))
                            {
                                times[level.Session.MapData.Filename + "_" + level.Session.Level]++;
                            }
                        }
                        else
                        {
                            times[level.Session.MapData.Filename + "_" + level.Session.Level] = 0;
                        }
                        File.AppendAllText(@"fitnesses.fit", level.Session.MapData.Filename + "_" + level.Session.Level + "_" + times[level.Session.MapData.Filename + "_" + level.Session.Level] + ": [" + player.BottomCenter.X + ", " + player.BottomCenter.Y + ", " + player.Speed.X + ", " + player.Speed.Y + "]\n");

                    }
                    catch (NullReferenceException)
                    {
                        // The room/player does not exist, just skip this
                    }
                    catch (InvalidCastException)
                    {
                        // The room isn't really a room, just skip
                    }
                    timer = ResetTime;
                }
                timer--;
                //original();
                return true;
            }
            else if (IsKeyDown(Keys.OemQuestion))
            {
                BotState = State.Disabled;
                //GeneratePlayer();
            }


            //if (state == State.Running)
            //{
            //    if (buffer > 0)
            //    {
            //        buffer--;
            //        //original();
            //        inputPlayer.UpdateData(nextInput);
            //        return true;
            //    }

            //}
            return false;
        }
        public static void Engine_Update(On.Monocle.Engine.orig_Update original, Engine self, GameTime gameTime)
        {
            //try
            //{
            //    if (Settings.TrainingEnabled && AIPlayer.player.Dead)
            //    {
            //        InputData data = new InputData();
            //        data.QuickRestart = true;
            //        inputPlayer.UpdateData(data);
            //    }
            //}
            //catch (NullReferenceException)
            //{
            //    // Player has not been setup yet
            //}
            if (FrameLoops > 1)
            {
                for (int i = 0; i < FrameLoops; i++)
                {

                    original(self, gameTime);

                    //catch (NullReferenceException)
                    //{
                    //    // we're going tooo fast, so wait 10 milliseconds
                    //    CelesteBotManager.Log("NullReferenceException on engine update, waiting 20 milliseconds", LogLevel.Warn);
                    //    System.Threading.Thread.Sleep(20);
                    //}
                }

            }
            else
            {
                original(self, gameTime);
            }
        }
        private static void Level_TransitionTo(On.Celeste.Level.orig_TransitionTo original, Level self, LevelData next, Vector2 direction)
        {
            IsTransitioning = true;
            original(self, next, direction);
            IsTransitioning = false;
        }

        public static void OnScene_Transition(On.Celeste.Celeste.orig_OnSceneTransition original, Celeste.Celeste self, Scene last, Scene next)
        {
            IsTransitioning = true;

            original(self, last, next);
            if (!BotRunner.VisionSetup)
            {
                BotRunner.SetupVision();
            }
            //TileFinder.GetAllEntities();
            ResetRooms();
            IsTransitioning = false;

        }
        private static void ResetRooms()
        {
            times = new Dictionary<string, int>();
        }

        [AttributeUsage(AttributeTargets.Method)]
        internal class LoadAttribute : Attribute { }

        [AttributeUsage(AttributeTargets.Method)]
        internal class UnloadAttribute : Attribute { }

        [AttributeUsage(AttributeTargets.Method)]
        internal class InitializeAttribute : Attribute { }
    }
}
