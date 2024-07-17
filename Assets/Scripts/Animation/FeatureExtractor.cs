using System;
using Accord;
using UnityEngine;


namespace AI4Animation
{
    public class FeatureExtractor
    {
        public MotionAsset Asset;
        
        public enum CHANNEL_TYPE
        {
            Positions,
            Velocities,
            Rotations,
            PhaseManifold,
            Frequencies,
            VQPhaseManifold,
            VQPhaseManifoldOri,
            State,
            Phase,
            EmbedIndex
        }

        public static CHANNEL_TYPE String2ChannelType(string name)
        {
            if (name.StartsWith("Velocities")) return CHANNEL_TYPE.Velocities;
            if (name.StartsWith("Rotations")) return CHANNEL_TYPE.Rotations;
            if (name.StartsWith("Positions")) return CHANNEL_TYPE.Positions;
            if (name.ToLower().StartsWith("vqmanifolds")) return CHANNEL_TYPE.VQPhaseManifold;
            if (name.ToLower().StartsWith("manifold")) return CHANNEL_TYPE.VQPhaseManifold;
            if (name.ToLower().Contains("phase")) return CHANNEL_TYPE.Phase;
            if (name.ToLower().Contains("state")) return CHANNEL_TYPE.State;
            else throw new Exception("Unknown channel type.");
        }

        public static int Get(MotionEditor editor, CHANNEL_TYPE channelType, string tag,
            ref float[] buffer)
        {
            var session = editor.GetSession();
            var asset = session.Asset;
            var mirror = editor.Mirror;
            var frameIdx = editor.GetCurrentFrame().Index - 1;
            var actor = session.GetActor();
            return Get(asset, mirror, channelType, tag, frameIdx, actor, ref buffer);
        }

        public static int GetSize(MotionAsset asset, Actor actor, CHANNEL_TYPE channelType, string tag)
        {
            int[] mapping = actor != null ? asset.Source.GetBoneIndices(actor.GetBoneNames()) : null;
            switch (channelType)
            {
                case CHANNEL_TYPE.Positions:
                    return mapping.Length * 3;
                case CHANNEL_TYPE.Velocities:
                    return mapping.Length * 3;
                case CHANNEL_TYPE.Rotations:
                    return mapping.Length * 9;
                case CHANNEL_TYPE.PhaseManifold:
                {
                    var phaseModule = asset.GetModule<DeepPhaseModule>();
                    if (phaseModule == null)
                    {
                        Debug.Log($"Phase module on motion asset is null.");
                        return 0;
                    }
                    return phaseModule.GetManifold(asset.Frames[0].Timestamp, false).Length * 2;
                }
                case CHANNEL_TYPE.Frequencies:
                {
                    var phaseModule = asset.GetModule<DeepPhaseModule>(tag);
                    if (phaseModule == null)
                    {
                        Debug.Log($"Phase module on motion asset is null.");
                        return 0;
                    }
                    return phaseModule.GetFrequencies(asset.Frames[0].Timestamp, false).Length;
                }
                case CHANNEL_TYPE.VQPhaseManifold:
                case CHANNEL_TYPE.VQPhaseManifoldOri:
                {
                    var vqPhaseModule = asset.GetModule<VQPhaseModule>(tag);
                    if (vqPhaseModule == null)
                    {
                        Debug.Log($"VQPhase module on motion asset is null.");
                        return 0;
                    }

                    return vqPhaseModule.ManifoldSize;
                }
                case CHANNEL_TYPE.Phase:
                {
                    return 1;
                }
                case CHANNEL_TYPE.State:
                {
                    var vqPhaseModule = asset.GetModule<VQPhaseModule>(tag);
                    if (vqPhaseModule == null)
                    {
                        Debug.Log($"VQPhase module on motion asset is null.");
                        return 0;
                    }

                    return vqPhaseModule.RegularState.shape[1];
                }
            }

            return 0;
        }

