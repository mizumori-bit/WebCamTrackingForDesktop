using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

namespace VRCFullBodyTracking
{
    public static class HandGestureLayerGenerator
    {
        private static readonly Dictionary<string, HumanBodyBones[]> HandBones = new Dictionary<string, HumanBodyBones[]>
        {
            {
                "Left", new[] {
                    HumanBodyBones.LeftHand,
                    HumanBodyBones.LeftIndexProximal,
                    HumanBodyBones.LeftIndexIntermediate,
                    HumanBodyBones.LeftIndexDistal,
                    HumanBodyBones.LeftMiddleProximal,
                    HumanBodyBones.LeftMiddleIntermediate,
                    HumanBodyBones.LeftMiddleDistal,
                    HumanBodyBones.LeftRingProximal,
                    HumanBodyBones.LeftRingIntermediate,
                    HumanBodyBones.LeftRingDistal,
                    HumanBodyBones.LeftLittleProximal,
                    HumanBodyBones.LeftLittleIntermediate,
                    HumanBodyBones.LeftLittleDistal,
                    HumanBodyBones.LeftThumbProximal,
                    HumanBodyBones.LeftThumbIntermediate,
                    HumanBodyBones.LeftThumbDistal
                }
            },
            {
                "Right", new[] {
                    HumanBodyBones.RightHand,
                    HumanBodyBones.RightIndexProximal,
                    HumanBodyBones.RightIndexIntermediate,
                    HumanBodyBones.RightIndexDistal,
                    HumanBodyBones.RightMiddleProximal,
                    HumanBodyBones.RightMiddleIntermediate,
                    HumanBodyBones.RightMiddleDistal,
                    HumanBodyBones.RightRingProximal,
                    HumanBodyBones.RightRingIntermediate,
                    HumanBodyBones.RightRingDistal,
                    HumanBodyBones.RightLittleProximal,
                    HumanBodyBones.RightLittleIntermediate,
                    HumanBodyBones.RightLittleDistal,
                    HumanBodyBones.RightThumbProximal,
                    HumanBodyBones.RightThumbIntermediate,
                    HumanBodyBones.RightThumbDistal
                }
            }
        };

