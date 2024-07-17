#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using DeepPhase;
using SerializedKDTree;
using UnityEngine.Serialization;


namespace AI4Animation
{
    [ExecuteInEditMode]
    public class NearestNeighbors : MonoBehaviour
    {
        public bool ShowNN = false;
        [FormerlySerializedAs("savePath")] public string SavePath = "Assets/Projects/DeepPhase/Demos/Quadruped/KDTrees";
        [FormerlySerializedAs("motionEditor")] public MotionEditor SourceMotionEditor;
        public MotionEditor BaseMotionEditor;
        public int K = 5;
        private SerializedKDTree.KDTree CurrentTree = null;
        [FormerlySerializedAs("targetPhaseTag")] public string TargetPhaseTag = null;
        public FeatureExtractor.CHANNEL_TYPE ChannelType = FeatureExtractor.CHANNEL_TYPE.PhaseManifold;
        [FormerlySerializedAs("actorPrefab")] public GameObject ActorPrefab = null;
        public bool AlignRootTransform = true;
        [SerializeField]
        private List<Actor> Actors = new List<Actor>();
        public float TranslateX = 0;
        private string CurrentTreeName = null;
        public Color BoneColor = UltiDraw.Cyan;
        public int DistanceHistoryLength = 100;
        public PhaseSourceEnum  PhaseSource = PhaseSourceEnum.Editor;
        [NonSerialized] private VQPhaseController VQController = null;
        private UltiDraw.GUIRect DistanceWindow = new UltiDraw.GUIRect(0.85f, 0.5f, 0.15f, 0.15f);
        public int FutureFrames = 1;
        [NonSerialized] private float LastMatchTime = 0f;
        [NonSerialized] private FrameIndex LastMatch = null;

        private void OnEnable()
        {
            CacheDatabase = true;
            LastMatchTime = 0f;
            LastMatch = null;
        }

        private void OnDisable()
        {
            CacheDatabase = false;
        }

        public enum PhaseSourceEnum
        {
            Editor,
            VQController
        }

        private Queue<float> DistanceHistory;
        public bool ShowDistanceHistory
        {
            set
            {
                if (value == false) DistanceHistory = null;
                else DistanceHistory ??= new Queue<float>();
            }
            get => DistanceHistory != null;
        }
        public bool CacheDatabase
        {
            set
            {
                if (value == false)
                {
                    CachedMotionData = null;
                    Resources.UnloadUnusedAssets();
                }
                else CachedMotionData ??= new MotionDataHolder(SourceMotionEditor.Assets);
            }
            get => CachedMotionData != null;
        }
        private MotionDataHolder CachedMotionData = null;

        private VQPhaseController GetVQPhaseContorller()
        {
            if (VQController == null)
                VQController = GameObjectExtensions.Find<VQPhaseController>(true);
            return VQController;
        }

        public MotionAsset Retrieve(string guid)
        {
            if (CachedMotionData == null)
            {
                return MotionAsset.Retrieve(guid);
            }
            else
            {
                return CachedMotionData.Retrieve(guid);
            }
        }
        
        public class FrameIndex : KDNode.Value
        {
            public string assetIndex;
            public int frameIndex;
            public bool mirror;
            public FrameIndex(string assetIndex, int frameIndex, bool mirror)
            {
                this.assetIndex = assetIndex;
                this.frameIndex = frameIndex;
                this.mirror = mirror;
            }
        }
        
        public float[] CreatePointFromPhase(MotionAsset asset, Frame frame, bool mirror)
        {
            float[] point = new float[FeatureExtractor.GetSize(asset, null, ChannelType, TargetPhaseTag) * FutureFrames];
            var frameIndex = frame.Index - 1;
            FeatureExtractor.GetWindow(asset, mirror, ChannelType, TargetPhaseTag, frameIndex, frameIndex + FutureFrames, null, point);
            return point;
        }

        string GetTreeNameFromSettings()
        {
            string treeName = $"Database_{TargetPhaseTag}_{ChannelType}";
            if (FutureFrames > 1) treeName += $"_{FutureFrames}";
            return treeName;
        }

