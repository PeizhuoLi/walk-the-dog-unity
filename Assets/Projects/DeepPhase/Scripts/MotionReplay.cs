using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using NumSharp;
using Inertialization;
using Unity.Mathematics;


namespace AI4Animation
{
    // [ExecuteInEditMode]
    public class MotionReplay : MonoBehaviour
    {
        public bool SelfUpdate = false;
        public MotionEditor Editor;
        public MotionEditor BaseEditor;
        [Range(-2f, 8f)]public float TranslateX = 0;
        public Color BoneColor = UltiDraw.Cyan;
        public GameObject actorPrefab = null;
        public bool UseInertialization = false;
        [Range(0f, 1)] public float InertializationHalfLife = 0.2f;
        public bool UseGlobalForInertialization = true;
        public bool UpdateRotationOnly = false;
        public bool AlignRootTransform = true;
        public VQVisualizer PhaseVisualizer = null;
        public string TargetPhaseTag = null;
        public int Version;
        
        public string ReplayInfoFile = "";
        // public BlendingModeEnum BlendingMode = BlendingModeEnum.Average;
        [NonSerialized] private Inertialization.Inertialization Inertializer;
        [NonSerialized] private PoseVector LastPose;
        [NonSerialized] private StartingPointInfo[] StartingPoints;
        [NonSerialized] private Dictionary<string, MotionAsset> PreLoadedMotionAssets;
        [NonSerialized] private Frame CurrentFrame;
        [NonSerialized] private bool CurrentMirrored;
        [NonSerialized] private NDArray SegmentNumber;
        [NonSerialized] private NDArray SequenceNumber;
        [NonSerialized] private NDArray FrameNumber;
        [NonSerialized] private NDArray Mirrored;
        [NonSerialized] private float TimeStamp;

        private int NumFrames => FrameNumber is null ? 0 : FrameNumber.shape[0];
        private float Framerate;
        
        struct StartingPointInfo
        {
            public int sequenceNumber, startFrame;
            public bool mirrored;
            public int replayLength;
        }

        private int GetFrameNumberByTimestamp(float time)
        {
            return Mathf.Clamp(Mathf.RoundToInt(time * Framerate), 0, NumFrames - 1);
        }
        
        void ReadStartInfoOld(NDArray array)
        {
            // NDArray array = np.load(ReplayInfoFile);
            StartingPoints = new StartingPointInfo[array.shape[0]];
            for (int i = 0; i < array.shape[0]; i++)
            {
                StartingPoints[i].sequenceNumber = (int)array[i, 0];
                StartingPoints[i].startFrame = (int)array[i, 1];
                StartingPoints[i].mirrored = (int)array[i, 2] == 1;
                StartingPoints[i].replayLength = (int)array[i, 3];
            }
        }

        void ReadStartInfo()
        {
            var npzLoader = new NumSharpExtensions.NpzLoader(ReplayInfoFile);
            int version = npzLoader["Version"][0];
            Version = version;

            switch (version)
            {
                case 0:
                    ReadStartInfoOld(npzLoader["Data"]);
                    break;
                    // throw new NotImplementedException("Version 0 is not supported");
                case 1:
                case 2:
                    SegmentNumber = npzLoader["SegmentNumber"];
                    SequenceNumber = npzLoader["SequenceNumber"];
                    FrameNumber = npzLoader["FrameNumber"];
                    Mirrored = npzLoader["Mirrored"];
                    break;
                default:
                    throw new Exception("Unkown version");
            }
        }

