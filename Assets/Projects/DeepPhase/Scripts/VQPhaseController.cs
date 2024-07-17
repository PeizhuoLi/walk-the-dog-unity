using System;
using System.Collections.Generic;
using AI4Animation;
using UnityEngine;
using UnityEditor;
using NumSharp;

namespace DeepPhase
{
    [ExecuteInEditMode]
    public class VQPhaseController : MonoBehaviour
    {
        public bool Visualize = true;
        public bool VisualizeEmbedding = true;
        public bool VisualizeHighlight = true;
        public string TargetPhaseTag;
        public string SavePath = "Assets/Projects/DeepPhase/Demos/Retargeting/VQs";
        public bool UpdateSyntheticPhase = false;
        [HideInInspector] public int[] SyntheticPhaseIndex = null;
        [Range(0f, 5f)] public float SyntheticFrequency = 1f;
        [NonSerialized] private string CurrentTag = string.Empty;
        public UltiDraw.GUIRect[] EmbeddingWindows = null;

        [NonSerialized] public UltiDraw.GUIRect EmbeddingWindow = new(0.8f, 0.55f, 0.4f, 0.9f);
        
        public UltiDraw.GUIRect PhaseWindow = new(0.15f, 0.5f, 0.125f, 0.05f);
        [Range(0f, 0.01f)] public float DotSize = 0.004f;
        [Range(0f, 0.05f)] public float HighlightDotSize = 0.015f;
        private List<NDArray> HighlightList;
        private List<NDArray> ExtraEmbeddings;
        readonly Color[] PlotColors = {UltiDraw.Blue, UltiDraw.Orange, UltiDraw.Green, UltiDraw.Red, UltiDraw.Blue, UltiDraw.Orange, UltiDraw.Green, UltiDraw.Red,};

        private NDArray Embedding2D = null;
        private NDArray Embeddings = null;
        private NDArray PCAMat, PCAMean;
        private NDArray Usage = null;
        private NDArray HighlightArray = null;
        private float SyntheticPhase = 0f;
        private int QuantizationSteps = 0;
        
        private Vector2[][] Embedding2DNormalized = null;

        [NonSerialized] private VQVisualizer PhaseVisualizer;
        
        private VQVisualizer GetVQVisualizer()
        {
            if (PhaseVisualizer == null)
                PhaseVisualizer = GameObjectExtensions.Find<VQVisualizer>("EditorVQVisualizer");
            
            if (PhaseVisualizer != null && PhaseVisualizer.TargetPhaseTag == TargetPhaseTag) return PhaseVisualizer;
            return null;
        }

        private int NumEmbeddings => Embeddings == null ? 0 : Embeddings.shape[^2];

        private void OnEnable()
        {
            if (CurrentTag != TargetPhaseTag) 
                LoadVQData();
        }

        private NDArray GetCurrentEmbedding()
        {
            var embedding = Embeddings[0, SyntheticPhaseIndex[0]];
            for (int i = 1; i < QuantizationSteps; i++) 
                embedding += Embeddings[i, SyntheticPhaseIndex[i]];
            return embedding;
        }
        
        public float[] GetPhaseManifold(float deltaPhase = 0)
        {
            var embedding = GetCurrentEmbedding();
            return VQPhaseModule.CalculateManifold(embedding, SyntheticPhase + deltaPhase);
        }

        public int GetPhaseManifolds(float framerate, int windowSize, Span<float> buffer)
        {
            var phases = np.linspace(-windowSize / 2f, windowSize / 2f, windowSize) * 1f / framerate * SyntheticFrequency + SyntheticPhase;
            var phaseManifolds = VQPhaseModule.CalculateManifold(GetCurrentEmbedding(), phases);
            phaseManifolds.CopyTo(buffer);
            return phaseManifolds.Length;
        }