        private SerializedKDTree.KDTree GetKDTree(bool create = false)
        {
            if (CurrentTree != null && CurrentTreeName == GetTreeNameFromSettings())
                return CurrentTree;
            string[] folders = { SavePath };
            string treeName = GetTreeNameFromSettings();
            foreach (var asset in AssetDatabase.FindAssets(treeName, folders))
            {
                var path = AssetDatabase.GUIDToAssetPath(asset);
                var nameList = path.Split('/');
                var name = nameList[^1];
                if (name != treeName + ".asset") continue;
                var tree = AssetDatabase.LoadAssetAtPath<SerializedKDTree.KDTree>(path);
                Debug.Log($"Found existing tree at {path}");
                CurrentTree = tree;
                CurrentTreeName = treeName;
                return tree;
                // AssetDatabase.DeleteAsset(path);
            }

            if (create)
            {
                var newTree = ScriptableObjectExtensions.Create<SerializedKDTree.KDTree>(SavePath, treeName, true);
                BuildDatabase(newTree);

                CurrentTree = newTree;
                CurrentTreeName = GetTreeNameFromSettings();
                return newTree;
            }
            else
            {
                return null;
            }
        }

        public void BuildDatabase(SerializedKDTree.KDTree KDTree)
        {
            int numChannels = -1;
            for (int i = 0; i < BaseMotionEditor.Assets.Count; i++)
            {
                var motionAsset = MotionAsset.Retrieve(BaseMotionEditor.Assets[i]);
                float[] buffer = null;
                var numChannelsInAsset = FeatureExtractor.Get(motionAsset, false, ChannelType, TargetPhaseTag, 0, null, ref buffer) * FutureFrames;
                if (numChannelsInAsset == 0)
                {
                    Debug.Log("Given phase does not exist in the asset.");
                    continue;
                }

                if (numChannels == -1)
                {
                    numChannels = numChannelsInAsset;
                    KDTree.Initialize(numChannels);
                }

                for (int j = 0; j < motionAsset.Frames.Length; j++)
                {
                    KDTree.AddPoint(CreatePointFromPhase(motionAsset, motionAsset.Frames[j], true),
                        new FrameIndex(BaseMotionEditor.Assets[i], j, true));
                    KDTree.AddPoint(CreatePointFromPhase(motionAsset, motionAsset.Frames[j], false),
                        new FrameIndex(BaseMotionEditor.Assets[i], j, false));
                }
            }

            KDTree.Finish();
        }

        public void UpdateActorsFromNN()
        {
            var tree = GetKDTree();
            if (tree == null)
            {
                Debug.Log($"Tree of {ChannelType} with tag {TargetPhaseTag} not found.");
                return;
            }
            
            float[] queryPoint = null;
            if (PhaseSource == PhaseSourceEnum.Editor)
                queryPoint = CreatePointFromPhase(SourceMotionEditor.GetSession().Asset, SourceMotionEditor.GetCurrentFrame(), SourceMotionEditor.Mirror);
            else if (PhaseSource == PhaseSourceEnum.VQController)
                queryPoint = GetVQPhaseContorller().GetPhaseManifold();
            if (queryPoint == null)
            {
                Debug.Log($"{ChannelType} with tag {TargetPhaseTag} not found in current editor.");
                return;
            }

            if (!UpdateActorsFromLastMatch())
            {
                FindNewNN(tree, queryPoint);
                UpdateActorsFromLastMatch();
                // var nn = tree.NearestNeighbors(queryPoint, K + 1);
                // UpdateActors(nn);
            }
        }

        void FindNewNN(SerializedKDTree.KDTree tree, float[] queryPoint)
        {
            var nns = tree.NearestNeighbors(queryPoint, K + 1);
            foreach (FrameIndex nn in nns)
            {
                LastMatch = nn;
                Debug.Log($"Distance = {nns.CurrentDistance}");
                LastMatchTime = Time.time;
                break;
            }
        }

        public NearestNeighbour QueryNNFromEditor(MotionEditor editor, int k)
        {
            var tree = GetKDTree();
            if (tree == null) return null;
            float[] queryPoint = CreatePointFromPhase(editor.GetSession().Asset, editor.GetCurrentFrame(), editor.Mirror);
            if (queryPoint == null)
            {
                return null;
            }
            var nearestNeighbors = GetKDTree().NearestNeighbors(queryPoint, k);
            return nearestNeighbors;
        }

