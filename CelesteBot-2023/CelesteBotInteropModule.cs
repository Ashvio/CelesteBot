using Celeste.Mod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;
using System;
using System.Collections.Generic;
using Logger = Celeste.Mod.Logger;
using FMOD.Studio;
using Celeste;
using CelesteBot_2023.SimplifiedGraphics;
using static System.Runtime.InteropServices.JavaScript.JSType;
using FMOD;
using System.Collections;

/*
This is the CelesteBotInteropModule class located in the CelesteBotInteropModule.cs file. It is a module that allows 
Celeste to interact with external python applications that utilize the NEAT Machine Learning algorithm.
The module reads input from external data and applies the machine learning algorithm to it, represented in the program 
as a Population consisting of CelestePlayer instances. The LearningStyle specified in the module can be set to either 
LearningStyle.NEAT or LearningStyle.Q, representing two different learning algorithms:  the NEAT algorithm for evolving 
neural networks, and the Q-learning algorithm for tabular learning.

In addition to the module's learning functionalities, its implementation consist of multiple keyboard events which dictate
specific functionalities during runtime, such as, RunBest, RunThroughSpecies, ShowBestEachGen, and FrameLoops.
[CURRENT LINE WITH CURSOR] The code block that you have selected represents a portion of the MInput_Update method which 
handles input events. In the context of this block of code, if the last input was a request to talk to an NPC, it stops 
talking for a quick frame.
*/
namespace CelesteBot_2023
{
    public class CelesteBotInteropModule : EverestModule
    {
        public static CelesteBotInteropModule Instance;

        public override Type SettingsType => typeof(CelesteBotModuleSettings);
        public static CelesteBotModuleSettings Settings => (CelesteBotModuleSettings)Instance._Settings;

        public static string ModLogKey = "celeste-bot";

        public static ExternalActionManager ActionManager = new ExternalActionManager();
        public static ExternalGameStateManager GameStateManager = new ExternalGameStateManager();

        public static AIPlayerLoop AIPlayer;

        private static int buffer = 0; // The number of frames to wait when setting a new current player



        public static int FrameCounter { get; private set; }
        public static bool NeedGameStateUpdate { get; private set; }
        public static Action LatestAction { get; private set; }
        public static int RunActionInNFrames { get; private set; } = -1;
        public static string ActionRetrievalStatus { get; internal set; }
        public static bool NeedImmediateGameStateUpdate;
        public static bool PlayerDied;
        public static bool PlayerFinishedLevel { get; private set; }

        public static bool FitnessAppendMode = false;
        public static bool ShowNothing = false;

        // Learning
        public static bool AIEnabled = false;
        public static LearningStyle LearningStyle = LearningStyle.NEAT;

        public static bool ShowBest = false;
        public static bool RunBest = false;
        public static bool RunThroughSpecies = false;
        public static int UpToSpecies = 0;
        public static bool ShowBestEachGen = false;
        public static int UpToGen = 0;
        public static int FrameLoops = 1;


        private static int TalkCount = 0; // Counts how many times we attempted to talk to something
        private static int TalkMaxAttempts = 30; // How many attempts until we give up attempting to talk to something
        private static int MaxTimeSinceLastTalk = 100; // Number of skipped frames when we can talk if we have recently talked to something
        private static int TimeSinceLastTalk = MaxTimeSinceLastTalk; // Keeps track of frames since last talk
        internal static State BotState = State.None;
        [Flags]
        public enum State
        {
            None = 0,
            Running = 1,
            Disabled = 2
        }
        private static KeyboardState kbState; // For handling the bot enabling/disabling (state changes)
        private static int timer = 0; // Timer for Double-presses for fitness collection
        private static int ResetTime = 60;
        public static InputPlayer inputPlayer;

        private static Dictionary<string, int> times;
        public static bool IsInitializing;
        public static int InitializingFrameCounter = 0;
        private static bool IsKeyDown(Keys key)
        {
            return kbState.IsKeyDown(key);
        }

        public CelesteBotInteropModule()
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



        public override void Initialize()
        {
            IsInitializing = true;
            base.Initialize();
            AttributeUtils.Invoke<InitializeAttribute>();
            // Hey, InputPlayer should be made to work without removing self when players die
            inputPlayer = new InputPlayer(Celeste.Celeste.Instance, new InputData()); // Blank InputData when constructing. Overwrite it when needing to update inputs
            Celeste.Celeste.Instance.Components.Add(inputPlayer);

            AIPlayer = new AIPlayerLoop();
            //GeneratePlayer();
            FrameLoops = CelesteBotManager.IsWorker ? CelesteBotManager.FAST_MODE_MULTIPLIER : 1;
            TalkMaxAttempts = 30;
            MaxTimeSinceLastTalk = 100;
        }

