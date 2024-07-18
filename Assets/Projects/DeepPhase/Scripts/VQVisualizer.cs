using NumSharp;
using UnityEngine;
using System;
using System.Collections.Generic;
using DeepPhase;
using UnityEngine.UIElements;


namespace AI4Animation
{
    [ExecuteInEditMode]
    public class VQVisualizer : MonoBehaviour
    {
        public enum VisualizerTYPE
        {
            Scatter,
            Curve
        };

        public VisualizerTYPE VisualizerType;
        public bool Visualize = true;
        public bool VisualizeHighlight = true;
        public bool VisualizeExtra = true;
        public string TargetPhaseTag;
        public string SavePath = "Assets/Projects/DeepPhase/Demos/Retargeting/VQs";
        [NonSerialized] private string CurrentTag = string.Empty;
        public UltiDraw.GUIRect ViewWindow = new(0.8f, 0.5f, 0.3f, 1f);
        [Range(0f, 1f)] public float WindowSplitRatio = 0.72f;
        public bool ShowPhase = false;
        public float Phase = 0f;
        public bool UseMesh = false;

        [NonSerialized] private NDArray Embedding2D = null;
        [NonSerialized] private NDArray Embeddings = null;
        [NonSerialized] private NDArray PCAMat, PCAMean;
        [NonSerialized] private NDArray Usage = null;
        [NonSerialized] private NDArray HighlightArray = null;
        [NonSerialized] private int QuantizationSteps = 0;
        [NonSerialized] private Vector2[][] Embedding2DNormalized = null;
        [NonSerialized] private List<NDArray> HighlightList;
        [NonSerialized] private List<NDArray> ExtraEmbeddings;
        [NonSerialized] private List<NDArray> ExtraManifoldPoints;
        [NonSerialized] private UltiDraw.GUIRect[] EmbeddingWindows = null;
        [NonSerialized] private PhaseManifoldVisualizer curveVisualizer = null;
        [NonSerialized] private int CurrentEmbeddingIdx = 0;
        
        private PhaseManifoldVisualizer CurveVisualizer => curveVisualizer ??= new PhaseManifoldVisualizer();
        
        readonly Color[] PlotColors = {UltiDraw.Blue, UltiDraw.Orange, UltiDraw.Green, UltiDraw.Red, UltiDraw.Blue, UltiDraw.Orange, UltiDraw.Green, UltiDraw.Red,};
        [Range(0f, 0.05f)] public float HighlightDotSize = 0.015f;
        [Range(0f, 0.01f)] public float DotSize = 0.004f;
        [Range(0f, 10f)] public float MeshDotSize = 4f;

        private int NumEmbeddings => Embeddings == null ? 0 : Embeddings.shape[^2];

        void Start()
        {
            LoadVQData();
            if (UseMesh)
            {
                Camera.onPreCull += CurveVisualizer.ScatterPlot.OnPreCullCallBack;
                CurveVisualizer.ScatterPlot.InitializeGameObject(gameObject);
            }
        }

        public void AddHighlight(params int[] idx)
        {
            var array = np.array(idx);
            CurrentEmbeddingIdx = idx[0];
            AddHighlight(array);
        }
        public void AddHighlight(NDArray idx)
        {
            if (VisualizeHighlight)
                HighlightList.Add(idx);
        }
        public void AddExtraEmbedding(float[] manifold)
        {
            if (VisualizeExtra)
                ExtraEmbeddings.Add(np.array(manifold));
        }
        public void AddExtraManifoldPoint(float[] manifold)
        {
            if (VisualizeExtra)
                ExtraManifoldPoints.Add(np.array(manifold));
        }

        (NDArray, NDArray) GetEmbedding2DNormalization(NDArray embedding2D, UltiDraw.GUIRect embeddingWindow)
        {
            NDArray range = new[] { embeddingWindow.W / 2, embeddingWindow.H / 2 };
            var embedNormalized = embedding2D;
            var mean = embedNormalized.mean(0, keepdims: true);
            var scaling = np.abs(embedNormalized - mean).max(0, keepdims: true);
            range = range[Slice.NewAxis];
            scaling /= range;
            scaling *= 1.1f;
            return (mean, scaling);
        }

        UltiDraw.GUIRect EmbeddingWindow
        {
            get
            {
                if (VisualizerType == VisualizerTYPE.Scatter && ShowPhase)
                    return ViewWindow.SubRectWithCorner(new UltiDraw.GUIRect(0f, 1f - WindowSplitRatio, 1f,
                        WindowSplitRatio));
                else
                    return ViewWindow;
            }

        }
        UltiDraw.GUIRect PhaseWindow => ViewWindow.SubRectWithCorner(new UltiDraw.GUIRect(0f, 0, 1f, 1f - WindowSplitRatio));
        
