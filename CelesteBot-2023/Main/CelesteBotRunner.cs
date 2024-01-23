using Celeste;
using Celeste.Mod.CelesteBot_2023.Main;
using Microsoft.Xna.Framework;
using Python.Runtime;
using System;
using static CelesteBot_2023.CelesteBotMain;
/*
* The CelestePlayer class represents the player in the game and is located in the same file as the CelesteBotInteropModule. 
* It contains a Brain property which represents its neural network, a Fitness property to keep track of its performance in
* the game, InputData and Vision properties which are used to provide input to the neural network, and various other properties
* and methods related to gameplay.
The Brain property is used to decide which actions to take based on the inputs given to it. The Population of players contains 
multiple instances of CelestePlayer. During gameplay, the current player is retrieved from the Population and manipulated using
the CurrentPlayer property.
*/
namespace CelesteBot_2023
{
    public class CelesteBotRunner {
        public static PyList Vision2D;

        public Player CelesteGamePlayer
        {
            get
            {
                return Monocle.Engine.Scene?.Tracker?.GetEntity<Player>(); 
            }
            
        }
        public readonly int DEATHS_TO_RESTART_RANDOMIZER = 300;
        public double LastEpisodeReward { get { return TrainingEpisodeState.LastEpisodeReward; } }
        public int DeathCounter { get; private set; }
        public bool PlayerFinishedLevel { get; set; }
        public int RunActionInNFrames { get; private set; } = -1;
        public ActionSequence CurrentActionSequence { get; private set; }
        public bool NeedToRestartGame { get; set; }
        public bool NeedGameStateUpdate { get; private set; }
        public InputAction LatestAction { get; private set; }
        public InputPlayer Input;

        public static ExternalActionManager ActionManager = new();
        public static ExternalGameStateManager GameStateManager = new();
        public bool WaitingForRespawn = false;
        public bool Replay = false;

        public bool VisionSetup = false;

        public CameraManager cameraManager;
        public TrainingEpisodeState Episode;

        public bool EpisodeEnded;
        public bool PlayerDied;

        public int EpisodeNumber;
        public int EpisodeEndedNumber;
        private bool GameInitiated;

        private int TalkCount = 0; // Counts how many times we attempted to talk to something
        private int TalkMaxAttempts = 30; // How many attempts until we give up attempting to talk to something
        private int MaxTimeSinceLastTalk = 100; // Number of skipped frames when we can talk if we have recently talked to something
        private int TimeSinceLastTalk = 100; // Keeps track of frames since last talk

        public CelesteBotRunner()
        {
            Episode = new TrainingEpisodeState(this);
            cameraManager = new(this);
            WaitingForRespawn = false;
            CurrentActionSequence = new ActionSequence();
            TalkMaxAttempts = 30;
            MaxTimeSinceLastTalk = 100;
            GameInitiated = false;
            Input = new InputPlayer(Celeste.Celeste.Instance, new InputData()); // Blank InputData when constructing. Overwrite it when needing to update inputs
            Celeste.Celeste.Instance.Components.Add(Input);
            EpisodeNumber = 0;
            EpisodeEndedNumber = 0;
        }

