﻿using Celeste;
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

        public double LastEpisodeReward { get { return TrainingEpisodeState.LastEpisodeReward; } }
        public int DeathCounter { get; private set; }
        public bool PlayerFinishedLevel { get; private set; }
        public int RunActionInNFrames { get; private set; } = -1;
        public ActionSequence CurrentActionSequence { get; private set; }
        public bool NeedToRestartGame { get; set; }
        public bool NeedGameStateUpdate { get; private set; }
        public Action LatestAction { get; private set; }
        public InputPlayer inputPlayer;

        public ExternalActionManager ActionManager = new();
        public ExternalGameStateManager GameStateManager = new();
        public bool WaitingForRespawn = false;
        public bool Replay = false;

        public bool VisionSetup = false;

        public CameraManager cameraManager;
        public TrainingEpisodeState Episode;

        public bool NeedImmediateGameStateUpdate;
        public bool PlayerDied;

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
        }

        public void TryUpdateGameState(InputData nextInput)
        {
            TargetFinder.UpdateCurrentTarget();
            bool actionRunThisFrame = false;
            if ((CelesteGamePlayer.Dead && !WaitingForRespawn))
            {
                NeedImmediateGameStateUpdate = true;
                WaitingForRespawn = true;
                PlayerDied = CelesteGamePlayer.Dead;
                DeathCounter += 1;
                PlayerFinishedLevel = Episode.ReachedTarget;
                if (DeathCounter >= 1)
                {
                    RestartGame();
                    DeathCounter = 0;
                }
            }
            else if (WaitingForRespawn && CelesteGamePlayer.Dead)
            {
                // clear movement buffer
                nextInput = new InputData();
                inputPlayer.UpdateData(nextInput);
                actionRunThisFrame = true;
            }
            else if (WaitingForRespawn && !CelesteGamePlayer.Dead)
            {
                WaitingForRespawn = false;
            }
            // Handle RL state machine below
            Episode.IncrementFrames();
            if (!Episode.FirstObservationSent)
            {
                Episode.NewEpisodeSetOriginalDistance();
                ProcessGameState(false, false);
                bool observationSent = true;
                Episode.FirstObservationSent = observationSent;

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
                    return;
                }
                else
                {
                    RunActionInNFrames -= 1;
                }
            }

            else if (NeedImmediateGameStateUpdate || (NeedGameStateUpdate && Episode.IsCalculateFrame()) || !CelesteBotMain.Settings.TrainingEnabled)
            {
                // get reward and observation
                ProcessGameState(PlayerDied, PlayerFinishedLevel);

                double reward = Episode.GetReward();

                GameStateManager.AddReward(reward);
                if (!NeedImmediateGameStateUpdate)
                {
                    RunActionInNFrames = GetActionFrameDelay(true);
                }
                else
                {
                    // if player is dead or level finished, first need to reset the episode
                    Episode.ResetEpisode();
                    PlayerDied = false;
                    PlayerFinishedLevel = false;
                }
                NeedGameStateUpdate = false;
                NeedImmediateGameStateUpdate = false;

            }
            if (!actionRunThisFrame)
            {
                Action NOOP = new();
                nextInput.UpdateData(NOOP);

                inputPlayer.UpdateData(nextInput, true);
            }
        }
        public bool ProcessGameState(bool PlayerDied, bool PlayerFinishedLevel)
        {
            if (WaitingForRespawn)
            {

                if (!CelesteGamePlayer.Dead)
                {
                    // waiting for respawn
                    WaitingForRespawn = false;
                }
                else
                {
                }
            }
            // player just died, so set it to dead and record observation
            else if (CelesteGamePlayer.Dead)
            {
                WaitingForRespawn = true;
            }
            cameraManager.UpdateScreenVision(Episode.Target);
            GameState gameState = new(cameraManager.CameraVision, CelesteGamePlayer, Episode, PlayerDied, PlayerFinishedLevel);
            GameStateManager.AddObservation(gameState);
            return true;
            
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
            Level level = TileFinder.GetCelesteLevel();
            if (level == null || level.Transitioning || AIPlayer.CelesteGamePlayer == null || AIPlayer.WaitingForRespawn)
            {
                if (AIPlayer.CelesteGamePlayer != null)
                {
                    int framesAlive = (int)typeof(Player).GetField("framesAlive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(AIPlayer.CelesteGamePlayer);
                    if (AIPlayer.WaitingForRespawn && framesAlive > 10)
                    {
                        AIPlayer.WaitingForRespawn = false;
                    }
                }
                if (AIPlayer.NeedToRestartGame)
                {
                    // spam "A" if not in a level
                    //StartRandomizerGame();d
                }
                BotState = State.Disabled;
                return true;
            }
            if (AIPlayer.CurrentActionSequence.HasNextAction())
            {
                nextInput.UpdateData(AIPlayer.CurrentActionSequence.GetNextAction());
                inputPlayer.UpdateData(nextInput);
                return true;
            }

            AIPlayer.NeedToRestartGame = false;
            BotState = State.Running;
            if (CelesteBotMain.Settings.TrainingEnabled)
            {
                if (CheckDashTutorial(level) || CutsceneManager.CompleteRestart(inputPlayer) || CutsceneManager.CheckForCutsceneSkip(inputPlayer) || CutsceneManager.CompleteCutsceneSkip(inputPlayer))
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
            string sequence = "Pause,MenuDown,MenuDown,MenuDown,MenuDown,MenuDown,MenuConfirm,Wait,MenuConfirm,Wait,Wait,Wait,Wait,Wait,Wait,Wait,Wait,Wait,Wait,Wait,Wait,Wait,Wait,Wait,Wait,Wait,MenuConfirm";
            CurrentActionSequence = ActionSequence.GenerateActionSequence(sequence);
            NeedToRestartGame = true;
        }

        private bool CheckDashTutorial(Level level)
        {
            if (AIPlayer.CelesteGamePlayer.StateMachine.State == 11 && !AIPlayer.CelesteGamePlayer.OnGround() && inputPlayer.LastData.Dash != true && (level.Session.MapData.Filename + "_" + level.Session.Level == "0-Intro_3"))// this makes sure we retry
            {
                // This means we are in the bird tutorial.
                // Make us finish it right away.
                InputData data = new()
                {
                    MoveX = 1,
                    MoveY = -1,
                    Dash = true
                };
                inputPlayer.UpdateData(data);
                Log("The player is in the dash cutscene, so we tried to get them out of it by dashing.");

                return true;
            }
            return false;
        }

        private bool CheckTalk()
        {
            if (TalkComponent.PlayerOver != null && TalkCount < TalkMaxAttempts && TimeSinceLastTalk >= MaxTimeSinceLastTalk)
            {
                if (inputPlayer.LastData.Talk)
                    return false;
                // Hey we can talk!
                InputData data = new()
                {
                    Talk = true
                };
                inputPlayer.UpdateData(data);
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
