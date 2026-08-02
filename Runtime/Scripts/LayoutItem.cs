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
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Poke.UI
{
    [
        ExecuteAlways,
        RequireComponent(typeof(RectTransform))
    ]
    public class LayoutItem : MonoBehaviour, ILayoutElement
    {
        [SerializeField] protected bool m_log;
        
        [Header("Layout Item")]
        [SerializeField] protected bool m_ignoreLayout = false;
        [SerializeField] protected SizeModes m_sizing;
        [SerializeField] protected float m_minWidth;
        [SerializeField] protected float m_minHeight;
        [SerializeField] protected float m_maxWidth;
        [SerializeField] protected float m_maxHeight;
        [Tooltip("The relative \"weight\" of this element in the horizontal layout")]
        [SerializeField] protected float m_flexWidth;
        [Tooltip("The relative \"weight\" of this element in the vertical layout")]
        [SerializeField] protected float m_flexHeight;
        // In wrap mode, this item shouldn't contribute to the line's cross size — it retains
        // its natural cross size but does not inflate the line, and blocks columns in subsequent
        // lines (similar to a "floating" image in Word). Only meaningful under a wrap parent.
        [SerializeField] protected bool m_overflowsCrossLine = false;
        
        public float minWidth => m_minWidth;
        public float minHeight => m_minHeight;
        public float flexibleWidth => m_flexWidth; // relative "weight" of this item in the horizontal layout
        public float flexibleHeight => m_flexHeight; // relative "weight" of this item in the vertical layout
        public float preferredWidth => m_maxWidth;
        public float preferredHeight => m_maxHeight;
        public int layoutPriority => _layoutPriority; // useless in uLayout (bc only one UL component per object)

        private float _preferredWidth, _preferredHeight;
        private int _layoutPriority;
        
        public bool IgnoreLayout
        {
            get => m_ignoreLayout;
            set => m_ignoreLayout = value;
        }
        public RectTransform Rect => _rect;
        public DrivenTransformProperties TrackerProps
        {
            get => _trackerProps;
            set => _trackerProps = value;
        }
        public SizeModes SizeMode => m_sizing;
        public bool OverflowsLineCross
        {
            get => m_overflowsCrossLine;
            set { m_overflowsCrossLine = value; SetDirty(); }
        }

        protected RectTransform _rect;
        protected DrivenRectTransformTracker _tracker;
        protected DrivenTransformProperties _trackerProps;
        protected RectTransform _parentRect;
        protected Layout _parent;
        protected bool _dirty = true;
        protected int _frame;
        protected readonly Vector3[] _rectCorners = new Vector3[4];

        private Vector2 _parentSize;

        [Serializable]
        public struct SizeModes
        {
            public SizingMode x;
            public SizingMode y;
        }

        #region LayoutItem MonoBehavior
        protected virtual void Awake() {
            _rect = GetComponent<RectTransform>();
            _tracker = new DrivenRectTransformTracker();

            _parentSize = _parentRect ? _parentRect.rect.size : default;
        }

        protected virtual void OnEnable() {
            if(transform.parent) {
                _parentRect = transform.parent.GetComponent<RectTransform>();
                _parent = transform.parent.GetComponent<Layout>();
            }

            _trackerProps = DrivenTransformProperties.None;
            _dirty = true;
        }

        public virtual void Update() {
            //Log("update");
            _frame = Time.frameCount;

#if UNITY_EDITOR
            _tracker.Clear();
            _trackerProps = DrivenTransformProperties.None;

            SetDrivenProperties();

            _tracker.Add(this, _rect, _trackerProps);
#endif

            // Do grow sizing here if parent is not a Layout
            if(!_parent && _parentRect) {
                // only update size if parent size has changed
                if(m_sizing.x == SizingMode.Grow && !Mathf.Approximately(_parentRect.rect.size.x, _parentSize.x)) {
                    _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _parentRect.rect.size.x);
                    _parentSize = _parentSize.SetX(_parentRect.rect.size.x);
                }
                if(m_sizing.y == SizingMode.Grow && !Mathf.Approximately(_parentRect.rect.size.y, _parentSize.y)) {
                    _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _parentRect.rect.size.y);
                    _parentSize = _parentSize.SetY(_parentRect.rect.size.y);
                }
            }
        }
        
        protected virtual void OnDrawGizmosSelected() {
            _rect.GetWorldCorners(_rectCorners);
            
            foreach(Vector3 v in _rectCorners) {
                LayoutUtil.DrawCenteredDebugBox(v, 0.15f, 0.15f, Color.red);
            }

            Rect r = new Rect(_rectCorners[0], _rectCorners[2] - _rectCorners[0]);
            LayoutUtil.DrawDebugBox(r, _rect.position.z, Color.white);
        }
        #endregion

        private void Log(object msg) {
            if(m_log) Debug.Log($"[{_frame}] [LI:{gameObject.name}]: {msg}");
        }
        
        protected virtual void SetDrivenProperties() {
            if((m_sizing.x == SizingMode.FitContent && transform.childCount > 0) || m_sizing.x == SizingMode.Grow)
                _trackerProps |= DrivenTransformProperties.SizeDeltaX;
            if((m_sizing.y == SizingMode.FitContent && transform.childCount > 0) || m_sizing.y == SizingMode.Grow)
                _trackerProps |= DrivenTransformProperties.SizeDeltaY;

            if(_parent && !m_ignoreLayout) 
                _trackerProps |= DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.Anchors;
        }

        public virtual void SetDirty() {
            _dirty = true;
            if(_parent) {
                _parent.SetDirty();
            }
        }

        public virtual void CalculateLayoutInputHorizontal() {
            Log("CalculateLayoutInputHorizontal");
        }

        public virtual void CalculateLayoutInputVertical() {
            Log("CalculateLayoutInputVertical");
        }
    }
}
