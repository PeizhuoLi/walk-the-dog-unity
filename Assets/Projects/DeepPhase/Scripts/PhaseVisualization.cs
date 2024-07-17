using UnityEngine;

namespace DeepPhase
{
    public static class PhaseVisualization
    {
        // private static UltiDraw.GUIRect PhaseWindow = new UltiDraw.GUIRect(0.5f, 0.95f, 0.125f, 0.05f);
        // private static float TextScale = 0.0225f;
        
        public static void DrawPhaseState(Vector2 center, float radius, float[] amplitudes, float[] phases, 
            int channels, float max, bool insideUltidrawContext=false, UltiDraw.GUIRect window=null, float primalAngle=float.NegativeInfinity) {
            float outerRadius = radius;
            float innerRadius = 2f*Mathf.PI*outerRadius/(channels+1);
            float amplitude = max == float.MinValue ? 1f : max;

            if (!insideUltidrawContext)
                UltiDraw.Begin();
            
            if (window != null)
                UltiDraw.GUIRectangle(window.GetCenter(), window.GetSize() * 0.98f, UltiDraw.Plotting.Background);

            if (channels > 1)
            {
                UltiDraw.GUICircle(center, 1.05f * 2f * outerRadius, UltiDraw.White);
                UltiDraw.GUICircle(center, 2f * outerRadius, UltiDraw.BlackGrey);


                for (int i = 0; i < channels; i++)
                {
                    float activation = amplitudes[i].Normalize(0f, max, 0f, 1f);
                    Color color = UltiDraw.GetRainbowColor(i, channels).Darken(0.5f);
                    float angle = Mathf.Deg2Rad * 360f * i.Ratio(0, channels);
                    Vector2 position = center + outerRadius *
                        new Vector2(Mathf.Sin(angle), UltiDraw.AspectRatio() * Mathf.Cos(angle));
                    UltiDraw.GUILine(center, position, 0f, activation * innerRadius,
                        UltiDraw.GetRainbowColor(i, channels).Opacity(activation));
                    UltiDraw.GUICircle(position, innerRadius, color);
                    UltiDraw.PlotCircularPivot(position, 0.9f * innerRadius,
                        360f * Utility.PhaseValue(new Vector2(phases[2 * i], phases[2 * i + 1])),
                        amplitudes[i].Normalize(0f, amplitude, 0f, 1f),
                        amplitudes[i] == 0f ? UltiDraw.Red : UltiDraw.White, UltiDraw.Black);
                }
            }

            else
            {
                var i = 0;
                float activation = amplitudes[i].Normalize(0f, max, 0f, 1f);
                Color color = UltiDraw.GetRainbowColor(i, channels).Darken(0.5f);
                float angle = Mathf.Deg2Rad * 360f * i.Ratio(0, channels);
                Vector2 position = center;
                UltiDraw.GUILine(center, position, 0f, activation * innerRadius,
                    UltiDraw.GetRainbowColor(i, channels).Opacity(activation));
                UltiDraw.GUICircle(position, innerRadius, color);
                UltiDraw.PlotCircularPivot(position, 0.9f * innerRadius,
                    360f * Utility.PhaseValue(new Vector2(phases[2 * i], phases[2 * i + 1])),
                    amplitudes[i].Normalize(0f, amplitude, 0f, 1f),
                    amplitudes[i] == 0f ? UltiDraw.Red : UltiDraw.White, UltiDraw.Black);
                
                if (primalAngle > float.NegativeInfinity)
                    DrawPrimalAxis(center, innerRadius / 2, primalAngle, true);
            }

            if (!insideUltidrawContext)
                UltiDraw.End();
        }

        public static void DrawPrimalAxis(Vector2 center, float radius, float primalAngle, bool insideUltidrawContext=false)
        {
            if (!insideUltidrawContext)
                UltiDraw.Begin();

            var v0 = new Vector2(Mathf.Cos(primalAngle), Mathf.Sin(primalAngle) * UltiDraw.AspectRatio());
            primalAngle += Mathf.PI / 2;
            var v1 = new Vector2(Mathf.Cos(primalAngle), Mathf.Sin(primalAngle) * UltiDraw.AspectRatio());

            radius *= 1.5f;

            float centralThickness = 0.005f;
            
            UltiDraw.GUILine(center, center + radius * v0, centralThickness, 0f, UltiDraw.Red);
            UltiDraw.GUILine(center, center - radius * v0, centralThickness, 2 * centralThickness, UltiDraw.Red);
            UltiDraw.GUILine(center, center + radius * v1, 0.005f, 0f, UltiDraw.Green);
            UltiDraw.GUILine(center, center - radius * v1, 0.005f, 2 * centralThickness, UltiDraw.Green);

            if (!insideUltidrawContext)
                UltiDraw.End();
        }
    }
}