using Celeste.Mod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Logger = Celeste.Mod.Logger;
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
        public static ExternalObservationManager ObservationManager = new ExternalObservationManager();

        public static CelestePlayer CurrentPlayer;

        private static int buffer = 0; // The number of frames to wait when setting a new current player


        public static bool DrawPlayer { get { return !ShowNothing && Settings.ShowPlayerBrain; } set { } }
        public static bool DrawDetails { get { return !ShowNothing && Settings.ShowDetailedPlayerInfo; } set { } }
        public static bool DrawBestFitness { get { return !ShowNothing && Settings.ShowBestFitness; } set { } }
        public static bool DrawGraph { get { return !ShowNothing && Settings.ShowGraph; } set { } }
        public static bool DrawTarget { get { return !ShowNothing && Settings.ShowTarget; } set { } }
        public static bool DrawRewardGraph { get { return !ShowNothing && Settings.ShowRewardGraph && LearningStyle == LearningStyle.Q; } set { } }
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

        public static CelestePlayer SpeciesChamp;
        public static CelestePlayer GenPlayerTemp;
        
        private static int TalkCount = 0; // Counts how many times we attempted to talk to something
        private static int TalkMaxAttempts = 30; // How many attempts until we give up attempting to talk to something
        private static int MaxTimeSinceLastTalk = 100; // Number of skipped frames when we can talk if we have recently talked to something
        private static int TimeSinceLastTalk = MaxTimeSinceLastTalk; // Keeps track of frames since last talk

        private static State state = State.None;
        [Flags]
        private enum State
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

        private static bool IsKeyDown(Keys key)
        {
            return kbState.IsKeyDown(key);
        }

        public CelesteBotInteropModule()
        {
            Instance = this;
        }

        public override void Load()
        {
            On.Monocle.Engine.Draw += Engine_Draw;
            //On.Monocle.Engine.Update += Engine_Update;
            On.Monocle.MInput.Update += MInput_Update;
            On.Celeste.Celeste.OnSceneTransition += OnScene_Transition;

            Logger.Log(ModLogKey, "Load successful");
        }
        public override void Initialize()
        {
            base.Initialize();
            CelesteBotManager.Initialize();

            // Hey, InputPlayer should be made to work without removing self when players die
            inputPlayer = new InputPlayer(Celeste.Celeste.Instance, new InputData()); // Blank InputData when constructing. Overwrite it when needing to update inputs
            Celeste.Celeste.Instance.Components.Add(inputPlayer);

            CurrentPlayer = new CelestePlayer();
            //GeneratePlayer();

            TalkMaxAttempts = Settings.MaxTalkAttempts;
            MaxTimeSinceLastTalk = Settings.TalkFrameBuffer;
        }
        //public static void GeneratePlayer()
        //{
        //    CurrentPlayer = new CelestePlayer();
        //    CurrentPlayer.Brain.GenerateNetwork();
        //    CurrentPlayer.Brain.Mutate(innovationHistory);
        //}
        public override void Unload()
        {
            //On.Monocle.Engine.Draw -= Engine_Draw;
            On.Monocle.Engine.Update -= Engine_Update;
            On.Monocle.MInput.Update -= MInput_Update;
            On.Celeste.Celeste.OnSceneTransition -= OnScene_Transition;
            Logger.Log(ModLogKey, "Unload successful");
        }

        public static void Engine_Draw(On.Monocle.Engine.orig_Draw original, Engine self, GameTime time)
        {
            original(self, time);
            //if (state == State.Running || Settings.DrawAlways || FitnessAppendMode) {
            //    CelesteBotManager.Draw();
            //}
        }

        private static void Reset(InputData temp)
        {
            temp.QuickRestart = true;
            buffer = CelesteBotManager.PLAYER_GRACE_BUFFER; // sets the buffer to desired wait time... magic
            inputPlayer.UpdateData(temp);
        }
        
        public static void MInput_Update(On.Monocle.MInput.orig_Update original)
        {   
            // Comment this function  
            if (!Settings.Enabled)
            {
                original();
                return;
            }
            try
            {
                Celeste.Player player = Celeste.Celeste.Scene.Tracker.GetEntity<Celeste.Player>();
                if (Celeste.TalkComponent.PlayerOver != null && TalkCount < TalkMaxAttempts && TimeSinceLastTalk >= MaxTimeSinceLastTalk)
                {
                    if (inputPlayer.LastData.Talk)
                    {
                        // Lets stop talking for a quick frame
                        throw new InvalidCastException("This is just to get us out of the rest of this try-catch, normal operation applies again");
                    }
                    // Hey we can talk!
                    InputData data = new InputData();
                    data.Talk = true;
                    inputPlayer.UpdateData(data);
                    Logger.Log(ModLogKey, "We tried to talk!");
                    TalkCount++;
                    TimeSinceLastTalk = 0;
                    return;
                } if (Celeste.TalkComponent.PlayerOver == null)
                {
                    TimeSinceLastTalk = MaxTimeSinceLastTalk;
                    TalkCount = 0;
                }
                TimeSinceLastTalk++;
                Celeste.Level level = TileFinder.GetCelesteLevel();
                if (player.StateMachine.State == 11 && !player.OnGround() && inputPlayer.LastData.Dash != true && (level.Session.MapData.Filename + "_" + level.Session.Level == "0-Intro_3"))// this makes sure we retry
                {
                    // This means we are in the bird tutorial.
                    // Make us finish it right away.
                    InputData data = new InputData();
                    data.MoveX = 1;
                    data.MoveY = -1;
                    data.Dash = true;
                    inputPlayer.UpdateData(data);
                    Logger.Log(ModLogKey, "The player is in the dash cutscene, so we tried to get them out of it by dashing.");

                    return;
                }

            } catch (Exception ex)
            {
                // level doesn't exist yet
                if (ex is NullReferenceException || ex is InvalidCastException)
                {
                    original();
                    AIEnabled = false;
                    return;
                } 
                else
                {
                    throw;
                }
            }
            AIEnabled = true;
            if (CelesteBotManager.CompleteRestart(inputPlayer))
            {
                Logger.Log(ModLogKey, "Restarting!");
                return;
            }
            // If in cutscene skip state, skip it the rest of the way.
            if (CelesteBotManager.CheckForCutsceneSkip(inputPlayer))
            {
                Logger.Log(ModLogKey, "Confirmed a cutscene skip!");
                return;
            }
            if (CelesteBotManager.CompleteCutsceneSkip(inputPlayer))
            {
                Logger.Log(ModLogKey, "Completing a Cutscene Skip!");
                return;
            }
            

            kbState = Keyboard.GetState();
            Action nextAction = ActionManager.GetNextAction();
            InputData nextInput = new InputData(nextAction);
            bool handled = HandleKB(nextInput);
            if (handled)
            {
                original();
                return;
            }

            inputPlayer.UpdateData(nextInput);

            original();
        }
        static bool HandleKB(InputData nextInput)
        {
            if (IsKeyDown(Keys.A))
            {
                FitnessAppendMode = !FitnessAppendMode;
            }
            if (FitnessAppendMode)
            {
                if (IsKeyDown(Keys.Space) && timer <= 0)
                {
                    // Add it to the file
                    try
                    {

                        Celeste.Player player = Celeste.Celeste.Scene.Tracker.GetEntity<Celeste.Player>();
                        Celeste.Level level = (Celeste.Level)Celeste.Celeste.Scene;
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
                    catch (NullReferenceException e)
                    {
                        // The room/player does not exist, just skip this
                    }
                    catch (InvalidCastException e)
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
                state = State.Disabled;
                nextInput.QuickRestart = true;
            }
            else if (IsKeyDown(Keys.OemComma))
            {
                state = State.Disabled;
                nextInput.ESC = true;
            }
            else if (IsKeyDown(Keys.OemQuestion))
            {
                state = State.Disabled;
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
            else if (IsKeyDown(Keys.F) && IsKeyDown(Keys.LeftShift))
            {
                FrameLoops = 1;
            }
            else if (IsKeyDown(Keys.F))
            {
                FrameLoops = CelesteBotManager.FAST_MODE_MULTIPLIER;
            }
            if (state == State.Running)
            {
                if (buffer > 0)
                {
                    buffer--;
                    //original();
                    inputPlayer.UpdateData(nextInput);
                    return true;
                }

            }
            return false;
        }
        public static void Engine_Update(On.Monocle.Engine.orig_Update original, Engine self, GameTime gameTime)
        {
            //try
            //{
            //    if (CurrentPlayer.player.Dead)
            //    {
            //        CurrentPlayer.Dead = true;
            //        InputData data = new InputData();
            //        data.QuickRestart = true;
            //        inputPlayer.UpdateData(data);
            //    }
            //} catch (NullReferenceException e)
            //{
            //    // Player has not been setup yet
            //}
            
            for (int i = 0; i < FrameLoops; i++)
            {
                original(self, gameTime);
            }
        }
        public static void OnScene_Transition(On.Celeste.Celeste.orig_OnSceneTransition original, Celeste.Celeste self, Scene last, Scene next)
        {
            original(self, last, next);
            if (!CurrentPlayer.VisionSetup)
            {
                CurrentPlayer.SetupVision();
            }
            //TileFinder.GetAllEntities();
            ResetRooms();
        }
        private static void ResetRooms()
        {
            times = new Dictionary<string, int>();
        }
    }
}