        public override void Unload()
        {
            AttributeUtils.Invoke<UnloadAttribute>();

            On.Monocle.Engine.Draw -= Engine_Draw;
            On.Monocle.Engine.Update -= Engine_Update;
            On.Monocle.MInput.Update -= MInput_Update;
            On.Celeste.Celeste.OnSceneTransition -= OnScene_Transition;
            On.Celeste.Player.ClimbCheck -= Player_ClimbCheck;

            Logger.Log(ModLogKey, "Unload successful");
        }

        private bool Player_ClimbCheck(On.Celeste.Player.orig_ClimbCheck orig, Player self, int dir, int yAdd)
        {
            bool result = orig(self, dir, yAdd);
            AIPlayer.Episode.IsClimbing = result;
            return result;
        }

        public static void Engine_Draw(On.Monocle.Engine.orig_Draw original, Engine self, GameTime time)
        {
            //FrameCounter++; 
            //if (FrameLoops == 1 || FrameCounter % FrameLoops == 0)
            //{
            original(self, time);
            if (BotState == State.Running && Settings.DrawAlways)
            {
                Draw.SpriteBatch.Begin();
                DrawMetrics.Draw();
                Draw.SpriteBatch.End();

            }
            //}
        }

        public static Player GetPlayer()
        {
            Scene scene = Engine.Scene;
            if (scene == null)
            {
                return null;
            }
            Tracker tracker = scene.Tracker;
            if (tracker == null)
            {
                return null;
            }
            return tracker.GetEntity<Player>();
        }

        public static void MInput_Update(On.Monocle.MInput.orig_Update original)
        {
            if (!Settings.Enabled)
            {
                original();
                return;
            }

            Level level = TileFinder.GetCelesteLevel();
            if (level == null || AIPlayer.player == null)
            {
                original();
                BotState = State.Disabled;
                return;
            }
            BotState = State.Running;
            if (Settings.TrainingEnabled)
            {
                if (CheckDashTutorial(level) || CelesteBotManager.CompleteRestart(inputPlayer) || CelesteBotManager.CheckForCutsceneSkip(inputPlayer) || CelesteBotManager.CompleteCutsceneSkip(inputPlayer))
                {
                    original();
                    return;
                }
                bool talked = CheckTalk();
            }

            InputData nextInput = new InputData();
            HandleKB(nextInput);
            AIPlayer.Episode.ReachedTarget = TargetFinder.CheckTargetReached(AIPlayer.player);
            if ((AIPlayer.player.Dead && !AIPlayer.WaitingForRespawn))
            {
                NeedImmediateGameStateUpdate = true;
                AIPlayer.WaitingForRespawn = true;
                PlayerDied = AIPlayer.player.Dead; 
                PlayerFinishedLevel = AIPlayer.Episode.ReachedTarget;
            }
            else if (AIPlayer.WaitingForRespawn && AIPlayer.player.Dead)
            {
                // clear movement buffer
                nextInput = new InputData();
                inputPlayer.UpdateData(nextInput);
            } else if (AIPlayer.WaitingForRespawn && !AIPlayer.player.Dead)
            {
                AIPlayer.WaitingForRespawn = false;
            }
            // Handle RL state machine below
            AIPlayer.Episode.IncrementFrames();
            if (!AIPlayer.Episode.FirstObservationSent)
            {
                AIPlayer.Episode.NewEpisodeSetOriginalDistance();
                AIPlayer.ProcessGameState(false, false);
                bool observationSent = true;
                AIPlayer.Episode.FirstObservationSent = observationSent;

                RunActionInNFrames = GetActionFrameDelay(observationSent);
            }
            else if (RunActionInNFrames != -1)
            {
                if (RunActionInNFrames == 0)
                {
                    // We have an action waiting for us!
                    Action nextAction = ActionManager.GetNextAction();
                    // send new inputs to player
                    nextInput.UpdateData(nextAction);
                    inputPlayer.UpdateData(nextInput);
                    RunActionInNFrames = -1;
                    LatestAction = nextAction;
                    NeedGameStateUpdate = true;
                    original();
                    return;
                }
                else
                {
                    RunActionInNFrames -= 1;
                }
            }
            
            else if (NeedImmediateGameStateUpdate || (NeedGameStateUpdate && AIPlayer.Episode.IsCalculateFrame()) || !Settings.TrainingEnabled)
            {
                // get reward and observation
                AIPlayer.ProcessGameState(PlayerDied, PlayerFinishedLevel);
                
                double reward = AIPlayer.Episode.GetReward();

                GameStateManager.AddReward(reward);
                if (!NeedImmediateGameStateUpdate)
                {
                    RunActionInNFrames = GetActionFrameDelay(true);
                }
                else
                {
                    // if player is dead or level finished, first need to reset the episode
                    AIPlayer.Episode.ResetEpisode();
                    PlayerDied = false;
                    PlayerFinishedLevel = false;
                }
                NeedGameStateUpdate = false;
                NeedImmediateGameStateUpdate = false;

            }

            original();
        }
        private static int GetActionFrameDelay(bool observationSent)
        {
            return (observationSent && Settings.TrainingEnabled) ? Settings.ActionCalculationFrames : -1;
        }

