using Unity.Mathematics;
using UnityEngine;

namespace Inertialization
{
    public class Inertialization
    {
        public quaternion[] InertializedRotations;
        public float3[] InertializedAngularVelocities;

        public float3[] InertializedPositions;
        public float3[] InertializedVelocities;
        public float3 InertializedHips;
        public float3 InertializedHipsVelocity;
        // Contacts
        public float3 InertializedLeftContact;
        public float3 InertializedLeftContactVelocity;
        public float3 InertializedRightContact;
        public float3 InertializedRightContactVelocity;

        private quaternion[] OffsetRotations;
        private float3[] OffsetAngularVelocities;
        private float3[] OffsetPositions;
        private float3[] OffsetVelocities;
        private float3 OffsetHips;
        private float3 OffsetHipsVelocity;
        // Contacts
        private float3 OffsetLeftContact;
        private float3 OffsetLeftContactVelocity;
        private float3 OffsetRightContact;
        private float3 OffsetRightContactVelocity;

        public Inertialization(int numJ)
        {
            int numJoints = numJ;
            InertializedRotations = new quaternion[numJoints];
            InertializedAngularVelocities = new float3[numJoints];
            InertializedPositions = new float3[numJoints];
            InertializedVelocities = new float3[numJoints];
            
            OffsetRotations = new quaternion[numJoints];
            for (int i = 0; i < numJoints; i++) OffsetRotations[i] = quaternion.identity; // init to a valid quaternion
            OffsetAngularVelocities = new float3[numJoints];
            OffsetPositions = new float3[numJoints];
            OffsetVelocities = new float3[numJoints];
        }

        /// <summary>
        /// It takes as input the current state of the source pose and the target pose.
        /// It sets up the inertialization, which can then by updated by calling Update(...).
        /// </summary>
        public void PoseTransition(PoseVector sourcePose, PoseVector targetPose)
        {
            // Set up the inertialization for joint local rotations (no simulation bone)
            for (int i = 0; i < sourcePose.JointLocalRotations.Length; i++)
            {
                quaternion sourceJointRotation = sourcePose.JointLocalRotations[i];
                quaternion targetJointRotation = targetPose.JointLocalRotations[i];
                float3 sourceJointAngularVelocity = sourcePose.JointLocalAngularVelocities[i];
                float3 targetJointAngularVelocity = targetPose.JointLocalAngularVelocities[i];
                InertializeJointTransition(sourceJointRotation, sourceJointAngularVelocity,
                                           targetJointRotation, targetJointAngularVelocity,
                                           ref OffsetRotations[i], ref OffsetAngularVelocities[i]);
                
                float3 sourceJointPosition = sourcePose.JointLocalPositions[i];
                float3 targetJointPosition = targetPose.JointLocalPositions[i];
                float3 sourceJointVelocity = sourcePose.JointLocalVelocities[i];
                float3 targetJointVelocity = targetPose.JointLocalVelocities[i];
                InertializeJointTransition(sourceJointPosition, sourceJointVelocity,
                                           targetJointPosition, targetJointVelocity,
                                           ref OffsetPositions[i], ref OffsetVelocities[i]);
            }
            // Set up the inertialization for hips
            float3 sourceHips = sourcePose.JointLocalPositions[1];
            float3 targetHips = targetPose.JointLocalPositions[1];
            float3 sourceHipsVelocity = sourcePose.JointLocalVelocities[1];
            float3 targetHipsVelocity = targetPose.JointLocalVelocities[1];
            InertializeJointTransition(sourceHips, sourceHipsVelocity,
                                       targetHips, targetHipsVelocity,
                                       ref OffsetHips, ref OffsetHipsVelocity);
        }

        /// <summary>
        /// It takes as input the current state of the source contact and the target contact.
        /// It sets up the inertialization, which can then by updated by calling UpdateContact(...).
        /// </summary>
        public void LeftContactTransition(float3 sourceLeftContact, float3 sourceLeftContactVelocity, float3 targetLeftContact, float3 targetLeftContactVelocity)
        {
            InertializeJointTransition(sourceLeftContact, sourceLeftContactVelocity,
                                       targetLeftContact, targetLeftContactVelocity,
                                       ref OffsetLeftContact, ref OffsetLeftContactVelocity);
        }

        /// <summary>
        /// It takes as input the current state of the source contact and the target contact.
        /// It sets up the inertialization, which can then by updated by calling UpdateContact(...).
        /// </summary>
        public void RightContactTransition(float3 sourceRightContact, float3 sourceRightContactVelocity, float3 targetRightContact, float3 targetRightContactVelocity)
        {
            InertializeJointTransition(sourceRightContact, sourceRightContactVelocity,
                                       targetRightContact, targetRightContactVelocity,
                                       ref OffsetRightContact, ref OffsetRightContactVelocity);
        }

