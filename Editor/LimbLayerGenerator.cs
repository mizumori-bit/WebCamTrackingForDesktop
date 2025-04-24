using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

namespace VRCFullBodyTracking
{
    public static class LimbLayerGenerator
    {
        public static void CreateArmPositionLayers(AnimatorController animator, Animator avatarAnimator, string assetPath)
        {
            var armBones = new Dictionary<string, HumanBodyBones>
            {
                { "Left", HumanBodyBones.LeftUpperArm },
                { "Right", HumanBodyBones.RightUpperArm }
            };

            var armMasks = CreateArmMasks(assetPath);

            foreach (var side in armBones.Keys)
            {
                AnimationUtility.CreateLayer(animator, $"{side} Arm Position", (layer, stateMachine) =>
                {
                    var blendTree = AnimationUtility.CreateBlendTree(animator, $"{side}ArmBlend", BlendTreeType.SimpleDirectional2D);
                    blendTree.blendParameter = $"{side}ArmX";
                    blendTree.blendParameterY = $"{side}ArmHeight";

                    var bone = armBones[side];
                    var neutralClip = AnimationUtility.CreateClip($"{side}ArmNeutral", assetPath + "/Animations");
                    var upClip = CreateArmPositionClip($"{side}ArmUp", avatarAnimator, bone, Vector3.up, assetPath);
                    var downClip = CreateArmPositionClip($"{side}ArmDown", avatarAnimator, bone, Vector3.down, assetPath);
                    var forwardClip = CreateArmPositionClip($"{side}ArmForward", avatarAnimator, bone, Vector3.forward, assetPath);
                    var backClip = CreateArmPositionClip($"{side}ArmBack", avatarAnimator, bone, Vector3.back, assetPath);

                    blendTree.AddChild(neutralClip, Vector2.zero);
                    blendTree.AddChild(upClip, Vector2.up);
                    blendTree.AddChild(downClip, Vector2.down);
                    blendTree.AddChild(forwardClip, Vector2.right);
                    blendTree.AddChild(backClip, Vector2.left);

                    CreateLimbStates(stateMachine, blendTree, side, "Arm", layer, armMasks[side]);
                });
            }
        }

        public static void CreateLegPositionLayers(AnimatorController animator, Animator avatarAnimator, string assetPath)
        {
            var legBones = new Dictionary<string, HumanBodyBones>
            {
                { "Left", HumanBodyBones.LeftUpperLeg },
                { "Right", HumanBodyBones.RightUpperLeg }
            };

            var legMasks = CreateLegMasks(assetPath);

            foreach (var side in legBones.Keys)
            {
                AnimationUtility.CreateLayer(animator, $"{side} Leg Position", (layer, stateMachine) =>
                {
                    var blendTree = AnimationUtility.CreateBlendTree(animator, $"{side}LegBlend", BlendTreeType.Simple1D);
                    blendTree.blendParameter = $"{side}LegLift";

                    var bone = legBones[side];
                    var neutralClip = AnimationUtility.CreateClip($"{side}LegNeutral", assetPath + "/Animations");
                    var liftedClip = CreateLegLiftClip($"{side}LegLifted", avatarAnimator, bone, assetPath);

                    blendTree.AddChild(neutralClip, 0f);
                    blendTree.AddChild(liftedClip, 1f);

                    CreateLimbStates(stateMachine, blendTree, side, "Leg", layer, legMasks[side]);
                });
            }
        }

        private static void CreateLimbStates(AnimatorStateMachine stateMachine, Motion motion, string side, string limbType, AnimatorControllerLayer layer, AvatarMask mask)
        {
            var activeState = stateMachine.AddState($"{side} {limbType} Movement Active", new Vector3(300, 100, 0));
            activeState.motion = motion;
            activeState.writeDefaultValues = false;

            var inactiveState = stateMachine.AddState($"{side} {limbType} Movement Inactive", new Vector3(300, 200, 0));
            inactiveState.writeDefaultValues = false;

            string detectionParam = side + limbType + "Detected";

            // 検出時のみアクティブになる遷移
            var toActiveTransition = inactiveState.AddTransition(activeState);
            toActiveTransition.hasExitTime = false;
            toActiveTransition.duration = 0.2f;
            toActiveTransition.AddCondition(AnimatorConditionMode.If, 0, "TrackingEnabled");
            toActiveTransition.AddCondition(AnimatorConditionMode.If, 0, detectionParam);

            // 非検出時は非アクティブになる遷移
            var toInactiveTransition = activeState.AddTransition(inactiveState);
            toInactiveTransition.hasExitTime = false;
            toInactiveTransition.duration = 0.2f;
            toInactiveTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "TrackingEnabled");