        (NDArray, NDArray) GetEmbedding2DNormalization(NDArray embedding2D, UltiDraw.GUIRect embeddingWindow)
        {
            NDArray range = new [] { embeddingWindow.W / 2, embeddingWindow.H / 2 };
            var embedNormalized = embedding2D;
            var mean = embedNormalized.mean(0, keepdims:true);
            var scaling = np.abs(embedNormalized - mean).max(0, keepdims: true);
            range = range[Slice.NewAxis];
            scaling /= range;
            scaling *= 1.1f;
            return (mean, scaling);
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

        // Vector2[] CalcNormalizedEmbedding2DInplace()
        // {
        //     int n = Embedding2D.shape[0];
        //     for (int i = 0; i < n; i++) 
        //         EmbeddingNormalized[i] = new Vector2(Embedding2D[i, 0], Embedding2D[i, 1]);
        //     Vector2 mean = Vector2.zero;
        //     for (int i = 0; i < n; i++)
        //         mean += EmbeddingNormalized[i];
        //     mean /= n;
        //     for (int i = 0; i < n; i++)
        //         EmbeddingNormalized[i] -= mean;
        //     
        //     float dxRange = EmbeddingWindow.W / 2, dyRange = EmbeddingWindow.H / 2;
        //     Vector2 scaling = Vector2.one;
        //     for (int i = 0; i < n; i++)
        //     {
        //         scaling.x = Mathf.Max(scaling.x, Mathf.Abs(EmbeddingNormalized[i].x));
        //         scaling.y = Mathf.Max(scaling.y, Mathf.Abs(EmbeddingNormalized[i].y));
        //     }
        //     scaling.x /= dxRange;
        //     scaling.y /= dyRange;
        //     scaling *= 1.1f;
        //     for (int i = 0; i < n; i++)
        //         EmbeddingNormalized[i] /= scaling;
        //     return EmbeddingNormalized;
        // }
        
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

        void CreateEmbeddingWindows(int numWindows)
        {
            EmbeddingWindows = new UltiDraw.GUIRect[numWindows];
            (int rows, int columns) layout = FindBestGridLayout(numWindows);
            float dx = 1f / layout.columns;
            float dy = 1f / layout.rows;
            int cnt = 0;
            for (int j = layout.rows - 1; j >= 0; j--)
                for (int i = 0; i < layout.columns; i++)
                {
                    EmbeddingWindows[cnt] = EmbeddingWindow.SubRect(new UltiDraw.GUIRect(
                        dx*i + dx / 2, dy*j + dy / 2, dx, dy));
                    cnt++;
                    if (cnt == numWindows) return;
                }
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
                
                // Precalculate windows for visualize embeddings
                // float w = 0.4f, h = 0.3f;
                // float x = 0.8f, y = 0.8f;
                // EmbeddingWindows = new UltiDraw.GUIRect[QuantizationSteps];
                // for (int i = 0; i < QuantizationSteps; i++)
                //     EmbeddingWindows[i] = new UltiDraw.GUIRect(x, y - h * i, w, h);
                CreateEmbeddingWindows(QuantizationSteps);

                SyntheticPhaseIndex = new int[QuantizationSteps];
                HighlightArray = np.zeros((QuantizationSteps, Embeddings.shape[1]), dtype: np.bool_);
                
                PrepareEmbedding2DNormalized();

                npzLoader.Dispose();

                // EmbeddingNormalized = new Vector2[Embedding2D.shape[0]];

                // var verification = PCATransform(Embeddings);
                // var delta = np.abs(verification - embedding2d);
                // if (delta.max() > 1e-5f)
                // {
                //     Debug.Log($"PCA transform verification failed. Max delta: {delta.max()}");
                // }
            }
        }

        private NDArray PCATransform(NDArray X, int step)
        {
            var XTransformed = X - PCAMean[step];
            XTransformed = np.matmul(XTransformed, PCAMat[step]);
            return XTransformed;
        }

        private void Update()
        {
            if (CurrentTag != TargetPhaseTag) 
                LoadVQData();
            if (UpdateSyntheticPhase)
            {
                SyntheticPhase += Time.deltaTime * SyntheticFrequency;
                if (SyntheticPhase > 1f) SyntheticPhase -= 1f;
            }
        }

        private void CalcHighlightArray()
        {
            HighlightArray[Slice.All] = false;
            for (int i = 0; i < QuantizationSteps; i++)
            {
                foreach (var idx in HighlightList)
                    HighlightArray[i, (int)idx[i]] = true;
            }
            HighlightList.Clear();
            
            if (UpdateSyntheticPhase)
            {
                for (int i = 0; i < QuantizationSteps; i++)
                    HighlightArray[i, SyntheticPhaseIndex[i]] = true;
            }
        }

