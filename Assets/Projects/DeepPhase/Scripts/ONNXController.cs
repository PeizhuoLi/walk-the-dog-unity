using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using DeepPhase;
using SerializedNDArray;
using Unity.Sentis;
using NumSharp;
using Inertialization;


namespace AI4Animation
{
    // [ExecuteInEditMode]
    public class ONNXController : MonoBehaviour
    {
        
        public bool Active = false;
        public ONNXNetwork NeuralNetwork;
        [SerializeField, HideInInspector] private int _WindowSize = 0;

        public int WindowSize
        {
            get => _WindowSize;
            set {
                if (value != _WindowSize)
                {
                    _WindowSize = value;
                    ResetInputShape();
                }
            }
        }

        public int ShowFuture = 0;
        [Range(-2f, 2f)] public float FutureDeltaX = 0.1f; 
        [Range(0f, 1f)] public float FutureDeltaPhase = 0.02f;
        public MotionEditor Editor;
        public MotionEditor Editor2;
        public MotionEditor BaseEditor;
        [Range(-2f, 2f)] public float translate_x = 0;
        public Color BoneColor = UltiDraw.Cyan;
        public GameObject actorPrefab = null;
        public string targetPhaseTag = null;
        public bool UseInertialization = false;
        [Range(0f, 10f)] public float InertializationHalfLife = 1f;
        public bool Debugging = false;
        public bool UpdateRotationOnly = false;
        public bool NetworkPredictsPosition = true;
        public bool NetworkPredictsVelocity = true;
        public bool NetworkPredictsRotation = true;
        public bool EulerIntegrate = true;
        public bool VisualizePhase = false;
        public NearestNeighbors NNModule = null;
        private float[] AmplitudeAmp = null;
        public UpdateModeEnum UpdateMode = UpdateModeEnum.CopyPastFromEditor;
        public bool Unrolling = false;
        public bool Add180Degree = false;
        private DeepPhaseStatus CurrentDeepPhaseStatus = new DeepPhaseStatus();
        private float LastSyncTime = -10f;
        private string SyncMethod = string.Empty;
        private float[] NoiseForVae = null;
        [NonSerialized] public bool Running = false;
        [Range(0f, 2f)] private float SyncDisplayTime = 1f;
        [NonSerialized] private Inertialization.Inertialization Inertializer;
        [NonSerialized] private int LastEmbedIndex, NewEmbedIndex;
        [NonSerialized] public float LastInertializationStartTime = 0f;
        [NonSerialized] private PoseVector LastPose;
        
        [NonSerialized] private VQPhaseController VQController = null;
        [NonSerialized] private float[] InputBuffer = null;
        [NonSerialized] private float[] InputBuffer2 = null;

        public bool _UseWindowedInference = false;
        [NonSerialized] private Dictionary<string, SNDArray<float>> OutputBuffer = null;
        [NonSerialized] private WindowInformation CurrentWindow;
        [NonSerialized] private Actor[] FutureActors;
        public bool UseWindowedInference
        {
            get => _UseWindowedInference;
            set
            {
                if (value != _UseWindowedInference)
                {
                    _UseWindowedInference = value;
                    CreateWindowedInference();
                }
            }
        }
        
        class WindowInformation
        {
            public MotionAsset Asset;
            public int Start, End;
        }
        

        public FeatureExtractor.CHANNEL_TYPE InputChannelType = FeatureExtractor.CHANNEL_TYPE.PhaseManifold;

        void CreateWindowedInference()
        {
            OutputBuffer = new Dictionary<string, SNDArray<float>>();
            CurrentWindow = null;
        }

        void ResetInputShape()
        {
            if (!NeuralNetwork.IsSessionActive()) return;
            var featureSize = GetFeatureSize();
            NeuralNetwork.GetSession().ResetXShape(new TensorShape(1, featureSize, WindowSize), "input");
            InputBuffer = new float[featureSize * WindowSize];
            InputBuffer2 = new float[featureSize * WindowSize];
        }

        ONNXNetwork.ONNXInference GetInferenceSession()
        {
            return Running ? NeuralNetwork.GetSession() as ONNXNetwork.ONNXInference : null;
        }
        
        void ResetNoise()
        {
            NoiseForVae = np.random.randn(GetInferenceSession().GetFeedSize("noise")).astype(np.float32).ToArray<float>();
            CurrentWindow = null; // Force the network to run.
        }
        void SetZeroNoise()
        {
            NoiseForVae = np.zeros(GetInferenceSession().GetFeedSize("noise")).astype(np.float32).ToArray<float>();
            CurrentWindow = null; // Force the network to run.
        }

        public void OnEnable()
        {
            var featureSize = GetFeatureSize();
            NeuralNetwork.CloseSession();
            LastEmbedIndex = -1;
            Running = true;
            if (WindowSize == 0)
            {
                NeuralNetwork.CreateSession();
                InputBuffer = new float[featureSize];
                InputBuffer2 = new float[featureSize];
            }
            else
            {
                NeuralNetwork.CreateSession();
                ResetInputShape();
                if (GetInferenceSession().RequiresNoise) ResetNoise();
            }
            
            if (UpdateMode == UpdateModeEnum.AutoRegressiveUnroll)
                CurrentDeepPhaseStatus.SetFromEditor(GetEditor(), targetPhaseTag);

            if (UseInertialization)
            {
                Inertializer = new Inertialization.Inertialization(GetActor().NumValidBones);
            }

            if (ShowFuture > 0)
            {
                FutureActors = new Actor[ShowFuture];
                for (int i = 0; i < ShowFuture; i++)
                {
                    FutureActors[i] = CreateActor(false);
                }
            }
        }
        
