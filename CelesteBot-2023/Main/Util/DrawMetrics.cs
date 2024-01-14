using Celeste;
using CelesteBot_2023.SimplifiedGraphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using static CelesteBot_2023.CelesteBotMain;
using static Monocle.Draw;

namespace CelesteBot_2023
{
    public class DrawMetrics
	{
		public const int SCREEN_WIDTH = 960;
		public const int SCREEN_HEIGHT = 540;
		private static List<Color> Colors;

		[Initialize]
        public static void Initialize()
		{
            Colors = GetColorList();
			InitializeControlButtons();

            rect = new();
			parent = new MTexture(VirtualContent.CreateTexture("debug-pixel", 3, 3, Color.White));

			Pixel = new MTexture(parent, 1, 1, 1, 1);
    }

		
        public static void Draw(CelesteBotRunner AIPlayer)
		{

            DrawControls(AIPlayer.LatestAction);
            
            DrawMetricStrings(AIPlayer);
			try
			{
				if (BotState != State.None)
					DrawCameraVision(AIPlayer.cameraManager.CameraVision);
			}
            catch (NullReferenceException)
			{
                // Do nothing
            }
		}
        private static Rectangle rect;
        private static MTexture parent;

        private static MTexture Pixel;
        public static bool DrawThickHollowRect(float x, float y, float width, float height, int outlineWidth,  Color color)
		{

            rect.X = (int)x;
            rect.Y = (int)y;
            rect.Width = (int)width;
            rect.Height = outlineWidth;
            Monocle.Draw.SpriteBatch.Draw(Pixel.Texture.Texture_Safe, rect, Pixel.ClipRect, color);
            rect.Y += (int)height - outlineWidth;
            Monocle.Draw.SpriteBatch.Draw(Pixel.Texture.Texture_Safe, rect, Pixel.ClipRect, color);
            rect.Y -= (int)height - outlineWidth;
            rect.Width = outlineWidth;
            rect.Height = (int)height;
            Monocle.Draw.SpriteBatch.Draw(Pixel.Texture.Texture_Safe, rect, Pixel.ClipRect, color);
            rect.X += (int)width - outlineWidth;
            Monocle.Draw.SpriteBatch.Draw(Pixel.Texture.Texture_Safe, rect, Pixel.ClipRect, color);
			return true;
		}

