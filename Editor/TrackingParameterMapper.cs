using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using System.Collections.Generic;
using System.IO;

namespace VRCFullBodyTracking
{
    public class TrackingParameterMapper
    {
        private class ParameterMapping
        {
            public string Name { get; }
            public VRCExpressionParameters.ValueType Type { get; }
            public float DefaultValue { get; }
            public float MinValue { get; }
            public float MaxValue { get; }
            public bool SaveValue { get; }
            public bool Synced { get; }

            public ParameterMapping(string name, VRCExpressionParameters.ValueType type, float defaultValue = 0.0f, float minValue = 0.0f, float maxValue = 1.0f, bool saveValue = false, bool synced = true)
            {
                Name = name;
                Type = type;
                DefaultValue = defaultValue;
                MinValue = minValue;
                MaxValue = maxValue;
                SaveValue = saveValue;
                Synced = synced;
            }
        }

        // ボーンマッピング情報を保持
        private static Dictionary<HumanBodyBones, string> customBoneMapping = new Dictionary<HumanBodyBones, string>();

        public static bool LoadBoneMapping(string mappingFilePath)
        {
            if (!File.Exists(mappingFilePath))
            {
                Debug.LogError($"Bone mapping file not found: {mappingFilePath}");
                return false;
            }

            try
            {
                customBoneMapping.Clear();
                string[] lines = File.ReadAllLines(mappingFilePath);
                bool inHumanoidSection = false;

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (line.Contains("--- Humanoid Bone Mappings ---"))
                    {
                        inHumanoidSection = true;
                        continue;
                    }

                    if (inHumanoidSection && !line.StartsWith("---"))
                    {
                        string[] parts = line.Split(':');
                        if (parts.Length == 2)
                        {
                            string boneName = parts[0].Trim();
                            string bonePath = parts[1].Trim();

                            if (bonePath != "無し" && System.Enum.TryParse<HumanBodyBones>(boneName, out var boneType))
                            {
                                customBoneMapping[boneType] = bonePath;
                            }
                        }
                    }
                }

                Debug.Log($"Loaded {customBoneMapping.Count} bone mappings from {mappingFilePath}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error loading bone mapping: {e.Message}");
                return false;
            }
        }

        public static string GetCustomBonePath(HumanBodyBones bone)
        {
            return customBoneMapping.TryGetValue(bone, out string path) ? path : null;
        }

        public static bool HasCustomBoneMapping()
        {
            return customBoneMapping.Count > 0;
        }