        private VQPhaseController GetVQPhaseContorller()
        {
            if (VQController == null)
                VQController = GameObjectExtensions.Find<VQPhaseController>(true);
            return VQController;
        }

        public class DeepPhaseStatus
        {
            public float[] phaseManifold;
            public float[] frequencies;

            public void Update(float dt)
            {
                for (int i = 0; i < frequencies.Length; i++)
                {
                    float dphi = -frequencies[i] * dt * 2 * Mathf.PI;
                    float dx = Mathf.Cos(dphi), dy = Mathf.Sin(dphi);
                    float x = phaseManifold[i * 2], y = phaseManifold[i * 2 + 1];
                    phaseManifold[i * 2] = x * dx - y * dy;
                    phaseManifold[i * 2 + 1] = x * dy + y * dx;
                }
            }

            public void SetFromEditor(MotionEditor editor, string tag)
            {
                FeatureExtractor.Get(editor, FeatureExtractor.CHANNEL_TYPE.PhaseManifold, tag,
                    ref phaseManifold);
                FeatureExtractor.Get(editor, FeatureExtractor.CHANNEL_TYPE.Frequencies, tag,
                    ref frequencies);
            }

            public void SetFromNN(MotionEditor editor, NearestNeighbors nnModule, string tag)
            {
                var nearestNeighbors = nnModule.QueryNNFromEditor(editor, 1);
                int i = 0;
                foreach (NearestNeighbors.FrameIndex nn in nearestNeighbors)
                {
                    i++;
                    // if (i == 1) continue;
                    var asset = MotionAsset.Retrieve(nn.assetIndex);
                    FeatureExtractor.Get(asset, nn.mirror, FeatureExtractor.CHANNEL_TYPE.PhaseManifold, tag,
                        nn.frameIndex, null, ref phaseManifold);
                    FeatureExtractor.Get(asset, nn.mirror, FeatureExtractor.CHANNEL_TYPE.Frequencies, tag,
                        nn.frameIndex, null, ref frequencies);
                    break;
                }
            }
        }

        // public enum BlendingModeEnum
        // {
        //     NoBlending,
        //     Average,
        //     AmplitudeSubstitution
        // }

        public enum UpdateModeEnum
        {
            CopyPastFromEditor,
            AutoRegressiveUnroll,
            FromVQController,
            VQMixing,
            DeepPhaseMixing,
        }
        
        [SerializeField]
        private Actor Actor;
        private FeatureExtractor AFeatureExtractor = new FeatureExtractor();

        bool RequiresNNEvaluation()
        {
            var currentFrame = GetEditor().GetCurrentFrameIndex();
            if (!UseWindowedInference) return true;
            if (CurrentWindow != null && CurrentWindow.Asset == GetEditor().GetSession().Asset && 
                CurrentWindow.Start <= currentFrame && currentFrame < CurrentWindow.End)
                return false;
            return true;
        }
        
        public MotionEditor GetEditor() {
            if(Editor == null) {
                Editor = GameObjectExtensions.Find<MotionEditor>(true);
            }
            return Editor;
        }
        
        public float[] GetAmplitudeAmp()
        {
            if (AmplitudeAmp == null || AmplitudeAmp.Length != GetPhaseNumChannel())
            {
                AmplitudeAmp = new float[GetPhaseNumChannel()];
                for (int i = 0; i < AmplitudeAmp.Length; i++) AmplitudeAmp[i] = 1f;
            }

            return AmplitudeAmp;
        }

        public void ResetActor()
        {
            var exampleActor = Editor.Session.GetActor();
            for (int i = 0; i < Actor.Bones.Length; i++)
            {
                Actor.Bones[i].SetTransformation(exampleActor.Bones[i].GetTransformation());
                Actor.Bones[i].SetVelocity(exampleActor.Bones[i].GetVelocity());
            }
        }
        
        private Actor CreateActor(bool useMesh = true)
        {
            Actor actor;
            if (actorPrefab != null && useMesh)
            {
                var prefab = Instantiate(actorPrefab, Vector3.zero, Quaternion.identity, transform);
                actor = prefab.AddComponent<Actor>();
                actor.Initialize();  // This is equivalent to actor.Reset(), which is only called in editor mode.
                var boneMapping = BaseEditor.GetSession().Asset.Source.GetBoneIndices(actor.GetBoneNames());
                var bones = new List<Transform>();

                for (int i = 0; i < boneMapping.Length; i++)
                {
                    if (boneMapping[i] != -1) bones.Add(actor.Bones[i].GetTransform());
                }
            
                actor.Create(bones.ToArray());
            }
            else
            {
                actor = Actor.CreateActor(BaseEditor.GetSession().Asset, transform, BaseEditor.GetSession().GetActor().GetBoneNames());
            }

            var exampleActor = BaseEditor.Session.GetActor();
            for (int i = 0; i < actor.Bones.Length; i++)
            {
                actor.Bones[i].SetTransformation(exampleActor.Bones[i].GetTransformation());
                actor.Bones[i].SetVelocity(exampleActor.Bones[i].GetVelocity());
            }
            return actor;
        }

