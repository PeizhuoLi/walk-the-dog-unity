#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using System.Threading.Tasks;

namespace AI4Animation
{
    public class ModuleManager : EditorIterator
    {
        private static Type[] SupportedTypes = { typeof(DeepPhaseModule), typeof(VQPhaseModule) };
        public enum MODE { Iterate, Delete }
        
        public MODE Mode = MODE.Delete;
        public int TargetingTypeIndex;
        public string Tag = string.Empty;

        [MenuItem ("AI4Animation/Tools/Module Manager")] 
        static void Init() {
            Window = EditorWindow.GetWindow(typeof(ModuleManager));
        }

        public ModuleManager()
        {
            BatchSize = 1;
        }

        private string[] GetOptions()
        {
            string[] options = new string[SupportedTypes.Length];
            for (int i = 0; i < SupportedTypes.Length; i++)
            {
                options[i] = SupportedTypes[i].Name;
            }

            return options;
        }

        public override void DerivedOnGUI()
        {
            Tag = EditorGUILayout.TextField("Tag", Tag);
            Mode = (MODE)EditorGUILayout.EnumPopup("Mode", Mode);
            TargetingTypeIndex = EditorGUILayout.Popup("Targeting Type", TargetingTypeIndex, GetOptions());
        }

        public override async Task Process()
        {
            Count = 0;
            Total = Editor.Assets.Count;
            var targetingType = SupportedTypes[TargetingTypeIndex];
            foreach (var asset in Editor.Assets)
            {
                if (CheckAndResetCancel()) break;
                var motionAsset = MotionAsset.Retrieve(asset);
                
                if (Mode == MODE.Delete)
                {
                    if (targetingType == typeof(VQPhaseModule))
                        motionAsset.RemoveModule<VQPhaseModule>(Tag);
                    else if (targetingType == typeof(DeepPhaseModule))
                        motionAsset.RemoveModule<DeepPhaseModule>(Tag);
                }

                Count += 1;
                
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                if (Count % BatchSize == 0)
                {
                    await Task.Yield();
                }
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Resources.UnloadUnusedAssets();
        }
    }
}

#endif