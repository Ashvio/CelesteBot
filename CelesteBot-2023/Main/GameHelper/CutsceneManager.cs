using Celeste;
using Celeste.Mod;
using Monocle;
using System;
/*
* The selected code block belongs to the CelesteBotManager class and is found in the CelesteBotManager.cs file.
This class contains various member variables that hold the configurations for the program as well as some utility 
methods namely Initialize, Draw, UpdateQTable.
The CelesteBotManager class contains parameters that configure the way the NEAT algorithm functions when training,
such as how weights are mutated (with the parameter WEIGHT_MUTATION_CHANCE), the chance for a new connection to be added 
(ADD_CONNECTION_CHANCE), and the chance for a new node to be added (ADD_NODE_CHANCE). Other parameters include learning 
rate, gamma, and epsilon for the Q-learning algorithm, and some fitness parameters for the training process.
The Initialize method loads the configuration settings that can be modified by the user, and sets them in their respective 
variables. The Draw method is responsible for rendering visual elements like graphs and the player's neural network. 
Finally, the UpdateQTable method performs the necessary calculations on the current state and action, thus updating the 
Q Table for the player.
* 
*/
namespace CelesteBot_2023
{
    public class CutsceneManager
    { 
        public static bool Cutscene = false;
       

        public static void Log(string message, LogLevel level = LogLevel.Verbose)
        {
            Log(message);
        }

        public static bool CompleteCutsceneSkip(InputPlayer inputPlayer)
        {
            InputData thisFrame = new();
            // If the last frame contains an escape, make this frame contain a menu down
            if (inputPlayer.Data.ESC)
            {
                thisFrame.MenuDown = true;
            }
            // If the last frame contains a menu down, make this frame contain a menu confirm
            else if (inputPlayer.Data.MenuDown)
            {
                thisFrame.MenuConfirm = true;
            }
            else
            {
                // This means we are done with handling a cutscene.
                // Just make sure we are playing again!
                return false;
            }
            Log("Completing Cutscene Skip with inputs: " + thisFrame + " and Cutscene: " + Cutscene);
            inputPlayer.UpdateData(thisFrame);
            return true;
        }
        public static bool CheckForCutsceneSkip(InputPlayer inputPlayer)
        {
            // three inputs need to be done to successfully skip a cutscene.
            // esc --> menu down --> menu confirm
            try
            {
                if (Cutscene)
                {
                    Cutscene = CompleteCutsceneSkip(inputPlayer);
                    Log("Confirmed a cutscene skip!");
                    Log("After Cutscene skip: " + Cutscene);
                    return true; // even if it returned false last time, still skip
                }
                try
                {
                    Level level = (Level)Engine.Scene;

                    if (level.InCutscene)
                    {
                        Log("Confirmed a cutscene skip!");

                        Log("Entered Cutscene! With Cutscene: " + Cutscene);
                        Cutscene = true;
                        InputData newFrame = new()
                        {
                            ESC = true
                        };
                        inputPlayer.UpdateData(newFrame);
                        return true;
                    }
                }
                catch (InvalidCastException)
                {
                    // Game still hasn't finished loading...
                }
            }
            catch (NullReferenceException)
            {
                // Level or Player hasn't been setup yet. Just continue on for now.
            }
            return false;
        }
        public static bool CompleteRestart(InputPlayer inputPlayer)
        {
            if (inputPlayer.LastData.QuickRestart)
            {
                InputData temp = new()
                {
                    MenuConfirm = true
                };
                inputPlayer.UpdateData(temp);
                Log("Restarting!");
                return true;
            }
            return false;
        }
    }
}