        public Actor GetActor()
        {
            if (Actor == null) Actor = CreateActor();
            return Actor;
        }

        void OnDestroy() {
            //Close the session which disposes allocated memory.
            NeuralNetwork.CloseSession();
        }

        static SNDArray<float> SNDArrayFromTensor(TensorFloat tensor)
        {
            SNDArray<float> result = new SNDArray<float>
            {
                data = tensor.ToReadOnlyArray(),
                shape = tensor.shape.ToArray()
            };
            return result;
        }

        void UpdateActorFromOutputBuffer(Dictionary<string, SNDArray<float>> outputBuffer, Matrix4x4 rootTransform, 
            int pivot, Actor actor, Actor referenceActor)
        {
            Vector3[] positions = new Vector3[actor.Bones.Length];
            Vector3[] velocities = new Vector3[actor.Bones.Length];
            Matrix4x4[] rotations = new Matrix4x4[actor.Bones.Length];

            if (NetworkPredictsPosition)
            {
                int pt = 0;
                var output = outputBuffer["Positions"];
                
                for (int i = 0; i < actor.Bones.Length; i++)
                {
                    positions[i].x = output[pt++, pivot];
                    positions[i].y = output[pt++, pivot];
                    positions[i].z = output[pt++, pivot];
                }
            }

            if (NetworkPredictsVelocity)
            {
                int pt = 0;
                var output = outputBuffer["Velocities"];
                for (int i = 0; i < actor.Bones.Length; i++)
                {
                    velocities[i].x = output[pt++, pivot];
                    velocities[i].y = output[pt++, pivot];
                    velocities[i].z = output[pt++, pivot];
                }
            }

            if (NetworkPredictsRotation)
            {
                int pt = 0;
                var output = outputBuffer["Rotations"];
                for (int i = 0; i < actor.Bones.Length; i++)
                {
                    rotations[i].m00 = output[pt++, pivot];
                    rotations[i].m01 = output[pt++, pivot];
                    rotations[i].m02 = output[pt++, pivot];
                    rotations[i].m03 = 0;
                    rotations[i].m10 = output[pt++, pivot];
                    rotations[i].m11 = output[pt++, pivot];
                    rotations[i].m12 = output[pt++, pivot];
                    rotations[i].m13 = 0;
                    rotations[i].m20 = output[pt++, pivot];
                    rotations[i].m21 = output[pt++, pivot];
                    rotations[i].m22 = output[pt++, pivot];
                    rotations[i].m23 = 0;
                    rotations[i].m30 = 0;
                    rotations[i].m31 = 0;
                    rotations[i].m32 = 0;
                    rotations[i].m33 = 1;
                }
            }
            
            // Replace the network's output with ground-truth.
            float[] buffer = new float[400];
            var mirror = GetEditor().Mirror;

            if (referenceActor != null && (Debugging || !NetworkPredictsPosition))
            {
                int pt = 0;
                AFeatureExtractor.Get(mirror, FeatureExtractor.CHANNEL_TYPE.Positions,
                    targetPhaseTag, GetEditor().GetCurrentFrame(), referenceActor, ref buffer);
                for (int i = 0; i < actor.Bones.Length; i++)
                {
                    positions[i].x = buffer[pt++];
                    positions[i].y = buffer[pt++];
                    positions[i].z = buffer[pt++];
                }
            }

            if (referenceActor != null && (Debugging || !NetworkPredictsVelocity))
            {
                int pt = 0;
                AFeatureExtractor.Get(mirror, FeatureExtractor.CHANNEL_TYPE.Velocities,
                    targetPhaseTag, GetEditor().GetCurrentFrame(), referenceActor, ref buffer);
                for (int i = 0; i < actor.Bones.Length; i++)
                {
                    velocities[i].x = buffer[pt++];
                    velocities[i].y = buffer[pt++];
                    velocities[i].z = buffer[pt++];
                }
            }

            if (referenceActor != null && (Debugging || !NetworkPredictsRotation))
            {
                int pt = 0;
                AFeatureExtractor.Get(mirror, FeatureExtractor.CHANNEL_TYPE.Rotations,
                    targetPhaseTag, GetEditor().GetCurrentFrame(), referenceActor, ref buffer);
                for (int i = 0; i < actor.Bones.Length; i++)
                {
                    rotations[i].m00 = buffer[pt++];
                    rotations[i].m01 = buffer[pt++];
                    rotations[i].m02 = buffer[pt++];
                    rotations[i].m03 = 0;
                    rotations[i].m10 = buffer[pt++];
                    rotations[i].m11 = buffer[pt++];
                    rotations[i].m12 = buffer[pt++];
                    rotations[i].m13 = 0;
                    rotations[i].m20 = buffer[pt++];
                    rotations[i].m21 = buffer[pt++];
                    rotations[i].m22 = buffer[pt++];
                    rotations[i].m23 = 0;
                    rotations[i].m30 = 0;
                    rotations[i].m31 = 0;
                    rotations[i].m32 = 0;
                    rotations[i].m33 = 1;
                }
            }
            
            
            Quaternion[] rot_quat = new Quaternion[rotations.Length];
            for (int i = 0; i < rotations.Length; i++)
            {
                rot_quat[i] = rotations[i].GetRotation();
            }

            var angularVelocity = new Vector3[positions.Length];

            if (LastPose.JointLocalPositions != null)
            {
                // for (int i = 0; i < positions.Length; i++)
                // {
                //     newPose.JointLocalVelocities[i] *= VelocityScalingFactor;
                // }

                for (int i = 0; i < positions.Length; i++)
                {
                    // angularVelocity[i] =
                        // MathExtensions.AngularVelocity(LastPose.JointLocalRotations[i], rot_quat[i], Time.deltaTime);
                }
            }
            
            var newPose = new PoseVector(positions, rot_quat, velocities, angularVelocity, false, false);

                
            if (UseInertialization)
            {
                if (LastEmbedIndex != -1 && NewEmbedIndex != LastEmbedIndex)
                {
                    Inertializer.PoseTransition(LastPose, newPose);
                    LastInertializationStartTime = Time.time;
                }
                Inertializer.Update(newPose, InertializationHalfLife, Time.deltaTime);
                for (int i = 0; i < rotations.Length; i++)
                {
                    velocities[i] = Inertializer.InertializedVelocities[i];
                    positions[i] = Inertializer.InertializedPositions[i];
                    rot_quat[i] = Inertializer.InertializedRotations[i];
                }
            }

            LastPose = newPose;
            LastEmbedIndex = NewEmbedIndex;
            UpdateActorWithPose(rootTransform, velocities, positions, rot_quat);
        }

