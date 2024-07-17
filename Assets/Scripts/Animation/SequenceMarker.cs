using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AI4Animation
{
    public class SequenceMarker : EditorIterator
    {
        private enum OperationEnum { MarkDogAll, MarkSelectedDog, MarkAll }
        private OperationEnum Operation = OperationEnum.MarkDogAll;
        
        [MenuItem ("AI4Animation/Tools/Sequence Marker")]
        static void Init() {
            Window = EditorWindow.GetWindow(typeof(SequenceMarker));
        }

        public override void DerivedOnGUI()
        {
            Operation = (OperationEnum)EditorGUILayout.EnumPopup("Operation", Operation);
        }

        private void MarkSelectedDog()
        {
            MotionAsset.Retrieve(Editor.Assets[0]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[0]).SetSequence(0, 180, 1531);
            MotionAsset.Retrieve(Editor.Assets[2]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[2]).SetSequence(0, 680, 820);
            MotionAsset.Retrieve(Editor.Assets[6]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[6]).SetSequence(0, 90, 593);
            MotionAsset.Retrieve(Editor.Assets[7]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[7]).SetSequence(0, 290, 1072);
            MotionAsset.Retrieve(Editor.Assets[8]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[8]).SetSequence(0, 1, 50);
            MotionAsset.Retrieve(Editor.Assets[8]).SetSequence(1, 400, 911);
            MotionAsset.Retrieve(Editor.Assets[9]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[10]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[10]).SetSequence(0, 230, 548);
            MotionAsset.Retrieve(Editor.Assets[11]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[11]).SetSequence(0, 400, 567);
            MotionAsset.Retrieve(Editor.Assets[12]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[13]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[14]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[16]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[16]).SetSequence(0, 200, 550);
            MotionAsset.Retrieve(Editor.Assets[17]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[17]).SetSequence(0, 470, 720);
            MotionAsset.Retrieve(Editor.Assets[18]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[18]).SetSequence(0, 175, 395);
            MotionAsset.Retrieve(Editor.Assets[19]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[19]).SetSequence(0, 300, 750);
            MotionAsset.Retrieve(Editor.Assets[19]).SetSequence(1, 1040, 1079);
            MotionAsset.Retrieve(Editor.Assets[20]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[21]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[21]).SetSequence(0, 1, 1300);
            MotionAsset.Retrieve(Editor.Assets[21]).SetSequence(1, 2950, 3530);
            MotionAsset.Retrieve(Editor.Assets[21]).SetSequence(2, 3730, 4200);
            MotionAsset.Retrieve(Editor.Assets[22]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[23]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[23]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[24]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[24]).SetSequence(0, 200, 630);
            MotionAsset.Retrieve(Editor.Assets[25]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[25]).SetSequence(0, 1, 2690);
            MotionAsset.Retrieve(Editor.Assets[25]).SetSequence(1, 2760, 4336);
            MotionAsset.Retrieve(Editor.Assets[26]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[27]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[27]).SetSequence(0, 1, 1100);
            MotionAsset.Retrieve(Editor.Assets[27]).SetSequence(1, 2820, 3940);
            MotionAsset.Retrieve(Editor.Assets[27]).SetSequence(2, 4100, 4500);
            MotionAsset.Retrieve(Editor.Assets[27]).SetSequence(3, 5660, 6010);
            MotionAsset.Retrieve(Editor.Assets[27]).SetSequence(4, 6600, 7200);
            MotionAsset.Retrieve(Editor.Assets[27]).SetSequence(5, 12300, 12850);
            MotionAsset.Retrieve(Editor.Assets[27]).SetSequence(6, 13200, 13399);
            MotionAsset.Retrieve(Editor.Assets[28]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[28]).SetSequence(0, 920, 985);
            MotionAsset.Retrieve(Editor.Assets[28]).SetSequence(1, 1700, 1907);
            MotionAsset.Retrieve(Editor.Assets[29]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[29]).SetSequence(0, 250, 790);
            MotionAsset.Retrieve(Editor.Assets[29]).SetSequence(1, 970, 1575);
            MotionAsset.Retrieve(Editor.Assets[29]).SetSequence(2, 1630, 1750);
            MotionAsset.Retrieve(Editor.Assets[30]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[30]).SetSequence(0, 1790, 1920);
            MotionAsset.Retrieve(Editor.Assets[30]).SetSequence(1, 2070, 2470);
            MotionAsset.Retrieve(Editor.Assets[30]).SetSequence(2, 2770, 3025);
            MotionAsset.Retrieve(Editor.Assets[31]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[31]).SetSequence(0, 170, 500);
            MotionAsset.Retrieve(Editor.Assets[31]).SetSequence(1, 1250, 2460);
            MotionAsset.Retrieve(Editor.Assets[31]).SetSequence(2, 3040, 3200);
            MotionAsset.Retrieve(Editor.Assets[31]).SetSequence(3, 4680, 6550);
            MotionAsset.Retrieve(Editor.Assets[31]).SetSequence(4, 7600, 9450);
            MotionAsset.Retrieve(Editor.Assets[31]).SetSequence(5, 11540, 11691);
            MotionAsset.Retrieve(Editor.Assets[32]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[32]).SetSequence(0, 1, 300);
            MotionAsset.Retrieve(Editor.Assets[32]).SetSequence(1, 1360, 1540);
            MotionAsset.Retrieve(Editor.Assets[32]).SetSequence(2, 2380, 3086);
            MotionAsset.Retrieve(Editor.Assets[33]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[33]).SetSequence(0, 1, 1170);
            MotionAsset.Retrieve(Editor.Assets[33]).SetSequence(1, 1980, 2160);
            MotionAsset.Retrieve(Editor.Assets[33]).SetSequence(2, 7830, 8090);
            MotionAsset.Retrieve(Editor.Assets[34]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[34]).SetSequence(0, 1, 270);
            MotionAsset.Retrieve(Editor.Assets[34]).SetSequence(1, 2490, 2856);
            MotionAsset.Retrieve(Editor.Assets[35]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[37]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[38]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[38]).SetSequence(0, 3330, 3900);
            MotionAsset.Retrieve(Editor.Assets[39]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[39]).SetSequence(0, 880, 920);
            MotionAsset.Retrieve(Editor.Assets[39]).SetSequence(1, 1280, 5052);
            MotionAsset.Retrieve(Editor.Assets[41]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[41]).SetSequence(0, 4690, 6190);
            MotionAsset.Retrieve(Editor.Assets[42]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[42]).SetSequence(0, 900, 3594);
            MotionAsset.Retrieve(Editor.Assets[43]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[43]).SetSequence(0, 1, 500);
            MotionAsset.Retrieve(Editor.Assets[43]).SetSequence(1, 4340, 4577);
            MotionAsset.Retrieve(Editor.Assets[44]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[44]).SetSequence(0, 1, 700);
            MotionAsset.Retrieve(Editor.Assets[44]).SetSequence(1, 950, 2000);
            MotionAsset.Retrieve(Editor.Assets[45]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[45]).SetSequence(0, 1, 410);
            MotionAsset.Retrieve(Editor.Assets[45]).SetSequence(1, 680, 778);
            MotionAsset.Retrieve(Editor.Assets[46]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[46]).SetSequence(0, 175, 235);
            MotionAsset.Retrieve(Editor.Assets[47]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[47]).SetSequence(0, 275, 498);
            MotionAsset.Retrieve(Editor.Assets[48]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[48]).SetSequence(0, 1, 220);
            MotionAsset.Retrieve(Editor.Assets[48]).SetSequence(1, 675, 748);
            MotionAsset.Retrieve(Editor.Assets[49]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[49]).SetSequence(0, 1, 700);
            MotionAsset.Retrieve(Editor.Assets[49]).SetSequence(1, 1510, 8300);
            MotionAsset.Retrieve(Editor.Assets[50]).Export = true;
            MotionAsset.Retrieve(Editor.Assets[50]).SetSequence(0, 200, 1000);
            MotionAsset.Retrieve(Editor.Assets[50]).SetSequence(1, 1850, 2100);
            MotionAsset.Retrieve(Editor.Assets[50]).SetSequence(2, 4150, 4700);
            MotionAsset.Retrieve(Editor.Assets[50]).SetSequence(3, 5030, 5356);
        }

        public override async Task Process()
        {
            int numSequence = 0;
            Total = Editor.Assets.Count;
            Count = 0;
            await Task.Yield();
            for (int i = 0; i < Total; i++)
            {
                if (CheckAndResetCancel()) break;
                numSequence += 1;
                var asset = MotionAsset.Retrieve(Editor.Assets[i]);
                asset.Export = false;
                asset.ClearSequences();

                if (Operation == OperationEnum.MarkDogAll)
                {
                    if (!asset.name.Contains("ex"))
                    {
                        asset.AddSequence();
                        asset.Export = true;
                    }
                }
                else if (Operation == OperationEnum.MarkAll)
                {
                    asset.AddSequence();
                    asset.Export = true;
                }
                EditorUtility.SetDirty(asset); 

                if (numSequence % BatchSize == 0) 
                    await Task.Yield();

                Count += 1;
            }

            if (Operation == OperationEnum.MarkSelectedDog)
            {
                MarkSelectedDog();
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}