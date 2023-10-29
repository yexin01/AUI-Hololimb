// Copyright (c) Meta Platforms, Inc. and affiliates.

using Oculus.Interaction;
using Oculus.Interaction.Input;
using System.Collections.Generic;
using UnityEngine;

namespace Oculus.Movement.AnimationRigging
{
    /// <summary>
    /// Retargeting processor used to fix the arm via an IK algorithm so that the retargeted hand position matches
    /// the tracked hand position.
    /// </summary>
    [CreateAssetMenu(fileName = "Correct Hand", menuName = "Movement Samples/Data/Retargeting Processors/Correct Hand", order = 1)]
    public sealed class RetargetingProcessorCorrectHand : RetargetingProcessor
    {
        /// <summary>
        /// The types of IK available to be used.
        /// </summary>
        public enum IKType
        {
            None,
            CCDIK
        }

        /// <summary>
        /// The hand that this is correcting.
        /// </summary>
        [SerializeField]
        private Handedness _handedness = Handedness.Left;
        /// <inheritdoc cref="_handedness" />
        public Handedness Handedness
        {
            get => _handedness;
            set => _handedness = value;
        }

        /// <summary>
        /// The type of IK that should be applied to modify the arm bones toward the
        /// correct hand target.
        /// </summary>
        [Tooltip(RetargetingLayerTooltips.HandIKType)]
        [SerializeField]
        private IKType _handIKType = IKType.None;
        /// <inheritdoc cref="_handIKType" />
        public IKType HandIKType
        {
            get => _handIKType;
            set => _handIKType = value;
        }

        /// <summary>
        /// The maximum distance between the resulting position and target position that is allowed.
        /// </summary>
        [Tooltip(RetargetingLayerTooltips.IKTolerance)]
        [SerializeField, ConditionalHide("_handIKType", IKType.CCDIK)]
        private float _ikTolerance = 1e-6f;
        /// <inheritdoc cref="_ikTolerance" />
        public float IKTolerance
        {
            get => _ikTolerance;
            set => _ikTolerance = value;
        }

        /// <summary>
        /// The maximum number of iterations allowed for the IK algorithm.
        /// </summary>
        [Tooltip(RetargetingLayerTooltips.IKIterations)]
        [SerializeField, ConditionalHide("_handIKType", IKType.CCDIK)]
        private int _ikIterations = 10;
        /// <inheritdoc cref="_ikIterations" />
        public int IKIterations
        {
            get => _ikIterations;
            set => _ikIterations = value;
        }

        private Transform[] _armBones;
        private Vector3 _originalHandPosition;

        /// <inheritdoc />
        public override void SetupRetargetingProcessor(RetargetingLayer retargetingLayer)
        {
            // Skip the finger bones.
            var armBones = new List<Transform>();
            var animator = retargetingLayer.GetAnimatorTargetSkeleton();

            // We iterate from the jaw downward, as the first bone is the effector, which is the hand.
            // Hand -> Lower Arm -> Upper Arm -> Shoulder.
            for (var i = HumanBodyBones.Jaw; i >= HumanBodyBones.Hips; i-- )
            {
                var boneTransform = animator.GetBoneTransform(i);
                if (boneTransform == null)
                {
                    continue;
                }

                if ((_handedness == Handedness.Left &&
                    CustomMappings.HumanBoneToAvatarBodyPart[i] == AvatarMaskBodyPart.LeftArm) ||
                    (_handedness == Handedness.Right &&
                     CustomMappings.HumanBoneToAvatarBodyPart[i] == AvatarMaskBodyPart.RightArm))
                {
                    armBones.Add(boneTransform);
                }
            }
            _armBones = armBones.ToArray();
        }

        /// <inheritdoc />
        public override void PrepareRetargetingProcessor(RetargetingLayer retargetingLayer, IList<OVRBone> ovrBones)
        {
            _originalHandPosition = _armBones[0].position;
        }

        /// <inheritdoc />
        public override void ProcessRetargetingLayer(RetargetingLayer retargetingLayer, IList<OVRBone> ovrBones)
        {
            if ((_handedness == Handedness.Left &&
                ovrBones.Count < (int)OVRSkeleton.BoneId.Body_LeftHandWrist) ||
                (_handedness == Handedness.Right &&
                ovrBones.Count < (int)OVRSkeleton.BoneId.Body_RightHandWrist))
            {
                return;
            }

            var targetHand = ovrBones[_handedness == Handedness.Left ?
                (int)OVRSkeleton.BoneId.Body_LeftHandWrist :
                (int)OVRSkeleton.BoneId.Body_RightHandWrist]?.Transform;
            if (targetHand == null)
            {
                return;
            }

            var handBone = _armBones[0];
            handBone.position = _originalHandPosition;
            var handRotation = handBone.rotation;
            Vector3 targetPosition = Vector3.Lerp(handBone.position, targetHand.position, Weight);
            if (Weight > 0.0f)
            {
                if (_handIKType == IKType.CCDIK)
                {
                    AnimationUtilities.SolveCCDIK(_armBones, targetPosition, _ikTolerance, _ikIterations);
                }
            }
            handBone.position = targetPosition;
            handBone.rotation = handRotation;
        }
    }
}