        void UpdateActorWithPose(Matrix4x4 rootTransform, Vector3[] velocities, Vector3[] positions, Quaternion[] rotations)
        {
            var actor = GetActor();
            float t = EulerIntegrate ? 0.5f : 0f;
            Vector3[] oldPositions = actor.GetBonePositions();
            Matrix4x4 previousRootTransform = actor.GetRoot().transform.GetWorldMatrix();
            actor.GetRoot().SetPositionAndRotation(rootTransform.GetPosition(), rootTransform.GetRotation());
            for (int i = 0; i < actor.Bones.Length; i++)
            {
                var oldLocalPosition = oldPositions[i].PositionTo(previousRootTransform);
                var newLocalPosition = oldLocalPosition + velocities[i] * GetEditor().GetSession().Asset.GetDeltaTime();

                Vector3 position = Vector3.Lerp(positions[i].PositionFrom(rootTransform),
                    newLocalPosition.PositionFrom(rootTransform), t);
                if (!UpdateRotationOnly || i == 0) actor.Bones[i].SetPosition(position);
                actor.Bones[i].SetRotation(rotations[i].RotationFrom(rootTransform.GetRotation()));
                actor.Bones[i].SetVelocity(velocities[i]);
            }
            
            actor.GetRoot().position += Vector3.right * translate_x;
            actor.BoneColor = BoneColor;
        }
        
        void EvaluateNeuralNetwork(Dictionary<string, float[]> inputBuffer, WindowInformation window)
        {
            // Feed inputs
            foreach (var key in inputBuffer.Keys)
            {
                var input = inputBuffer[key];
                if (input.Length != NeuralNetwork.GetSession().GetFeedSize(key))
                {
                    throw new Exception($"Input size mismatch for key: \"{key}\".");
                }

                foreach (var val in input)
                    NeuralNetwork.Feed(val, key);
            }
            
            // Run the inference.
            NeuralNetwork.RunSession();

            OutputBuffer = new Dictionary<string, SNDArray<float>>();
            
            SNDArray<float> prepareOutput(TensorFloat tensor)
            {
                var output = SNDArrayFromTensor(tensor);
                output.Squeeze();
                if (WindowSize == 0) output.UnsqueezeLast();
                return output;
            }
            
            // Store the output into the output buffer.
            foreach (var key in GetInferenceSession().OutputNames)
            {
                OutputBuffer[key] = prepareOutput(NeuralNetwork.GetOutput(key));
            }

            CurrentWindow = window;
        }

