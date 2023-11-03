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
                case SpecialMoveActionType.NOOP:
                    return false;
                case SpecialMoveActionType.Jump:
                    return true;
                case SpecialMoveActionType.Dash:
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
        public int numRequestedActions { get; private set; }
        public int numAvailableActions { get; private set; }

        public ExternalActionManager()
        {
            ActionQueue = new BlockingCollection<Action>();
        }

        public void PythonAddAction(int[] action)
        {
            ActionQueue.Add(new Action(action));
            numAvailableActions++;
        }

        public Action GetNextAction()
        {
            double start = DateTime.Now.TimeOfDay.TotalMilliseconds;
            Action output;
            numRequestedActions++;
            bool success = ActionQueue.TryTake(out output, 5000);
            if (!success)
            {
                CelesteBotManager.Log("Action retrieval timed out!", LogLevel.Error);
                throw new TimeoutException("Action retrieval timed out!");
            }
            double end = DateTime.Now.TimeOfDay.TotalMilliseconds;
            double total_time = end - start;
            double frameCalculationTime = 1000 / (CelesteBotInteropModule.Settings.CalculationsPerSecond * CelesteBotInteropModule.FrameLoops);
            if (total_time > frameCalculationTime) {
                CelesteBotManager.Log("Action retrieval time too slow! " + total_time.ToString() + "Frame calculation time: " + frameCalculationTime , LogLevel.Info);
            }
            return output;
        }
    }
}