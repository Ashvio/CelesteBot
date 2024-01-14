using Celeste.Mod;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Timers;
namespace CelesteBot_2023
{
    enum UpDownActionType
    {
        NOOP = 0,
        Up = 1,
        Down = 2,
    }
    enum LeftRightActionType
    {
        NOOP = 0,
        Left = 1,
        Right = 2,
    }
    enum SpecialMoveActionType
    {
        NOOP = 0,
        Jump = 1,
        LongJump = 2,
        Dash = 3,
    }
    enum GrabActionType
    {
        NOOP = 0,
        Grab = 1,
    }

    public enum MenuActionType
    {
        NOOP = 0,
        MenuConfirm = 1,
        MenuDown = 2,
        Pause = 3,
    }
    public class InputAction
    {
        /*
         * RL Client returns actions as a list of ints corresponding to the enum values above.
         * NOOP = 0, other actions correspond to the integer.
         */
        readonly UpDownActionType UpDownAction;
        readonly LeftRightActionType LeftRightAction;
        readonly SpecialMoveActionType SpecialMoveAction;
        readonly GrabActionType grabAction;
        readonly MenuActionType MenuAction;
        public bool IsWaitingAction { get; private set; }


        public InputAction(int[] action)
        {
            UpDownAction = (UpDownActionType)action[0];
            LeftRightAction = (LeftRightActionType)action[1];

            SpecialMoveAction = (SpecialMoveActionType)action[2];
            grabAction = (GrabActionType)action[3];
        }
        public InputAction(MenuActionType menuAction)
        {
            MenuAction = menuAction;
        }
        public InputAction(bool isWaitingAction = false)
        {
            IsWaitingAction = isWaitingAction;
        }
        public override string ToString()
        {
            return "Move:" + Enum.GetName(typeof(UpDownActionType), UpDownAction) + " " + Enum.GetName(typeof(LeftRightActionType), LeftRightAction) + "\nSpecial:" + Enum.GetName(typeof(SpecialMoveActionType), SpecialMoveAction) + " Grab:" + Enum.GetName(typeof(GrabActionType), grabAction);
        }

        public float GetMoveX()
        {
            return LeftRightAction switch
            {
                LeftRightActionType.NOOP => 0,
                LeftRightActionType.Left => -1,
                LeftRightActionType.Right => 1,
                _ => (float)0,
            };
        }
        public float GetMoveY()
        {
            //when using a keyboard, Input.MoveX.Value is -1 when pressing left, 1 when pressing right, 0 otherwise. (same applies for Input.MoveY.Value
            return UpDownAction switch
            {
                UpDownActionType.NOOP => 0,
                UpDownActionType.Up => -1,
                UpDownActionType.Down => 1,
                _ => (float)0,
            };
        }
        public bool GetJump()
        {
            return SpecialMoveAction switch
            {
                SpecialMoveActionType.Jump => true,
                _ => false,
            };
        }
        public bool GetLongJump()
        {
            return SpecialMoveAction switch
            {
                SpecialMoveActionType.LongJump => true,
                _ => false,
            };
        }
        public bool GetDash()
        {
            return SpecialMoveAction switch
            {
                SpecialMoveActionType.NOOP => false,
                SpecialMoveActionType.Jump => false,
                SpecialMoveActionType.Dash => true,
                _ => false,
            };
        }
        public bool GetGrab()
        {
            return grabAction switch
            {
                GrabActionType.NOOP => false,
                GrabActionType.Grab => true,
                _ => false,
            };
        }

        public bool GetMenuConfirm()
        {
            return MenuAction switch
            {
                MenuActionType.MenuConfirm => true,
                _ => false,
            };
        }

        public bool GetMenuDown()
        {
            return MenuAction switch
            {
                MenuActionType.MenuDown => true,
                _ => false,
            };
        }

        public bool GetPause()
        {
            return MenuAction switch
            {
                MenuActionType.Pause => true,
                _ => false,
            };
        }
    }

    public class ActionSequence
    {
        readonly Queue<InputAction> sequence;
        public ActionSequence() { }
        public ActionSequence(Queue<InputAction> sequence)
        {
            this.sequence = sequence;
        }
        private static Timer WaitTimer;
        private static bool Waiting = false;
        public static ActionSequence GenerateActionSequence(string sequenceString)
        {
            Queue<InputAction> sequence = new();
            string[] actions = sequenceString.Split(',');
            foreach (string action in actions)
            {
                InputAction next = GenerateAction(action);
                sequence.Enqueue(next);
                sequence.Enqueue(new InputAction());
                sequence.Enqueue(new InputAction());
                sequence.Enqueue(new InputAction());
                sequence.Enqueue(new InputAction());
                sequence.Enqueue(new InputAction());
                sequence.Enqueue(new InputAction());
                sequence.Enqueue(new InputAction());
                sequence.Enqueue(new InputAction());

            }
            return new ActionSequence(sequence);
        }
        public bool HasNextAction()
        {
            return sequence != null && sequence.Count > 0;
        }
        public InputAction GetNextAction()
        {
            if (Waiting)
            {
                return new InputAction();
            }
            InputAction nextAction = sequence.Dequeue();

            if (nextAction.IsWaitingAction)
            {
                Waiting = true;
                WaitTimer = new Timer(1000 / CelesteBotMain.FrameLoops)
                {
                    AutoReset = false,
                    Enabled = true
                };
                WaitTimer.Elapsed += WaitTimer_Elapsed;
            }

            return nextAction;
        }
        private static void WaitTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            CelesteBotMain.Log("Done Waiting");

