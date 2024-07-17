using System;
using System.IO;
using DeepPhase;
using Unity.Sentis;
using UnityEngine;
using UnityEditor;


namespace AI4Animation
{
    public class QuickSetup : MonoBehaviour
    {
        public MotionEditor[] Editors;
        public ONNXController[] OnnxControllers; 
        public VQPhaseController VQController;
        public GameCamera ViewCamera;

        public ONNXController[] CmpControllers;
        public IndividualONNXConfig[] CmpONNXConfigs;
        [HideInInspector] public GameObject CmpObjectParent;
        
        public string TargetPhaseTag;
        public FeatureExtractor.CHANNEL_TYPE InputChannelType;
        [HideInInspector] public ONNXController.UpdateModeEnum UpdateMode;
        public SetupEnum Setup;

        public int Source0, Source1;

        public string[] OnnxPrefixes;
        public string OnnxPrefix;

        [Serializable]
        public class IndividualONNXConfig
        {
            public string TargetPhaseTag;
            public bool WindowedInference = false;
            public int ClassIdentifier = -1;
            public string ExtraFlag = string.Empty;
        }
        
        public enum SetupEnum
        {
            Reconstruction,
            Retargeting,
            VQAutoregressive
        }

        Transform GetCmpObjectParent()
        {
            if (CmpObjectParent == null)
            {
                CmpObjectParent = GameObject.Find("CmpControllers");
            }
            if (CmpObjectParent == null)
            {
                CmpObjectParent = new GameObject("CmpControllers");
            }
            return CmpObjectParent.transform;
        }

        void SetupCmpONNX()
        {
            var num = CmpONNXConfigs.Length;
            int cmpClassIdentifier = 0;

            if (Setup is SetupEnum.Reconstruction) cmpClassIdentifier = Source0;
            if (Setup is SetupEnum.Retargeting) cmpClassIdentifier = Source1;

            if (Setup is not SetupEnum.VQAutoregressive)
                for (int i = 0; i < num; i++)
                    CmpONNXConfigs[i].ClassIdentifier = cmpClassIdentifier;
            
            foreach (var c in CmpControllers)
            {
                DestroyImmediate(c.gameObject);
            }
            CmpControllers = new ONNXController[num];
            for (int i = 0; i < num; i++)
            {
                var newObj = Instantiate(OnnxControllers[cmpClassIdentifier], GetCmpObjectParent());
                newObj.name = $"CmpController-{i}";
                var controller = newObj.GetComponent<ONNXController>();
                controller.translate_x = 1 + 0.5f * (1 + i);
                CmpControllers[i] = controller;
            }
            
            for (int i = 0; i < CmpControllers.Length; i++)
            {
                CmpControllers[i].gameObject.SetActive(true);
                CmpControllers[i].targetPhaseTag = CmpONNXConfigs[i].TargetPhaseTag;
                OnnxAutoPickModel(CmpControllers[i], CmpONNXConfigs[i]);
                CmpControllers[i].UseWindowedInference = CmpONNXConfigs[i].WindowedInference;
            }
        }
        
        void SetupVQController()
        {
            VQController.Visualize = InputChannelType is FeatureExtractor.CHANNEL_TYPE.VQPhaseManifold
                or FeatureExtractor.CHANNEL_TYPE.VQPhaseManifoldOri;

            if (Setup != SetupEnum.VQAutoregressive)
            {
                VQController.UpdateSyntheticPhase = false;
            }
            else
            {
                VQController.UpdateSyntheticPhase = true;
            }
        }

        void DeactivateEditorBesides(int index)
        {
            for (int i = 0; i < Editors.Length; i++) 
                if (i != index)
                    DeactivateEditor(Editors[i]);
        }

        void DeactivateONNXBesides(int index)
        {
            for (int i = 0; i < OnnxControllers.Length; i++)
                if (i != index)
                    OnnxControllers[i].gameObject.SetActive(false);
        }

        void SetupEditorsAndCameras()
        {
            if (Setup is SetupEnum.Reconstruction or SetupEnum.Retargeting)
            {
                ActivateEditor(Editors[Source0], true);
                DeactivateEditorBesides(Source0);
            }
            
            else if (Setup is SetupEnum.VQAutoregressive)
            {
                ActivateEditor(Editors[0], true); // Focus camera on first one
                DeactivateEditorBesides(0);
                DeactivateEditor(Editors[0]);
            }
        }
        
        void OnnxAutoPickModel(ONNXController controller, IndividualONNXConfig config)
        {
            if (!controller.gameObject.activeSelf) return;
            var classIdentifier = config.ClassIdentifier;
            var targetPhaseTag = config.TargetPhaseTag;
                
            string characterName = Editors[classIdentifier].name.Split('-')[1];
            bool needOri = InputChannelType is FeatureExtractor.CHANNEL_TYPE.VQPhaseManifoldOri;
            string prefix = classIdentifier < OnnxPrefixes.Length ? OnnxPrefixes[classIdentifier] : OnnxPrefix;

            var files = Directory.GetFiles(prefix);
            var filePath = string.Empty;
            foreach (var file in files)
            {
                if (file.EndsWith(".onnx") && file.Contains(targetPhaseTag) && file.Contains(characterName))
                {
                    if (config.ExtraFlag != string.Empty && !file.Contains(config.ExtraFlag)) continue;
                    
                    if (needOri && !file.Contains("ori")) continue;
                    if (!needOri && file.Contains("ori")) continue;
                    
                    filePath = file;
                    break;
                }
            }
                
            if (filePath == string.Empty)
            {
                throw new Exception($"No onnx file found for tag {targetPhaseTag} with extra flag {config.ExtraFlag}");
            }

            controller.NeuralNetwork.Model = AssetDatabase.LoadAssetAtPath<ModelAsset>(filePath);
        }

