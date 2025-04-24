using UnityEngine;
using UnityEditor;
using System.Text;
using System.IO;
using VRC.SDK3.Avatars.Components;

public class SimpleBoneMapper : EditorWindow
{
    private GameObject avatarObject;
    private string exportPath = "Assets/BoneMapping.txt";

    [MenuItem("VRChat/Web Tracking/Simple Bone Mapper")]
    public static void ShowWindow()
    {
        GetWindow<SimpleBoneMapper>("Bone Mapper");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Simple Bone Mapping Exporter", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        avatarObject = (GameObject)EditorGUILayout.ObjectField("Avatar", avatarObject, typeof(GameObject), true);
        exportPath = EditorGUILayout.TextField("Export Path", exportPath);

        EditorGUILayout.Space(10);

        using (new EditorGUI.DisabledScope(avatarObject == null))
        {
            if (GUILayout.Button("Export Bone Mapping"))
            {
                ExportBoneMapping();
            }
        }

        EditorGUILayout.HelpBox(
            "アバターを選択して「Export Bone Mapping」ボタンを押すと、" +
            "ボーン名とそのマッピング情報をテキストファイルに出力します。",
            MessageType.Info);
    }

    private void ExportBoneMapping()
    {
        if (avatarObject == null)
        {
            EditorUtility.DisplayDialog("Error", "アバターが選択されていません", "OK");
            return;
        }

        Animator animator = avatarObject.GetComponent<Animator>();
        if (animator == null)
        {
            EditorUtility.DisplayDialog("Error", "選択したオブジェクトにAnimatorがありません", "OK");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Avatar Bone Mapping: {avatarObject.name}");
        sb.AppendLine("----------------------------------------");

        // ヒューマノイドマッピング情報を出力
        OutputHumanoidMapping(sb, animator);

        // 階層構造内のすべてのボーンを出力
        sb.AppendLine("\n--- All Bones in Hierarchy ---");
        OutputAllBones(sb, avatarObject.transform);

        // ファイルに出力
        try
        {
            string directory = Path.GetDirectoryName(exportPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(exportPath, sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"Bone mapping exported to: {exportPath}");
            EditorUtility.DisplayDialog("Export Complete", $"ファイルを出力しました: {exportPath}", "OK");
            EditorUtility.RevealInFinder(exportPath);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Export failed: {e.Message}");
            EditorUtility.DisplayDialog("Error", $"出力に失敗しました: {e.Message}", "OK");
        }
    }

    private void OutputHumanoidMapping(StringBuilder sb, Animator animator)
    {
        sb.AppendLine("--- Humanoid Bone Mappings ---");

        if (!animator.isHuman)
        {
            sb.AppendLine("このアバターはヒューマノイドではありません。ボーンのマッピングはありません。");
            return;
        }

        // すべてのヒューマノイドボーンタイプを列挙
        foreach (HumanBodyBones bone in System.Enum.GetValues(typeof(HumanBodyBones)))
        {
            if (bone != HumanBodyBones.LastBone)
            {
                Transform boneTransform = animator.GetBoneTransform(bone);
                string bonePath = boneTransform != null
                    ? GetTransformPath(boneTransform, avatarObject.transform)
                    : "無し";

                sb.AppendLine($"{bone}: {bonePath}");
            }
        }
    }

    private void OutputAllBones(StringBuilder sb, Transform transform, string path = "")
    {
        string currentPath = string.IsNullOrEmpty(path) ? transform.name : path + "/" + transform.name;

        // このボーンにマッピングされているヒューマノイドボーン名を取得
        string humanoidMapping = GetHumanoidMapping(transform);
        sb.AppendLine($"{currentPath}: {humanoidMapping}");

        // 子ボーンに対して再帰的に処理
        foreach (Transform child in transform)
        {
            OutputAllBones(sb, child, currentPath);
        }
    }

    private string GetHumanoidMapping(Transform boneTransform)
    {
        Animator animator = avatarObject.GetComponent<Animator>();
        if (!animator.isHuman)
            return "無し";

        // 各ヒューマノイドボーンタイプをチェック
        foreach (HumanBodyBones bone in System.Enum.GetValues(typeof(HumanBodyBones)))
        {
            if (bone != HumanBodyBones.LastBone)
            {
                Transform mappedBone = animator.GetBoneTransform(bone);
                if (mappedBone == boneTransform)
                {
                    return bone.ToString();
                }
            }
        }

        return "無し";
    }

    private string GetTransformPath(Transform transform, Transform root)
    {
        if (transform == root)
            return root.name;

        if (transform.parent == null)
            return transform.name;

        return GetTransformPath(transform.parent, root) + "/" + transform.name;
    }
}