        void CreateEmbeddingWindows(int numWindows)
        {
            if (EmbeddingWindows == null || EmbeddingWindows.Length != numWindows) 
                EmbeddingWindows = new UltiDraw.GUIRect[numWindows];
            (int rows, int columns) layout = FindBestGridLayout(numWindows);
            float dx = 1f / layout.columns;
            float dy = 1f / layout.rows;
            int cnt = 0;
            for (int j = layout.rows - 1; j >= 0; j--)
            for (int i = 0; i < layout.columns; i++)
            {
                if (cnt == numWindows) break;
                EmbeddingWindows[cnt] = EmbeddingWindow.SubRect(new UltiDraw.GUIRect(
                    dx*i + dx / 2, dy*j + dy / 2, dx, dy));
                cnt++;
            }

            PrepareEmbedding2DNormalized();
        }
        
        private static float LayoutScore(int rows, int columns, int numWindows, float aspectRatio)
        {
            float layoutAspectRatio = (float)columns / rows / aspectRatio;
            float aspectRatioScore = Math.Abs(1 - layoutAspectRatio);

            // Calculate wasted area score
            int totalSlots = rows * columns;
            float wastedAreaScore = (float)(totalSlots - numWindows) / totalSlots;

            // Combine scores (you may adjust the weights as necessary)
            float combinedScore = aspectRatioScore + wastedAreaScore;

            return combinedScore;
        }
        
        private static (int rows, int columns) FindBestGridLayout(int numWindows, float aspectRatio = 1.0f)
        {
            (int rows, int columns) bestLayout = (0, 0);
            float bestScore = float.MaxValue;

            for (int rows = 1; rows <= numWindows; rows++)
            {
                int columns = (int)Math.Ceiling((float)numWindows / rows);
                float score = LayoutScore(rows, columns, numWindows, aspectRatio);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestLayout = (rows, columns);
                }
            }

            return bestLayout;
        }
        
        void PrepareEmbedding2DNormalized()
        {
            var embedding2ds = new List<Vector2[]>();
            for (int i = 0; i < Embedding2D.shape[0]; i++)
            {
                var embedding2d = Embedding2D[i];
                var window = EmbeddingWindows[i];
                var (mean, std) = GetEmbedding2DNormalization(embedding2d, window);
                var embedRender = ((embedding2d - mean) / std).ToArrayVector2();
                embedding2ds.Add(embedRender);
            }
            Embedding2DNormalized = embedding2ds.ToArray();
        }
        
        void LoadVQData()
        {
            var npzName = SavePath + $"/{TargetPhaseTag}.npz";
            if (!System.IO.File.Exists(npzName))
            {
                Debug.Log("VQ file not found at " + npzName);
                Embedding2D = null;
                Usage = null;
                HighlightList = null;
                PCAMat = null;
                PCAMean = null;
                CurrentTag = TargetPhaseTag;
            }
            else
            {
                var npzLoader = new NumSharpExtensions.NpzLoader(npzName);
                Embedding2D = npzLoader["embedding2d"];
                Usage = npzLoader["usage"];
                PCAMat = npzLoader["pca_mat"];
                PCAMean = npzLoader["pca_mean"];
                Embeddings = npzLoader["embeddings"];

                if (Embeddings.ndim == 2)
                {
                    // No residual quantization used. So make it looks like a one step.
                    Embedding2D = Embedding2D[np.newaxis];
                    Usage = Usage[np.newaxis];
                    Embeddings = Embeddings[np.newaxis];
                    PCAMat = PCAMat[np.newaxis];
                }

                QuantizationSteps = Embeddings.shape[0];
                HighlightList = new List<NDArray>();
                ExtraEmbeddings = new List<NDArray>();
                CurrentTag = TargetPhaseTag;
                
                CreateEmbeddingWindows(QuantizationSteps);

                HighlightArray = np.zeros((QuantizationSteps, Embeddings.shape[1]), dtype: np.bool_);
                
                PrepareEmbedding2DNormalized();
                
                CurveVisualizer.Embeddings = Embeddings;

                npzLoader.Dispose();
            }
        }
        
        private void CalcHighlightArray()
        {
            HighlightArray[Slice.All] = false;
            for (var i = 0; i < QuantizationSteps; i++)
            {
                foreach (var idx in HighlightList)
                    HighlightArray[i, (int)idx[i]] = true;
            }
            HighlightList.Clear();
        }
        