        void SetupONNX()
        {
            if (Setup is SetupEnum.Reconstruction)
            {
                OnnxControllers[Source0].gameObject.SetActive(true);
                OnnxControllers[Source0].translate_x = 1;
                OnnxControllers[Source0].Editor = Editors[Source0];
                DeactivateONNXBesides(Source0);
            }
            else if (Setup is SetupEnum.Retargeting)
            {
                OnnxControllers[Source1].gameObject.SetActive(true);
                OnnxControllers[Source1].translate_x = 1;
                OnnxControllers[Source1].Editor = Editors[Source0];
                DeactivateONNXBesides(Source1);
            }
            else if (Setup is SetupEnum.VQAutoregressive)
            {
                for (int i = 0; i < OnnxControllers.Length; i++)
                {
                    OnnxControllers[i].gameObject.SetActive(true);
                    OnnxControllers[i].translate_x = i;
                    OnnxControllers[i].Editor = Editors[0];
                }
            }

            if (Setup is SetupEnum.VQAutoregressive)
            {
                UpdateMode = ONNXController.UpdateModeEnum.FromVQController;
            }
            else
            {
                UpdateMode = ONNXController.UpdateModeEnum.CopyPastFromEditor;
            }
            ApplyUpdateMode();

            for (int i = 0; i < OnnxControllers.Length; i++)
            {
                var config = new IndividualONNXConfig
                {
                    ClassIdentifier = i,
                    TargetPhaseTag = TargetPhaseTag,
                };
                OnnxAutoPickModel(OnnxControllers[i], config);
            }
        }

        void ApplyUpdateMode()
        {
            foreach (var controller in OnnxControllers)
            {
                controller.UpdateMode = UpdateMode;
            }
        }

        void ApplyTag()
        {
            foreach (var controller in OnnxControllers)
            {
                controller.targetPhaseTag = TargetPhaseTag;
            }

            VQController.TargetPhaseTag = TargetPhaseTag;
        }

        void ApplyInputChannelType()
        {
            foreach (var controller in OnnxControllers)
            {
                controller.InputChannelType = InputChannelType;
            }
        }

        void DeactivateEditor(MotionEditor editor)
        {
            editor.gameObject.SetActive(false);
            editor.Visualize.Clear();
        }

        void ActivateEditor(MotionEditor editor, bool setCameraFocus)
        {
            editor.gameObject.SetActive(true);
            if (setCameraFocus)
            {
                if (editor.name.Contains("dog")) 
                    ViewCamera.Target = editor.GetSession().GetActor().transform;
                else
                {
                    ViewCamera.AutoLocateEditorName = editor.name;
                    ViewCamera.Target = null;
                }
            }

            if (InputChannelType is FeatureExtractor.CHANNEL_TYPE.VQPhaseManifold or FeatureExtractor.CHANNEL_TYPE.VQPhaseManifoldOri)
            {
                var asset = editor.GetSession().Asset;
                var vqPhaseModule = asset.GetModule<VQPhaseModule>(TargetPhaseTag);
                editor.Visualize.Clear();
                editor.Visualize.Add(vqPhaseModule.GetID());
            }
        }

        void ActivateNewVisualizer()
        {
            if (Setup != SetupEnum.VQAutoregressive) return;

            var visualizerObj = GameObject.Find("EditorVQVisualizer");
            var visualizer = visualizerObj.GetComponent<VQVisualizer>();

            visualizer.VisualizerType = VQVisualizer.VisualizerTYPE.Curve;
            visualizer.Visualize = true;
            visualizer.VisualizeHighlight = true;
            visualizer.VisualizeExtra = true;
            
            visualizer.TargetPhaseTag = TargetPhaseTag;
            
            visualizer.ShowPhase = true;
        }
        
        void ApplySetup()
        {
            ApplyTag();
            ApplyInputChannelType();
            SetupEditorsAndCameras();
            SetupONNX();
            SetupVQController();
            SetupCmpONNX();
            ActivateNewVisualizer();
        }
        
        #if UNITY_EDITOR
        [CustomEditor(typeof(QuickSetup), true)]
        public class QuickSetup_Editor : Editor
        {
            public QuickSetup Target;

            private void Awake()
            {
                Target = (QuickSetup)target;
            }

            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();
                if (Utility.GUIButton("Apply Tag", UltiDraw.DarkGrey, UltiDraw.White))
                {
                    Target.ApplyTag();
                }
                if (Utility.GUIButton("Apply Input Channel Type", UltiDraw.DarkGrey, UltiDraw.White))
                {
                    Target.ApplyInputChannelType();
                }
                if (Utility.GUIButton("Apply Setup", UltiDraw.DarkGrey, UltiDraw.White))
                {
                    Target.ApplySetup();
                }
            }
        }
        #endif
    }
}