using System;
using DeepPhase;
using UnityEngine;
using NumSharp;
using SerializedNDArray;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Single;

namespace AI4Animation
{
    public class VQPhaseModule : Module
    {
        public SNDArray<float> RegularPhaseManifold, MirroredPhaseManifold;
        public SNDArray<int> RegularEmbedIndex, MirroredEmbedIndex;
        public SNDArray<float> RegularPhase, MirroredPhase;
        public SNDArray<float> RegularState, MirroredState;
        public SNDArray<float> RegularStateOri, MirroredStateOri;
        public SNDArray<float> RegularPhaseManifoldOri, MirroredPhaseManifoldOri;
        
        private UltiDraw.GUIRect PhaseWindow = new(0.15f, 0.5f, 0.125f, 0.05f);
        private VQVisualizer PhaseVisualizer;
        [NonSerialized] private float[] Angles;
        [NonSerialized] private float[] Amplitudes;
        [NonSerialized] private float[] Phases;
        [NonSerialized] private float[] Embeddings;

        public int ManifoldSize => RegularPhaseManifold.shape[1];

        public static float[] CalculateManifold(NDArray state, float phase)
        {
            return CalculateManifoldNDArray(state, phase).ToArray<float>();
        }
        
        public static NDArray CalculateManifoldNDArray(NDArray state, float phase)
        {
            state = state.reshape(-1, 2);
            var circle = np.array(Mathf.Cos(2 * Mathf.PI * phase), Mathf.Sin(2 * Mathf.PI * phase));
            circle = circle.reshape(2, 1);
            var manifold = np.matmul(state, circle).reshape(-1);
            return manifold;
        }

        public static float GetPrimalAngle(NDArray state)
        {
            state = state.reshape(-1, 2);
            var array = new float[state.shape[0], 2];
            
            for (var i = 0; i < state.shape[0]; i++)
            {
                array[i, 0] = state[i, 0];
                array[i, 1] = state[i, 1];
            }
            
            Matrix<float> stateMatrix = DenseMatrix.OfArray(array);
            var svd = stateMatrix.Svd(true);
            var vT = svd.VT;

            var sinx = vT[1, 0];
            var cosx = vT[0, 0];
            var angle = Mathf.Atan2(sinx, cosx);
            
            return angle;
        }

        public static float[] CalculateManifold(NDArray state, NDArray phase)
        {
            state = state.reshape(-1, 2);
            phase = phase.reshape(-1, 1);
            var manifold = state[Slice.All, 0] * np.cos(2 * Mathf.PI * phase) +
                            state[Slice.All, 1] * np.sin(2 * Mathf.PI * phase);
            return manifold.ToArray<float>();
        }

        public void CreateArray(int manifoldChannels, int stateChannels, int phaseChannels, int quantizeLevels)
        {
            RegularPhaseManifold = new SNDArray<float>(Asset.Frames.Length, manifoldChannels);
            MirroredPhaseManifold = new SNDArray<float>(Asset.Frames.Length, manifoldChannels);
            RegularEmbedIndex = new SNDArray<int>(Asset.Frames.Length, quantizeLevels);
            MirroredEmbedIndex = new SNDArray<int>(Asset.Frames.Length, quantizeLevels);
            RegularPhase = new SNDArray<float>(Asset.Frames.Length, phaseChannels);
            MirroredPhase = new SNDArray<float>(Asset.Frames.Length, phaseChannels);
            RegularState = new SNDArray<float>(Asset.Frames.Length, stateChannels);
            MirroredState = new SNDArray<float>(Asset.Frames.Length, stateChannels);
            
            RegularStateOri = new SNDArray<float>(Asset.Frames.Length, stateChannels);
            MirroredStateOri = new SNDArray<float>(Asset.Frames.Length, stateChannels);
            RegularPhaseManifoldOri = new SNDArray<float>(Asset.Frames.Length, manifoldChannels);
            MirroredPhaseManifoldOri = new SNDArray<float>(Asset.Frames.Length, manifoldChannels);
            
            Asset.MarkDirty(true, false);
        }
        
        private VQVisualizer GetVQVisualizer(string tag = null)
        {
            if (PhaseVisualizer == null)
                PhaseVisualizer = GameObjectExtensions.Find<VQVisualizer>("EditorVQVisualizer");
            
            if (PhaseVisualizer != null && PhaseVisualizer.TargetPhaseTag == tag) return PhaseVisualizer;
            return null;
        }
        
         
        protected override void DerivedInitialize()
        {
            
        }

        protected override void DerivedLoad(MotionEditor editor)
        {
            
        }
        
        protected override void DerivedUnload(MotionEditor editor) {

        }


        protected override void DerivedCallback(MotionEditor editor) {

        }
        
        public override void DerivedResetPrecomputation()
        {
            
        }

        protected override void DerivedGUI(MotionEditor editor) 
        {
            // UltiDraw.Begin();
            // var scale = 0.0225f;
            // var offset = new Vector2(0f, PhaseWindow.GetSize().y / 2 + scale);
            // UltiDraw.OnGUILabel(PhaseWindow.GetCenter() - offset, PhaseWindow.GetSize(), scale, "Phase", UltiDraw.White);
            // UltiDraw.End();
        }