        private Actor CreateActor()
        {
            Actor actor;
            if (ActorPrefab != null)
            {
                var prefab = Instantiate(ActorPrefab, Vector3.zero, Quaternion.identity, transform);
                actor = prefab.AddComponent<Actor>();
                actor.Initialize();
                var boneMapping = BaseMotionEditor.GetSession().Asset.Source.GetBoneIndices(actor.GetBoneNames());
                var bones = new List<Transform>();

                for (int i = 0; i < boneMapping.Length; i++)
                {
                    if (boneMapping[i] != -1) bones.Add(actor.Bones[i].GetTransform());
                }

                actor.Create(bones.ToArray());
            }
            else
            {
                actor = Actor.CreateActor(BaseMotionEditor.GetSession().Asset, transform, 
                    BaseMotionEditor.GetSession().GetActor().GetBoneNames());
            }

            actor.BoneColor = BoneColor;
            return actor;
        }

        public bool UpdateActorsFromLastMatch()
        {
            if (LastMatch == null) return false;
            
            while (K > Actors.Count)
            {
                Actors.Add(CreateActor());
            }

            while (K < Actors.Count)
            {
                var lastActor = Actors[Actors.Count - 1];
                Actors.RemoveAt(Actors.Count - 1);
                DestroyImmediate(lastActor.gameObject);
            }

            if (K == 0) return true;

            var asset = Retrieve(LastMatch.assetIndex);
            float deltaTimestamp = Time.time - LastMatchTime;
            int deltaIndex = (int)Mathf.Floor(deltaTimestamp * asset.Framerate);

            // Debug.Log($"Last frame = {deltaIndex}");

            if (deltaIndex >= FutureFrames) return false;

            var frameIdx = Math.Min(LastMatch.frameIndex + deltaIndex, asset.Frames.Length - 1);
            var frameToUse = asset.Frames[frameIdx];
            var timestampInAsset = frameToUse.Timestamp;
            
            var rootTransformRef = SourceMotionEditor.GetSession().GetActor().GetRoot().GetWorldMatrix();
            var i = 0;
            var rootTransformNN = asset.GetModule<RootModule>().GetRootTransformation(timestampInAsset, LastMatch.mirror);
            var boneMapping = asset.Source.GetBoneIndices(Actors[i].GetBoneNames());
            var worldMatrix = AlignRootTransform ? rootTransformRef * rootTransformNN.inverse : rootTransformNN;
            var rootMatrix = AlignRootTransform ? rootTransformRef : rootTransformNN;
                
            Actors[i].GetRoot().SetPositionAndRotation(rootMatrix.GetPosition(), rootMatrix.GetRotation());
            for (int j = 0; j < Actors[i].GetBoneNames().Length; j++)
            {
                var boneName = Actors[i].GetBoneNames()[j];
                if (boneMapping[j] != -1)
                {
                    var transformation = frameToUse.GetBoneTransformation(boneName, LastMatch.mirror);
                    transformation = worldMatrix * transformation;
                    transformation.m03 += TranslateX;
                    Actors[i].SetBoneTransformation(transformation, boneName);
                }
            }

            return true;
        }

