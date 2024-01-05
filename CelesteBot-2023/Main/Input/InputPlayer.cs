using Celeste;
using Celeste.Mod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace CelesteBot_2023
{
    public class InputPlayer : GameComponent
    {
        // Each Player class also contains an InputPlayer, which plays their input
        // Might change this to have only one...

        public InputData Data;
        public InputData LastData = new();

        public InputPlayer(Game game, InputData Data) : base(game)
        {
            this.Data = Data;
            Everest.Events.Input.OnInitialize += HookInput;
            HookInput();
        }
        public void UpdateData(InputData newData, bool waitingForNextAction = false)
        {

            if (waitingForNextAction)
            {
                // only update jump value
                Data.Jump = newData.Jump;
            }
            else
            {
                LastData = Data;
                Data = newData;
            }
            // Handle + to - swaps
            // These swaps should actually release the button for one frame
            //if ((LastData.JumpValue < 0 && newData.JumpValue > 0) || (newData.JumpValue < 0 && LastData.JumpValue > 0))
            //{
            //    // Release for one frame
            //    newData.Jump = false;
            //    newData.JumpValue = 0;
            //}
            //if ((LastData.DashValue < 0 && newData.DashValue > 0) || (newData.DashValue < 0 && LastData.DashValue > 0))
            //{
            //    // Release for one frame
            //    newData.Dash = false;
            //    newData.DashValue = 0;
            //}


            //if ((LastData.GrabValue < 0 && newData.GrabValue > 0) || (newData.GrabValue < 0 && LastData.GrabValue > 0))
            //{
            //    // Release for one frame
            //    newData.Grab = false;
            //    newData.GrabValue = 0;
            //}
            //if ((LastData.LongJumpValue < 0 && newData.LongJumpValue > 0) || (newData.LongJumpValue < 0 && LastData.LongJumpValue > 0))
            //{
            //    // Release for one frame
            //    newData.Jump = false;
            //    newData.JumpValue = 0;
            //}

        }
        public void HookInput()
        {
            Input.MoveX.Nodes.Add(new VirtualController.MoveX(this));
            Input.MoveY.Nodes.Add(new VirtualController.MoveY(this));
            Input.Aim.Nodes.Add(new VirtualController.Aim(this));
            Input.MountainAim.Nodes.Add(new VirtualController.MountainAim(this));
            Input.Pause.Nodes.Add(new VirtualController.Button(this, InputData.ButtonMask.Pause));

            Input.ESC.Nodes.Add(new VirtualController.Button(this, InputData.ButtonMask.ESC));
            Input.QuickRestart.Nodes.Add(new VirtualController.Button(this, InputData.ButtonMask.QuickRestart));
            Input.MenuConfirm.Nodes.Add(new VirtualController.Button(this, InputData.ButtonMask.MenuConfirm));
            Input.MenuCancel.Nodes.Add(new VirtualController.Button(this, InputData.ButtonMask.MenuCancel));
            Input.MenuDown.Nodes.Add(new VirtualController.Button(this, InputData.ButtonMask.MenuDown));
            Input.Jump.Nodes.Add(new VirtualController.Button(this, InputData.ButtonMask.Jump));
            Input.Dash.Nodes.Add(new VirtualController.Button(this, InputData.ButtonMask.Dash));
            Input.Grab.Nodes.Add(new VirtualController.Button(this, InputData.ButtonMask.Grab));
            Input.Talk.Nodes.Add(new VirtualController.Button(this, InputData.ButtonMask.Talk));

            Logger.Log(CelesteBotMain.ModLogKey, "Hooked Input with Nodes!");
        }
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
  
                //Player p = Celeste.Celeste.Scene.Tracker.GetEntity<Player>();
                //if (p == null || p.Dead ) // Not sure if this works if quickRestarting
                //{
                //    Logger.Log(CelesteBotInteropModule.ModLogKey, "Player is either null or dead, but NOT removing!");
                //}
            
                // Game has yet to load, wait a bit. The Celeste.Celeste.Scene does not exist.
            
        }
        public void Remove()
        {
            Everest.Events.Input.OnInitialize -= HookInput;
            Input.Initialize();
            Logger.Log(CelesteBotMain.ModLogKey, "Unloading Input, (hopefully) returning input to player...");
            Game.Components.Remove(this);
        }
    }

    public class KeyboardManager
    {

        public static void SetConfirm()
        {

            MInput.Keyboard.PreviousState = MInput.Keyboard.CurrentState;
            Keys[] keys = new Keys[1]; 

            keys[0] = Keys.NumPad0;
            

            MInput.Keyboard.CurrentState = new KeyboardState(keys);
        }
    }
}