        protected override void DerivedDraw(MotionEditor editor)
        {
            int frameIndex = GetFrameIndex(editor.GetTimestamp());
            bool mirror = editor.Mirror;
            var radius = 0.02f;
            var center = PhaseWindow.GetCenter() + new Vector2(0f, 2.1f * radius);
            
            if (Amplitudes == null)
            {
                var phaseChannels = RegularPhase.shape[1];
                Angles = new float[phaseChannels];
                Amplitudes = new float[phaseChannels];
                Phases = new float[phaseChannels * 2];
                Embeddings = new float[RegularState.shape[1]];
            }

            if (mirror)
            {
                MirroredState.GetSlice(ref Embeddings, frameIndex);
                MirroredPhase.GetSlice(ref Angles, frameIndex);
            }
            else
            {
                RegularState.GetSlice(ref Embeddings, frameIndex);
                RegularPhase.GetSlice(ref Angles, frameIndex);
            }

            for (int i = 0; i < Angles.Length; i++)
            {
                Amplitudes[i] = 1f;
                Phases[2 * i] = Mathf.Cos(-2 * Mathf.PI * Angles[i]);
                Phases[2 * i + 1] = Mathf.Sin(-2 * Mathf.PI * Angles[i]);
            }
            
            var vqVisualizer = GetVQVisualizer(Tag);
            if (vqVisualizer != null)
            {
                var embedIndex = mirror ? MirroredEmbedIndex : RegularEmbedIndex;
                vqVisualizer.AddHighlight(embedIndex.GetSlice(frameIndex));
                vqVisualizer.AddExtraEmbedding(mirror ? MirroredStateOri.GetSlice(frameIndex) : RegularStateOri.GetSlice(frameIndex));
                vqVisualizer.Phase = Angles[0];
            }
            
            var primalAngle = GetPrimalAngle(Embeddings);
            PhaseVisualization.DrawPhaseState(center, radius, Amplitudes, Phases, Angles.Length, 1f, primalAngle: primalAngle);
            
        }

        protected override void DerivedInspector(MotionEditor editor)
        {
            
        }
        
        public override TimeSeries.Component DerivedExtractSeries(TimeSeries global, float timestamp, bool mirrored, params object[] parameters) {
            return null;
        }
        
        private int GetManifold(int index, bool mirrored, Span<float> buffer)
        {
            var manifolds = mirrored ? MirroredPhaseManifold : RegularPhaseManifold;
            return manifolds.GetSlice(buffer, index);
        }

        public int GetEmbedIndex(int index, bool mirrored, Span<float> buffer)
        {
            var idxArray = mirrored ? MirroredEmbedIndex : RegularEmbedIndex;
            buffer[0] = idxArray[index, 0];
            return 1;
        }

        private float[] GetManifold(int index, bool mirrored)
        {
            var manifolds = mirrored ? MirroredPhaseManifold : RegularPhaseManifold;
            float[] buffer = null;
            manifolds.GetSlice(ref buffer, index);
            return buffer;
        }

        int GetFrameIndex(float timestamp)
        {
            float start = Asset.Frames.First().Timestamp;
            float end = Asset.Frames.Last().Timestamp;
            if(timestamp < start || timestamp > end) {
                float boundary = Mathf.Clamp(timestamp, start, end);
                float pivot = 2f*boundary - timestamp;
                timestamp = Mathf.Repeat(pivot-start, end-start) + start;
            }
            return Asset.GetFrame(timestamp).Index - 1;
        }

        public float[] GetManifold(float timestamp, bool mirrored)
        {
            return GetManifold(GetFrameIndex(timestamp), mirrored);
        }

        public int GetManifold(float timestamp, bool mirrored, Span<float> buffer)
        {
            return GetManifold(GetFrameIndex(timestamp), mirrored, buffer);
        }
        
        private float[] GetManifoldOri(int index, bool mirrored)
        {
            var manifolds = mirrored ? MirroredPhaseManifoldOri : RegularPhaseManifoldOri;
            return manifolds.GetSlice(index);
        }

        private int GetManifoldOri(int index, bool mirrored, Span<float> buffer)
        {
            var manifolds = mirrored ? MirroredPhaseManifoldOri : RegularPhaseManifoldOri;
            return manifolds.GetSlice(buffer, index);
        }

        public float[] GetManifoldOri(float timestamp, bool mirrored)
        {
            return GetManifoldOri(GetFrameIndex(timestamp), mirrored);
        }

        public int GetManifoldOri(float timestamp, bool mirrored, Span<float> buffer)
        {
            return GetManifoldOri(GetFrameIndex(timestamp), mirrored, buffer);
        }

        public int GetState(int index, bool mirrored, Span<float> buffer)
        {
            var state = mirrored ? MirroredState : RegularState;
            return state.GetSlice(buffer, index);
        }

        public int GetState(float timestamp, bool mirrored, Span<float> buffer)
        {
            return GetState(GetFrameIndex(timestamp), mirrored, buffer);
        }
        
        public int GetPhase(int index, bool mirrored, Span<float> buffer)
        {
            var phase = mirrored ? MirroredPhase : RegularPhase;
            return phase.GetSlice(buffer, index);
        }
        
        public int GetPhase(float timestamp, bool mirrored, Span<float> buffer)
        {
            return GetPhase(GetFrameIndex(timestamp), mirrored, buffer);
        }

        public int GetEmbedIndex(float timestamp, bool mirrored, Span<float> buffer)
        {
            return GetEmbedIndex(GetFrameIndex(timestamp), mirrored, buffer);
        }

        // public void SetManifold(int index, bool mirrored, float[] buffer)
        // {
        //     var manifolds = mirrored ? MirroredPhaseManifold : RegularPhaseManifold;
        //     for (int j = 0; j < manifolds.shape[1]; j++)
        //     {
        //         manifolds[index, j] = buffer[j];
        //     }
        // }
    }
}