// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using UnityEngine;
using UnityEditor;

namespace Meta.XR.Movement.Retargeting.Editor
{
    /// <summary>
    /// Custom property drawer for <see cref="PoseRetargeterConfig.JointPair"/>.
    /// </summary>
    [CustomPropertyDrawer(typeof(PoseRetargeterConfig.JointPair))]
    public class JointPairDrawer : PropertyDrawer
    {
        /// <inheritdoc />
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var isArrayElement = property.propertyPath.Contains("Array.data[");
            if (!isArrayElement)
            {
                position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            }

            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var childRect = new Rect(position.x, position.y, position.width, 16);
            var parentRect = new Rect(position.x + 15, position.y + 18, position.width - 15, 16);

            var childProp = property.FindPropertyRelative("Joint");
            var parentProp = property.FindPropertyRelative("ParentJoint");

            EditorGUI.PropertyField(childRect, childProp, new GUIContent("Joint"));
            EditorGUI.PropertyField(parentRect, parentProp, new GUIContent("Parent Joint"));

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }

        /// <inheritdoc />
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2;
        }
    }
}