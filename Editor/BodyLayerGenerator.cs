using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace VRCFullBodyTracking
{
    public static class BodyLayerGenerator
    {
        public static void CreateBodyRotationLayer(AnimatorController animator, Animator avatarAnimator, string assetPath)
        {
            var bodyMask = AnimationUtility.CreateAvatarMask("BodyRotationMask", assetPath, mask => {
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, false);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, false);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, false);
            });

            AnimationUtility.CreateLayer(animator, "Body Rotation", (layer, stateMachine) =>
            {
                var blendTree = AnimationUtility.CreateBlendTree(animator, "BodyRotation", BlendTreeType.Simple1D, "BodyRotation");

                var neutralClip = AnimationUtility.CreateClip("BodyNeutral", assetPath + "/Animations");
                var leftClip = AnimationUtility.CreateClip("BodyLeft", assetPath + "/Animations", clip =>
                {
                    string bonePath = TrackingParameterMapper.GetCustomBonePath(HumanBodyBones.Hips) ?? 
                        AnimationUtility.GetBonePath(avatarAnimator, HumanBodyBones.Hips);
                    if (!string.IsNullOrEmpty(bonePath))
                    {
                        clip.SetCurve(bonePath, typeof(Transform), "localEulerAngles.y", new AnimationCurve(new Keyframe(0, -30f)));
                    }
                });
                var rightClip = AnimationUtility.CreateClip("BodyRight", assetPath + "/Animations", clip =>
                {
                    string bonePath = TrackingParameterMapper.GetCustomBonePath(HumanBodyBones.Hips) ?? 
                        AnimationUtility.GetBonePath(avatarAnimator, HumanBodyBones.Hips);
                    if (!string.IsNullOrEmpty(bonePath))
                    {
                        clip.SetCurve(bonePath, typeof(Transform), "localEulerAngles.y", new AnimationCurve(new Keyframe(0, 30f)));
                    }
                });

                blendTree.AddChild(leftClip, -1f);
                blendTree.AddChild(neutralClip, 0f);
                blendTree.AddChild(rightClip, 1f);

                var activeState = stateMachine.AddState("Body Rotation Active", new Vector3(300, 100, 0));
                activeState.motion = blendTree;
                activeState.writeDefaultValues = false;

                var inactiveState = stateMachine.AddState("Body Rotation Inactive", new Vector3(300, 200, 0));
                inactiveState.writeDefaultValues = false;

                // TrackingEnabledとBodyDetectedの両方が有効な場合のみアクティブ
                var toActiveTransition = inactiveState.AddTransition(activeState);
                toActiveTransition.hasExitTime = false;
                toActiveTransition.duration = 0.2f;
                toActiveTransition.AddCondition(AnimatorConditionMode.If, 0, "TrackingEnabled");
                toActiveTransition.AddCondition(AnimatorConditionMode.If, 0, "BodyDetected");

                // どちらかが無効な場合は非アクティブ
                var toInactiveTransition = activeState.AddTransition(inactiveState);
                toInactiveTransition.hasExitTime = false;
                toInactiveTransition.duration = 0.2f;
                toInactiveTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "TrackingEnabled");

                var toInactiveTransition2 = activeState.AddTransition(inactiveState);
                toInactiveTransition2.hasExitTime = false;
                toInactiveTransition2.duration = 0.2f;
                toInactiveTransition2.AddCondition(AnimatorConditionMode.IfNot, 0, "BodyDetected");

                stateMachine.defaultState = inactiveState;

                layer.avatarMask = bodyMask;
                layer.defaultWeight = 1f;
            });
        }
    }
}