		struct ControlButton
		{
			public string name;
			public float dx;
			public float dy;
			public Func<InputAction, bool> pressed;
		}
        public static void InitializeControlButtons()
        {
			ControlButtons.Add(new ControlButton() { name = "Up", dx = 2, dy = 0, pressed = (InputAction action) => action.GetMoveY() == -1 });
			ControlButtons.Add(new ControlButton() { name = "Left", dx = 1, dy = 1, pressed = (InputAction action) => action.GetMoveX() == -1 });
			ControlButtons.Add(new ControlButton() { name = "Down", dx = 2, dy = 1, pressed = (InputAction action) => action.GetMoveY() == 1 });
			ControlButtons.Add(new ControlButton() { name = "Right", dx = 3, dy = 1, pressed = (InputAction action) => action.GetMoveX() == 1 });
			ControlButtons.Add(new ControlButton() { name = "Jump", dx = 4.2f, dy = 1, pressed = (InputAction action) => action.GetJump() });
            ControlButtons.Add(new ControlButton() { name = "L.Jump", dx = 4.2f, dy = 0, pressed = (InputAction action) => action.GetLongJump() });

            ControlButtons.Add(new ControlButton() { name = "Dash", dx = 5.2f, dy = 1, pressed = (InputAction action) => action.GetDash() });
			ControlButtons.Add(new ControlButton() { name = "Grab", dx = 5.2f, dy = 0, pressed = (InputAction action) => action.GetGrab() });
        }
        private static List<ControlButton> ControlButtons = new();
		public static void DrawControls(InputAction LastAction)
		{
			// Displays the last action taken by the AI visually. There is a set of 4 squares for movement, one on top
			// 3 at the bottom, simulating a "WASD" layout. There are also three additional squares for jumping,
			// dashing, and grabbing. The squares are filled in based on the last action taken.
			//Monocle.Draw.SpriteBatch.Dispose();
			if (LastAction == null)
			{
				// Display empty squares if no action
				LastAction = new();
			}
			float SQUARE_WIDTH = 50;
			float TOTAL_WIDTH = 6 * SQUARE_WIDTH + 30; // 6 rows plus padding
			float startX = SCREEN_WIDTH - TOTAL_WIDTH;
			float startY = 10; // padding
			int BORDER_WIDTH = 2;
			float INNER_WIDTH = SQUARE_WIDTH - BORDER_WIDTH * 2;

            bool hollowRectHelper(float dx, float dy) => DrawThickHollowRect(startX + dx * SQUARE_WIDTH, startY + dy * SQUARE_WIDTH, SQUARE_WIDTH, SQUARE_WIDTH, BORDER_WIDTH, Color.Black * 1f);
            // Draw the first single square at the top, representing the up key

            // Fill in the squares based on the last action taken	

            bool solidRectHelper(float dx, float dy, bool pressed, string action)
            {
                Color color;

                if (pressed)
                {
                    switch (action)
                    {
                        case "Up":
                        case "Left":
                        case "Down":
                        case "Right":
                            color = Color.LightSeaGreen * 0.8f;
                            break;
                        case "Jump":
                            color = Color.LightSalmon * 0.8f;
                            break;
						case "L.Jump":
							color = Color.DarkSalmon * 0.8f;
							break;
                        case "Dash":
                            color = Color.LightSkyBlue * 0.8f;
                            break;
                        case "Grab":
                            color = Color.Chartreuse * 0.8f;
                            break;
                        default:
                            color = Color.LightGray * 0.8f;
                            break;
                    }
                }
                else
                {
                    color = Color.LightGray * 0.8f;
                }
                Rect(startX + dx * SQUARE_WIDTH + BORDER_WIDTH, startY + dy * SQUARE_WIDTH + BORDER_WIDTH, INNER_WIDTH, INNER_WIDTH, color);
                return true;
            }
            foreach (ControlButton button in ControlButtons)
            {
				// Draw outline
                hollowRectHelper(button.dx, button.dy);
				// Draw solid color
                solidRectHelper(button.dx, button.dy, button.pressed(LastAction), button.name);
				// Draw text
				ActiveFont.DrawOutline(button.name, 
					new Vector2(startX + button.dx * SQUARE_WIDTH + SQUARE_WIDTH / 2, startY + button.dy * SQUARE_WIDTH + SQUARE_WIDTH / 2),
					new Vector2(0.5f, 0.5f), 
					new Vector2(0.27f, 0.27f),
					Color.AntiqueWhite,
					0.5f,
					Color.Black
					);
            }
        }
		public static void DrawMetricStrings(CelesteBotRunner AIPlayer)
		{

            string[] metrics = new string[9];
            metrics[0] = "Last Eps. Reward: " + AIPlayer.LastEpisodeReward.ToString("F2");
            metrics[1] = "Current Eps. Reward: " + AIPlayer.Episode.TotalReward.ToString("F2");
			metrics[2] = "Last Action Reward" + AIPlayer.Episode.LastActionReward.ToString("F2");
            metrics[3] = "Target Distance: " + AIPlayer.Episode.LastDistanceFromTarget.ToString("F2");

                //metrics[4] = "Last Action: " + AIPlayer.LatestAction.ToString();
			
			metrics[5] = "Action Latency " + ActionRetrievalStatus;
            metrics[6] = "Death Count: " + AIPlayer.DeathCounter;
			List < Vector2 > targets = TargetFinder.GetLightBeamTargets();
			string targetString = "";
			foreach (Vector2 target in targets)
			{
				targetString += target.ToString() + ", ";
			}
            metrics[7] = "No progress timer: " + AIPlayer.Episode.FramesSinceMadeProgress / 60;
            metrics[8] = "Levels beaten: " + (AIPlayer.Episode.FinishedLevelCount - 1);

            float width = 300;
            float height = 15;
            float x = SCREEN_WIDTH  - width - 300;
			float y = 0;
            Rect(x, y, width, height * metrics.Length, Color.Black * 0.3f);
			for (int i = 0; i < metrics.Length && y < SCREEN_HEIGHT; i++, y+=height)
			{
				if (metrics[i] == null)
                    continue;
				if (metrics[i].Contains("\n"))
				{
					y += height;
				}
				ActiveFont.DrawOutline(metrics[i], new Vector2(x + 3, y), Vector2.Zero, new Vector2(0.3f, 0.3f), Color.White, 1f, Color.Black);
			}
        }

		public static List<Color> GetColorList()
		{
			var list = new List<Color>();
			List<PropertyInfo> fields = Color.Blue.GetType()
				.GetProperties(BindingFlags.Static | BindingFlags.Public)
				.Where(f => f.PropertyType == typeof(Color))
				.ToList();
    
			HashSet<Color> usedColors = new() { Color.Transparent, Color.Blue, Color.Red, Color.Pink, Color.Yellow, Color.LimeGreen, Color.White, Color.WhiteSmoke, Color.AntiqueWhite, Color.GhostWhite, Color.FloralWhite, Color.NavajoWhite};	
			foreach (var item in fields)
			{
				Color color = (Color)item.GetValue(null, null);
				if (!usedColors.Contains(color)) {
					list.Add(color);
				}
			}
			return list;
		}
		private static Dictionary<int, Color> ColorMap = new();
		public static void DrawCameraVision(PyList cameraVision)
		{
            // There are 40 x 22.5 tiles on the screen. Draw an ActiveFont for each with the value of cameraVision[i][j]
            _ = Color.Transparent;
            float y = 0;
			float width = 8;	
			float height = 8;
			int numColors = Colors.Count;
			using (Py.GIL())
			{
				for (int i = 0; i < 23; i++, y += height)
				{
                    float x = 0;
                    for (int j = 0; j < 40; j++, x += width)
					{
                        Color color = Color.Transparent;
						int tileID = cameraVision[i][j][0].As<int>();
                        color = tileID switch
                        {
                            (int)Entity.Unset => Color.White,
                            (int)Entity.Air => Color.Blue,
                            (int)Entity.Tile => Color.Red,
                            (int)Entity.Madeline => Color.Pink,
                            (int)Entity.Target => Color.Yellow,
							(int)Entity.Other => Color.LimeGreen,
                            _ => Colors[cameraVision[i][j][0].As<int>() % numColors],
                        } * 0.8f;
						if (! ColorMap.ContainsKey(tileID))
						{
							ColorMap.Add(tileID, color);
						}
                        Rect(x, y, width, height, color);
					}
				}
			}
            foreach (KeyValuePair<int, Color> entry in ColorMap)
            {
                //CelesteBotMain.Log("tileID:" + entry.Key + " color: " + entry.Value.ToString());
            }
        }
    }
}