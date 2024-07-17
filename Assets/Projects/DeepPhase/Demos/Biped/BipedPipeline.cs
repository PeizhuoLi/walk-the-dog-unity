#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AI4Animation;

namespace DeepPhase {
    public class BipedPipeline : QuadrupedPipeline {

         protected override void ProcessAssets(MotionAsset asset) {
            asset.RemoveAllModules();
            
            asset.MirrorAxis = Axis.ZPositive;
            asset.Model = "Biped";
            asset.Scale = 0.01f;
            asset.Source.FindBone("Head").Alignment = new Vector3(90f, 0f, 0f);
            asset.Source.FindBone("LeftShoulder").Alignment = new Vector3(90f, 0f, 0f);
            asset.Source.FindBone("RightShoulder").Alignment = new Vector3(90f, 0f, 0f);

            {
                RootModule module = asset.HasModule<RootModule>() ? asset.GetModule<RootModule>() : asset.AddModule<RootModule>();
                module.Topology = RootModule.TOPOLOGY.Biped;
                module.SmoothRotations = true;
            }

            {
                ContactModule module = asset.HasModule<ContactModule>() ? asset.GetModule<ContactModule>() : asset.AddModule<ContactModule>();
                module.Clear();
                module.AddSensor("Hips", Vector3.zero, Vector3.zero, 0.2f*Vector3.one, 1f, LayerMask.GetMask("Ground"), ContactModule.ContactType.Translational, ContactModule.ColliderType.Sphere);
                module.AddSensor("Neck", Vector3.zero, Vector3.zero, 0.25f*Vector3.one, 1f, LayerMask.GetMask("Ground"), ContactModule.ContactType.Translational, ContactModule.ColliderType.Sphere);
                // module.AddSensor("LeftWristSite", Vector3.zero, Vector3.zero, 1f/30f*Vector3.one, 1f, LayerMask.GetMask("Ground"), ContactModule.ContactType.Translational, ContactModule.ColliderType.Sphere);
                // module.AddSensor("RightWristSite", Vector3.zero, Vector3.zero, 1f/30f*Vector3.one, 1f, LayerMask.GetMask("Ground"), ContactModule.ContactType.Translational, ContactModule.ColliderType.Sphere);
                // module.AddSensor("LeftToeSite", Vector3.zero, Vector3.zero, 1f/30f*Vector3.one, 1f, LayerMask.GetMask("Ground"), ContactModule.ContactType.Translational, ContactModule.ColliderType.Sphere);
                // module.AddSensor("RightToeSite", Vector3.zero, Vector3.zero, 1f/30f*Vector3.one, 1f, LayerMask.GetMask("Ground"), ContactModule.ContactType.Translational, ContactModule.ColliderType.Sphere);
                module.CaptureContacts(Pipeline.GetEditor());
            }

            {
                StyleModule module = asset.HasModule<StyleModule>() ? asset.GetModule<StyleModule>() : asset.AddModule<StyleModule>();
                module.Clear();

                RootModule root = asset.GetModule<RootModule>();
                StyleModule.Function idle = module.AddFunction("Idle");
                StyleModule.Function move = module.AddFunction("Move");
                StyleModule.Function speed = module.AddFunction("Speed");
                float threshold = 0.1f;
                float[] weights = new float[asset.Frames.Length];
                float[] rootMotion = new float[asset.Frames.Length];
                float[] bodyMotion = new float[asset.Frames.Length];
                for(int f=0; f<asset.Frames.Length; f++) {
                    rootMotion[f] = root.GetRootVelocity(asset.Frames[f].Timestamp, false).magnitude;
                    bodyMotion[f] = asset.Frames[f].GetBoneVelocities(Pipeline.GetEditor().GetSession().GetBoneMapping(), false).Magnitudes().Mean();
                }
                {
                    float[] copy = rootMotion.Copy();
                    for(int i=0; i<copy.Length; i++) {
                        rootMotion[i] = copy.GatherByWindow(i, Mathf.RoundToInt(0.5f*root.Window*asset.Framerate)).Gaussian();
                    }
                }
                {
                    float[] copy = bodyMotion.Copy();
                    for(int i=0; i<copy.Length; i++) {
                        bodyMotion[i] = copy.GatherByWindow(i, Mathf.RoundToInt(0.5f*root.Window*asset.Framerate)).Gaussian();
                    }
                }
                for(int f=0; f<asset.Frames.Length; f++) {
                    float motion = Mathf.Min(rootMotion[f], bodyMotion[f]);
                    float movement = root.GetRootLength(asset.Frames[f].Timestamp, false);
                    idle.StandardValues[f] = motion < threshold ? 1f : 0f;
                    idle.MirroredValues[f] = motion < threshold ? 1f : 0f;
                    move.StandardValues[f] = 1f - idle.StandardValues[f];
                    move.MirroredValues[f] = 1f - idle.StandardValues[f];
                    speed.StandardValues[f] = movement;
                    speed.MirroredValues[f] = movement;
                    weights[f] = Mathf.Sqrt(Mathf.Clamp(motion, 0f, threshold).Normalize(0f, threshold, 0f, 1f));
                }
                {
                    float[] copy = idle.StandardValues.Copy();
                    for(int i=0; i<copy.Length; i++) {
                        idle.StandardValues[i] = copy.GatherByWindow(i, Mathf.RoundToInt(weights[i] * 0.5f*root.Window*asset.Framerate)).Gaussian().SmoothStep(2f, 0.5f);
                        idle.MirroredValues[i] = copy.GatherByWindow(i, Mathf.RoundToInt(weights[i] * 0.5f*root.Window*asset.Framerate)).Gaussian().SmoothStep(2f, 0.5f);
                    }
                }
                {
                    float[] copy = move.StandardValues.Copy();
                    for(int i=0; i<copy.Length; i++) {
                        move.StandardValues[i] = copy.GatherByWindow(i, Mathf.RoundToInt(weights[i] * 0.5f*root.Window*asset.Framerate)).Gaussian().SmoothStep(2f, 0.5f);
                        move.MirroredValues[i] = copy.GatherByWindow(i, Mathf.RoundToInt(weights[i] * 0.5f*root.Window*asset.Framerate)).Gaussian().SmoothStep(2f, 0.5f);
                    }
                }
                {
                    float[] copy = speed.StandardValues.Copy();
                    float[] grads = copy.Gradients(asset.GetDeltaTime());
                    for(int i=0; i<speed.StandardValues.Length; i++) {
                        int padding = Mathf.RoundToInt(weights[i] * 0.5f*root.Window*asset.Framerate);
                        float power = Mathf.Abs(grads.GatherByWindow(i, padding).Gaussian());
                        speed.StandardValues[i] = copy.GatherByWindow(i, padding).Gaussian(power);
                        speed.StandardValues[i] = Mathf.Lerp(speed.StandardValues[i], 0f, idle.StandardValues[i]);

                        speed.MirroredValues[i] = copy.GatherByWindow(i, padding).Gaussian(power);
                        speed.MirroredValues[i] = Mathf.Lerp(speed.MirroredValues[i], 0f, idle.MirroredValues[i]);
                    }
                }
            }

            // {
            //     StyleModule module = asset.HasModule<StyleModule>() ? asset.GetModule<StyleModule>() : asset.AddModule<StyleModule>();
            //     module.Clear();

            //     RootModule root = asset.GetModule<RootModule>();
            //     StyleModule.StyleFunction rooted = module.AddStyle("Rooted");
            //     StyleModule.StyleFunction speed = module.AddStyle("Speed");
            //     float threshold = 0.1f;
            //     float[] rootMotion = new float[asset.Frames.Length];
            //     float[] bodyMotion = new float[asset.Frames.Length];
            //     for(int f=0; f<asset.Frames.Length; f++) {
            //         rootMotion[f] = root.GetRootVelocity(asset.Frames[f].Timestamp, false).magnitude;
            //         bodyMotion[f] = asset.Frames[f].GetBoneVelocities(Pipeline.GetEditor().GetSession().GetBoneMapping(), false).Magnitudes().Mean();
            //     }
            //     float GetMotionValue(int index) {
            //         return Mathf.Max(rootMotion[index], bodyMotion[index]);
            //     }
            //     float GetMotionWeight(int index) {
            //         return Mathf.Clamp(GetMotionValue(index), 0f, threshold).Normalize(0f, threshold, 0f, 1f).SmoothStep(2f, 0.5f);
            //     }
            //     for(int f=0; f<asset.Frames.Length; f++) {
            //         rooted.Values[f] = 1f-GetMotionWeight(f);
            //         speed.Values[f] = root.GetTranslationalSpeed(asset.Frames[f].Timestamp, false);
            //     }
            //     rooted.Values.SmoothGaussian(Mathf.RoundToInt(0.5f*root.Window*asset.Framerate));
            //     {
            //         float[] copy = speed.Values.Copy();
            //         float[] grads = copy.Gradients(asset.GetDeltaTime());
            //         for(int i=0; i<copy.Length; i++) {
            //             int padding = Mathf.RoundToInt(0.5f*root.Window*asset.Framerate);
            //             float power = Mathf.Abs(grads.GatherByWindow(i, padding).Gaussian());
            //             speed.Values[i] = Mathf.Lerp(copy.GatherByWindow(i, padding).Gaussian(power), speed.Values[i], 1f-GetMotionWeight(i));
            //         }
            //     }
            // }

            asset.MarkDirty(true, false);
        }
    }
}
#endif