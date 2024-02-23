// Copyright (c) Meta Platforms, Inc. and affiliates.

using Oculus.Interaction;
using Oculus.Interaction.Input;
using System.Collections.Generic;
using UnityEngine;
using static OVRUnityHumanoidSkeletonRetargeter;

namespace Oculus.Movement.AnimationRigging
{
    /// <summary>
    /// Increases hand accuracy as they appear into view.
    /// </summary>
    [CreateAssetMenu(fileName = "Blend Hands", menuName = "Movement Samples/Data/Retargeting Processors/Blend Hands", order = 2)]

    public sealed class BlendHandConstraintProcessor : RetargetingProcessor
    {
        /// <summary>
        /// Distance where constraints are set to 1.0.
        /// </summary>
        [SerializeField]
        [Tooltip(BlendHandConstraintProcessorTooltips.ConstraintsMinDistance)]
        private float _constraintsMinDistance = 0.2f;
        public float ConstraintsMinDistance
        {
            get => _constraintsMinDistance;
            set => _constraintsMinDistance = value;
        }

        /// <summary>
        /// Distance where constraints are set to 0.0.
        /// </summary>
        [SerializeField]
        [Tooltip(BlendHandConstraintProcessorTooltips.ConstraintsMaxDistance)]
        private float _constraintsMaxDistance = 0.5f;
        public float ConstraintsMaxDistance
        {
            get => _constraintsMaxDistance;
            set => _constraintsMaxDistance = value;
        }

        /// <summary>
        /// Multiplier that influences weight interpolation based on distance.
        /// </summary>
        [SerializeField]
        [Tooltip(BlendHandConstraintProcessorTooltips.BlendCurve)]
        private AnimationCurve _blendCurve;
        public AnimationCurve BlendCurve
        {
            get => _blendCurve;
            set => _blendCurve = value;
        }

        /// <summary>
        /// (Full Body) Bone ID, usually the wrist. Can be modified depending
        /// on the skeleton used.
        /// </summary>
        [SerializeField, ConditionalHide("_isFullBody", true)]
        [Tooltip(BlendHandConstraintProcessorTooltips.FullBodyBoneIdToTest)]
        private OVRHumanBodyBonesMappings.FullBodyTrackingBoneId _fullBodyBoneIdToTest =
            OVRHumanBodyBonesMappings.FullBodyTrackingBoneId.FullBody_LeftHandWrist;
        public OVRHumanBodyBonesMappings.FullBodyTrackingBoneId FullBodyBoneIdToTest
        {
            get => _fullBodyBoneIdToTest;
            set => _fullBodyBoneIdToTest = value;
        }

        /// <summary>
        /// Bone ID, usually the wrist. Can be modified depending
        /// on the skeleton used.
        /// </summary>
        [SerializeField, ConditionalHide("_isFullBody", false)]
        [Tooltip(BlendHandConstraintProcessorTooltips.BoneIdToTest)]
        private OVRHumanBodyBonesMappings.BodyTrackingBoneId _boneIdToTest =
            OVRHumanBodyBonesMappings.BodyTrackingBoneId.Body_LeftHandWrist;
        public OVRHumanBodyBonesMappings.BodyTrackingBoneId BoneIdToTest
        {
            get => _boneIdToTest;
            set => _boneIdToTest = value;
        }

        /// <summary>
        /// Specifies if this is full body or not.
        /// </summary>
        [SerializeField]
        [Tooltip(BlendHandConstraintProcessorTooltips.IsFullBody)]
        private bool _isFullBody;
        public bool IsFullBody
        {
            get => _isFullBody;
            set => _isFullBody = value;
        }

        private RetargetingProcessorCorrectBones _retargetingProcessorCorrectBones;
        private RetargetingProcessorCorrectHand _retargetingProcessorCorrectHand;
        private Transform _cachedTransform;
        private Transform _cachedHeadTransform;
        private float _cachedWeight;

        /// <inheritdoc />
        public override void CopyData(RetargetingProcessor source)
        {
            Weight = source.Weight;
            var sourceBlendHand = source as BlendHandConstraintProcessor;

            _constraintsMinDistance = sourceBlendHand.ConstraintsMinDistance;
            _constraintsMaxDistance = sourceBlendHand.ConstraintsMaxDistance;
            _blendCurve = sourceBlendHand.BlendCurve;
            _fullBodyBoneIdToTest = sourceBlendHand._fullBodyBoneIdToTest;
            _boneIdToTest = sourceBlendHand._boneIdToTest;
            _isFullBody = sourceBlendHand._isFullBody;
        }

        /// <inheritdoc />
        public override void SetupRetargetingProcessor(RetargetingLayer retargetingLayer)
        {
            // Make sure our processor is before others.
            bool foundOurProcessor = false;

            foreach (var retargetingProcessor in retargetingLayer.RetargetingProcessors)
            {
                var blendHandProcessor = retargetingProcessor as BlendHandConstraintProcessor;
                if (blendHandProcessor != null && blendHandProcessor == this)
                {
                    foundOurProcessor = true;
                }

                var correctBonesProcessor = retargetingProcessor as RetargetingProcessorCorrectBones;
                if (correctBonesProcessor != null)
                {
                    _retargetingProcessorCorrectBones = correctBonesProcessor;
                    if (!foundOurProcessor)
                    {
                        Debug.LogWarning($"{this.GetType()} should be before {correctBonesProcessor} in processor " +
                            $"stack as it needs to affect it.");
                    }
                    continue;
                }

                var correctHandProcessor = retargetingProcessor as RetargetingProcessorCorrectHand;
                if (correctHandProcessor != null)
                {
                    if (correctHandProcessor.Handedness == Handedness.Left && IsLeftSideOfBody())
                    {
                        _retargetingProcessorCorrectHand = correctHandProcessor;
                    }
                    else if (correctHandProcessor.Handedness == Handedness.Right && !IsLeftSideOfBody())
                    {
                        _retargetingProcessorCorrectHand = correctHandProcessor;
                    }
                    if (!foundOurProcessor)
                    {
                        Debug.LogWarning($"{this.GetType()} should be before {correctHandProcessor} in processor " +
                            $"stack as it needs to affect it.");
                    }
                }
            }
        }