        private NDArray PCATransform(NDArray X, int step)
        {
            var XTransformed = X - PCAMean[step];
            XTransformed = np.matmul(XTransformed, PCAMat[step]);
            return XTransformed;
        }
        
        private NDArray GetExtraEmbeddings()
        {
            if (ExtraEmbeddings.Count == 0 || !VisualizeExtra) return null;
            var extraEmbeddings = np.stack(ExtraEmbeddings.ToArray());
            extraEmbeddings = PCATransform(extraEmbeddings, 0);
            ExtraEmbeddings.Clear();
            return extraEmbeddings;
        }


        void RenderScatter()
        {
            for (int step = 0; step < QuantizationSteps; step++)
            {
                var usage = Usage[step];
                var window = EmbeddingWindows[step];
                var highlight = HighlightArray[step];
                UltiDraw.GUIRectangle(window.GetCenter(), window.GetSize() * 0.98f, UltiDraw.Plotting.Background);
                var embedRender = Embedding2DNormalized[step];
                for (int i = 0; i < embedRender.Length; i++)
                {
                    var dotSize = (VisualizeHighlight && (bool)highlight[i]) ? HighlightDotSize : DotSize;
                    UltiDraw.GUICircle(window.GetCenter() + embedRender[i], dotSize, PlotColors[usage[i]]);
                }
                
                if (step == 0 && ExtraEmbeddings.Count > 0 && VisualizeExtra)
                {
                    var (mean, std) = GetEmbedding2DNormalization(Embedding2D[0], window);
                    var extraEmbeddingsRender = ((GetExtraEmbeddings() - mean) / std).ToArrayVector2();
                    foreach (var t in extraEmbeddingsRender)
                    {
                        UltiDraw.GUICircle(window.GetCenter() + t, HighlightDotSize, PlotColors[4]);
                    }
                }
            }
        }

        void RenderCurve()
        {
            var window = EmbeddingWindows[0];
            UltiDraw.GUIRectangle(window.GetCenter(), window.GetSize() * 0.98f, UltiDraw.Plotting.Background);
            CurveVisualizer.SetWindow(window);
            CurveVisualizer.OnRenderObject(DotSize);
        }

        void RenderCurveMesh()
        {
            var window = EmbeddingWindows[0];
            // UltiDraw.GUIRectangle(window.GetCenter(), window.GetSize() * 0.98f, UltiDraw.Plotting.Background);
            CurveVisualizer.SetWindow(window);
            if (ShowPhase)
            {
                int currentIndex = CurrentEmbeddingIdx;
                var newPoint = VQPhaseModule.CalculateManifoldNDArray(Embeddings[0, currentIndex], Phase);
                CurveVisualizer.AddAdditionalPoints(np.array(newPoint));
            }
            CurveVisualizer.OnRenderObject(MeshDotSize, true);
        }
        
        private void OnRenderObject()
        {
            if (!Visualize) return;
            if (Embedding2D == null) return;
            
            CalcHighlightArray();
            
            UltiDraw.Begin();
            
            var meshRenderer = gameObject.GetComponent<MeshRenderer>();
            
            
            if (VisualizerType == VisualizerTYPE.Scatter)
            {
                if (meshRenderer != null) meshRenderer.enabled = false;
                RenderScatter();
                if (ShowPhase)
                {
                    var radius = Mathf.Min(0.02f, PhaseWindow.H/2f * 0.4f, PhaseWindow.W/2f * 0.4f);
                    var amplitude = new float[1];
                    var phase = new float[2];
                    phase[0] = Mathf.Cos(-2 * Mathf.PI * Phase);
                    phase[1] = Mathf.Sin(-2 * Mathf.PI * Phase);
                    amplitude[0] = 1f;
                    PhaseVisualization.DrawPhaseState(PhaseWindow.GetCenter(), radius, amplitude, phase, 
                        1, 1f, true);
                }
            }
            else if (VisualizerType == VisualizerTYPE.Curve)
            {
                if (UseMesh)
                {
                    if (meshRenderer != null) meshRenderer.enabled = true;
                }
                else
                {
                    if (meshRenderer != null) meshRenderer.enabled = false;
                    RenderCurve();
                }
            }
            
            UltiDraw.End();
        }

        private void Update()
        {
            if ((CurrentTag != TargetPhaseTag) || (Embedding2D is null))
            {
                LoadVQData();
            }
            
            CreateEmbeddingWindows(QuantizationSteps);
            
            if (VisualizerType == VisualizerTYPE.Curve && UseMesh)
                RenderCurveMesh();
        }
    }
}