        // Return will be a 1D array with shape (numFrames, numFeatures)
        public static int GetWindow(MotionAsset asset, bool mirrored, CHANNEL_TYPE channelType, string tag,
            int frameStart, int frameEnd, Actor actor, Span<float> buffer)
        {
            if (asset == null) return 0;
            var sizePerFrame = GetSize(asset, actor, channelType, tag);
            int totalSize = 0;
            for (int i = 0; i < frameEnd - frameStart; i++)
            {
                int frameClamped = Math.Clamp(i + frameStart, 0, asset.Frames.Length - 1);
                totalSize += GetInplace(asset, mirrored, channelType, tag, frameClamped, actor, buffer.Slice(i * sizePerFrame, sizePerFrame));
            }

            return totalSize;
        }
        
        public static int GetInplace(MotionAsset asset, bool mirrored, CHANNEL_TYPE channelType, string tag,
            int frameIdx, Actor actor, Span<float> buffer)
        {
            if (asset == null) return 0;
            frameIdx = Math.Clamp(frameIdx, 0, asset.Frames.Length - 1);
            RootModule rootModule = asset.GetModule<RootModule>();
            int[] mapping = actor != null ? asset.Source.GetBoneIndices(actor.GetBoneNames()) : null;
            int length = 0;

            switch (channelType)
            {
                case CHANNEL_TYPE.Positions:
                {
                    Matrix4x4 spaceC = rootModule.GetRootTransformation(asset.Frames[frameIdx].Timestamp, mirrored);
                    for (int i = 0; i < mapping.Length; i++)
                    {
                        Vector3 posC = asset.Frames[frameIdx].GetBoneTransformation(mapping[i], mirrored).GetPosition();
                        Vector3 relativePosition = posC.PositionTo(spaceC);
                        buffer[length++] = relativePosition.x;
                        buffer[length++] = relativePosition.y;
                        buffer[length++] = relativePosition.z;
                    }
                    break;
                }
                case CHANNEL_TYPE.Velocities:
                {
                    Matrix4x4 spaceP =
                        rootModule.GetRootTransformation(asset.Frames[Mathf.Max(frameIdx - 1, 0)].Timestamp, mirrored);
                    Matrix4x4 spaceC = rootModule.GetRootTransformation(asset.Frames[frameIdx].Timestamp, mirrored);
                    for (int i = 0; i < mapping.Length; i++)
                    {
                        Vector3 posP = asset.Frames[Mathf.Max(frameIdx - 1, 0)].GetBoneTransformation(mapping[i], mirrored)
                            .GetPosition();
                        Vector3 posC = asset.Frames[frameIdx].GetBoneTransformation(mapping[i], mirrored).GetPosition();
                        Vector3 velo = (posC.PositionTo(spaceC) - posP.PositionTo(spaceP)) / asset.GetDeltaTime();

                        buffer[length++] = velo.x;
                        buffer[length++] = velo.y;
                        buffer[length++] = velo.z;
                    }
                    break;
                }
                case CHANNEL_TYPE.Rotations:
                {
                    Matrix4x4 spaceC = rootModule.GetRootTransformation(asset.Frames[frameIdx].Timestamp, mirrored);
                    for (int i = 0; i < mapping.Length; i++)
                    {
                        Matrix4x4 jointSpace = asset.Frames[frameIdx].GetBoneTransformation(mapping[i], mirrored);
                        Matrix4x4 relativeSpace = jointSpace.TransformationTo(spaceC);
                        buffer[length++] = relativeSpace.m00;
                        buffer[length++] = relativeSpace.m01;
                        buffer[length++] = relativeSpace.m02;
                        buffer[length++] = relativeSpace.m10;
                        buffer[length++] = relativeSpace.m11;
                        buffer[length++] = relativeSpace.m12;
                        buffer[length++] = relativeSpace.m20;
                        buffer[length++] = relativeSpace.m21;
                        buffer[length++] = relativeSpace.m22;
                    }
                    break;
                }
                case CHANNEL_TYPE.PhaseManifold:
                {
                    throw new Exception("Inplace get of phase manifold is not implemented.");
                }
                case CHANNEL_TYPE.Frequencies:
                {
                    throw new Exception("Inplace get of phase frequency is not implemented.");

                }
                case CHANNEL_TYPE.VQPhaseManifold:
                {
                    var vqPhaseModule = asset.GetModule<VQPhaseModule>(tag);
                    if (vqPhaseModule == null)
                    {
                        Debug.Log($"VQPhase module on motion asset is null.");
                        return 0;
                    }

                    length = vqPhaseModule.GetManifold(asset.Frames[frameIdx].Timestamp, mirrored, buffer);
                    break;
                }
                case CHANNEL_TYPE.VQPhaseManifoldOri:
                {
                    var vqPhaseModule = asset.GetModule<VQPhaseModule>(tag);
                    if (vqPhaseModule == null)
                    {
                        Debug.Log($"VQPhase module on motion asset is null.");
                        return 0;
                    }

                    length = vqPhaseModule.GetManifoldOri(asset.Frames[frameIdx].Timestamp, mirrored, buffer);
                    break;
                }
                case CHANNEL_TYPE.Phase:
                {
                    var vqPhaseModule = asset.GetModule<VQPhaseModule>(tag);
                    if (vqPhaseModule == null)
                    {
                        Debug.Log($"Phase module on motion asset is null.");
                        return 0;
                    }
                    
                    length = vqPhaseModule.GetPhase(asset.Frames[frameIdx].Timestamp, mirrored, buffer);
                    break;
                }
                case CHANNEL_TYPE.State:
                {
                    var vqPhaseModule = asset.GetModule<VQPhaseModule>(tag);
                    if (vqPhaseModule == null)
                    {
                        Debug.Log($"State module on motion asset is null.");
                        return 0;
                    }
                    
                    length = vqPhaseModule.GetState(asset.Frames[frameIdx].Timestamp, mirrored, buffer);
                    break;
                }
                case CHANNEL_TYPE.EmbedIndex:
                {
                    var vqPhaseModule = asset.GetModule<VQPhaseModule>(tag);
                    if (vqPhaseModule == null)
                    {
                        Debug.Log($"State module on motion asset is null.");
                        return 0;
                    }
                    
                    length = vqPhaseModule.GetEmbedIndex(asset.Frames[frameIdx].Timestamp, mirrored, buffer);
                    break;
                }
            }

            return length;
        }