        public void OnEnable()
        {
            ReadStartInfo();
            PreLoadedMotionAssets = new Dictionary<string, MotionAsset>();

            if (Version == 0)
            {
                for (int i = 0; i < StartingPoints.Length; i++)
                {
                    var info = StartingPoints[i];
                    var guid = BaseEditor.Assets[info.sequenceNumber];
                    if (!PreLoadedMotionAssets.ContainsKey(guid))
                    {
                        var asset = (MotionAsset)AssetDatabase.LoadMainAssetAtPath(Utility.GetAssetPath(guid));
                        PreLoadedMotionAssets[guid] = asset;
                    }
                }
            }
            else if (Version is 1 or 2)
            {
                for (int i = 0; i < SequenceNumber.shape[0]; i++)
                {
                    var guid = BaseEditor.Assets[(int)SequenceNumber[i]];
                    if (!PreLoadedMotionAssets.ContainsKey(guid))
                    {
                        var asset = (MotionAsset)AssetDatabase.LoadMainAssetAtPath(Utility.GetAssetPath(guid));
                        PreLoadedMotionAssets[guid] = asset;
                    }
                }
            }
            
            Framerate = PreLoadedMotionAssets.First().Value.Framerate;
            
            if (UseInertialization)
            {
                LastSegmentId = -1;
                Inertializer = new Inertialization.Inertialization(GetActor().NumValidBones);
            }

            LastPose.JointLocalVelocities = null;
        }

        [NonSerialized] private int LastSegmentId = -1;

        (Frame, bool, bool) GetReplayFrame(int frameIdx)
        {
            int cFrame = 0;
            for (int i = 0; i < StartingPoints.Length; i++)
            {
                if (frameIdx >= cFrame && frameIdx < cFrame + StartingPoints[i].replayLength)
                {
                    var info = StartingPoints[i];
                    var guid = BaseEditor.Assets[info.sequenceNumber];
                    var asset = PreLoadedMotionAssets[guid];
                    var finalFrameIndex = info.startFrame + frameIdx - cFrame;
                    finalFrameIndex = Math.Clamp(finalFrameIndex, 0, asset.Frames.Length - 1);
                    var transit = LastSegmentId != i;
                    LastSegmentId = i;
                    CurrentFrame = asset.Frames[finalFrameIndex];
                    CurrentMirrored = info.mirrored;
                    return (asset.Frames[finalFrameIndex], info.mirrored, transit);
                }
                cFrame += StartingPoints[i].replayLength;
            }
            return (null, false, false);
        }

        (Frame, bool, bool) GetReplayFrameV1(int frameIdx)
        {
            var sequenceNumber = (int)SequenceNumber[frameIdx];
            var guid = BaseEditor.Assets[sequenceNumber];
            var asset = PreLoadedMotionAssets[guid];
            var finalFrameIndex = (int)FrameNumber[frameIdx];
            int segmentNumber = SegmentNumber[frameIdx];
            var transit = segmentNumber != LastSegmentId;
            LastSegmentId = segmentNumber;
            var frame = asset.Frames[finalFrameIndex];
            var mirrored = Mirrored[frameIdx];
            CurrentMirrored = mirrored;
            CurrentFrame = frame;
            return (frame, mirrored, transit);
        }
        
        (Frame, Frame, float, bool, bool) GetReplayFrameV2(int frameIdx)
        {
            var sequenceNumber = (int)SequenceNumber[frameIdx];
            var guid = BaseEditor.Assets[sequenceNumber];
            var asset = PreLoadedMotionAssets[guid];
            var finalFrameIndex = (float)FrameNumber[frameIdx];
            var frameIdxLow = (int)Mathf.Floor(finalFrameIndex);
            var frameIdxHigh = frameIdxLow + 1;
            int segmentNumber = SegmentNumber[frameIdx];
            var transit = segmentNumber != LastSegmentId;
            LastSegmentId = segmentNumber;
            var frame0 = asset.Frames[frameIdxLow];
            frameIdxHigh = Math.Min(frameIdxHigh, asset.Frames.Length - 1);
            var frame1 = asset.Frames[frameIdxHigh];
            var mirrored = Mirrored[frameIdx];
            var t = finalFrameIndex - frameIdxLow;
            CurrentMirrored = mirrored;
            CurrentFrame = t < 0.5 ? frame0 : frame1;
            return (frame0, frame1, t, mirrored, transit);
        }
        