        public static void CreateHandGestureLayers(AnimatorController animator, Animator avatarAnimator, string assetPath)
        {
            var handMasks = new Dictionary<string, AvatarMask>();
            foreach (var side in new[] { "Left", "Right" })
            {
                handMasks[side] = AnimationUtility.CreateAvatarMask($"{side}HandMask", assetPath, mask => {
                    mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
                    mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, false);
                    mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, false);
                    mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
                    mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
                    mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, side == "Left");
                    mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, side == "Right");
                });
            }

            foreach (var side in new[] { "Left", "Right" })
            {
                AnimationUtility.CreateLayer(animator, $"{side} Hand Gesture", (layer, stateMachine) =>
                {
                    var defaultClip = CreateHandGestureClip($"{side}HandDefault", avatarAnimator, side, GestureType.Default, assetPath);
                    var peaceClip = CreateHandGestureClip($"{side}HandPeace", avatarAnimator, side, GestureType.Peace, assetPath);
                    var pointClip = CreateHandGestureClip($"{side}HandPoint", avatarAnimator, side, GestureType.Point, assetPath);

                    var defaultState = stateMachine.AddState($"{side} Hand Default", new Vector3(200, 100, 0));
                    var peaceState = stateMachine.AddState($"{side} Hand Peace", new Vector3(500, 0, 0));
                    var pointState = stateMachine.AddState($"{side} Hand Point", new Vector3(500, 200, 0));

                    defaultState.motion = defaultClip;
                    peaceState.motion = peaceClip;
                    pointState.motion = pointClip;

                    defaultState.writeDefaultValues = false;
                    peaceState.writeDefaultValues = false;
                    pointState.writeDefaultValues = false;

                    var activeState = stateMachine.AddState($"{side} Hand Active", new Vector3(300, 100, 0));
                    activeState.writeDefaultValues = false;

                    var inactiveState = stateMachine.AddState($"{side} Hand Inactive", new Vector3(300, 200, 0));
                    inactiveState.writeDefaultValues = false;

                    // ジェスチャー遷移を設定
                    CreateHandGestureTransitions(defaultState, peaceState, pointState, $"{side}HandGesture");

                    string detectionParam = side == "Left" ? "LeftHandDetected" : "RightHandDetected";

                    // 検出時のみアクティブになる遷移
                    var toActiveTransition = inactiveState.AddTransition(activeState);
                    toActiveTransition.hasExitTime = false;
                    toActiveTransition.duration = 0.2f;
                    toActiveTransition.AddCondition(AnimatorConditionMode.If, 0, "TrackingEnabled");
                    toActiveTransition.AddCondition(AnimatorConditionMode.If, 0, detectionParam);

                    // ジェスチャー状態への遷移
                    var toGestureTransition = activeState.AddTransition(defaultState);
                    toGestureTransition.hasExitTime = false;
                    toGestureTransition.duration = 0.1f;

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

                    layer.avatarMask = handMasks[side];
                    layer.defaultWeight = 1f;
                });
            }
        }

        private enum GestureType
        {
            Default,
            Peace,
            Point
        }

        private static AnimationClip CreateHandGestureClip(string name, Animator avatarAnimator, string side, GestureType gesture, string assetPath)
        {
            return AnimationUtility.CreateClip(name, assetPath + "/Animations", clip => {
                var bones = HandBones[side];
                foreach (var bone in bones)
                {
                    string bonePath = TrackingParameterMapper.GetCustomBonePath(bone) ?? 
                        AnimationUtility.GetBonePath(avatarAnimator, bone);

                    if (string.IsNullOrEmpty(bonePath)) continue;

                    switch (gesture)
                    {
                        case GestureType.Default:
                            // デフォルトの手のポーズ（手を開いた状態）
                            SetFingerRotation(clip, bonePath, 0f, 0f, 0f);
                            break;

                        case GestureType.Peace:
                            // ピースサイン
                            if (bone.ToString().Contains("Index") || bone.ToString().Contains("Middle"))
                            {
                                SetFingerRotation(clip, bonePath, 0f, 0f, 0f); // 伸ばした指
                            }
                            else if (!bone.ToString().Contains("Thumb") && !bone.ToString().EndsWith("Hand"))
                            {
                                SetFingerRotation(clip, bonePath, -80f, 0f, 0f); // 曲げた指
                            }
                            break;

                        case GestureType.Point:
                            // 人差し指を指す
                            if (bone.ToString().Contains("Index"))
                            {
                                SetFingerRotation(clip, bonePath, 0f, 0f, 0f); // 伸ばした指
                            }
                            else if (!bone.ToString().Contains("Thumb") && !bone.ToString().EndsWith("Hand"))
                            {
                                SetFingerRotation(clip, bonePath, -80f, 0f, 0f); // 曲げた指
                            }
                            break;
                    }
                }
            });
        }

        private static void SetFingerRotation(AnimationClip clip, string bonePath, float x, float y, float z)
        {
            clip.SetCurve(bonePath, typeof(Transform), "localEulerAngles.x", 
                new AnimationCurve(new Keyframe(0, x)));
            clip.SetCurve(bonePath, typeof(Transform), "localEulerAngles.y", 
                new AnimationCurve(new Keyframe(0, y)));
            clip.SetCurve(bonePath, typeof(Transform), "localEulerAngles.z", 
                new AnimationCurve(new Keyframe(0, z)));
        }

        private static void CreateHandGestureTransitions(AnimatorState defaultState, AnimatorState peaceState, AnimatorState pointState, string parameter)
        {
            AnimationUtility.CreateTransition(defaultState, peaceState, parameter, 1);
            AnimationUtility.CreateTransition(defaultState, pointState, parameter, 2);
            AnimationUtility.CreateTransition(peaceState, defaultState, parameter, 0);
            AnimationUtility.CreateTransition(peaceState, pointState, parameter, 2);
            AnimationUtility.CreateTransition(pointState, defaultState, parameter, 0);
            AnimationUtility.CreateTransition(pointState, peaceState, parameter, 1);
        }
    }
}