        private static readonly Dictionary<string, ParameterMapping> DefaultParameterMappings = new Dictionary<string, ParameterMapping>
        {
            // メイントラッキング制御
            {
                "TrackingEnabled",
                new ParameterMapping("TrackingEnabled", VRCExpressionParameters.ValueType.Bool, 0.0f, 0.0f, 1.0f, true, true)
            },

            // 体の回転
            {
                "BodyRotation",
                new ParameterMapping("BodyRotation", VRCExpressionParameters.ValueType.Float, 0.0f, -1.0f, 1.0f, false, true)
            },
            {
                "BodyDetected",
                new ParameterMapping("BodyDetected", VRCExpressionParameters.ValueType.Bool, 0.0f, 0.0f, 1.0f, false, true)
            },

            // 左腕の制御
            {
                "LeftArmX",
                new ParameterMapping("LeftArmX", VRCExpressionParameters.ValueType.Float, 0.0f, -1.0f, 1.0f, false, true)
            },
            {
                "LeftArmHeight",
                new ParameterMapping("LeftArmHeight", VRCExpressionParameters.ValueType.Float, 0.0f, -1.0f, 1.0f, false, true)
            },
            {
                "LeftArmDetected",
                new ParameterMapping("LeftArmDetected", VRCExpressionParameters.ValueType.Bool, 0.0f, 0.0f, 1.0f, false, true)
            },

            // 右腕の制御
            {
                "RightArmX",
                new ParameterMapping("RightArmX", VRCExpressionParameters.ValueType.Float, 0.0f, -1.0f, 1.0f, false, true)
            },
            {
                "RightArmHeight",
                new ParameterMapping("RightArmHeight", VRCExpressionParameters.ValueType.Float, 0.0f, -1.0f, 1.0f, false, true)
            },
            {
                "RightArmDetected",
                new ParameterMapping("RightArmDetected", VRCExpressionParameters.ValueType.Bool, 0.0f, 0.0f, 1.0f, false, true)
            },

            // 左手の制御
            {
                "LeftHandGesture",
                new ParameterMapping("LeftHandGesture", VRCExpressionParameters.ValueType.Int, 0.0f, 0.0f, 2.0f, false, true)
            },
            {
                "LeftHandDetected",
                new ParameterMapping("LeftHandDetected", VRCExpressionParameters.ValueType.Bool, 0.0f, 0.0f, 1.0f, false, true)
            },

            // 右手の制御
            {
                "RightHandGesture",
                new ParameterMapping("RightHandGesture", VRCExpressionParameters.ValueType.Int, 0.0f, 0.0f, 2.0f, false, true)
            },
            {
                "RightHandDetected",
                new ParameterMapping("RightHandDetected", VRCExpressionParameters.ValueType.Bool, 0.0f, 0.0f, 1.0f, false, true)
            },

            // 左脚の制御
            {
                "LeftLegLift",
                new ParameterMapping("LeftLegLift", VRCExpressionParameters.ValueType.Float, 0.0f, 0.0f, 1.0f, false, true)
            },
            {
                "LeftLegDetected",
                new ParameterMapping("LeftLegDetected", VRCExpressionParameters.ValueType.Bool, 0.0f, 0.0f, 1.0f, false, true)
            },

            // 右脚の制御
            {
                "RightLegLift",
                new ParameterMapping("RightLegLift", VRCExpressionParameters.ValueType.Float, 0.0f, 0.0f, 1.0f, false, true)
            },
            {
                "RightLegDetected",
                new ParameterMapping("RightLegDetected", VRCExpressionParameters.ValueType.Bool, 0.0f, 0.0f, 1.0f, false, true)
            }
        };

        public static void SetupAnimatorParameters(AnimatorController animator)
        {
            // 既存のパラメータを取得
            var existingParameters = new HashSet<string>();
            foreach (var param in animator.parameters)
            {
                existingParameters.Add(param.name);
            }

            // 必要なパラメータを追加
            foreach (var mapping in DefaultParameterMappings)
            {
                if (!existingParameters.Contains(mapping.Key))
                {
                    switch (mapping.Value.Type)
                    {
                        case VRCExpressionParameters.ValueType.Int:
                            animator.AddParameter(mapping.Key, AnimatorControllerParameterType.Int);
                            break;
                        case VRCExpressionParameters.ValueType.Float:
                            animator.AddParameter(mapping.Key, AnimatorControllerParameterType.Float);
                            break;
                        case VRCExpressionParameters.ValueType.Bool:
                            animator.AddParameter(mapping.Key, AnimatorControllerParameterType.Bool);
                            break;
                    }
                }
            }

            EditorUtility.SetDirty(animator);
        }

        public static VRCExpressionParameters CreateExpressionParameters()
        {
            var parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            parameters.parameters = new VRCExpressionParameters.Parameter[DefaultParameterMappings.Count];

            int i = 0;
            foreach (var mapping in DefaultParameterMappings)
            {
                parameters.parameters[i] = new VRCExpressionParameters.Parameter
                {
                    name = mapping.Key,
                    valueType = mapping.Value.Type,
                    defaultValue = mapping.Value.DefaultValue,
                    saved = mapping.Value.SaveValue
                };
                i++;
            }

            return parameters;
        }

        public static VRCExpressionsMenu CreateExpressionsMenu()
        {
            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            
            // TrackingEnabled のトグルを追加
            menu.controls.Add(new VRCExpressionsMenu.Control
            {
                name = "Tracking",
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "TrackingEnabled" },
                value = 1f
            });

            return menu;
        }
    }
}