        [SerializeField]
        private Actor Actor;
        private FeatureExtractor AFeatureExtractor = new FeatureExtractor();
        
        public MotionEditor GetEditor() {
            if(Editor == null) {
                Editor = GameObjectExtensions.Find<MotionEditor>(true);
            }
            return Editor;
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

        void OnRenderObject()
        {
            if (PhaseVisualizer == null || PhaseVisualizer.TargetPhaseTag != TargetPhaseTag) return;
            var vqPhaseModule = CurrentFrame.Asset.GetModule<VQPhaseModule>(TargetPhaseTag);
            if (vqPhaseModule == null) return;
            Span<float> buffer = stackalloc float[1];
            vqPhaseModule.GetPhase(CurrentFrame.Timestamp, CurrentMirrored, buffer);
            PhaseVisualizer.Phase = buffer[0];
            vqPhaseModule.GetEmbedIndex(CurrentFrame.Timestamp, CurrentMirrored, buffer);
            PhaseVisualizer.AddHighlight((int)buffer[0]);
        }
        
        private Actor CreateActor()
        {
            Actor actor;
            if (actorPrefab != null)
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

        PoseVector CreatePoseVecFromFrame(Frame f, bool mirrored)
        {
            var (f0, f1) = f.GetConsecutiveFrames();
            var boneNames = GetActor().GetBoneNames();
            var pose = new PoseVector(boneNames.Length);
            
            var spaceCi = f.Asset.GetModule<RootModule>().GetRootTransformation(f.Timestamp, mirrored).inverse;
            var space0i = f0.Asset.GetModule<RootModule>().GetRootTransformation(f0.Timestamp, mirrored).inverse;
            var space1i = f1.Asset.GetModule<RootModule>().GetRootTransformation(f1.Timestamp, mirrored).inverse;

            for (int i = 0; i < boneNames.Length; i++)
            {
                var transformation = spaceCi * f.GetBoneTransformation(boneNames[i], mirrored);
                var t0 = space0i * f0.GetBoneTransformation(boneNames[i], mirrored);
                var t1 = space1i * f1.GetBoneTransformation(boneNames[i], mirrored);
                
                pose.JointLocalPositions[i] = transformation.GetPosition();
                pose.JointLocalRotations[i] = transformation.GetRotation();
                // pose.JointLocalVelocities[i] = (t1.GetPosition() - t0.GetPosition()) / (f1.Timestamp - f0.Timestamp);
                // pose.JointLocalAngularVelocities[i] = MathExtensions.AngularVelocity(t0.GetRotation(), t1.GetRotation(), f1.Timestamp - f0.Timestamp);
            }

            return pose;
        }
        
        PoseVector CreatePoseVecFromFrameLocal(Frame f, bool mirrored)
        {
            var (f0, f1) = f.GetConsecutiveFrames();
            var boneNames = GetActor().GetBoneNames();
            var pose = new PoseVector(boneNames.Length);
            
            var spaceCi = f.Asset.GetModule<RootModule>().GetRootTransformation(f.Timestamp, mirrored).inverse;
            var space0i = f0.Asset.GetModule<RootModule>().GetRootTransformation(f0.Timestamp, mirrored).inverse;
            var space1i = f1.Asset.GetModule<RootModule>().GetRootTransformation(f1.Timestamp, mirrored).inverse;

            for (int i = 0; i < boneNames.Length; i++)
            {
                Matrix4x4 transformation;
                Matrix4x4 t0, t1;
                if (i == 0)
                {
                    transformation = spaceCi * f.GetBoneTransformation(boneNames[i], mirrored);
                    t0 = space0i * f0.GetBoneTransformation(boneNames[i], mirrored);
                    t1 = space1i * f1.GetBoneTransformation(boneNames[i], mirrored);
                }
                else
                {
                    transformation = f.GetBoneTransformation(boneNames[i], mirrored, true);
                    t0 = f0.GetBoneTransformation(boneNames[i], mirrored, true);
                    t1 = f1.GetBoneTransformation(boneNames[i], mirrored, true);
                }
                pose.JointLocalPositions[i] = transformation.GetPosition();
                pose.JointLocalRotations[i] = transformation.GetRotation();
                pose.JointLocalVelocities[i] = (t1.GetPosition() - t0.GetPosition()) / (f1.Timestamp - f0.Timestamp);
                pose.JointLocalAngularVelocities[i] = MathExtensions.AngularVelocity(t0.GetRotation(), t1.GetRotation(), f1.Timestamp - f0.Timestamp);
            }

            return pose;
        }
        
        void UpdateWithInertialization(PoseVector newPose, bool transit)
        {
            var boneNames = GetActor().GetBoneNames();
            var actor = GetActor();
            var worldMat = GetEditor().GetSession().GetActor().GetRoot().GetWorldMatrix();
            worldMat.m03 += TranslateX;

            if (UseGlobalForInertialization)
            {

                if (transit && LastPose.JointLocalVelocities != null)
                {
                    Inertializer.PoseTransition(LastPose, newPose);
                    Debug.Log($"Transit at frame {GetEditor().GetCurrentFrameIndex()}");
                }
            
                Inertializer.Update(newPose, InertializationHalfLife, Time.deltaTime);
                LastPose = newPose;
                actor.transform.SetPositionAndRotation(worldMat.GetPosition(), worldMat.GetRotation());

                for (int i = 0; i < boneNames.Length; i++)
                {
                    Matrix4x4 mat = Matrix4x4.TRS(Inertializer.InertializedPositions[i], Inertializer.InertializedRotations[i], Vector3.one);
                    mat = worldMat * mat;

                    if (UpdateRotationOnly && i != 0)
                    {
                        actor.Bones[i].SetRotation(mat.GetRotation());
                    }
                    else
                    {
                        actor.SetBoneTransformation(mat, boneNames[i]);
                    }
                    actor.Bones[i].SetVelocity(Inertializer.InertializedVelocities[i]);
                }
            }
            else
            {
                throw new Exception("Not yet implemented");
                // newPose = CreatePoseVecFromFrameLocal(frame, mirrored);
                //
                // if (transit && LastPose.JointLocalVelocities != null)
                // {
                //     Inertializer.PoseTransition(LastPose, newPose);
                // }
                //
                // Inertializer.Update(newPose, InertializationHalfLife, Time.deltaTime);
                // LastPose = newPose;
                // actor.transform.SetPositionAndRotation(worldMat.GetPosition(), worldMat.GetRotation());
                //
                // if (UpdateRotationOnly)
                // {
                //     // Manually run FK on rotation.
                //     var globalRotations = new quaternion[boneNames.Length];
                //     globalRotations[0] = math.mul(worldMat.GetRotation(), Inertializer.InertializedRotations[0]);
                //     for (int i = 1; i < boneNames.Length; i++)
                //     {
                //         var parent = actor.Bones[i].GetParent().GetIndex();
                //         globalRotations[i] = math.mul(globalRotations[parent], Inertializer.InertializedRotations[i]);
                //     }
                //     
                //     for (int i = 0; i < boneNames.Length; i++)
                //     {
                //         Matrix4x4 mat = Matrix4x4.TRS(Inertializer.InertializedPositions[i], Inertializer.InertializedRotations[i], Vector3.one);
                //
                //         if (i == 0)
                //         {
                //             mat = worldMat * mat;
                //             actor.Bones[i].SetTransformation(mat);
                //         }
                //         else
                //         {
                //             actor.Bones[i].SetRotation(globalRotations[i]);
                //         }
                //     }
                // }
                //
                // else
                // {
                //     for (int i = 0; i < boneNames.Length; i++)
                //     {
                //         Matrix4x4 mat = Matrix4x4.TRS(Inertializer.InertializedPositions[i], Inertializer.InertializedRotations[i], Vector3.one);
                //
                //         if (i == 0)
                //         {
                //             mat = worldMat * mat;
                //             actor.Bones[i].SetTransformation(mat);
                //         }
                //         else
                //         {
                //             // actor.Bones[i].GetTransform().SetLocalPositionAndRotation(mat.GetPosition(), mat.GetRotation());
                //         }
                //     }
                // }
            }
        }

        // void Update()
        // {
        //     if (Version == 0) UpdateV0();
        //     else if (Version == 1) UpdateV1();
        // }

        int GetFrameIndex()
        {
            if (!SelfUpdate) return GetEditor().GetCurrentFrameIndex();
            else
            {
                TimeStamp = Mathf.Repeat(TimeStamp + Time.deltaTime, NumFrames / Framerate);
                return GetFrameNumberByTimestamp(TimeStamp);
            }
        }
        
        void Update()
        {
            var frameIndex = GetFrameIndex();
            PoseVector newPose;
            bool mirrored, transit;

            if (Version <= 1)
            {
                Frame frame;
                (frame, mirrored, transit) = Version == 0 ? GetReplayFrame(frameIndex) : GetReplayFrameV1(frameIndex);
                if (frame == null)
                {
                    Debug.Log("Frame not found");
                    return;
                }
                newPose = CreatePoseVecFromFrame(frame, mirrored);
            }
            else if (Version == 2)
            {
                Frame f0, f1;
                float t;
                (f0, f1, t, mirrored, transit) = GetReplayFrameV2(frameIndex);
                newPose = PoseVector.Interpolate(CreatePoseVecFromFrame(f0, mirrored), CreatePoseVecFromFrame(f1, mirrored), t);
            }
            else
            {
                Debug.Log("Unknown version");
                return;
            }
            
            if (UseInertialization)
            {
                UpdateWithInertialization(newPose, transit);
            }

            else
            {
                Frame frame;
                (frame, mirrored, transit) = Version == 0 ? GetReplayFrame(frameIndex) : GetReplayFrameV1(frameIndex);
                var rootTransformRef = GetEditor().GetSession().GetActor().GetRoot().GetWorldMatrix();
                var rootTransformNN = frame.Asset.GetModule<RootModule>().GetRootTransformation(frame.Timestamp, mirrored);
                var worldMatrix = AlignRootTransform ? rootTransformRef * rootTransformNN.inverse : rootTransformNN;
                var rootMatrix = AlignRootTransform ? rootTransformRef : rootTransformNN;
                var boneNames = GetActor().GetBoneNames();
                var boneTransformations = new Matrix4x4[boneNames.Length];
                GetActor().GetRoot().SetPositionAndRotation(rootMatrix.GetPosition(), rootMatrix.GetRotation());

                for (int i = 0; i < boneNames.Length; i++)
                {
                    var transformation = frame.GetBoneTransformation(boneNames[i], mirrored);
                    transformation = worldMatrix * transformation;
                    transformation.m03 += TranslateX;
                    boneTransformations[i] = transformation;

                    if (i == 0 || !UpdateRotationOnly)
                    {
                        GetActor().SetBoneTransformation(boneTransformations[i], boneNames[i]);
                    }
                    else if (UpdateRotationOnly)
                    {
                        GetActor().Bones[i].SetRotation(boneTransformations[i].GetRotation());
                    }
                }
            }
            
            GetActor().BoneColor = BoneColor;
        }

        #if UNITY_EDITOR
        [CustomEditor(typeof(MotionReplay), true)]
        public class MotionReplay_Editor : Editor 
        {
            public MotionReplay Target;

            void Awake() {
                Target = (MotionReplay)target;
            }

            public override void OnInspectorGUI() {
                Undo.RecordObject(Target, Target.name);

                DrawDefaultInspector();

                if(GUI.changed) {
                    EditorUtility.SetDirty(Target);
                }
            }
        }
        #endif
    }
}