using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;

namespace VRCFullBodyTracking
{
    public class AnimationLayerGenerator
    {
        private const string ASSET_PATH = "Assets/FullBodyTracking";
        private const string ANIM_PATH = "Assets/FullBodyTracking/Animations";

        public static void SetupAnimationLayers(VRCAvatarDescriptor avatar, AnimatorController animator)
        {
            if (avatar == null || animator == null)
            {
                Debug.LogError("Avatar or Animator is null");
                return;
            }

            try
            {
                // アバターの Animator コンポーネントを取得
                Animator avatarAnimator = avatar.gameObject.GetComponent<Animator>();
                if (avatarAnimator == null)
                {
                    Debug.LogError("Avatar doesn't have an Animator component");
                    return;
                }

                if (!avatarAnimator.isHuman)
                {
                    Debug.LogError("Avatar is not humanoid");
                    return;
                }

                // フォルダの作成
                AnimationUtility.EnsureDirectoryExists(ASSET_PATH);
                AnimationUtility.EnsureDirectoryExists(ANIM_PATH);
                AnimationUtility.EnsureDirectoryExists($"{ASSET_PATH}/Masks");

                // 既存のレイヤーをクリア
                ClearAllLayers(animator);

                // パラメータの設定
                TrackingParameterMapper.SetupAnimatorParameters(animator);

                // 各レイヤーを作成
                CreateBaseLayer(animator);
                BodyLayerGenerator.CreateBodyRotationLayer(animator, avatarAnimator, ASSET_PATH);
                LimbLayerGenerator.CreateArmPositionLayers(animator, avatarAnimator, ASSET_PATH);
                LimbLayerGenerator.CreateLegPositionLayers(animator, avatarAnimator, ASSET_PATH);
                HandGestureLayerGenerator.CreateHandGestureLayers(animator, avatarAnimator, ASSET_PATH);

                // VRChatのアバター設定を更新
                ConfigureVRChatLayers(avatar, animator);

                // 変更を保存
                EditorUtility.SetDirty(avatar);
                EditorUtility.SetDirty(animator);
                AssetDatabase.SaveAssets();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to setup animation layers: {ex}");
                EditorUtility.DisplayDialog("Setup Failed", 
                    "Failed to setup animation layers. Check the console for details.", "OK");
            }
        }

        private static void CreateBaseLayer(AnimatorController animator)
        {
            AnimationUtility.CreateLayer(animator, "Base Layer", (layer, stateMachine) =>
            {
                // TrackingEnabledのデフォルト値を設定するクリップを作成
                var enableTrackingClip = AnimationUtility.CreateClip("EnableTracking", ANIM_PATH, clip => {
                    clip.SetCurve("", typeof(Animator), "TrackingEnabled", 
                        new AnimationCurve(new Keyframe(0, 1f)));
                });

                var defaultState = stateMachine.AddState("Default", new Vector3(200, 0, 0));
                defaultState.motion = enableTrackingClip;
                defaultState.writeDefaultValues = false;
                stateMachine.defaultState = defaultState;
                layer.defaultWeight = 1f;
            });
        }

        private static void ClearAllLayers(AnimatorController animator)
        {
            while (animator.layers.Length > 0)
            {
                animator.RemoveLayer(0);
            }
        }

        private static void ConfigureVRChatLayers(VRCAvatarDescriptor avatar, AnimatorController controller)
        {
            avatar.customizeAnimationLayers = true;
            
            // 元の設定を保持する新しい配列
            var newLayers = new VRCAvatarDescriptor.CustomAnimLayer[5];
            
            // 既存の設定を保持
            if (avatar.baseAnimationLayers != null && avatar.baseAnimationLayers.Length > 0)
            {
                for (int i = 0; i < avatar.baseAnimationLayers.Length && i < newLayers.Length; i++)
                {
                    if (avatar.baseAnimationLayers[i].type != VRCAvatarDescriptor.AnimLayerType.FX)
                    {
                        newLayers[i] = avatar.baseAnimationLayers[i];
                    }
                }
            }
            
            // 各レイヤーのデフォルト値を設定
            for (int i = 0; i < newLayers.Length; i++)
            {
                if (newLayers[i].animatorController == null)
                {
                    newLayers[i].isDefault = true;
                    
                    switch (i)
                    {
                        case 0:
                            newLayers[i].type = VRCAvatarDescriptor.AnimLayerType.Base;
                            break;
                        case 1: 
                            newLayers[i].type = VRCAvatarDescriptor.AnimLayerType.Additive;
                            break;
                        case 2:
                            newLayers[i].type = VRCAvatarDescriptor.AnimLayerType.Gesture;
                            break;
                        case 3:
                            newLayers[i].type = VRCAvatarDescriptor.AnimLayerType.Action;
                            break;
                        case 4:
                            newLayers[i].type = VRCAvatarDescriptor.AnimLayerType.FX;
                            break;
                    }
                }
            }
            
            // Actionレイヤーを設定
            newLayers[3] = new VRCAvatarDescriptor.CustomAnimLayer
            {
                type = VRCAvatarDescriptor.AnimLayerType.Action,
                isDefault = false,
                animatorController = controller,
                mask = null
            };
            
            avatar.baseAnimationLayers = newLayers;
        }
    }
}