        void UpdateActorWithPhaseManifoldPerFrameEvaluation(ReadOnlySpan<float> input, Matrix4x4 rootTransform, Actor actor, Actor referenceActor=null,
            float extraTranslateX=0f)
        {
            var mirror = GetEditor().Mirror;
            //Give your inputs to the network. You can directly feed your inputs to the network without allocating the inputs array,
            //which is faster. If not enough or too many inputs are given to the network, it will throw warnings.
            if (input.Length != NeuralNetwork.GetSession().GetFeedSize())
            {
                throw new Exception("Input size mismatch");
            }

            for (int i = 0; i < NeuralNetwork.GetSession().GetFeedSize(); i++)
            {
                NeuralNetwork.Feed(input[i], "input");
            }

            if (GetInferenceSession().RequiresNoise)
                foreach (var v in NoiseForVae) NeuralNetwork.Feed(v, "noise");

            //Run the inference.
            NeuralNetwork.RunSession();
            
            // Debug.Log("Computing inference took " + NeuralNetwork.GetSession().Time + "s.");

            //Read your outputs from the network. You can directly read all outputs from the network without allocating the outputs array,
            //which is faster. If not enough or too many outputs are read from the network, it will throw warnings.
            // float[] output;

            Vector3[] positions = new Vector3[actor.Bones.Length];
            Vector3[] velocities = new Vector3[actor.Bones.Length];
            Matrix4x4[] rotations = new Matrix4x4[actor.Bones.Length];
            int pivot = WindowSize / 2;

            SNDArray<float> prepareOutput(TensorFloat tensor)
            {
                var output = SNDArrayFromTensor(tensor);
                output.Squeeze();
                if (WindowSize == 0) output.UnsqueezeLast();
                return output;
            }

            if (NetworkPredictsPosition)
            {
                int pt = 0;
                var output = prepareOutput(NeuralNetwork.GetOutput("Positions"));
                
                for (int i = 0; i < actor.Bones.Length; i++)
                {
                    positions[i].x = output[pt++, pivot];
                    positions[i].y = output[pt++, pivot];
                    positions[i].z = output[pt++, pivot];
                }
            }

            if (NetworkPredictsVelocity)
            {
                int pt = 0;
                var output = prepareOutput(NeuralNetwork.GetOutput("Velocities"));
                for (int i = 0; i < actor.Bones.Length; i++)
                {
                    velocities[i].x = output[pt++, pivot];
                    velocities[i].y = output[pt++, pivot];
                    velocities[i].z = output[pt++, pivot];
                }
            }

            if (NetworkPredictsRotation)
            {
                int pt = 0;
                var output = prepareOutput(NeuralNetwork.GetOutput("Rotations"));
                for (int i = 0; i < actor.Bones.Length; i++)
                {
                    rotations[i].m00 = output[pt++, pivot];
                    rotations[i].m01 = output[pt++, pivot];
                    rotations[i].m02 = output[pt++, pivot];
                    rotations[i].m03 = 0;
                    rotations[i].m10 = output[pt++, pivot];
                    rotations[i].m11 = output[pt++, pivot];
                    rotations[i].m12 = output[pt++, pivot];
                    rotations[i].m13 = 0;
                    rotations[i].m20 = output[pt++, pivot];
                    rotations[i].m21 = output[pt++, pivot];
                    rotations[i].m22 = output[pt++, pivot];
                    rotations[i].m23 = 0;
                    rotations[i].m30 = 0;
                    rotations[i].m31 = 0;
                    rotations[i].m32 = 0;
                    rotations[i].m33 = 1;
                }
            }
            
            // Replace the network's output with ground-truth.
            float[] buffer = new float[400];
            if (referenceActor != null && (Debugging || !NetworkPredictsPosition))
            {
                int pt = 0;
                AFeatureExtractor.Get(mirror, FeatureExtractor.CHANNEL_TYPE.Positions,
                    targetPhaseTag, GetEditor().GetCurrentFrame(), referenceActor, ref buffer);
                for (int i = 0; i < actor.Bones.Length; i++)
                {
                    positions[i].x = buffer[pt++];
                    positions[i].y = buffer[pt++];
                    positions[i].z = buffer[pt++];
                }
            }

            if (referenceActor != null && (Debugging || !NetworkPredictsVelocity))
            {
                int pt = 0;
                AFeatureExtractor.Get(mirror, FeatureExtractor.CHANNEL_TYPE.Velocities,
                    targetPhaseTag, GetEditor().GetCurrentFrame(), referenceActor, ref buffer);
                for (int i = 0; i < actor.Bones.Length; i++)
                {
                    velocities[i].x = buffer[pt++];
                    velocities[i].y = buffer[pt++];
                    velocities[i].z = buffer[pt++];
                }
            }

            if (referenceActor != null && (Debugging || !NetworkPredictsRotation))
            {
                int pt = 0;
                AFeatureExtractor.Get(mirror, FeatureExtractor.CHANNEL_TYPE.Rotations,
                    targetPhaseTag, GetEditor().GetCurrentFrame(), referenceActor, ref buffer);
                for (int i = 0; i < actor.Bones.Length; i++)
                {
                    rotations[i].m00 = buffer[pt++];
                    rotations[i].m01 = buffer[pt++];
                    rotations[i].m02 = buffer[pt++];
                    rotations[i].m03 = 0;
                    rotations[i].m10 = buffer[pt++];
                    rotations[i].m11 = buffer[pt++];
                    rotations[i].m12 = buffer[pt++];
                    rotations[i].m13 = 0;
                    rotations[i].m20 = buffer[pt++];
                    rotations[i].m21 = buffer[pt++];
                    rotations[i].m22 = buffer[pt++];
                    rotations[i].m23 = 0;
                    rotations[i].m30 = 0;
                    rotations[i].m31 = 0;
                    rotations[i].m32 = 0;
                    rotations[i].m33 = 1;
                }
            }
            
            float t = EulerIntegrate ? 0.5f : 0f;
            Vector3[] oldPositions = actor.GetBonePositions();
            Matrix4x4 previousRootTransform = actor.GetRoot().transform.GetWorldMatrix();
            actor.GetRoot().SetPositionAndRotation(rootTransform.GetPosition(), rootTransform.GetRotation());
            for (int i = 0; i < actor.Bones.Length; i++)
            {
                var oldLocalPosition = oldPositions[i].PositionTo(previousRootTransform);
                var newLocalPosition = oldLocalPosition + velocities[i] * GetEditor().GetSession().Asset.GetDeltaTime();

                Vector3 position = Vector3.Lerp(positions[i].PositionFrom(rootTransform),
                    newLocalPosition.PositionFrom(rootTransform), t);
                if (!UpdateRotationOnly || i == 0)
                    actor.Bones[i].SetPosition(position);
                actor.Bones[i].SetRotation(rotations[i].GetRotation().RotationFrom(rootTransform.GetRotation()));
                actor.Bones[i].SetVelocity(velocities[i]);
            }
            
            actor.GetRoot().position += Vector3.right * (translate_x + extraTranslateX);
            actor.BoneColor = BoneColor;
        }