        public static int Get(MotionAsset asset, bool mirrored, CHANNEL_TYPE channelType, string tag,
            int frameIdx, Actor actor, ref float[] buffer)
        {
            if (asset == null) return 0;
            RootModule rootModule = asset.GetModule<RootModule>();
            int[] mapping = actor != null ? asset.Source.GetBoneIndices(actor.GetBoneNames()) : null;
            int length = 0;

            switch (channelType)
            {
                case CHANNEL_TYPE.Positions:
                {
                    if (buffer == null) buffer = new float[mapping.Length * 3];
                    Matrix4x4 spaceC = rootModule.GetRootTransformation(asset.Frames[frameIdx].Timestamp, mirrored);
                    for (int i = 0; i < mapping.Length; i++)
                    {
                        Vector3 posC = asset.Frames[frameIdx].GetBoneTransformation(mapping[i], mirrored).GetPosition();
                        Vector3 relativePosition = posC.PositionTo(spaceC);
                        buffer[length++] = relativePosition.x;
                        buffer[length++] = relativePosition.y;
                        buffer[length++] = relativePosition.z;
                    }
                    break;
                }
                case CHANNEL_TYPE.Velocities:
                {
                    if (buffer == null) buffer = new float[mapping.Length * 3];
                    Matrix4x4 spaceP =
                        rootModule.GetRootTransformation(asset.Frames[Mathf.Max(frameIdx - 1, 0)].Timestamp, mirrored);
                    Matrix4x4 spaceC = rootModule.GetRootTransformation(asset.Frames[frameIdx].Timestamp, mirrored);
                    for (int i = 0; i < mapping.Length; i++)
                    {
                        Vector3 posP = asset.Frames[Mathf.Max(frameIdx - 1, 0)].GetBoneTransformation(mapping[i], mirrored)
                            .GetPosition();
                        Vector3 posC = asset.Frames[frameIdx].GetBoneTransformation(mapping[i], mirrored).GetPosition();
                        Vector3 velo = (posC.PositionTo(spaceC) - posP.PositionTo(spaceP)) / asset.GetDeltaTime();

                        buffer[length++] = velo.x;
                        buffer[length++] = velo.y;
                        buffer[length++] = velo.z;
                    }
                    break;
                }
                case CHANNEL_TYPE.Rotations:
                {
                    if (buffer == null) buffer = new float[mapping.Length * 9];
                    Matrix4x4 spaceC = rootModule.GetRootTransformation(asset.Frames[frameIdx].Timestamp, mirrored);
                    for (int i = 0; i < mapping.Length; i++)
                    {
                        Matrix4x4 jointSpace = asset.Frames[frameIdx].GetBoneTransformation(mapping[i], mirrored);
                        Matrix4x4 relativeSpace = jointSpace.TransformationTo(spaceC);
                        buffer[length++] = relativeSpace.m00;
                        buffer[length++] = relativeSpace.m01;
                        buffer[length++] = relativeSpace.m02;
                        buffer[length++] = relativeSpace.m10;
                        buffer[length++] = relativeSpace.m11;
                        buffer[length++] = relativeSpace.m12;
                        buffer[length++] = relativeSpace.m20;
                        buffer[length++] = relativeSpace.m21;
                        buffer[length++] = relativeSpace.m22;
                    }
                    break;
                }
                case CHANNEL_TYPE.PhaseManifold:
                {
                    var phaseModule = asset.GetModule<DeepPhaseModule>(tag);
                    if (phaseModule == null)
                    {
                        Debug.Log($"Phase module on motion asset is null.");
                        return 0;
                    }

                    var phaseManifold = phaseModule.GetManifold(asset.Frames[frameIdx].Timestamp, mirrored);
                    if (buffer == null) buffer = new float[2 * phaseManifold.Length];
                    for (int k = 0; k < phaseManifold.Length; k++)
                    {
                        buffer[length++] = phaseManifold[k].x;
                        buffer[length++] = phaseManifold[k].y;
                    }
                    break;
                }
                case CHANNEL_TYPE.Frequencies:
                {
                    var phaseModule = asset.GetModule<DeepPhaseModule>(tag);
                    if (phaseModule == null)
                    {
                        Debug.Log($"Phase module on motion asset is null.");
                        return 0;
                    }
                    
                    buffer = phaseModule.GetFrequencies(asset.Frames[frameIdx].Timestamp, mirrored);
                    length = buffer.Length;
                    break;
                }
                case CHANNEL_TYPE.VQPhaseManifold:
                {
                    var vqPhaseModule = asset.GetModule<VQPhaseModule>(tag);
                    if (vqPhaseModule == null)
                    {
                        Debug.Log($"VQPhase module on motion asset is null.");
                        return 0;
                    }

                    buffer = vqPhaseModule.GetManifold(asset.Frames[frameIdx].Timestamp, mirrored);
                    length = buffer.Length;
                    break;
                }
                case CHANNEL_TYPE.VQPhaseManifoldOri:
                {
                    var vqPhaseModule = asset.GetModule<VQPhaseModule>(tag);
                    if (vqPhaseModule == null)
                    {
                        Debug.Log($"VQPhase module on motion asset is null.");
                        return 0;
                    }

                    buffer = vqPhaseModule.GetManifoldOri(asset.Frames[frameIdx].Timestamp, mirrored);
                    length = buffer.Length;
                    break;
                }
            }

            return length;
        }
        
        public int Get(bool mirrored, CHANNEL_TYPE channelType, string tag,
            Frame frame, Actor actor, ref float[] buffer)
        {
            return Get(Asset, mirrored, channelType, tag, frame.Index - 1, actor, ref buffer);
        }
    }
}