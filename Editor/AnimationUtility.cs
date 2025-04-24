using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace VRCFullBodyTracking
{
    public static class AnimationUtility
    {
        public static string GetBonePath(Animator animator, HumanBodyBones bone)
        {
            if (animator == null || !animator.isHuman)
            {
                Debug.LogError("Animator is null or not humanoid");
                return "";
            }
                
            Transform boneTransform = animator.GetBoneTransform(bone);
            if (boneTransform == null)
            {
                Debug.LogError($"Could not find bone transform for {bone}");
                return "";
            }
                
            return GetRelativePath(boneTransform, animator.transform);
        }
        
        public static string GetRelativePath(Transform target, Transform root)
        {
            if (target == root)
                return "";
                
            if (target.parent == null)
                return target.name;
                
            string path = target.name;
            Transform current = target.parent;
            
            while (current != root && current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            
            return path;
        }

        public static void CreateLayer(AnimatorController animator, string name, System.Action<AnimatorControllerLayer, AnimatorStateMachine> setup)
        {
            var stateMachine = new AnimatorStateMachine();
            stateMachine.hideFlags = HideFlags.None;
            AssetDatabase.AddObjectToAsset(stateMachine, animator);

            var layer = new AnimatorControllerLayer
            {
                name = name,
                defaultWeight = 1f,
                stateMachine = stateMachine
            };

            setup(layer, stateMachine);
            animator.AddLayer(layer);
            EditorUtility.SetDirty(stateMachine);
        }

        public static BlendTree CreateBlendTree(AnimatorController animator, string name, BlendTreeType type, string parameter = null)
        {
            var blendTree = new BlendTree
            {
                name = name,
                blendType = type,
                hideFlags = HideFlags.None,
                useAutomaticThresholds = false
            };
            if (parameter != null)
                blendTree.blendParameter = parameter;

            AssetDatabase.AddObjectToAsset(blendTree, animator);
            EditorUtility.SetDirty(blendTree);
            return blendTree;
        }

        public static void CreateTransition(AnimatorState sourceState, AnimatorState destState, string parameter, int value)
        {
            var transition = sourceState.AddTransition(destState);
            transition.duration = 0.1f;
            transition.hasExitTime = false;
            transition.AddCondition(AnimatorConditionMode.Equals, value, parameter);
        }

        public static AnimationClip CreateClip(string name, string path)
        {
            string fullPath = $"{path}/{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fullPath);
            if (clip != null) return clip;

            clip = new AnimationClip { name = name };
            clip.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(clip, fullPath);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        public static AnimationClip CreateClip(string name, string path, System.Action<AnimationClip> setup)
        {
            var clip = CreateClip(name, path);
            setup?.Invoke(clip);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        public static AvatarMask CreateAvatarMask(string name, string path, System.Action<AvatarMask> setup)
        {
            string fullPath = $"{path}/Masks/{name}.mask";
            
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(fullPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, fullPath);
            }
            
            setup(mask);
            EditorUtility.SetDirty(mask);
            return mask;
        }

        public static void EnsureDirectoryExists(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parentPath = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
                var folderName = System.IO.Path.GetFileName(path);
                AssetDatabase.CreateFolder(parentPath, folderName);
            }
        }
    }
}
