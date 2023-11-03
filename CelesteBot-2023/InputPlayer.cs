using Celeste;
using Celeste.Mod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using static Celeste.Player.ChaserStateSound;
using System.Collections.Generic;
using Monocle;

namespace CelesteBot_2023
{
    public class InputPlayer : GameComponent
    {
        // Each Player class also contains an InputPlayer, which plays their input
        // Might change this to have only one...

        public InputData Data;
        public InputData LastData = new InputData();

        public InputPlayer(Game game, InputData Data) : base(game)
        {
            this.Data = Data;
            Everest.Events.Input.OnInitialize += HookInput;
            HookInput();
        }
        public void UpdateData(InputData newData)
        {
            LastData = Data;
            // Handle + to - swaps
            // These swaps should actually release the button for one frame
            if ((LastData.JumpValue < 0 && newData.JumpValue > 0) || (newData.JumpValue < 0 && LastData.JumpValue > 0))
            {
                // Release for one frame
                newData.Jump = false;
                newData.JumpValue = 0;
            }
            if ((LastData.DashValue < 0 && newData.DashValue > 0) || (newData.DashValue < 0 && LastData.DashValue > 0))
            {
                // Release for one frame
                newData.Dash = false;
                newData.DashValue = 0;
            }
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

            Data = newData;
        }
        public void HookInput()
        {
            Input.MoveX.Nodes.Add(new InputNodes.MoveX(this));
            Input.MoveY.Nodes.Add(new InputNodes.MoveY(this));
            Input.Aim.Nodes.Add(new InputNodes.Aim(this));
            Input.MountainAim.Nodes.Add(new InputNodes.MountainAim(this));

            Input.ESC.Nodes.Add(new InputNodes.Button(this, InputData.ButtonMask.ESC));
            Input.QuickRestart.Nodes.Add(new InputNodes.Button(this, InputData.ButtonMask.QuickRestart));
            Input.MenuConfirm.Nodes.Add(new InputNodes.Button(this, InputData.ButtonMask.MenuConfirm));
            Input.MenuCancel.Nodes.Add(new InputNodes.Button(this, InputData.ButtonMask.MenuCancel));
            Input.MenuDown.Nodes.Add(new InputNodes.Button(this, InputData.ButtonMask.MenuDown));
            Input.Jump.Nodes.Add(new InputNodes.Button(this, InputData.ButtonMask.Jump));
            Input.Dash.Nodes.Add(new InputNodes.Button(this, InputData.ButtonMask.Dash));
            Input.Grab.Nodes.Add(new InputNodes.Button(this, InputData.ButtonMask.Grab));
            Input.Talk.Nodes.Add(new InputNodes.Button(this, InputData.ButtonMask.Talk));

            Logger.Log(CelesteBotInteropModule.ModLogKey, "Hooked Input with Nodes!");
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
            Logger.Log(CelesteBotInteropModule.ModLogKey, "Unloading Input, (hopefully) returning input to player...");
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