            var toInactiveTransition2 = activeState.AddTransition(inactiveState);
            toInactiveTransition2.hasExitTime = false;
            toInactiveTransition2.duration = 0.2f;
            toInactiveTransition2.AddCondition(AnimatorConditionMode.IfNot, 0, detectionParam);

            stateMachine.defaultState = inactiveState;

            layer.avatarMask = mask;
            layer.defaultWeight = 1f;
        }

        private static Dictionary<string, AvatarMask> CreateArmMasks(string assetPath)
        {
            var armMasks = new Dictionary<string, AvatarMask>();
            foreach (var side in new[] { "Left", "Right" })
            {
                armMasks[side] = AnimationUtility.CreateAvatarMask($"{side}ArmMask", assetPath, mask => {
                    mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
                    mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, false);
                    mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, false);
                    mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
                    mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
                    mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, side == "Left");
                    mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, side == "Right");
                });
            }
            return armMasks;
        }

        private static Dictionary<string, AvatarMask> CreateLegMasks(string assetPath)
        {
            var legMasks = new Dictionary<string, AvatarMask>();
            foreach (var side in new[] { "Left", "Right" })
            {
                legMasks[side] = AnimationUtility.CreateAvatarMask($"{side}LegMask", assetPath, mask => {
                    mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
                    mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, false);
                    mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, false);
                    mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, side == "Left");
                    mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, side == "Right");
                    mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, false);
                    mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, false);
                });
            }
            return legMasks;
        }

        private static AnimationClip CreateArmPositionClip(string name, Animator avatarAnimator, HumanBodyBones bone, Vector3 direction, string assetPath)
        {
            return AnimationUtility.CreateClip(name, assetPath + "/Animations", clip => {
                string bonePath = TrackingParameterMapper.GetCustomBonePath(bone) ?? 
                    AnimationUtility.GetBonePath(avatarAnimator, bone);
                if (!string.IsNullOrEmpty(bonePath))
                {
                    if (bone == HumanBodyBones.LeftUpperArm)
                    {
                        if (direction == Vector3.up)
                            clip.SetCurve(bonePath, typeof(Transform), "localEulerAngles.z", new AnimationCurve(new Keyframe(0, -90f)));
                        else if (direction == Vector3.down)
                            clip.SetCurve(bonePath, typeof(Transform), "localEulerAngles.z", new AnimationCurve(new Keyframe(0, 90f)));
                        else if (direction == Vector3.forward)
                            clip.SetCurve(bonePath, typeof(Transform), "localEulerAngles.x", new AnimationCurve(new Keyframe(0, -60f)));
                        else if (direction == Vector3.back)
                            clip.SetCurve(bonePath, typeof(Transform), "localEulerAngles.x", new AnimationCurve(new Keyframe(0, 60f)));
                    }
                    else if (bone == HumanBodyBones.RightUpperArm)
                    {
                        if (direction == Vector3.up)
                            clip.SetCurve(bonePath, typeof(Transform), "localEulerAngles.z", new AnimationCurve(new Keyframe(0, 90f)));
                        else if (direction == Vector3.down)
                            clip.SetCurve(bonePath, typeof(Transform), "localEulerAngles.z", new AnimationCurve(new Keyframe(0, -90f)));
                        else if (direction == Vector3.forward)
                            clip.SetCurve(bonePath, typeof(Transform), "localEulerAngles.x", new AnimationCurve(new Keyframe(0, -60f)));
                        else if (direction == Vector3.back)
                            clip.SetCurve(bonePath, typeof(Transform), "localEulerAngles.x", new AnimationCurve(new Keyframe(0, 60f)));
                    }
                }
            });
        }

        private static AnimationClip CreateLegLiftClip(string name, Animator avatarAnimator, HumanBodyBones bone, string assetPath)
        {
            return AnimationUtility.CreateClip(name, assetPath + "/Animations", clip => {
                string bonePath = TrackingParameterMapper.GetCustomBonePath(bone) ?? 
                    AnimationUtility.GetBonePath(avatarAnimator, bone);
                if (!string.IsNullOrEmpty(bonePath))
                {
                    if (bone == HumanBodyBones.LeftUpperLeg || bone == HumanBodyBones.RightUpperLeg)
                        clip.SetCurve(bonePath, typeof(Transform), "localEulerAngles.x", new AnimationCurve(new Keyframe(0, -45f)));
                }
            });
        }
    }
}