        void OnGUI()
        {
            if (Time.time - LastSyncTime < SyncDisplayTime)
            {
                UltiDraw.GUIRect PhaseWindow = new UltiDraw.GUIRect(0.25f, 0.15f, 0.125f, 0.05f);
                float textScale = 0.0225f;
                var color = Color.white;
                color.a = 1 - (Time.time - LastSyncTime) / SyncDisplayTime;
                UltiDraw.Begin();
                UltiDraw.OnGUILabel(PhaseWindow.GetCenter(), PhaseWindow.GetSize(), textScale, SyncMethod, color);
                UltiDraw.End();
            }
        }

        void OnRenderObject()
        {
            if (VisualizePhase) 
            {
                var channels = GetPhaseNumChannel();
                var center = new Vector2(0.15f, 0.5f);
                var amplitudes = new float[channels];
                var input = CurrentDeepPhaseStatus.phaseManifold;
                for (int i = 0; i < amplitudes.Length; i++)
                {
                    amplitudes[i] = Mathf.Sqrt(input[i * 2] * input[i * 2] + input[i * 2 + 1] * input[i * 2 + 1]);
                }
                PhaseVisualization.DrawPhaseState(center, 0.04f, amplitudes, input, channels, 1f);
            }
        }

        int GetFeatureSize()
        {
            return FeatureExtractor.GetSize(GetEditor().GetSession().Asset, GetEditor().GetSession().GetActor(),
                InputChannelType, targetPhaseTag);
        }

        void CopyPasteFromEditor()
        {
            var mirror = GetEditor().Mirror;
            var referenceActor = GetEditor().GetSession().GetActor();
            AFeatureExtractor.Asset = GetEditor().Session.Asset;
            var newRoot = GetEditor().GetSession().GetActor().transform.GetWorldMatrix();
            int currentFrameIdx = Editor.GetCurrentFrameIndex();
            WindowInformation newWindow = null;

            if (RequiresNNEvaluation())
            {
                int frameStart, frameEnd;
                if (WindowSize == 0)
                {
                    frameStart = currentFrameIdx;
                    frameEnd = currentFrameIdx + 1;
                }
                else
                {
                    frameStart = currentFrameIdx - WindowSize / 2;
                    frameEnd = currentFrameIdx + (WindowSize + 1) / 2;
                    newWindow = new WindowInformation
                    {
                        Asset = GetEditor().GetSession().Asset,
                        Start = frameStart,
                        End = frameEnd
                    };
                }

                var lengths = FeatureExtractor.GetWindow(GetEditor().Session.Asset, mirror, InputChannelType,
                    targetPhaseTag, frameStart, frameEnd, GetActor(), InputBuffer);

                var newIndex = new float[1];
                FeatureExtractor.GetInplace(GetEditor().Session.Asset, mirror, FeatureExtractor.CHANNEL_TYPE.EmbedIndex,
                    targetPhaseTag, frameStart, GetActor(), newIndex);
                NewEmbedIndex = (int)newIndex[0];

                float[] input;

                if (WindowSize > 0)
                {
                    // Transpose the input buffer because convolution requires the temporal axis to be the last axis.
                    int windowSize = frameEnd - frameStart;
                    int numChannels = lengths / windowSize;

                    ArrayExtensions.Transpose2D(InputBuffer, InputBuffer2, windowSize, numChannels);

                    input = InputBuffer2;
                }
                else
                    input = InputBuffer;
                
                var inputDict = new Dictionary<string, float[]>();
                inputDict["input"] = input;
                if (GetInferenceSession().RequiresNoise) inputDict["noise"] = NoiseForVae;
                EvaluateNeuralNetwork(inputDict, newWindow);
            }

            var offset = UseWindowedInference ? currentFrameIdx - CurrentWindow.Start : 0; 
            UpdateActorFromOutputBuffer(OutputBuffer, newRoot, offset, Actor, referenceActor);
        }
        