            Waiting = false;
        }
        private static InputAction GenerateAction(string action)
        {
            
            return action switch
            {
                "Up" => new InputAction(new int[] { (int)UpDownActionType.Up, (int)LeftRightActionType.NOOP, (int)SpecialMoveActionType.NOOP, (int)GrabActionType.NOOP }),
                "Down" => new InputAction(new int[] { (int)UpDownActionType.Down, (int)LeftRightActionType.NOOP, (int)SpecialMoveActionType.NOOP, (int)GrabActionType.NOOP }),
                "Left" => new InputAction(new int[] { (int)UpDownActionType.NOOP, (int)LeftRightActionType.Left, (int)SpecialMoveActionType.NOOP, (int)GrabActionType.NOOP }),
                "Right" => new InputAction(new int[] { (int)UpDownActionType.NOOP, (int)LeftRightActionType.Right, (int)SpecialMoveActionType.NOOP, (int)GrabActionType.NOOP }),
                "Jump" => new InputAction(new int[] { (int)UpDownActionType.NOOP, (int)LeftRightActionType.NOOP, (int)SpecialMoveActionType.Jump, (int)GrabActionType.NOOP }),
                "LongJump" => new InputAction(new int[] { (int)UpDownActionType.NOOP, (int)LeftRightActionType.NOOP, (int)SpecialMoveActionType.LongJump, (int)GrabActionType.NOOP }),
                "Dash" => new InputAction(new int[] { (int)UpDownActionType.NOOP, (int)LeftRightActionType.NOOP, (int)SpecialMoveActionType.Dash, (int)GrabActionType.NOOP }),
                "Grab" => new InputAction(new int[] { (int)UpDownActionType.NOOP, (int)LeftRightActionType.NOOP, (int)SpecialMoveActionType.NOOP, (int)GrabActionType.Grab }),
                "MenuConfirm" or "MenuDown" or "Pause" => new InputAction((MenuActionType)Enum.Parse(typeof(MenuActionType), action)),
                "Wait" => new InputAction(isWaitingAction: true),
                _ => throw new InvalidEnumArgumentException("Invalid action: " + action),
            };
        }
    }
    public class ExternalActionManager
    {
        readonly BlockingCollection<InputAction> ActionQueue;
        public static int NumRequestedActions { get; private set; }
        public static int NumAvailableActions { get; private set; }

        public ExternalActionManager()
        {
            ActionQueue = new BlockingCollection<InputAction>();
        }

        public void PythonAddAction(int[] action)
        {
            ActionQueue.Add(new InputAction(action));
            NumAvailableActions++;
        }

        public void Flush()
        {
            if (ActionQueue.Count > 0 )
            {
                CelesteBotMain.Log("Flushing action queue!" + ActionQueue.Count , LogLevel.Warn);
            }
            while (ActionQueue.TryTake(out InputAction item)) { CelesteBotMain.Log(item.ToString()); }
        }
        public InputAction GetNextAction()
        {
            if (NumRequestedActions >= ExternalGameStateManager.NumSentObservations)
            {
                CelesteBotMain.Log("Too many actions requested!", LogLevel.Error);   
            }
            double start = DateTime.Now.TimeOfDay.TotalMilliseconds;
            NumRequestedActions++;
            int timeout = CelesteBotMain.IsWorker ? 1000 * 30 : 1000 * 5;
            bool success = ActionQueue.TryTake(out InputAction output, timeout);
            if (!success)
            {
                CelesteBotMain.Log("Action retrieval timed out!", LogLevel.Error);
                CelesteBotMain.BotState = CelesteBotMain.State.None;
                CelesteBotMain.Instance.Unload();
                throw new TimeoutException("Action retrieval timed out!");
            }
            double end = DateTime.Now.TimeOfDay.TotalMilliseconds;
            double totalTime = end - start;
            double frameCalculationTime = 1000 / (CelesteBotMain.Settings.CalculationsPerSecond * CelesteBotMain.FrameLoops);
            if (totalTime > frameCalculationTime) {
                CelesteBotMain.Log("Action retrieval time too slow! " + totalTime.ToString() + "Frame calculation time: " + frameCalculationTime);
                CelesteBotMain.ActionRetrievalStatus = "DELAYED: " + (totalTime - frameCalculationTime).ToString("F2") +"ms too slow";
            }
            else
            {
                CelesteBotMain.ActionRetrievalStatus = "Normal: " + totalTime.ToString("F2") + " < " + frameCalculationTime.ToString("F2") + "ms";
            }
            return output;
        }
    }
}