        /// <inheritdoc />
        public override void PrepareRetargetingProcessor(RetargetingLayer retargetingLayer, IList<OVRBone> ovrBones)
        {
        }

        /// <inheritdoc />
        public override void ProcessRetargetingLayer(RetargetingLayer retargetingLayer, IList<OVRBone> ovrBones)
        {
            if (!BonesAreValid(retargetingLayer))
            {
                return;
            }

            float blendWeight = ComputeCurrentBlendWeight(retargetingLayer);
            // the weight of this processor can be used to reduce its effect
            if (Weight < float.Epsilon)
            {
                blendWeight = 1.0f;
            }
            
            if (IsLeftSideOfBody())
            {
                _retargetingProcessorCorrectBones.LeftHandCorrectionWeightLateUpdate = blendWeight;
            }
            else
            {
                _retargetingProcessorCorrectBones.RightHandCorrectionWeightLateUpdate = blendWeight;
            }
            if (_retargetingProcessorCorrectHand != null)
            {
                _retargetingProcessorCorrectHand.Weight = blendWeight;
            }
        }

        private bool IsLeftSideOfBody()
        {
            if (IsFullBody)
            {
                return _fullBodyBoneIdToTest < OVRHumanBodyBonesMappings.FullBodyTrackingBoneId.FullBody_RightHandPalm;
            }
            return _boneIdToTest < OVRHumanBodyBonesMappings.BodyTrackingBoneId.Body_RightHandPalm;
        }

        public Handedness GetHandedness()
        {
            return IsLeftSideOfBody() ? Handedness.Left : Handedness.Right;
        }

        private bool BonesAreValid(OVRSkeleton skeleton)
        {
            return IsFullBody ? skeleton.Bones.Count >= (int)_fullBodyBoneIdToTest : skeleton.Bones.Count >= (int)_boneIdToTest;
        }

        public override void DrawGizmos()
        {
            if (_cachedTransform == null || _cachedHeadTransform == null)
            {
                return;
            }
            var viewVector = GetViewVector();
            var viewOriginToPointVector = GetViewOriginToPointVector(_cachedTransform.position);

            Gizmos.color = new Color(_cachedWeight, _cachedWeight, _cachedWeight);
            Gizmos.DrawRay(_cachedHeadTransform.position, viewOriginToPointVector);

            Gizmos.color = Color.white;
            Gizmos.DrawRay(_cachedHeadTransform.transform.position, 0.7f * viewVector);
        }

        private float ComputeCurrentBlendWeight(RetargetingLayer skeleton)
        {
            var bones = skeleton.Bones;
            var boneToTest = bones[GetBoneIndex()].Transform;
            _cachedTransform = boneToTest;
            _cachedHeadTransform = skeleton.GetAnimatorTargetSkeleton().GetBoneTransform(HumanBodyBones.Head);
            var boneDistanceToViewVector = GetDistanceToViewVector(boneToTest.position);

            _cachedWeight = 0.0f;
            var scaledMaxDistance = _constraintsMaxDistance * skeleton.transform.lossyScale.x;
            var scaledMinDistance = _constraintsMinDistance * skeleton.transform.lossyScale.x;
            // If the hand is close enough to the view vector, start to increase the
            // weight.
            if (boneDistanceToViewVector <= scaledMaxDistance)
            {
                if (boneDistanceToViewVector <= scaledMinDistance)
                {
                    _cachedWeight = 1.0f;
                }
                else
                {
                    float lerpValue = (scaledMaxDistance - boneDistanceToViewVector) /
                        (scaledMaxDistance - scaledMinDistance);
                    float curveValue = _blendCurve.Evaluate(lerpValue);
                    // amplify lerping before clamping it
                    _cachedWeight = Mathf.Clamp01(curveValue);
                }
            }

            return _cachedWeight;
        }

        private int GetBoneIndex()
        {
            return IsFullBody ? (int)_fullBodyBoneIdToTest : (int)_boneIdToTest;
        }

        /// <summary>
        /// This is solved via the cross product method, as seen here:
        /// https://qc.edu.hk/math/Advanced%20Level/Point_to_line.htm.
        /// What this function does is create a parallelogram that is located
        /// at the view origin, and has two properties: the base (view vector)
        /// and height (distance from view vector to point passed as an
        /// argument). The magnitude of the cross product of the view vector
        /// and (point - viewOrigin) gives us the area of the parallelogram.
        /// To solve for the height, we divide the area by the magnitude of the base
        /// vector.
        /// </summary>=
        /// <param name="point">Point to evaluate</param>
        /// <returns></returns>
        private float GetDistanceToViewVector(Vector3 point)
        {
            var viewVector = GetViewVector();
            var viewOriginToPointVector = GetViewOriginToPointVector(point);

            var parallelogramArea = Vector3.Cross(viewVector, viewOriginToPointVector).magnitude;
            var parallelogramBaseLength = viewVector.magnitude;
            return parallelogramArea / parallelogramBaseLength;
        }

        private Vector3 GetViewVector()
        {
            return _cachedHeadTransform.forward;
        }

        private Vector3 GetViewOriginToPointVector(Vector3 point)
        {
            var viewOrigin = _cachedHeadTransform.position;
            return point - viewOrigin;
        }
    }
}