        void CopyPasteFromEditorOld()
        {
            var mirror = GetEditor().Mirror;
            var referenceActor = GetEditor().GetSession().GetActor();
            AFeatureExtractor.Asset = GetEditor().Session.Asset;

            int pivot = Editor.GetCurrentFrameIndex();
            int frameStart, frameEnd;
            if (WindowSize == 0)
            {
                frameStart = pivot;
                frameEnd = pivot + 1;
            }
            else
            {
                frameStart = pivot - WindowSize / 2;
                frameEnd = pivot + (WindowSize + 1) / 2;
            }

            var lengths = FeatureExtractor.GetWindow(GetEditor().Session.Asset, mirror, InputChannelType,
                targetPhaseTag, frameStart, frameEnd, GetActor(), InputBuffer);
            
            if (InputChannelType is FeatureExtractor.CHANNEL_TYPE.Frequencies or FeatureExtractor.CHANNEL_TYPE.PhaseManifold)
                CurrentDeepPhaseStatus.SetFromEditor(GetEditor(), targetPhaseTag);

            float[] input;

            if (WindowSize > 0)
            {
                // Transpose the input buffer because convolution requires the temporal axis to be the last axis.
                int windowSize = frameEnd - frameStart;
                int numChannels = lengths / windowSize;

                ArrayExtensions.Transpose2D(InputBuffer, InputBuffer2, windowSize, numChannels);

                input = InputBuffer2;
            }
            else
                input = InputBuffer;
            
            var newRoot = GetEditor().GetSession().GetActor().transform.GetWorldMatrix();
            UpdateActorWithPhaseManifoldPerFrameEvaluation(input, newRoot, Actor, referenceActor);
        }

        void MixingVQPhase()
        {
            var mirror = GetEditor().Mirror;
            var referenceActor = GetEditor().GetSession().GetActor();
            AFeatureExtractor.Asset = GetEditor().Session.Asset;
            //Give your inputs to the network. You can directly feed your inputs to the network without allocating the inputs array,
            //which is faster. If not enough or too many inputs are given to the network, it will throw warnings.
            float[] phase = new float[1];
            float[] amplitude = new float[20];
            var featureExtractor2 = new FeatureExtractor
            {
                Asset = Editor2 != null ? Editor2.Session.Asset : null
            };
        
            var length1 = FeatureExtractor.GetInplace(Editor.GetSession().Asset, Editor.Mirror, FeatureExtractor.CHANNEL_TYPE.Phase,
                targetPhaseTag, Editor.GetCurrentFrameIndex(), GetActor(), phase);
            var length2 = FeatureExtractor.GetInplace(Editor2.GetSession().Asset, Editor2.Mirror, FeatureExtractor.CHANNEL_TYPE.State,
                targetPhaseTag, Editor2.GetCurrentFrameIndex(), GetActor(), amplitude);

            var manifold = VQPhaseModule.CalculateManifold(amplitude, phase);
            
            
            
            var newRoot = GetEditor().GetSession().GetActor().transform.GetWorldMatrix();
            var inputDict = new Dictionary<string, float[]>();
            inputDict["input"] = manifold;
            EvaluateNeuralNetwork(inputDict, null);
            UpdateActorFromOutputBuffer(OutputBuffer, newRoot, 0, Actor, referenceActor);
        }
        
        void MixingDeepPhase()
        {
            var mirror = GetEditor().Mirror;
            var referenceActor = GetEditor().GetSession().GetActor();
            AFeatureExtractor.Asset = GetEditor().Session.Asset;
            //Give your inputs to the network. You can directly feed your inputs to the network without allocating the inputs array,
            //which is faster. If not enough or too many inputs are given to the network, it will throw warnings.
            float[] input = new float[NeuralNetwork.GetSession().GetFeedSize()];
            float[] input2 = new float[NeuralNetwork.GetSession().GetFeedSize()];
            var featureExtractor2 = new FeatureExtractor
            {
                Asset = Editor2 != null ? Editor2.Session.Asset : null
            };
        
            var lengths = AFeatureExtractor.Get(mirror, InputChannelType,
                targetPhaseTag, Editor.GetCurrentFrame(), GetActor(), ref input);
            var length2 = Editor2 != null
                ? featureExtractor2.Get(mirror, InputChannelType,
                    targetPhaseTag, Editor2.GetCurrentFrame(), GetActor(), ref input2)
                : -1;
        
            if (length2 == lengths)
            {
                // Apply the blending
                
                for (int i = 0; i < input.Length; i += 2)
                {
                    var A1 = Mathf.Sqrt(input[i] * input[i] + input[i + 1] * input[i + 1]);
                    var A2 = Mathf.Sqrt(input2[i] * input2[i] + input2[i + 1] * input2[i + 1]);
                    input[i] *= A2 / A1;
                }
            }
        
            var amplitudeAmp = GetAmplitudeAmp();
            for (int i = 0; i < input.Length; i++)
            {
                input[i] *= amplitudeAmp[i / 2];
            }
            
            var newRoot = GetEditor().GetSession().GetActor().transform.GetWorldMatrix();
            var inputDict = new Dictionary<string, float[]>();
            inputDict["input"] = input;
            EvaluateNeuralNetwork(inputDict, null);
            UpdateActorFromOutputBuffer(OutputBuffer, newRoot, 0, Actor, referenceActor);
        }

        void AutoRegressiveUnroll()
        {
            var deltaTime = Time.deltaTime;
            if (!Unrolling) deltaTime = 0f;
            var referenceActor = GetEditor().Session.GetActor();
            CurrentDeepPhaseStatus.Update(deltaTime);
            var newRoot = GetEditor().GetSession().GetActor().transform.GetWorldMatrix();
            UpdateActorWithPhaseManifoldPerFrameEvaluation(CurrentDeepPhaseStatus.phaseManifold, newRoot, GetActor(), referenceActor);
        }

