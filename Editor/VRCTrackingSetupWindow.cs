using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VRCFullBodyTracking
{
    public class VRCTrackingSetupWindow : EditorWindow
    {
        private VRCAvatarDescriptor selectedAvatar;
        private string boneMappingPath = "Assets/BoneMapping.txt";
        private Vector2 scrollPosition;
        private bool showHelp = false;

        [MenuItem("VRChat/Full Body Tracking Setup")]
        public static void ShowWindow()
        {
            GetWindow<VRCTrackingSetupWindow>("FBT Setup");
        }

        private void OnGUI()
        {
            using (var scrollView = new EditorGUILayout.ScrollViewScope(scrollPosition))
            {
                scrollPosition = scrollView.scrollPosition;

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("VRChat Full Body Tracking Setup", EditorStyles.boldLabel);
                EditorGUILayout.Space(10);

                // アバターの選択
                selectedAvatar = EditorGUILayout.ObjectField(
                    "Avatar", selectedAvatar, typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;

                EditorGUILayout.Space(5);

                // ボーンマッピングファイルの設定
                using (new EditorGUILayout.HorizontalScope())
                {
                    boneMappingPath = EditorGUILayout.TextField("Bone Mapping File", boneMappingPath);
                    if (GUILayout.Button("Browse", GUILayout.Width(60)))
                    {
                        string path = EditorUtility.OpenFilePanel("Select Bone Mapping File", "Assets", "txt");
                        if (!string.IsNullOrEmpty(path))
                        {
                            boneMappingPath = FileUtil.GetProjectRelativePath(path);
                        }
                    }
                }

                EditorGUILayout.Space(10);

                // セットアップボタン
                using (new EditorGUI.DisabledScope(selectedAvatar == null))
                {
                    if (GUILayout.Button("Setup Tracking"))
                    {
                        SetupTracking();
                    }
                }

                // ヘルプの表示
                EditorGUILayout.Space(20);
                showHelp = EditorGUILayout.Foldout(showHelp, "Help");
                if (showHelp)
                {
                    ShowHelpBox();
                }
            }
        }

        private void SetupTracking()
        {
            try
            {
                // ボーンマッピングファイルを読み込み
                if (!string.IsNullOrEmpty(boneMappingPath))
                {
                    if (!TrackingParameterMapper.LoadBoneMapping(boneMappingPath))
                    {
                        if (!EditorUtility.DisplayDialog("Warning",
                            "ボーンマッピングファイルの読み込みに失敗しました。デフォルトのマッピングで続行しますか？",
                            "続行", "キャンセル"))
                        {
                            return;
                        }
                    }
                }

                // Actionレイヤー用AnimatorControllerを自動生成
                AnimatorController animatorController = CreateActionLayerController(selectedAvatar);
                
                if (animatorController == null)
                {
                    EditorUtility.DisplayDialog("Setup Failed", 
                        "Failed to create animator controller.", "OK");
                    return;
                }

                // アバターのActionレイヤーを設定
                AnimationLayerGenerator.SetupAnimationLayers(selectedAvatar, animatorController);

                // ExpressionParametersを作成して設定
                var parameters = TrackingParameterMapper.CreateExpressionParameters();
                string paramPath = "Assets/FullBodyTracking/TrackingParameters.asset";
                AnimationUtility.EnsureDirectoryExists("Assets/FullBodyTracking");
                AssetDatabase.CreateAsset(parameters, paramPath);
                selectedAvatar.expressionParameters = parameters;

                // ExpressionMenuを作成して設定
                var menu = TrackingParameterMapper.CreateExpressionsMenu();
                string menuPath = "Assets/FullBodyTracking/TrackingMenu.asset";
                AssetDatabase.CreateAsset(menu, menuPath);
                selectedAvatar.expressionsMenu = menu;

                // メニューを保存
                if (selectedAvatar.expressionsMenu != null)
                {
                    EditorUtility.SetDirty(selectedAvatar.expressionsMenu);
                }

                // パラメータを保存
                if (selectedAvatar.expressionParameters != null)
                {
                    EditorUtility.SetDirty(selectedAvatar.expressionParameters);
                }

                // アバターの変更を保存
                EditorUtility.SetDirty(selectedAvatar);
                AssetDatabase.SaveAssets();

                Debug.Log("Successfully set up full body tracking!");
                EditorUtility.DisplayDialog("Setup Complete", 
                    "Full body tracking has been successfully set up!", "OK");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to setup tracking: {ex}");
                EditorUtility.DisplayDialog("Setup Failed", 
                    "Failed to setup tracking. Check the console for details.", "OK");
            }
        }
        
        private AnimatorController CreateActionLayerController(VRCAvatarDescriptor avatar)
        {
            if (avatar == null)
                return null;
            
            // アバター名に基づいたファイル名を作成（FXをActionに変更）
            string avatarName = avatar.gameObject.name;
            string controllerName = $"{avatarName}_FBTrackingAction";
            string controllerPath = $"Assets/FullBodyTracking/{controllerName}.controller";
            
            // ディレクトリを確保
            AnimationUtility.EnsureDirectoryExists("Assets/FullBodyTracking");
            
            // 既存のコントローラがあればそれを返す
            var existingController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (existingController != null)
            {
                return existingController;
            }
            
            // 新しいコントローラを作成
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            
            return controller;
        }

        private void ShowHelpBox()
        {
            EditorGUILayout.HelpBox(
                "このツールはVRChatのフルボディトラッキングをセットアップします。\n\n" +
                "使用手順:\n" +
                "1. アバターをシーンに配置\n" +
                "2. アバターのVRCAvatarDescriptorを選択\n" +
                "3. 必要に応じてボーンマッピングファイルを選択\n" +
                "4. Setup Trackingボタンをクリック\n\n" +
                "注意:\n" +
                "・アバターはヒューマノイドである必要があります\n" +
                "・OSCプロトコルを使用してPythonからの姿勢情報を受け取ります\n" +
                "・ボーンマッピングファイルはSimple Bone Mapperで生成できます",
                MessageType.Info);
        }
    }
}