        /// <summary>
        /// Updates the inertialization decaying the offset from the source pose (specified in PoseTransition(...))
        /// to the target pose.
        /// </summary>
        public void Update(PoseVector targetPose, float halfLife, float deltaTime)
        {
            // Update the inertialization for joint local rotations
            for (int i = 0; i < targetPose.JointLocalRotations.Length; i++)
            {
                quaternion targetJointRotation = targetPose.JointLocalRotations[i];
                float3 targetAngularVelocity = targetPose.JointLocalAngularVelocities[i];
                InertializeJointUpdate(targetJointRotation, targetAngularVelocity,
                                       halfLife, deltaTime,
                                       ref OffsetRotations[i], ref OffsetAngularVelocities[i],
                                       out InertializedRotations[i], out InertializedAngularVelocities[i]);
                
                float3 targetJointPosition = targetPose.JointLocalPositions[i];
                float3 targetJointVelocity = targetPose.JointLocalVelocities[i];
                InertializeJointUpdate(targetJointPosition, targetJointVelocity,
                                       halfLife, deltaTime,
                                       ref OffsetPositions[i], ref OffsetVelocities[i],
                                       out InertializedPositions[i], out InertializedVelocities[i]);
            }
            // Update the inertialization for hips
            float3 targetHips = targetPose.JointLocalPositions[1];
            float3 targetRootVelocity = targetPose.JointLocalVelocities[1];
            InertializeJointUpdate(targetHips, targetRootVelocity,
                                   halfLife, deltaTime,
                                   ref OffsetHips, ref OffsetHipsVelocity,
                                   out InertializedHips, out InertializedHipsVelocity);
        }

        /// <summary>
        /// Updates the inertialization decaying the offset from the source contact (specified in ContactTransition(...))
        /// to the target contact.
        /// </summary>
        public void UpdateContact(float3 leftTargetPos, float3 leftTargetVelocity, float3 rightTargetPos, float3 rightTargetVelocity, float halfLife, float deltaTime)
        {
            // Update the inertialization for contacts
            InertializeJointUpdate(leftTargetPos, leftTargetVelocity,
                                   halfLife, deltaTime,
                                   ref OffsetLeftContact, ref OffsetLeftContactVelocity,
                                   out InertializedLeftContact, out InertializedLeftContactVelocity);
            InertializeJointUpdate(rightTargetPos, rightTargetVelocity,
                                   halfLife, deltaTime,
                                   ref OffsetRightContact, ref OffsetRightContactVelocity,
                                   out InertializedRightContact, out InertializedRightContactVelocity);
        }

        /// <summary>
        /// Compute the offsets from the source pose to the target pose.
        /// Offsets are in/out since we may start a inertialization in the middle of another inertialization.
        /// </summary>
        public static void InertializeJointTransition(quaternion sourceRot, float3 sourceAngularVel,
                                                      quaternion targetRot, float3 targetAngularVel,
                                                      ref quaternion offsetRot, ref float3 offsetAngularVel)
        {
            offsetRot = math.normalizesafe(MathExtensions.Abs(math.mul(math.inverse(targetRot), math.mul(sourceRot, offsetRot))));
            offsetAngularVel = (sourceAngularVel + offsetAngularVel) - targetAngularVel;
        }
        /// <summary>
        /// Compute the offsets from the source pose to the target pose.
        /// Offsets are in/out since we may start a inertialization in the middle of another inertialization.
        /// </summary>
        public static void InertializeJointTransition(float3 source, float3 sourceVel,
                                                      float3 target, float3 targetVel,
                                                      ref float3 offset, ref float3 offsetVel)
        {
            offset = (source + offset) - target;
            offsetVel = (sourceVel + offsetVel) - targetVel;
        }
        /// <summary>
        /// Compute the offsets from the source pose to the target pose.
        /// Offsets are in/out since we may start a inertialization in the middle of another inertialization.
        /// </summary>
        public static void InertializeJointTransition(float source, float sourceVel,
                                                      float target, float targetVel,
                                                      ref float offset, ref float offsetVel)
        {
            offset = (source + offset) - target;
            offsetVel = (sourceVel + offsetVel) - targetVel;
        }

        /// <summary>
        /// Updates the inertialization decaying the offset and applying it to the target pose
        /// </summary>
        public static void InertializeJointUpdate(quaternion targetRot, float3 targetAngularVel,
                                                  float halfLife, float deltaTime,
                                                  ref quaternion offsetRot, ref float3 offsetAngularVel,
                                                  out quaternion newRot, out float3 newAngularVel)
        {
            Spring.DecaySpringDamperImplicit(ref offsetRot, ref offsetAngularVel, halfLife, deltaTime);
            newRot = math.mul(targetRot, offsetRot);
            newAngularVel = targetAngularVel + offsetAngularVel;
        }
        /// <summary>
        /// Updates the inertialization decaying the offset and applying it to the target pose
        /// </summary>
        public static void InertializeJointUpdate(float3 target, float3 targetVel,
                                                  float halfLife, float deltaTime,
                                                  ref float3 offset, ref float3 offsetVel,
                                                  out float3 newValue, out float3 newVel)
        {
            Spring.DecaySpringDamperImplicit(ref offset, ref offsetVel, halfLife, deltaTime);
            newValue = target + offset;
            newVel = targetVel + offsetVel;
        }
        /// <summary>
        /// Updates the inertialization decaying the offset and applying it to the target pose
        /// </summary>
        public static void InertializeJointUpdate(float target, float targetVel,
                                                  float halfLife, float deltaTime,
                                                  ref float offset, ref float offsetVel,
                                                  out float newValue, out float newVel)
        {
            Spring.DecaySpringDamperImplicit(ref offset, ref offsetVel, halfLife, deltaTime);
            newValue = target + offset;
            newVel = targetVel + offsetVel;
        }
    }
}