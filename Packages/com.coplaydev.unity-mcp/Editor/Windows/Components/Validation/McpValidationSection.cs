using System;
using MCPForUnity.Editor.Constants;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MCPForUnity.Editor.Windows.Components.Validation
{
    /// <summary>
    /// Controller for the Script Validation section.
    /// Handles script validation level settings.
    /// </summary>
    public class McpValidationSection
    {
        // UI Elements
        private EnumField validationLevelField;
        private Label validationDescription;

        // Data
        private ValidationLevel currentValidationLevel = ValidationLevel.Standard;

        // Validation levels
        public enum ValidationLevel
        {
            Basic,
            Standard,
            Comprehensive,
            Strict
        }

        public VisualElement Root { get; private set; }

        public McpValidationSection(VisualElement root)
        {
            Root = root;
            CacheUIElements();
            InitializeUI();
            RegisterCallbacks();
        }

        private void CacheUIElements()
        {
            validationLevelField = Root.Q<EnumField>("validation-level");
            validationDescription = Root.Q<Label>("validation-description");
        }

        private void InitializeUI()
        {
            validationLevelField.Init(ValidationLevel.Standard);
            int savedLevel = EditorPrefs.GetInt(EditorPrefKeys.ValidationLevel, 1);
            currentValidationLevel = (ValidationLevel)Mathf.Clamp(savedLevel, 0, 3);
            validationLevelField.value = currentValidationLevel;
            UpdateValidationDescription();
        }

        private void RegisterCallbacks()
        {
            validationLevelField.RegisterValueChangedCallback(evt =>
            {
                currentValidationLevel = (ValidationLevel)evt.newValue;
                EditorPrefs.SetInt(EditorPrefKeys.ValidationLevel, (int)currentValidationLevel);
                UpdateValidationDescription();
            });
        }

        private void UpdateValidationDescription()
        {
            validationDescription.text = currentValidationLevel switch
            {
                ValidationLevel.Basic => "基础：仅验证语法，编译检查速度快。",
                ValidationLevel.Standard => "标准（推荐）：检查语法 + 常见错误，速度与覆盖率均衡。",
                ValidationLevel.Comprehensive => "全面：包含代码质量的详细验证，较慢但更彻底。",
                ValidationLevel.Strict => "严格：最高强度验证 + 警告视为错误，最慢但能捕获所有问题。",
                _ => "未知验证级别"
            };
        }
    }
}
