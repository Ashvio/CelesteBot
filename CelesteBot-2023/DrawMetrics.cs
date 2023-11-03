using Celeste;
using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System;
using System.Collections;
using System.Collections.Generic;
namespace CelesteBot_2023
{
	public class DrawMetrics
	{
		
		public static void Draw()
		{
			string[] metrics = new string[3];
			metrics[0] = "Latest Episode Reward: " + CelesteBotInteropModule.CurrentPlayer.LastEpisodeReward.ToString("F2");
			metrics[1] = "Current Episode Reward: " + CelesteBotInteropModule.CurrentPlayer.Episode.TotalReward.ToString("F2");
			metrics[2] = "Distance from target: " + CelesteBotInteropModule.CurrentPlayer.Episode.LastDistanceFromTarget.ToString("F2"); 
			DrawMetricStrings(metrics);
		}
		public static void DrawMetricStrings(string[] metrics)
		{
			float x = 0;
			float y = 0;
			float width = 600; 
			float height = 30;
			Color color = Color.Black * 0.8f;
			for (int i = 0; i < metrics.Length; i++, y+=30)
			{
                Monocle.Draw.Rect(x, y, width, height, color);
				ActiveFont.Draw(metrics[i], new Vector2(x + 3, y), Vector2.Zero, new Vector2(0.4f, 0.4f), Color.White);
			}
        }
    }
}