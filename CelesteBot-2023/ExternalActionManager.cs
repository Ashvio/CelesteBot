using Celeste.Mod;
using Celeste.Mod.Helpers;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;

namespace CelesteBot_2023
{
    enum UpDownActionType
    {
        NOOP,
        Up,
        Down,
    }
    enum LeftRightActionType
    {
        NOOP,
        Left,
        Right,
    }
    enum SpecialMoveActionType
    {
        NOOP,
        Jump,
        LongJump,
        Dash,
    }
    enum GrabActionType
    {
        NOOP,
        Grab,
    }
    public class Action
    {
        /*
         * RL Client returns actions as a list of ints corresponding to the enum values above.
         * NOOP = 0, other actions correspond to the integer.
         */
        UpDownActionType UpDownAction;
        LeftRightActionType LeftRightAction;
        SpecialMoveActionType SpecialMoveAction;
        GrabActionType grabAction;

        public Action(int[] action)
        {
            UpDownAction = (UpDownActionType)action[0];
            LeftRightAction = (LeftRightActionType)action[1];

            SpecialMoveAction = (SpecialMoveActionType)action[2];
            grabAction = (GrabActionType)action[3];
        }

        public override string ToString()
        {
            return "Move:" + Enum.GetName(typeof(UpDownActionType), UpDownAction) + " " + Enum.GetName(typeof(LeftRightActionType), LeftRightAction) + "\nSpecial:" + Enum.GetName(typeof(SpecialMoveActionType), SpecialMoveAction) + " Grab:" + Enum.GetName(typeof(GrabActionType), grabAction);
        }

        public float GetMoveX()
        {
            switch (LeftRightAction)
            {
                case LeftRightActionType.NOOP:
                    return 0;
                case LeftRightActionType.Left:
                    return -1;
                case LeftRightActionType.Right:
                    return 1;
                default:
                    return 0;
            }
        }
        public float GetMoveY()
        {
            //when using a keyboard, Input.MoveX.Value is -1 when pressing left, 1 when pressing right, 0 otherwise. (same applies for Input.MoveY.Value
            switch (UpDownAction)
            {
                case UpDownActionType.NOOP:
                    return 0;
                case UpDownActionType.Up:
                    return -1;
                case UpDownActionType.Down:
                    return 1;
                default:
                    return 0;
            }
        }
        public bool GetJump()
        {
            switch (SpecialMoveAction)
            {
                case SpecialMoveActionType.Jump:
                    return true;
                case SpecialMoveActionType.NOOP:
                case SpecialMoveActionType.Dash:
                case SpecialMoveActionType.LongJump:
                    return false;
                default:
                    return false;
            }
        }
        public bool GetLongJump()
        {
            switch (SpecialMoveAction)
            {
                case SpecialMoveActionType.LongJump:
                    return true;
                case SpecialMoveActionType.NOOP:
                case SpecialMoveActionType.Dash:
                case SpecialMoveActionType.Jump:
                    return false;
                default:
                    return false;
            }
        }
        public bool GetDash()
        {
            switch (SpecialMoveAction)
            {
                case SpecialMoveActionType.NOOP:
                    return false;
                case SpecialMoveActionType.Jump:
                    return false;
                case SpecialMoveActionType.Dash:
                    return true;
                default:
                    return false;
            }
        }
        public bool GetGrab()
        {
            switch (grabAction)
            {
                case GrabActionType.NOOP:
                    return false;
                case GrabActionType.Grab:
                    return true;
                default:
                    return false;
            }
        }
    }
    public class ExternalActionManager
    {
        BlockingCollection<Action> ActionQueue;
        public static int numRequestedActions { get; private set; }
        public static int numAvailableActions { get; private set; }

        public ExternalActionManager()
        {
            ActionQueue = new BlockingCollection<Action>();
        }

        public void PythonAddAction(int[] action)
        {
            ActionQueue.Add(new Action(action));
            numAvailableActions++;
        }

        public void Flush()
        {
            if (ActionQueue.Count > 0 )
            {
                CelesteBotManager.Log("Flushing action queue!" + ActionQueue.Count , LogLevel.Warn);
            }
            Action item;
            while (ActionQueue.TryTake(out item)) { CelesteBotManager.Log(item.ToString()); } 
        }
        public Action GetNextAction()
        {
            if (numRequestedActions >= ExternalGameStateManager.NumSentObservations)
            {
                CelesteBotManager.Log("Too many actions requested!", LogLevel.Error);   
            }
            double start = DateTime.Now.TimeOfDay.TotalMilliseconds;
            Action output;
            numRequestedActions++;
            bool success = ActionQueue.TryTake(out output, 5000);
            if (!success)
            {
                CelesteBotManager.Log("Action retrieval timed out!", LogLevel.Error);
                CelesteBotInteropModule.BotState = CelesteBotInteropModule.State.None;
                CelesteBotInteropModule.Instance.Unload();
                throw new TimeoutException("Action retrieval timed out!");
            }
            double end = DateTime.Now.TimeOfDay.TotalMilliseconds;
            double totalTime = end - start;
            double frameCalculationTime = 1000 / (CelesteBotInteropModule.Settings.CalculationsPerSecond * CelesteBotInteropModule.FrameLoops);
            if (totalTime > frameCalculationTime) {
                CelesteBotManager.Log("Action retrieval time too slow! " + totalTime.ToString() + "Frame calculation time: " + frameCalculationTime , LogLevel.Info);
                CelesteBotInteropModule.ActionRetrievalStatus = "DELAYED: " + (totalTime - frameCalculationTime).ToString("F2") +"ms too slow";
            }
            else
            {
                CelesteBotInteropModule.ActionRetrievalStatus = "Normal: " + totalTime.ToString("F2") + " < " + frameCalculationTime.ToString("F2") + "ms";
            }
            return output;
        }
    }
}