using Celeste;
using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using Python.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
namespace CelesteBot_2023
{
	public class DrawMetrics
	{
		public const int SCREEN_WIDTH = 960;
		public const int SCREEN_HEIGHT = 540;
		public static void Draw()
		{
			DrawMetricStrings();
			try
			{
				if (CelesteBotInteropModule.BotState == CelesteBotInteropModule.State.Running && !CelesteBotManager.IsWorker)
					DrawCameraVision(CelesteBotInteropModule.CurrentPlayer.cameraManager.CameraVision);
			}
            catch (NullReferenceException)
			{
                // Do nothing
            }
		}
		public static void DrawMetricStrings()
		{
            string[] metrics = new string[4];
            metrics[0] = "Latest Episode Reward: " + CelesteBotInteropModule.CurrentPlayer.LastEpisodeReward.ToString("F2");
            metrics[1] = "Current Episode Reward: " + CelesteBotInteropModule.CurrentPlayer.Episode.TotalReward.ToString("F2");
            metrics[2] = "Distance from target: " + CelesteBotInteropModule.CurrentPlayer.Episode.LastDistanceFromTarget.ToString("F2");
            if (CelesteBotInteropModule.LastAction != null)
                metrics[3] = "Last Action: " + CelesteBotInteropModule.LastAction.ToString();
            float width = 600;
            float height = 25;
            float x = SCREEN_WIDTH  - width;
			float y = 0;
			Color color = Color.Black * 0.8f;
			for (int i = 0; i < metrics.Length; i++, y+=30)
			{
                Monocle.Draw.Rect(x, y, width, height, color);
				ActiveFont.Draw(metrics[i], new Vector2(x + 3, y), Vector2.Zero, new Vector2(0.3f, 0.3f), Color.White);
			}
        }
		public static void DrawCameraVision(PyList cameraVision)
		{
			// There are 40 x 22.5 tiles on the screen. Draw an ActiveFont for each with the value of cameraVision[i][j]
			Color color = Color.Transparent;
			float x = 0;
			float y = 0;
			float width = 8;	
			float height = 8;
			using (Py.GIL())
			{
				for (int i = 0; i < cameraVision.Length(); i++, y += height)
				{
					x = 0;
					for (int j = 0; j < cameraVision[i].Length(); j++, x += width)
					{
						color = Color.Transparent;
						switch (cameraVision[i][j][0].As<int>())
						{
							case 1:
								color = Color.Blue * 0.5f;
								break;
							case 2:
								color = Color.Red * 0.5f; ;
								break;
                            case 3:
                                color = Color.Pink * 0.5f; ;
                                break;
                            case 4:
								color = Color.Green * 0.5f; ;
								break;
							default:
								break;
						}
						Monocle.Draw.Rect(x, y, width, height, color);
					}
				}
			}
		}
    }
}