        private NDArray GetExtraEmbeddings()
        {
            if (ExtraEmbeddings.Count == 0) return null;
            var extraEmbeddings = np.stack(ExtraEmbeddings.ToArray());
            extraEmbeddings = PCATransform(extraEmbeddings, 0);
            ExtraEmbeddings.Clear();
            return extraEmbeddings;
        }
        
        public void AddExtraEmbedding(float[] manifold)
        {
            ExtraEmbeddings.Add(np.array(manifold));
        }

        public void AddHighlight(NDArray idx)
        {
            HighlightList.Add(idx);
        }

        private void OnRenderObject()
        {
            if (!Visualize) return;
            if (Embedding2D == null) return;
            
            CalcHighlightArray();
            
            UltiDraw.Begin();
            if (VisualizeEmbedding)
            {
                for (int step = 0; step < QuantizationSteps; step++)
                {
                    // var embedding2d = Embedding2D[step];
                    var usage = Usage[step];
                    var window = EmbeddingWindows[step];
                    var highlight = HighlightArray[step];
                    // var (mean, std) = GetEmbedding2DNormalization(embedding2d, window);
                    // var embedRender = ((embedding2d - mean) / std).ToArrayVector2();
                    UltiDraw.GUIRectangle(window.GetCenter(), window.GetSize() * 0.98f, UltiDraw.Plotting.Background);
                    var embedRender = Embedding2DNormalized[step];
                    for (int i = 0; i < embedRender.Length; i++)
                    {
                        var dotSize = (VisualizeHighlight && (bool)highlight[i]) ? HighlightDotSize : DotSize;
                        UltiDraw.GUICircle(window.GetCenter() + embedRender[i], dotSize, PlotColors[usage[i]]);
                    }
                
                    if (step == 0 && ExtraEmbeddings.Count > 0)
                    {
                        var (mean, std) = GetEmbedding2DNormalization(Embedding2D[0], window);
                        var extraEmbeddingsRender = ((GetExtraEmbeddings() - mean) / std).ToArrayVector2();
                        for (int i = 0; i < extraEmbeddingsRender.Length; i++)
                        {
                            UltiDraw.GUICircle(window.GetCenter() + extraEmbeddingsRender[i], HighlightDotSize, PlotColors[4]);
                        }
                    }
                }
            }
            
            if (UpdateSyntheticPhase)
            {
                var radius = 0.02f;
                var amplitude = new float[1];
                var phase = new float[2];

                var vqVis = GetVQVisualizer();

                if (vqVis != null)
                {
                    vqVis.Phase = SyntheticPhase;
                    vqVis.AddHighlight(SyntheticPhaseIndex);
                }
                var primalAngle = VQPhaseModule.GetPrimalAngle(GetCurrentEmbedding());
                phase[0] = Mathf.Cos(-2 * Mathf.PI * SyntheticPhase);
                phase[1] = Mathf.Sin(-2 * Mathf.PI * SyntheticPhase);
                amplitude[0] = 1f;
                PhaseVisualization.DrawPhaseState(PhaseWindow.GetCenter(), radius, amplitude, phase, 
                    1, 1f, true, primalAngle: primalAngle);
            }
            
            UltiDraw.End();
        }

        private void OnGUI()
        {
            return;
            if (!Visualize) return;
            if (Embedding2D == null) return;
            var embeddingWindow = EmbeddingWindow;
            var scale = 0.0225f;
            UltiDraw.Begin();
            var offset = new Vector2(0f, embeddingWindow.GetSize().y / 2 + scale);
            UltiDraw.OnGUILabel(embeddingWindow.GetCenter() - offset, embeddingWindow.GetSize(), scale, "Embedding", UltiDraw.White);
            UltiDraw.End();
        }


        [CustomEditor(typeof(VQPhaseController))]
        public class VQPhaseContollerEditor : Editor {
            public VQPhaseController Target;
            void Awake() {
                Target = (VQPhaseController)target;
            }

            public override void OnInspectorGUI() {
                DrawDefaultInspector();
                for (int i = 0; i < Target.SyntheticPhaseIndex.Length; i++)
                {
                    Target.SyntheticPhaseIndex[i] = EditorGUILayout.IntSlider($"Synthetic Phase Index {i}",
                        Target.SyntheticPhaseIndex[i], 0, Target.NumEmbeddings - 1);
                }
            }
        }
    }
}