/*
    Copyright (c) 2026 Alex Howe

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.
*/
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Poke.UI
{
    [
        CustomEditor(typeof(LayoutItem)),
        CanEditMultipleObjects
    ]
    public class LayoutItem_Editor : Editor
    {
#if UNITY_6000_0_OR_NEWER
        public VisualTreeAsset layoutItem;
        
        private LayoutItem _item;

        private SerializedProperty _log;
        private SerializedProperty _ignoreLayout;
        private SerializedProperty _sizing;
        private SerializedProperty _sizingX;
        private SerializedProperty _sizingY;
        private SerializedProperty _minWidth;
        private SerializedProperty _minHeight;
        private SerializedProperty _maxWidth;
        private SerializedProperty _maxHeight;
        private SerializedProperty _flexWidth;
        private SerializedProperty _flexHieght;
        private SerializedProperty _margins;
        private SerializedProperty _overflowsCrossLine;

        protected virtual void OnEnable() {
            _item = target as LayoutItem;

            _log = serializedObject.FindProperty("m_log");
            _ignoreLayout = serializedObject.FindProperty("m_ignoreLayout");
            _sizing = serializedObject.FindProperty("m_sizing");
            _sizingX = _sizing.FindPropertyRelative("x");
            _sizingY = _sizing.FindPropertyRelative("y");
            _minWidth = serializedObject.FindProperty("m_minWidth");
            _minHeight = serializedObject.FindProperty("m_minHeight");
            _maxWidth = serializedObject.FindProperty("m_maxWidth");
            _maxHeight = serializedObject.FindProperty("m_maxHeight");
            _flexWidth = serializedObject.FindProperty("m_flexWidth");
            _flexHieght = serializedObject.FindProperty("m_flexHeight");
            _margins = serializedObject.FindProperty("m_margins");
            _overflowsCrossLine = serializedObject.FindProperty("m_overflowsCrossLine");
        }
        
        public override VisualElement CreateInspectorGUI() {
            VisualElement root = new();
            root.Add(layoutItem.CloneTree());
            
            root.Bind(serializedObject);
            root.TrackSerializedObjectValue(serializedObject, OnObjectChanged);

            Toggle log = root.Q<Toggle>("LogField");
            log.BindProperty(_log);
            
            Toggle ignoreLayout = root.Q<Toggle>("IgnoreLayoutField");
            ignoreLayout.BindProperty(_ignoreLayout);

            EnumField sizingX = root.Q<EnumField>("SizeModeX");
            sizingX.BindProperty(_sizingX);
            
            EnumField sizingY = root.Q<EnumField>("SizeModeY");
            sizingY.BindProperty(_sizingY);

            Label minLabel = root.Q<Label>("MinLabel");
            minLabel.SetEnabled((SizingMode)_sizingX.enumValueIndex != SizingMode.Fixed || (SizingMode)_sizingY.enumValueIndex != SizingMode.Fixed);
            minLabel.TrackPropertyValue(_sizing, prop => {
                minLabel.SetEnabled(
                    (SizingMode)prop.FindPropertyRelative("x").enumValueIndex != SizingMode.Fixed ||
                    (SizingMode)prop.FindPropertyRelative("y").enumValueIndex != SizingMode.Fixed
                );
            });
            
            FloatField minWidth = root.Q<FloatField>("MinWidthField");
            minWidth.BindProperty(_minWidth);
            minWidth.SetEnabled((SizingMode)_sizingX.enumValueIndex != SizingMode.Fixed);
            minWidth.TrackPropertyValue(_sizingX, prop => minWidth.SetEnabled((SizingMode)prop.enumValueIndex != SizingMode.Fixed));
            
            FloatField minHeight = root.Q<FloatField>("MinHeightField");
            minHeight.BindProperty(_minHeight);
            minHeight.SetEnabled((SizingMode)_sizingY.enumValueIndex != SizingMode.Fixed);
            minHeight.TrackPropertyValue(_sizingY, prop => minHeight.SetEnabled((SizingMode)prop.enumValueIndex != SizingMode.Fixed));
            
            Label maxLabel = root.Q<Label>("MaxLabel");
            maxLabel.SetEnabled((SizingMode)_sizingX.enumValueIndex != SizingMode.Fixed || (SizingMode)_sizingY.enumValueIndex != SizingMode.Fixed);
            maxLabel.TrackPropertyValue(_sizing, prop => {
                maxLabel.SetEnabled(
                    (SizingMode)prop.FindPropertyRelative("x").enumValueIndex != SizingMode.Fixed ||
                    (SizingMode)prop.FindPropertyRelative("y").enumValueIndex != SizingMode.Fixed
                );
            });
            
            FloatField maxWidth = root.Q<FloatField>("MaxWidthField");
            maxWidth.BindProperty(_maxWidth);
            maxWidth.SetEnabled((SizingMode)_sizingX.enumValueIndex != SizingMode.Fixed);
            maxWidth.TrackPropertyValue(_sizingX, prop => maxWidth.SetEnabled((SizingMode)prop.enumValueIndex != SizingMode.Fixed));
            
            FloatField maxHeight = root.Q<FloatField>("MaxHeightField");
            maxHeight.BindProperty(_maxHeight);
            maxHeight.SetEnabled((SizingMode)_sizingY.enumValueIndex != SizingMode.Fixed);
            maxHeight.TrackPropertyValue(_sizingY, prop => maxHeight.SetEnabled((SizingMode)prop.enumValueIndex != SizingMode.Fixed));

            Label flexLabel = root.Q<Label>("FlexLabel");
            flexLabel.SetEnabled((SizingMode)_sizingX.enumValueIndex == SizingMode.Grow || (SizingMode)_sizingY.enumValueIndex == SizingMode.Grow);
            flexLabel.TrackPropertyValue(_sizing, prop => {
                flexLabel.SetEnabled(
                    (SizingMode)prop.FindPropertyRelative("x").enumValueIndex == SizingMode.Grow ||
                    (SizingMode)prop.FindPropertyRelative("y").enumValueIndex == SizingMode.Grow);
            });
            
            FloatField flexWidth = root.Q<FloatField>("FlexWidthField");
            flexWidth.BindProperty(_flexWidth);
            flexWidth.SetEnabled((SizingMode)_sizingX.enumValueIndex == SizingMode.Grow);
            flexWidth.TrackPropertyValue(_sizingX, prop => flexWidth.SetEnabled((SizingMode)prop.enumValueIndex == SizingMode.Grow));
            
            FloatField flexHeight = root.Q<FloatField>("FlexHeightField");
            flexHeight.BindProperty(_flexHieght);
            flexHeight.SetEnabled((SizingMode)_sizingY.enumValueIndex == SizingMode.Grow);
            flexHeight.TrackPropertyValue(_sizingY, prop => flexWidth.SetEnabled((SizingMode)prop.enumValueIndex == SizingMode.Grow));

            PropertyField margins = root.Q<PropertyField>("MarginsField");
            margins.BindProperty(_margins);
            
            Toggle overflow = root.Q<Toggle>("CrossLineField");
            overflow.BindProperty(_overflowsCrossLine);
            
            return root;
        }
        
        private void OnObjectChanged(SerializedObject obj) {
            if(EditorUtility.IsDirty(obj.targetObject)) {
                foreach(Object item in obj.targetObjects) {
                    (item as LayoutItem).SetDirty();
                }
            }
        }
#else
        public override void OnInspectorGUI() {
            if(!_item)
                return;

            EditorGUILayout.PropertyField(_log);
            EditorGUILayout.PropertyField(_ignoreLayout);

            // disable sizing options if ignoreLayout is true
            GUI.enabled = !_ignoreLayout.boolValue;
            EditorGUILayout.PropertyField(_sizing);
            GUI.enabled = true;

            EditorGUILayout.PropertyField(_overflowsLineCross);

            if(serializedObject.hasModifiedProperties) {
                serializedObject.ApplyModifiedProperties();

                foreach(var obj in serializedObject.targetObjects) {
                    (obj as LayoutItem).SetDirty();
                }

                EditorApplication.QueuePlayerLoopUpdate();
            }
        }
#endif
    }
}