        public void UpdateActors(NearestNeighbour nearestNeighbours)
        {
            while (K > Actors.Count)
            {
                Actors.Add(CreateActor());
            }

            while (K < Actors.Count)
            {
                var lastActor = Actors[Actors.Count - 1];
                Actors.RemoveAt(Actors.Count - 1);
                DestroyImmediate(lastActor.gameObject);
            }

            if (nearestNeighbours == null)
            {
                Debug.Log("The query result is empty");
                return;
            }

            int i = -2;
            var rootTransformRef = SourceMotionEditor.GetSession().GetActor().GetRoot().GetWorldMatrix();
            
            foreach (FrameIndex nn in nearestNeighbours)
            {
                i++;
                if (i == -1) continue;
                float currentDistance = nearestNeighbours.CurrentDistance;
                if (ShowDistanceHistory && i == 0)
                {
                    DistanceHistory.Enqueue(currentDistance);
                    if (DistanceHistory.Count > DistanceHistoryLength) DistanceHistory.Dequeue();
                }
                Debug.Log($"Distance {currentDistance}");

                Actors[i].BoneColor = BoneColor;
                
                var asset = Retrieve(nn.assetIndex);
                var frameIdx = nn.frameIndex;
                var frame = asset.Frames[frameIdx];
                
                var rootTransformNN = asset.GetModule<RootModule>().GetRootTransformation(frame.Timestamp, nn.mirror);
                var boneMapping = asset.Source.GetBoneIndices(Actors[i].GetBoneNames());
                var worldMatrix = AlignRootTransform ? rootTransformRef * rootTransformNN.inverse : rootTransformNN;
                var rootMatrix = AlignRootTransform ? rootTransformRef : rootTransformNN;
                
                Actors[i].GetRoot().SetPositionAndRotation(rootMatrix.GetPosition(), rootMatrix.GetRotation());
                for (int j = 0; j < Actors[i].GetBoneNames().Length; j++)
                {
                    var boneName = Actors[i].GetBoneNames()[j];
                    if (boneMapping[j] != -1)
                    {
                        var transformation = frame.GetBoneTransformation(boneName, nn.mirror);
                        transformation = worldMatrix * transformation;
                        transformation.m03 += TranslateX;
                        Actors[i].SetBoneTransformation(transformation, boneName);
                    }
                }
            }
        }
        
        private void Update()
        {
            if (!ShowNN) return;
            UpdateActorsFromNN();
        }

        public class MotionDataHolder
        {
            private Dictionary<string, MotionAsset> Assets = new Dictionary<string, MotionAsset>();

            public MotionDataHolder(List<string> assets, bool lazy = true)
            {
                if (!lazy)
                {
                    foreach(var asset in assets)
                    {
                        Assets.Add(asset, MotionAsset.Retrieve(asset));
                    }
                }
            }

            public MotionAsset Retrieve(string asset)
            {
                if (!Assets.ContainsKey(asset))
                {
                    Assets.Add(asset, MotionAsset.Retrieve(asset));
                }

                return Assets[asset];
            }
        }

        private void OnGUI()
        {
            if (ShowDistanceHistory)
            {
                UltiDraw.Begin();
                var offset = new Vector2(0f, -0.1f);
                UltiDraw.OnGUILabel(DistanceWindow.GetCenter() + offset, DistanceWindow.GetSize(), 0.0125f,
                    "Nearest Neighbor Distance", UltiDraw.White);
                UltiDraw.End();
            }
        }

        private void OnRenderObject()
        {
            if (ShowDistanceHistory)
            {
                var values = new float[DistanceHistoryLength]; 
                int cnt = 0;
                foreach (var val in DistanceHistory)
                {
                    values[cnt++] = val;
                }
                UltiDraw.Begin();
                UltiDraw.PlotFunction(DistanceWindow.GetCenter(), DistanceWindow.GetSize(), values, 0, 2, 0.001f);
                UltiDraw.End();
            }
        }

        [CustomEditor(typeof(NearestNeighbors))]
        public class NearestNeighborsEditor : Editor {

            public NearestNeighbors Target;

            void Awake() {
                Target = (NearestNeighbors)target;
            }

            public override void OnInspectorGUI() {
                DrawDefaultInspector();
                Target.CacheDatabase = EditorGUILayout.Toggle("Cache Database", Target.CacheDatabase);
                Target.ShowDistanceHistory = EditorGUILayout.Toggle("Show Distance History", Target.ShowDistanceHistory);
                if(Utility.GUIButton("Build Database", UltiDraw.DarkGrey, UltiDraw.White)) {
                    Target.GetKDTree(true);
                }
                if(Utility.GUIButton("Query NN", UltiDraw.DarkGrey, UltiDraw.White)) {
                    Target.UpdateActorsFromNN();
                }
                if(Utility.GUIButton("Create", UltiDraw.DarkGrey, UltiDraw.White)) {
                    Target.CreateActor();
                }
                if(GUI.changed) {
                    EditorUtility.SetDirty(Target);
                }
            }
        }
    }
}
#endif