        public void TryUpdateGameState(InputData nextInput)
        {
            bool actionRunThisFrame = false;

            if ((CelesteGamePlayer.Dead && !WaitingForRespawn))
            {
                EpisodeEnded = true;
                WaitingForRespawn = true;
                PlayerDied = true;
                DeathCounter += 1;
                
                return;
            }
            else if (WaitingForRespawn && CelesteGamePlayer.Dead)
            {
                // clear movement buffer
                nextInput = new InputData();
                Input.UpdateData(nextInput);
                return;
            }
            else if (WaitingForRespawn && !CelesteGamePlayer.Dead)
            {
                WaitingForRespawn = false;
            }
            Episode.IncrementFrames();
            //if (DeathCounter >= DEATHS_TO_RESTART_RANDOMIZER)
            //{
            //    RestartGame();
            //    DeathCounter = 0;
            //}
            // Handle RL state machine below
            // First observation
            if (!Episode.FirstObservationSent)            {
                EpisodeNumber += 1;
                CelesteBotMain.Log("Episode " + EpisodeNumber + " started.");
                Episode.NewEpisodeSetOriginalDistance();
                TargetFinder.UpdateCurrentTarget();
                GameState gameState = ProcessGameState(false, false);
                GameStateManager.AddObservation(gameState);

                Episode.FirstObservationSent = true;

                RunActionInNFrames = GetActionFrameDelay(true);
            }
            else if (RunActionInNFrames != -1)
            {
                if (RunActionInNFrames == 0)
                {
                    // We have an action waiting for us!
                    InputAction nextAction = ActionManager.GetNextAction();
                    // send new inputs to player
                    nextInput.UpdateData(nextAction);
                    Input.UpdateData(nextInput);
                    RunActionInNFrames = -1;
                    LatestAction = nextAction;
                    NeedGameStateUpdate = true;
                    return;
                }
                else
                {
                    RunActionInNFrames -= 1;
                }
            }
            // 163/68 = 23
            //  32/15 = 5
            // Retrieve and send most recent reward
            if (/*NeedImmediateGameStateUpdate ||*/ (NeedGameStateUpdate && Episode.IsCalculateFrame()) || (!CelesteBotMain.Settings.TrainingEnabled && Episode.IsCalculateFrame()))
            {


                // get reward and observation
                TargetFinder.UpdateCurrentTarget();
                double reward = Episode.GetReward(); 
                GameState gameState = ProcessGameState(PlayerDied, PlayerFinishedLevel);
                GameStateManager.AddObservation(gameState);

                GameStateManager.AddReward(reward);
                if (PlayerDied || PlayerFinishedLevel)
                {
                    EpisodeEndedNumber += 1;
                    CelesteBotMain.Log("Episode " + EpisodeEndedNumber + " ended.");
                    // if player is dead or level finished, first need to reset the episode
                    Episode.ResetEpisode();
                    PlayerDied = false;
                    PlayerFinishedLevel = false;
                }
                else
                {
                    RunActionInNFrames = GetActionFrameDelay(true);
                }
                NeedGameStateUpdate = false;
                EpisodeEnded = false;

            }
            if (!actionRunThisFrame)
            {
                InputAction NOOP = new();
                nextInput.UpdateData(NOOP);

                Input.UpdateData(nextInput, true);
            }
        }
        public GameState ProcessGameState(bool PlayerDied, bool PlayerFinishedLevel)
        {
            cameraManager.UpdateScreenVision(Episode.Target);
            GameState gameState = new(cameraManager, CelesteGamePlayer, Episode, PlayerDied, PlayerFinishedLevel);
            return gameState;
        }
        public void SetupVision()
        {
            try
            {
                //TileFinder.TilesOffset = Celeste.Celeste.Scene.Entities.FindFirst<SolidTiles>().Center; // Thanks KDT#7539!
                TileFinder.SetupOffset();
            }
            catch (NullReferenceException)
            {
                // The Scene hasn't been created yet.
            }
            VisionSetup = true;
        }
        public bool HandleEdgeCases(InputData nextInput)
        {
            if (!GameInitiated && IsWorker)
            {
                CurrentActionSequence = ActionSequence.GenerateActionSequence("Wait,Wait,Wait,Wait,Wait,Wait,MenuConfirm,Wait,Wait,MenuConfirm,Wait,MenuConfirm,Wait,Wait,MenuConfirm");

                // CurrentActionSequence = ActionSequence.GenerateActionSequence("Wait,Wait,Wait,Wait,Wait,Wait,MenuConfirm,Wait,Wait,MenuDown,Wait,Wait,MenuConfirm,Wait,MenuConfirm,Wait,Wait,MenuConfirm");
                GameInitiated = true;
            }
            Level level = TileFinder.GetCelesteLevel();
            if (CurrentActionSequence.HasNextAction())
            {
                nextInput.UpdateData(CurrentActionSequence.GetNextAction());
                Input.UpdateData(nextInput);
                return true;
            }
            else if (level == null && CelesteGamePlayer == null && !WaitingForRespawn && GameInitiated)
            {
                CurrentActionSequence = ActionSequence.GenerateActionSequence("Wait,MenuConfirm");
            }
            if (level == null || level.Transitioning || CelesteGamePlayer == null || WaitingForRespawn)
            {
                if (CelesteGamePlayer != null)
                {
                    int framesAlive = (int)typeof(Player).GetField("framesAlive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(CelesteGamePlayer);
                    if (WaitingForRespawn && framesAlive > 10)
                    {
                        WaitingForRespawn = false;
                    }
                }
                BotState = State.Disabled;
                return true;
            }
 

            BotState = State.Running;
            if (CelesteBotMain.Settings.TrainingEnabled)
            {
                if (CheckDashTutorial(level) || CutsceneManager.CompleteRestart(Input) || CutsceneManager.CheckForCutsceneSkip(Input) || CutsceneManager.CompleteCutsceneSkip(Input))
                {
                    
                    return true;
                }

                _ = CheckTalk();
            }
            return false;
        }
        internal void KillPlayer()
        {

            CelesteGamePlayer.Die(Vector2.Zero, true);
        }
        private static int GetActionFrameDelay(bool observationSent)
        {
            return (observationSent && CelesteBotMain.Settings.TrainingEnabled) ? CelesteBotMain.Settings.ActionCalculationFrames : -1;
        }
        public void RestartGame()
        {
            string sequence = "Wait,Wait,Pause,MenuDown,MenuDown,MenuDown,MenuDown,MenuDown,MenuConfirm,Wait,MenuConfirm,Wait,Wait,MenuConfirm,Wait,MenuConfirm";
            CurrentActionSequence = ActionSequence.GenerateActionSequence(sequence);
            TargetFinder.ResetCache();
            Episode.FinishedLevelCount = 0;
            WaitingForRespawn = false;
        }

        private bool CheckDashTutorial(Level level)
        {
            if (CelesteGamePlayer.StateMachine.State == 11 && !CelesteGamePlayer.OnGround() && Input.LastData.Dash != true && (level.Session.MapData.Filename + "_" + level.Session.Level == "0-Intro_3"))// this makes sure we retry
            {
                // This means we are in the bird tutorial.
                // Make us finish it right away.
                InputData data = new()
                {
                    MoveX = 1,
                    MoveY = -1,
                    Dash = true
                };
                Input.UpdateData(data);
                Log("The player is in the dash cutscene, so we tried to get them out of it by dashing.");

                return true;
            }
            return false;
        }

        private bool CheckTalk()
        {
            if (TalkComponent.PlayerOver != null && TalkCount < TalkMaxAttempts && TimeSinceLastTalk >= MaxTimeSinceLastTalk)
            {
                if (Input.LastData.Talk)
                    return false;
                // Hey we can talk!
                InputData data = new()
                {
                    Talk = true
                };
                Input.UpdateData(data);
                Log("We tried to talk!");
                TalkCount++;
                TimeSinceLastTalk = 0;
                return true;
            }
            if (TalkComponent.PlayerOver == null)
            {
                TimeSinceLastTalk = MaxTimeSinceLastTalk;
                TalkCount = 0;
            }
            TimeSinceLastTalk++;
            return true;
        }
    }


}