        void UpdateFromVQController()
        {
            float deltaPhase = Add180Degree? 0.25f : 0f;
            float[] input;
            if (WindowSize == 0)
                input = GetVQPhaseContorller().GetPhaseManifold(deltaPhase);
            else
            {
                GetVQPhaseContorller().GetPhaseManifolds(GetEditor().GetSession().Asset.Framerate, WindowSize, InputBuffer);
                ArrayExtensions.Transpose2D(InputBuffer, InputBuffer2, WindowSize, GetFeatureSize());
                input = InputBuffer2;
            }
            
            var newRoot = GetEditor().GetSession().GetActor().transform.GetWorldMatrix();
            UpdateActorWithPhaseManifoldPerFrameEvaluation(input, newRoot, GetActor());

            if (ShowFuture > 0)
            {
                for (int i = 0; i < ShowFuture; i++)
                {
                    var futureActor = FutureActors[i];
                    var futurePhase = GetVQPhaseContorller().GetPhaseManifold(deltaPhase + (i + 1) * FutureDeltaPhase);
                    
                    UpdateActorWithPhaseManifoldPerFrameEvaluation(futurePhase, newRoot, futureActor, extraTranslateX:(i + 1) * FutureDeltaX);
                }
            }
        }

        void Update()
        {
            if (!Active) return;
            
            if (UpdateMode == UpdateModeEnum.CopyPastFromEditor) 
                CopyPasteFromEditor();
            else if (UpdateMode == UpdateModeEnum.AutoRegressiveUnroll)
                AutoRegressiveUnroll();
            else if (UpdateMode == UpdateModeEnum.FromVQController)
                UpdateFromVQController();
            else if (UpdateMode == UpdateModeEnum.VQMixing)
                MixingVQPhase();
            else if (UpdateMode == UpdateModeEnum.DeepPhaseMixing)
                MixingDeepPhase();
        }

        public int GetPhaseNumChannel()
        {
            return NeuralNetwork.GetSession().GetFeedSize() / 2;
        }

        #if UNITY_EDITOR
        [CustomEditor(typeof(ONNXController), true)]
        public class ONNXController_Editor : Editor 
        {
            public ONNXController Target;

            void Awake() {
                Target = (ONNXController)target;
            }

            public override void OnInspectorGUI() {
                Undo.RecordObject(Target, Target.name);

                DrawDefaultInspector();

                Target.WindowSize = EditorGUILayout.IntSlider("Window Size", Target.WindowSize, 0, 500);
                // Target.SetUpdateMode((UpdateModeEnum)EditorGUILayout.EnumPopup("UpdateMode", Target.UpdateMode));

                if (Target.AmplitudeAmp != null && Target.NeuralNetwork != null && Target.InputChannelType == FeatureExtractor.CHANNEL_TYPE.PhaseManifold)
                {
                    var amplitudeAmp = Target.GetAmplitudeAmp(); 
                    for (int i = 0; i < amplitudeAmp.Length; i++)
                    {
                        amplitudeAmp[i] = EditorGUILayout.Slider($"Amp{i}", amplitudeAmp[i], 0, 10);
                    }
                }
                if (Target.UpdateMode == UpdateModeEnum.AutoRegressiveUnroll &&
                    Utility.GUIButton("Reset Phase from Current Frame", UltiDraw.DarkGrey, UltiDraw.White))
                {
                    Target.CurrentDeepPhaseStatus.SetFromEditor(Target.GetEditor(), Target.targetPhaseTag);
                    Target.LastSyncTime = Time.time;
                    Target.SyncMethod = "Synced with Copy-Paste";
                }
                
                if (Target.UpdateMode == UpdateModeEnum.AutoRegressiveUnroll &&
                    Utility.GUIButton("Reset Phase from Current Frame (All ONNX)", UltiDraw.DarkGrey, UltiDraw.White))
                {
                    var targets = FindObjectsOfType<ONNXController>(false);
                    foreach (var target in targets)
                    {
                        target.CurrentDeepPhaseStatus.SetFromEditor(target.GetEditor(), target.targetPhaseTag);
                    }
                    Target.LastSyncTime = Time.time;
                    Target.SyncMethod = "Synced with Copy-Paste";
                }
                
                if (Target.UpdateMode == UpdateModeEnum.AutoRegressiveUnroll &&
                    Utility.GUIButton("Reset Phase Nearest Neighbor", UltiDraw.DarkGrey, UltiDraw.White))
                {
                    if (Target.NNModule != null)
                        Target.CurrentDeepPhaseStatus.SetFromNN(Target.Editor, Target.NNModule, Target.targetPhaseTag);
                    Target.LastSyncTime = Time.time;
                    Target.SyncMethod = $"Synced with Nearest Neighbor ({Target.NNModule.ChannelType})";
                }

                if (Target.Running && Target.GetInferenceSession().RequiresNoise)
                {
                    if (Utility.GUIButton("Reset Noise", UltiDraw.DarkGrey, UltiDraw.White))
                        Target.ResetNoise();
                    if (Utility.GUIButton("Set Zero Noise", UltiDraw.DarkGrey, UltiDraw.White))
                        Target.SetZeroNoise();
                }

                if(GUI.changed) {
                    EditorUtility.SetDirty(Target);
                }
            }
        }
        #endif
    }
}