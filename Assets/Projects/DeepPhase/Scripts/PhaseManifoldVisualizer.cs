using NumSharp;
using UnityEngine;
using System;
using DeepPhase;
using LinAlg;
using AI4Animation;
using MatPlot;


namespace DeepPhase
{
    // [ExecuteInEditMode]
    public class PhaseManifoldVisualizer
    {
        [NonSerialized] private NDArray embeddings;
        [NonSerialized] private NDArray basePoints;
        public UltiDraw.GUIRect Window = new UltiDraw.GUIRect(0.5f, 0.95f, 0.125f, 0.05f);
        private int resolution = 200;
        [NonSerialized] private LinearAlgebra.PCA pca;
        [NonSerialized] private ScatterPlot scatterPlot;

        private int NumColors => Embeddings.shape[0] + 1;

        public ScatterPlot ScatterPlot
        {
            get
            {
                if (scatterPlot == null)
                {
                    scatterPlot = new ScatterPlot();
                    scatterPlot.Init(basePoints.shape[0] * basePoints.shape[1]);
                }

                return scatterPlot;
            }
            set => scatterPlot = value;
        }
        
        public NDArray Embeddings
        {
            get => embeddings;
            set
            {
                embeddings = value;
                if (embeddings.ndim == 3) embeddings = embeddings[0];
                PreparePCA();
                
                for (var i = 0; i < embeddings.shape[0]; i++)
                {
                    ScatterPlot.Plot(basePoints[i], UltiDraw.GetRainbowColor(i, NumColors));
                }
                ScatterPlot.SetBasePoints();
            }
        }

        public void SetWindow(UltiDraw.GUIRect Window)
        {
            ScatterPlot.Window = Window;
        }

        private void PreparePCA()
        {
            var n_embeddings = embeddings.shape[0];
            var n_dim = embeddings.shape[1] / 2;
            var phases = np.linspace(0f, 1f, resolution);

            var data = np.zeros<float>(n_embeddings, resolution, n_dim);
            var cnt = 0;

            pca = new LinearAlgebra.PCA(2);
            
            for (var i = 0; i < n_embeddings; i++)
            {
                for (var j = 0; j < resolution; j++)
                {
                    data[i, j] = VQPhaseModule.CalculateManifoldNDArray(embeddings[i], phases[j]);
                }
            }
            
            var data_flat = data.reshape(-1, n_dim);
            pca.fit(data_flat);
            basePoints = pca.transform(data_flat).reshape(data.shape[0], -1, 2);
        }

        public void AddAdditionalPoints(NDArray point, int colorIndex = -1)
        {
            if (colorIndex == -1) colorIndex = NumColors - 1;
            point = pca.transform(point);
            ScatterPlot.Plot(point, UltiDraw.GetRainbowColor(colorIndex, NumColors));
        }

        public void OnRenderObject(float size, bool useMesh = false)
        {
            if (embeddings == null) return;
            var plotter = ScatterPlot;
            if (!useMesh)
            {
                plotter.Show(size);
            }
            else
            {
                plotter.ShowWithMesh(size);
            }
        }
    }
}