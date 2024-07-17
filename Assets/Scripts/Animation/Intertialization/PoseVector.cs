using System.Collections;
using System.Collections.Generic;
using MathNet.Numerics;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace Inertialization
{
    public struct PoseVector
    {
        // The first element is the SimulationBone (added artificially), and the rest are the bones of the original skeleton
        public float3[] JointLocalPositions;
        public quaternion[] JointLocalRotations;
        public float3[] JointLocalVelocities; // Computed from World Positions
        public float3[] JointLocalAngularVelocities; // Computed from World Rotations
        public bool LeftFootContact; // True if the foot is in contact with the ground, false otherwise
        public bool RightFootContact;
        
        public int NumJoint => JointLocalPositions.Length;

        public PoseVector(int numJoint)
        {
            JointLocalPositions = new float3[numJoint];
            JointLocalRotations = new quaternion[numJoint];
            JointLocalVelocities = new float3[numJoint];
            JointLocalAngularVelocities = new float3[numJoint];
            LeftFootContact = false;
            RightFootContact = false;
        }

        public PoseVector(float3[] jointLocalPositions, quaternion[] jointLocalRotations,
            float3[] jointLocalVelocities, float3[] jointLocalAngularVelocities,
            bool leftFootContact, bool rightFootContact)
        {
            JointLocalPositions = jointLocalPositions;
            JointLocalRotations = jointLocalRotations;
            JointLocalVelocities = jointLocalVelocities;
            JointLocalAngularVelocities = jointLocalAngularVelocities;
            LeftFootContact = leftFootContact;
            RightFootContact = rightFootContact;
        }
        
        public PoseVector(Vector3[] jointLocalPositions, Quaternion[] jointLocalRotations,
            Vector3[] jointLocalVelocities, Vector3[] jointLocalAngularVelocities,
            bool leftFootContact, bool rightFootContact)
        {
            JointLocalPositions = new float3[jointLocalPositions.Length];
            JointLocalRotations = new quaternion[jointLocalRotations.Length];
            JointLocalVelocities = new float3[jointLocalVelocities.Length];
            JointLocalAngularVelocities = new float3[jointLocalAngularVelocities.Length];
            for (int i = 0; i < jointLocalPositions.Length; i++)
            {
                JointLocalPositions[i] = jointLocalPositions[i];
                JointLocalRotations[i] = jointLocalRotations[i];
                JointLocalVelocities[i] = jointLocalVelocities[i];
                JointLocalAngularVelocities[i] = jointLocalAngularVelocities[i];
            }
            LeftFootContact = leftFootContact;
            RightFootContact = rightFootContact;
        }

        public static PoseVector Interpolate(PoseVector a, PoseVector b, float t)
        {
            var numJoint = a.NumJoint;
            PoseVector res = new PoseVector(numJoint);
            for (int i = 0; i < numJoint; i++)
            {
                res.JointLocalPositions[i] = lerp(a.JointLocalPositions[i], b.JointLocalPositions[i], t);
                res.JointLocalRotations[i] = slerp(a.JointLocalRotations[i], b.JointLocalRotations[i], t);
                res.JointLocalVelocities[i] = lerp(a.JointLocalVelocities[i], b.JointLocalVelocities[i], t);
                res.JointLocalAngularVelocities[i] = lerp(a.JointLocalAngularVelocities[i], b.JointLocalAngularVelocities[i], t);
            }

            return res;
        }
    }
}