        private static bool CheckDashTutorial(Level level)
        {
            if (AIPlayer.player.StateMachine.State == 11 && !AIPlayer.player.OnGround() && inputPlayer.LastData.Dash != true && (level.Session.MapData.Filename + "_" + level.Session.Level == "0-Intro_3"))// this makes sure we retry
            {
                // This means we are in the bird tutorial.
                // Make us finish it right away.
                InputData data = new InputData();
                data.MoveX = 1;
                data.MoveY = -1;
                data.Dash = true;
                inputPlayer.UpdateData(data);
                Logger.Log(ModLogKey, "The player is in the dash cutscene, so we tried to get them out of it by dashing.");

                return true;
            }
            return false;
        }

        static bool CheckTalk()
        {
            if (TalkComponent.PlayerOver != null && TalkCount < TalkMaxAttempts && TimeSinceLastTalk >= MaxTimeSinceLastTalk)
            {
                if (inputPlayer.LastData.Talk)
                    return false;
                // Hey we can talk!
                InputData data = new InputData();
                data.Talk = true;
                inputPlayer.UpdateData(data);
                Logger.Log(ModLogKey, "We tried to talk!");
                TalkCount++;
                TimeSinceLastTalk = 0;
                return true;
            }
            if (Celeste.TalkComponent.PlayerOver == null)
            {
                TimeSinceLastTalk = MaxTimeSinceLastTalk;
                TalkCount = 0;
            }
            TimeSinceLastTalk++;
            return true;
        }
        static bool HandleKB(InputData nextInput)
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
                FrameLoops = CelesteBotManager.FAST_MODE_MULTIPLIER;
            }
            else if (IsKeyDown(Keys.T))
            {
                Settings.TrainingEnabled = !Settings.TrainingEnabled;
            }
            else if (FrameLoops > 1 && FrameLoops < CelesteBotManager.FAST_MODE_MULTIPLIER && IsKeyDown(Keys.H))
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

                        Celeste.Player player = Engine.Scene.Tracker.GetEntity<Celeste.Player>();
                        Celeste.Level level = (Celeste.Level)Engine.Scene;
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
                        System.IO.File.AppendAllText(@"fitnesses.fit", level.Session.MapData.Filename + "_" + level.Session.Level + "_" + times[level.Session.MapData.Filename + "_" + level.Session.Level] + ": [" + player.BottomCenter.X + ", " + player.BottomCenter.Y + ", " + player.Speed.X + ", " + player.Speed.Y + "]\n");

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


            else if (IsKeyDown(Keys.OemPeriod))
            {
                BotState = State.Disabled;
                nextInput.QuickRestart = true;
            }
            else if (IsKeyDown(Keys.OemComma))
            {
                BotState = State.Disabled;
                nextInput.ESC = true;
            }
            else if (IsKeyDown(Keys.OemQuestion))
            {
                BotState = State.Disabled;
                //GeneratePlayer();
            }
            else if (IsKeyDown(Keys.N) && IsKeyDown(Keys.LeftShift))
            {
                ShowNothing = false;
            }
            else if (IsKeyDown(Keys.N))
            {
                ShowNothing = true;
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
        public static void OnScene_Transition(On.Celeste.Celeste.orig_OnSceneTransition original, Celeste.Celeste self, Scene last, Scene next)
        {
            original(self, last, next);
            if (!AIPlayer.VisionSetup)
            {
                AIPlayer.SetupVision();
            }
            //TileFinder.GetAllEntities();
            ResetRooms();
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
