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
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Poke.UI
{
    public class Layout : LayoutItem, IComparable<Layout>, ILayoutGroup
    {
        /* THINGS THAT CAN CAUSE A LAYOUT UPDATE
            - non-grow child RectTransform changes size
            - number of children change
            - child is enabled/disabled
            - this container changes
        */

#if UNITY_EDITOR
        public static readonly List<Layout> RefreshedThisFrame = new();
#endif
        public event Action OnLayoutChanged;
        
        [SerializeField] private Margins            m_padding;
        [SerializeField] private LayoutDirection    m_direction;
        [SerializeField] private Justification      m_justifyContent;
        [SerializeField] private Alignment          m_alignContent;
        [SerializeField] private float              m_innerSpacing;
        [SerializeField] private bool               m_ignoreChildScale;
        [SerializeField] private bool               m_wrap;
        [SerializeField] private float              m_lineSpacing;

        #region Properties
        public Margins Padding
        {
            get => m_padding;
            set
            {
                m_padding = value;
                SetDirty();
            }
        }
        public LayoutDirection Direction
        {
            get => m_direction;
            set
            {
                m_direction = value;
                SetDirty();
            }
        }
        public Justification JustifyContent
        {
            get => m_justifyContent;
            set
            {
                m_justifyContent = value;
                SetDirty();
            }
        }
        public Alignment AlignContent
        {
            get => m_alignContent;
            set
            {
                m_alignContent = value;
                SetDirty();
            }
        }
        public float InnerSpacing
        {
            get => m_innerSpacing;
            set
            {
                m_innerSpacing = value;
                SetDirty();
            }
        }
        public bool IgnoreChildScale
        {
            get => m_ignoreChildScale;
            set
            {
                m_ignoreChildScale = value;
                SetDirty();
            }
        }
        public bool Wrap
        {
            get => m_wrap;
            set
            {
                m_wrap = value;
                SetDirty();
            }
        }
        public float LineSpacing
        {
            get => m_lineSpacing;
            set
            {
                m_lineSpacing = value;
                SetDirty();
            }
        }
        #endregion

        public int ChildCount => _children?.Count ?? 0;
        public Vector2Int GrowChildCount => _growChildCount;

        private readonly List<ChildInfo>    _children = new();
        private Vector2                     _contentSize;
        private int                         _depth;
        private Vector2Int                  _growChildCount;
        private int                         _ignoreCount;
        private Rect                        _innerRect;
        private Vector2                     _lastSize;
        private readonly List<LineInfo>     _lines = new();

        #region TypeDef
        public enum Justification
        {
            Start,
            Center,
            End,
            SpaceBetween
        }

        public enum Alignment
        {
            Start,
            Center,
            End
        }

        public enum LayoutDirection
        {
            Row,
            Column,
            RowReverse,
            ColumnReverse
        }

        private class ChildInfo
        {
            public int index;
            public RectTransform rect;
            public LayoutItem li;
            public SizingMode sizingX;
            public SizingMode sizingY;
            public Vector2 size;
            public Margins margins;
            public bool enabled;
            public bool ignoreLayout;
            public int lineIndex;   // for wrap: which line index
        }

        private class LineInfo
        {
            public int firstChildIdx;      // first child index in _children for this line (inclusive)
            public int lastChildIdx;       // exclusive
            public float primarySize;      // sum of non-grow primary sizes + innerSpacing * (count-1)
            public float crossSize;        // max cross size of the line
            public int growCount;          // count of grow children in primary axis on this line
            public int activeCount;        // count of active children on this line (excluding ignored/disabled)
        }

        private struct BlockInfo
        {
            public float startX;
            public float endX;
            public float startY;
            public float endY;
            public int creatorLineIndex;
        }
        #endregion

        #region Layout MonoBehavior
        protected override void OnEnable() {
            base.OnEnable();
            Log("enable");
            RefreshChildCache();
        }

        public override void Update() {
            base.Update();

            bool layoutChanged = _dirty;
            bool needsCacheRefresh = false;

            // check for changes in children
            foreach(ChildInfo c in _children) {
                if(!c.rect) {
                    layoutChanged = true;
                    needsCacheRefresh = true;
                    continue;
                }

                // check if child index has changed
                if(c.rect.GetSiblingIndex() != c.index) {
                    layoutChanged = true;
                    needsCacheRefresh = true;
                }

                // check if item was disabled this frame
                if(c.rect.gameObject.activeInHierarchy != c.enabled) {
                    c.enabled = c.rect.gameObject.activeInHierarchy;
                    layoutChanged = true;
                }
                
                if(c.rect.rect.size != c.size) {
                    layoutChanged = true;
                }

                if(c.li) {
                    // check if ignore layout toggled this frame
                    if(c.li.IgnoreLayout != c.ignoreLayout) {
                        c.ignoreLayout = c.li.IgnoreLayout;
                        layoutChanged = true;
                    }

                    if(c.li.Margins != c.margins) {
                        c.margins = c.li.Margins;
                        layoutChanged = true;
                    }

                    if(c.li.Sizing.x != c.sizingX) {
                        c.sizingX = c.li.Sizing.x;
                        layoutChanged = true;
                    }
                    
                    if(c.li.Sizing.y != c.sizingY) {
                        c.sizingY = c.li.Sizing.y;
                        layoutChanged = true;
                    }
                }
                else {
                    _tracker.Add(
                        this,
                        c.rect,
                        DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.Anchors
                    );
                }
            }

            // check if the container changed this frame
            if(!Mathf.Approximately(_lastSize.x, _rect.rect.size.x) || !Mathf.Approximately(_lastSize.y, _rect.rect.size.y)) {
                layoutChanged = true;
            }
            // check if any children were added/removed this frame
            if(transform.childCount != _children.Count) {
                layoutChanged = true;
                needsCacheRefresh = true;
            }

            if(layoutChanged) {
                _dirty = true;
                LayoutRebuilder.MarkLayoutForRebuild(_rect);
                if(needsCacheRefresh)
                    RefreshChildCache();
            }

            _lastSize = _rect.rect.size;
        }
        
        protected override void OnDrawGizmosSelected() {
            base.OnDrawGizmosSelected();

            Matrix4x4 ltw = _rect.localToWorldMatrix;

            if(m_padding.top != 0 || m_padding.bottom != 0 || m_padding.left != 0 || m_padding.right != 0) {
                Rect r = new Rect(_rectCorners[0], _rectCorners[2] - _rectCorners[0]);
                r.position += (Vector2)(ltw * new Vector2(m_padding.left, m_padding.bottom));
                r.size -= (Vector2)(ltw * new Vector2(m_padding.left + m_padding.right, m_padding.top + m_padding.bottom));

                LayoutUtil.DrawDebugBox(r, _rect.position.z, Color.green);
            }
        }
        #endregion

        #region ILayoutGroup
        public override void CalculateLayoutInputHorizontal() {
            if(!_dirty) return;
            
#if UNITY_EDITOR
            RefreshedThisFrame.Add(this);
#endif
            Log("CalculateLayoutInputHorizontal");
            
            _growChildCount.x = 0;
            _ignoreCount = 0;

            if(_children.Count > 0) {
                // get number of disabled/ignore children
                foreach(ChildInfo c in _children) {
                    if(CheckIgnoreElem(c)) {
                        _ignoreCount++;
                    }
                    else {
                        c.size = c.size.SetX(c.rect.rect.size.x * (m_ignoreChildScale ? 1 : c.rect.localScale.x));
                    }
                }

                float primarySize = m_justifyContent == Justification.SpaceBetween ? 0 : m_innerSpacing * (_children.Count - _ignoreCount - 1);
                float crossSize = 0;

                // calculate content size
                float maxCrossSize = 0;
                foreach(ChildInfo c in _children) {
                    // skip disabled/ignore items
                    if(CheckIgnoreElem(c))
                        continue;

                    bool grow = false;
                    if(c.li) {
                        grow = c.li.Sizing.x == SizingMode.Grow;
                        if(grow) {
                            _growChildCount.x++;
                        }
                    }

                    float margins = c.margins.left + c.margins.right;
                    
                    switch(m_direction)
                    {
                        case LayoutDirection.Row:
                        case LayoutDirection.RowReverse:
                            primarySize += grow ? 0 : c.size.x + margins;
                            break;
                        case LayoutDirection.Column:
                        case LayoutDirection.ColumnReverse:
                            maxCrossSize = Mathf.Max(maxCrossSize, grow ? 0 : c.size.x + margins);
                            break;
                    }

                }
                crossSize += maxCrossSize;

                // save content size for later
                switch(m_direction)
                {
                    case LayoutDirection.Row:
                    case LayoutDirection.RowReverse:
                        _contentSize.x = primarySize;
                        break;
                    case LayoutDirection.Column:
                    case LayoutDirection.ColumnReverse:
                        _contentSize.x = crossSize;
                        break;
                }

                // WRAP: pack lines and override _contentSize.x with line-aware value
                if(m_wrap) {
                    bool isRow = IsRowDirection();
                    PackLines(primaryIsX: isRow);
                    if(isRow) {
                        // primary = x: contentSize.x = max line primarySize (only one line anyway when fallback is active)
                        if(m_sizing.x != SizingMode.FitContent) {
                            float maxLP = 0;
                            foreach(var l in _lines) {
                                maxLP = Mathf.Max(maxLP, l.primarySize);
                            }
                            _contentSize.x = maxLP;
                        }
                    }
                    else {
                        // primary = y, cross = x: contentSize.x = sum of line cross sizes + (n-1)*lineSpacing
                        if(m_sizing.y != SizingMode.FitContent) {
                            float sum = 0;
                            for (int i = 0; i < _lines.Count; i++) {
                                sum += _lines[i].crossSize;
                                if (i > 0) sum += m_lineSpacing;
                            }
                            _contentSize.x = sum;
                        }
                    }
                }

                // apply fit sizing X (now uses _contentSize.x — wrap overrides are also effective)
                if(m_sizing.x == SizingMode.FitContent) {
                    _rect.SetSizeWithCurrentAnchors(
                        RectTransform.Axis.Horizontal,
                        _contentSize.x + m_padding.left + m_padding.right
                    );
                }
                
                Log($"calculated rect x size: {_rect.rect.size.x:f3}");
            }
            else {
                _contentSize = Vector2.zero;
            }
            
            Log($"content x size: {_contentSize.x:f3}");
        }

        public override void CalculateLayoutInputVertical() {
            if(!_dirty) return;
            
            Log("CalculateLayoutInputVertical");
            
            _growChildCount.y = 0;

            if(_children.Count > 0) {
                foreach(ChildInfo c in _children) {
                    if(!CheckIgnoreElem(c)) {
                        c.size = c.size.SetY(c.rect.rect.size.y * (m_ignoreChildScale ? 1 : c.rect.localScale.y));
                    }
                }

                float primarySize = m_justifyContent == Justification.SpaceBetween ? 0 : m_innerSpacing * (_children.Count - _ignoreCount - 1);
                float crossSize = 0;

                // calculate content size
                float maxCrossSize = 0;
                foreach(ChildInfo c in _children) {
                    // skip disabled/ignore items
                    if(CheckIgnoreElem(c))
                        continue;

                    bool grow = false;
                    if(c.li) {
                        grow = c.li.Sizing.y == SizingMode.Grow;
                        if(grow) {
                            _growChildCount.y++;
                        }
                    }

                    float margins = c.margins.top + c.margins.bottom;
                    
                    switch(m_direction)
                    {
                        case LayoutDirection.Row:
                        case LayoutDirection.RowReverse:
                            maxCrossSize = Mathf.Max(maxCrossSize, grow ? 0 : c.size.y + margins);
                            break;
                        case LayoutDirection.Column:
                        case LayoutDirection.ColumnReverse:
                            primarySize += grow ? 0 : c.size.y + margins;
                            break;
                    }
                }
                crossSize += maxCrossSize;

                // save content size for later
                switch(m_direction)
                {
                    case LayoutDirection.Row:
                    case LayoutDirection.RowReverse:
                        _contentSize.y = crossSize;
                        break;
                    case LayoutDirection.Column:
                    case LayoutDirection.ColumnReverse:
                        _contentSize.y = primarySize;
                        break;
                }

                // WRAP: recalculate line cross size with Y and override _contentSize.y
                if(m_wrap && _lines.Count > 0) {
                    bool isRow = IsRowDirection();
                    if(isRow) {
                        // Row+Wrap: cross axis = y; refresh line crossSize.
                        // SKIP cross-grow (Y-Grow) items — they won't inflate the line cross;
                        // only their own rects will stretch.
                        for(int li = 0; li < _lines.Count; li++) {
                            _lines[li].crossSize = 0;
                        }
                        for(int i = 0; i < _children.Count; i++) {
                            ChildInfo c = _children[i];
                            if (CheckIgnoreElem(c)) continue;
                            if (c.li && (c.li.Sizing.y == SizingMode.Grow || c.li.OverflowsLineCross)) continue;
                            int li = c.lineIndex;
                            if (li < 0 || li >= _lines.Count) continue;
                            _lines[li].crossSize = Mathf.Max(_lines[li].crossSize, c.size.y);
                        }
                        float sum = 0;
                        for(int i = 0; i < _lines.Count; i++) {
                            sum += _lines[i].crossSize;
                            if (i > 0) sum += m_lineSpacing;
                        }
                        _contentSize.y = sum;
                    }
                    else {
                        // Column+Wrap: primary = y; contentSize.y = max line primary
                        if(m_sizing.y != SizingMode.FitContent) {
                            float max = 0;
                            foreach(LineInfo l in _lines) {
                                max = Mathf.Max(max, l.primarySize);
                            }
                            _contentSize.y = max;
                        }
                    }
                }

                // apply fit sizing Y (now uses _contentSize.y — wrap overrides are also effective)
                if(m_sizing.y == SizingMode.FitContent) {
                    _rect.SetSizeWithCurrentAnchors(
                        RectTransform.Axis.Vertical,
                        _contentSize.y + m_padding.top + m_padding.bottom
                    );
                }
                
                Log($"calculated rect y size: {_rect.rect.size.y:f3}");
            }
            else {
                _contentSize = Vector2.zero;
            }
            
            Log($"content y size: {_contentSize.y:f3}");
            
        }

        public void SetLayoutHorizontal() {
            if(!_dirty) return;
            
            Log("SetLayoutHorizontal");

            _innerRect = new Rect(_rect.rect);
            _innerRect.position += new Vector2(m_padding.left, m_padding.bottom);
            _innerRect.size -= new Vector2(m_padding.left + m_padding.right, m_padding.top + m_padding.bottom);
            
            if(m_wrap && _lines.Count > 0) {
                GrowChildrenWrapped(RectTransform.Axis.Horizontal);
                HorizontalLayoutWrapped();
            }
            else {
                GrowChildren(RectTransform.Axis.Horizontal);
                HorizontalLayout();
            }
        }

        public void SetLayoutVertical() {
            if(!_dirty) return;
            
            Log("SetLayoutVertical");
            
            if(m_wrap && _lines.Count > 0) {
                GrowChildrenWrapped(RectTransform.Axis.Vertical);
                VerticalLayoutWrapped();
            }
            else {
                GrowChildren(RectTransform.Axis.Vertical);
                VerticalLayout();
            }

            OnLayoutChanged?.Invoke();
            _dirty = false;
        }
        #endregion

        #region Layout Internal
        private void Log(object msg) {
            if(m_log) Debug.Log($"[{_frame}] [L:{gameObject.name}]: {msg}");
        }
        
        private bool CheckIgnoreElem(ChildInfo ci) {
            return ci.rect == null || !ci.enabled || ci.ignoreLayout;
        }

        private bool IsRowDirection() {
            return m_direction == LayoutDirection.Row || m_direction == LayoutDirection.RowReverse;
        }
        
        private void SetAnchorX(RectTransform rt, float x) {
            rt.anchorMin = rt.anchorMin.SetX(x);
            rt.anchorMax = rt.anchorMax.SetX(x);
        }
        private void SetAnchorY(RectTransform rt, float y) {
            rt.anchorMin = rt.anchorMin.SetY(y);
            rt.anchorMax = rt.anchorMax.SetY(y);
        }

        private void HorizontalLayout() {
            Log($"Horizontal Layout - content size x: {_contentSize.x}");
            
            float offset = 0;
            float leftover;
            float spacing = 0;
            int index = 0;
            switch(m_direction)
            {
                // ROW -> PRIMARY AXIS
                case LayoutDirection.Row:
                    switch(m_justifyContent)
                    {
                        case Justification.Start:
                            offset += m_padding.left;
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;

                                SetAnchorX(c.rect, 0);

                                float pivot = c.size.x * c.rect.pivot.x;
                                offset += c.margins.left + pivot;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(offset);
                                offset += (c.size.x - pivot) + c.margins.right + m_innerSpacing;
                            }
                            break;
                        case Justification.Center:
                            offset -= (_contentSize.x + m_padding.left + m_padding.right) / 2;

                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;

                                SetAnchorX(c.rect, 0.5f);

                                float pivot = c.size.x * c.rect.pivot.x;
                                offset += c.margins.left + pivot;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(offset + m_padding.left);
                                offset += (c.size.x - pivot) + c.margins.right + m_innerSpacing;
                            }
                            break;
                        case Justification.End:
                            offset -= m_padding.right + _contentSize.x;

                            foreach(ChildInfo c in _children)
                            {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;

                                SetAnchorX(c.rect, 1);

                                float pivot = c.size.x * c.rect.pivot.x;
                                offset += c.margins.left + pivot;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(offset);
                                offset += c.size.x - pivot + c.margins.right + m_innerSpacing;
                            }
                            break;
                        case Justification.SpaceBetween:
                            offset += m_padding.left;
                            leftover = _rect.rect.size.x - _contentSize.x - m_padding.left - m_padding.right;

                            if(_children.Count > 1)
                                spacing = leftover / (_children.Count - _ignoreCount - 1);

                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;

                                SetAnchorX(c.rect, 0);

                                if(index != 0) {
                                    offset += spacing;
                                }

                                float pivot = c.size.x * c.rect.pivot.x;
                                offset += c.margins.left + pivot;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(offset);
                                offset += c.size.x - pivot + c.margins.right;
                                index++;
                            }
                            break;
                    }
                    break;
                // ROW-REVERSE -> PRIMARY AXIS
                case LayoutDirection.RowReverse:
                    switch(m_justifyContent)
                    {
                        case Justification.Start:
                            offset += m_padding.left + _contentSize.x;

                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;

                                SetAnchorX(c.rect, 0);

                                float pivot = c.size.x * c.rect.pivot.x;
                                offset -= c.size.x - pivot + c.margins.right;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(offset);
                                offset -= pivot + c.margins.left + m_innerSpacing;
                            }
                            break;
                        case Justification.Center:
                            offset += (_contentSize.x + m_padding.left + m_padding.right) / 2;

                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if (CheckIgnoreElem(c))
                                    continue;

                                SetAnchorX(c.rect, 0.5f);

                                float pivot = c.size.x * c.rect.pivot.x;
                                offset -= c.size.x - pivot + c.margins.right;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(offset - m_padding.right);
                                offset -= pivot + c.margins.left + m_innerSpacing;
                            }
                            break;
                        case Justification.End:
                            offset += m_padding.right;

                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;

                                SetAnchorX(c.rect, 1);

                                float pivot = c.size.x * c.rect.pivot.x;
                                offset += c.size.x - pivot + c.margins.right;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(-offset);
                                offset += pivot + c.margins.left + m_innerSpacing;
                            }
                            break;
                        case Justification.SpaceBetween:
                            offset += m_padding.right;
                            leftover = _rect.rect.size.x - _contentSize.x - m_padding.left - m_padding.right;

                            if(_children.Count > 1)
                                spacing = leftover / (_children.Count - _ignoreCount - 1);

                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;

                                SetAnchorX(c.rect, 1);

                                float pivot = c.size.x * c.rect.pivot.x;
                                offset += c.size.x - pivot + c.margins.right;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(-offset);
                                offset += pivot + c.margins.left + spacing;
                            }
                            break;
                    }
                    break;
                // COLUMN/COLUMN-REVERSE -> CROSS AXIS
                case LayoutDirection.Column:
                case LayoutDirection.ColumnReverse:
                    switch(m_alignContent)
                    {
                        case Alignment.Start:
                            offset += m_padding.left;

                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;

                                SetAnchorX(c.rect, 0);

                                float pivot = c.size.x * c.rect.pivot.x;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(offset + pivot + c.margins.left);
                            }
                            break;
                        case Alignment.Center:
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;

                                SetAnchorX(c.rect, 0.5f);

                                float pivot = c.size.x * c.rect.pivot.x;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(m_padding.left / 2 - m_padding.right / 2 - (c.size.x / 2 - pivot) + c.margins.left/2 - c.margins.right/2);
                            }
                            break;
                        case Alignment.End:
                            offset += m_padding.right;

                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;

                                SetAnchorX(c.rect, 1);

                                float pivot = c.size.x * c.rect.pivot.x;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(-offset - (c.size.x - pivot) - c.margins.right);
                            }
                            break;
                    }
                    break;
            }

        }

        private void VerticalLayout() {
            Log($"Vertical Layout - content size y: {_contentSize.y}");
            
            float offset = 0;
            float leftover;
            float spacing = 0;
            int index = 0;
            switch(m_direction)
            {
                // ROW/ROW-REVERSE -> CROSS AXIS
                case LayoutDirection.Row:
                case LayoutDirection.RowReverse:
                    switch(m_alignContent)
                    {
                        case Alignment.Start:
                            offset += m_padding.top;

                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;

                                SetAnchorY(c.rect, 1);

                                float pivot = c.size.y * c.rect.pivot.y;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(-offset - (c.size.y - pivot) - c.margins.top);
                            }
                            break;
                        case Alignment.Center:
                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;

                                SetAnchorY(c.rect, 0.5f);

                                float pivot = c.size.y * c.rect.pivot.y;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(m_padding.bottom / 2 - m_padding.top / 2 - (c.size.y / 2 - pivot) + c.margins.bottom/2 - c.margins.top/2);
                            }
                            break;
                        case Alignment.End:
                            offset += m_padding.bottom;

                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;

                                SetAnchorY(c.rect, 0);

                                float pivot = c.size.y * c.rect.pivot.y;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(offset + pivot + c.margins.bottom);
                            }
                            break;
                    }
                    break;
                // COLUMN -> PRIMARY AXIS
                case LayoutDirection.Column:
                    switch(m_justifyContent)
                    {
                        case Justification.Start:
                            offset -= m_padding.top;

                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;

                                SetAnchorY(c.rect, 1);

                                float pivot = c.size.y * c.rect.pivot.y;
                                offset -= c.size.y - pivot + c.margins.top;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(offset);
                                offset -= pivot + c.margins.bottom + m_innerSpacing;
                            }
                            break;
                        case Justification.Center:
                            offset += (_contentSize.y + m_padding.top + m_padding.bottom) / 2;

                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;

                                SetAnchorY(c.rect, 0.5f);

                                float pivot = c.size.y * c.rect.pivot.y;
                                offset -= c.size.y - pivot + c.margins.top;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(offset - m_padding.top);
                                offset -= pivot + c.margins.bottom + m_innerSpacing;
                            }
                            break;
                        case Justification.End:
                            offset += _contentSize.y;

                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;

                                SetAnchorY(c.rect, 0);

                                float pivot = c.size.y * c.rect.pivot.y;
                                offset -= c.size.y - pivot + c.margins.top;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(offset + m_padding.bottom);
                                offset -= pivot + c.margins.bottom + m_innerSpacing;
                            }
                            break;
                        case Justification.SpaceBetween:
                            offset += m_padding.top;
                            leftover = _rect.rect.size.y - _contentSize.y - m_padding.top - m_padding.bottom;

                            if(_children.Count > 1)
                                spacing = leftover / (_children.Count - _ignoreCount - 1);

                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;

                                SetAnchorY(c.rect, 1);

                                if(index != 0) {
                                    offset += spacing;
                                }

                                float pivot = c.size.y * c.rect.pivot.y;
                                offset += c.size.y - pivot + c.margins.top;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(-offset);
                                offset += pivot + c.margins.bottom;

                                index++;
                            }
                            break;
                    }
                    break;
                // COLUMN-REVERSE -> PRIMARY AXIS
                case LayoutDirection.ColumnReverse:
                    switch(m_justifyContent)
                    {
                        case Justification.Start:
                            offset -= m_padding.top + _contentSize.y;

                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;

                                SetAnchorY(c.rect, 1);

                                float pivot = c.size.y * c.rect.pivot.y;
                                offset += pivot + c.margins.bottom;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(offset);
                                offset += c.size.y - pivot + c.margins.top + m_innerSpacing;
                            }
                            break;
                        case Justification.Center:
                            offset -= (_contentSize.y + m_padding.top + m_padding.bottom) / 2;

                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;

                                SetAnchorY(c.rect, 0.5f);

                                float pivot = c.size.y * c.rect.pivot.y;
                                offset += pivot + c.margins.bottom;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(offset + m_padding.bottom);
                                offset += c.size.y - pivot + c.margins.top + m_innerSpacing;
                            }
                            break;
                        case Justification.End:
                            offset += m_padding.bottom;

                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;

                                SetAnchorY(c.rect, 0);

                                float pivot = c.size.y * c.rect.pivot.y;
                                offset += pivot + c.margins.bottom;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(offset);
                                offset += c.size.y - pivot + c.margins.top + m_innerSpacing;
                            }
                            break;
                        case Justification.SpaceBetween:
                            offset += m_padding.bottom;
                            leftover = _rect.rect.size.y - _contentSize.y - m_padding.top - m_padding.bottom;

                            if(_children.Count > 1)
                                spacing = leftover / (_children.Count - _ignoreCount - 1);

                            foreach(ChildInfo c in _children) {
                                // skip disabled/ignore items
                                if(CheckIgnoreElem(c))
                                    continue;

                                SetAnchorY(c.rect, 0);

                                if(index != 0) {
                                    offset += spacing;
                                }

                                float pivot = c.size.y * c.rect.pivot.y;
                                offset += pivot + c.margins.bottom;
                                c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(offset);
                                offset += c.size.y - pivot + c.margins.top;

                                index++;
                            }
                            break;
                    }
                    break;
            }
        }
        
        private void GrowChildren(RectTransform.Axis axis) {
            float size;
            float crossSize;
            float leftover;
            float flexTotal = 0;
            
            switch(axis) {
                case RectTransform.Axis.Horizontal:
                    if(_growChildCount.x > 0) {
                        Log($"growing {_growChildCount.x} children horizontally (rect: {_rect.rect.size.x}, inner: {_innerRect.size.x}, content: {_contentSize.x})");
                        
                        float count = _growChildCount.x;
                        switch(m_direction) {
                            case LayoutDirection.Row:
                            case LayoutDirection.RowReverse:
                                // save total flex sum for size distribution
                                foreach(ChildInfo c in _children) {
                                    if(!c.li || CheckIgnoreElem(c))
                                        continue;

                                    flexTotal += c.li.flexibleWidth;
                                }
                                
                                leftover = _rect.rect.size.x - _contentSize.x - m_padding.left - m_padding.right;
                                Log($"free space: {leftover}");
                                
                                foreach(ChildInfo c in _children) {
                                    if(!c.li || CheckIgnoreElem(c))
                                        continue;

                                    if(c.li.Sizing.x == SizingMode.Grow) {
                                        size = leftover * (c.li.flexibleWidth / flexTotal) - c.margins.left - c.margins.right;
                                        Log($"growing \"{c.li.name}\" x axis ({size}) - margins: {c.margins.left}, {c.margins.right}");
                                        c.size.x = size;
                                        _contentSize.x += size + c.margins.left + c.margins.right;

                                        // size actually needs to change
                                        if(!Mathf.Approximately(c.rect.rect.size.x, size)) {
                                            c.rect.SetSizeWithCurrentAnchors(axis, size);

                                            // special case for text growing
                                            if(c.li is LayoutText t) {
                                                float oldSize = c.size.y;
                                                t.HandleGrowSizingX();
                                                float diff = c.rect.rect.size.y * (m_ignoreChildScale ? 1 : c.rect.localScale.y) - oldSize;
                                                // text resized vertically bc of growth
                                                if(!Mathf.Approximately(0, diff)) {
                                                    c.size.y = oldSize + diff;
                                                    GrowSizingXCallback(diff);
                                                }
                                            }
                                        }
                                    }
                                }
                                break;
                            case LayoutDirection.Column:
                            case LayoutDirection.ColumnReverse:
                                crossSize = _rect.rect.size.x - m_padding.left - m_padding.right;

                                foreach(ChildInfo c in _children) {
                                    if(!c.li || CheckIgnoreElem(c))
                                        continue;

                                    if(c.li.Sizing.x == SizingMode.Grow) {
                                        size = crossSize - c.margins.left - c.margins.right;
                                        
                                        Log($"growing \"{c.li.name}\" x axis ({size})");
                                        
                                        c.size.x = size;
                                        _contentSize.x = Mathf.Max(size + c.margins.left + c.margins.bottom, _contentSize.x);

                                        // size actually needs to change
                                        if(!Mathf.Approximately(c.rect.rect.size.x, size)) {
                                            c.rect.SetSizeWithCurrentAnchors(axis, size);

                                            // special case for text growing
                                            if(c.li is LayoutText t) {
                                                float oldSize = c.size.y;
                                                t.HandleGrowSizingX();
                                                float diff = c.rect.rect.size.y * (m_ignoreChildScale ? 1 : c.rect.localScale.y) - oldSize;
                                                // text resized vertically bc of growth
                                                if(!Mathf.Approximately(0, diff)) {
                                                    c.size.y = oldSize + diff;
                                                    GrowSizingXCallback(diff);
                                                }
                                            }
                                        }
                                    }
                                }
                                break;
                        }
                    }
                    break;
                case RectTransform.Axis.Vertical:
                    if(_growChildCount.y > 0) {
                        Log($"growing {_growChildCount.y} children vertically (rect: {_rect.rect.size.y}, content: {_contentSize.y})");
                        
                        float count = _growChildCount.y;
                        switch(m_direction)
                        {
                            case LayoutDirection.Row:
                            case LayoutDirection.RowReverse:
                                crossSize = _rect.rect.size.y - m_padding.top - m_padding.bottom;

                                foreach(ChildInfo c in _children) {
                                    if(!c.li || CheckIgnoreElem(c))
                                        continue;

                                    if(c.li.Sizing.y == SizingMode.Grow) {
                                        size = crossSize - c.margins.top - c.margins.bottom;
                                        
                                        Log($"growing \"{c.li.name}\" y axis ({size})");
                                        
                                        c.size.y = size;
                                        _contentSize.y = Mathf.Max(size + c.margins.top + c.margins.bottom, _contentSize.y);

                                        // size actually needs to change
                                        if(!Mathf.Approximately(c.rect.rect.size.y, size)) {
                                            c.rect.SetSizeWithCurrentAnchors(axis, size);
                                        }
                                    }
                                }
                                break;
                            case LayoutDirection.Column:
                            case LayoutDirection.ColumnReverse:
                                // save total flex sum for size distribution
                                foreach(ChildInfo c in _children) {
                                    if(!c.li || CheckIgnoreElem(c))
                                        continue;

                                    flexTotal += c.li.flexibleHeight;
                                }
                                
                                leftover = _rect.rect.size.y - _contentSize.y - m_padding.top - m_padding.bottom;
                                
                                foreach(ChildInfo c in _children) {
                                    if(!c.li || CheckIgnoreElem(c))
                                        continue;

                                    if(c.li.Sizing.y == SizingMode.Grow) {
                                        size = leftover * (c.li.flexibleHeight / flexTotal) - c.margins.top - c.margins.bottom;
                                        
                                        Log($"growing \"{c.li.name}\" y axis ({size})");
                                        c.size.y = size;
                                        _contentSize.y += size + c.margins.top + c.margins.bottom;

                                        // size actually needs to change
                                        if(!Mathf.Approximately(c.rect.rect.size.y, size)) {
                                            c.rect.SetSizeWithCurrentAnchors(axis, size);
                                        }
                                    }
                                }
                                break;
                        }
                    }
                    break;
            }
        }
        
        // Splits children into rows/columns and writes to _lines when wrap is active.
        // Creates a single line under NoWrap or if the primary axis is FitContent (fallback).
        // Reads directly from c.rect.rect.size — both axes are always fresh.
        private void PackLines(bool primaryIsX) {
            _lines.Clear();
            if(_children.Count == 0) return;

            SizingMode primarySizing = primaryIsX ? m_sizing.x : m_sizing.y;
            bool effectiveWrap = m_wrap && primarySizing != SizingMode.FitContent;

            if(!effectiveWrap) {
                _lines.Add(new LineInfo { firstChildIdx = 0, lastChildIdx = _children.Count });
                foreach(ChildInfo ch in _children) {
                    ch.lineIndex = 0;
                }
                return;
            }

            float containerPrimary = primaryIsX ? 
                _rect.rect.size.x - m_padding.left - m_padding.right
                : _rect.rect.size.y - m_padding.top - m_padding.bottom;

            var currentLine = new LineInfo { firstChildIdx = 0 };
            int activeInLine = 0;

            for(int i = 0; i < _children.Count; i++) {
                ChildInfo c = _children[i];
                c.lineIndex = _lines.Count;

                if(CheckIgnoreElem(c)) continue;

                Vector2 scale = m_ignoreChildScale ? Vector2.one : c.rect.localScale;
                Vector2 rectSize = c.rect.rect.size * scale;

                bool grow = c.li && (primaryIsX ? 
                    c.li.Sizing.x == SizingMode.Grow
                    : c.li.Sizing.y == SizingMode.Grow);
                // OverflowsLineCross flag: doesn't contribute to line cross size (like cross-grow), and
                // blocks columns in subsequent rows. The difference is: it doesn't stretch,
                // and retains its natural FitContent/Fixed size.
                bool crossGrow = c.li && (primaryIsX
                    ? (c.li.Sizing.y == SizingMode.Grow || c.li.OverflowsLineCross)
                    : (c.li.Sizing.x == SizingMode.Grow || c.li.OverflowsLineCross));
                float childPrimary = grow ? 0 : (primaryIsX ? rectSize.x : rectSize.y);
                // Cross grow item shouldn't contribute to line cross size (line stays at its natural cross size,
                // child will stretch to container cross later - acts like an overflow)
                float childCross = crossGrow ? 0 : (primaryIsX ? rectSize.y : rectSize.x);

                // Grow child: occupies a line on its own. If current active line is not empty, close it first,
                // take grow to a new line and close immediately. Thus grow takes ownership of the entire line.
                if(grow) {
                    if(activeInLine > 0) {
                        currentLine.lastChildIdx = i;
                        _lines.Add(currentLine);
                        currentLine = new LineInfo { firstChildIdx = i };
                        activeInLine = 0;
                    }
                    c.lineIndex = _lines.Count;
                    currentLine.growCount = 1;
                    currentLine.activeCount = 1;
                    currentLine.crossSize = childCross;
                    currentLine.primarySize = 0; // GrowChildrenWrapped will assign containerPrimary
                    currentLine.lastChildIdx = i + 1;
                    _lines.Add(currentLine);

                    currentLine = new LineInfo { firstChildIdx = i + 1 };
                    activeInLine = 0;
                    continue;
                }

                float spacingDelta = activeInLine > 0 ? m_innerSpacing : 0;
                float candidate = currentLine.primarySize + spacingDelta + childPrimary;

                if(activeInLine > 0 && candidate > containerPrimary) {
                    currentLine.lastChildIdx = i;
                    _lines.Add(currentLine);

                    currentLine = new LineInfo { firstChildIdx = i };
                    activeInLine = 0;
                    c.lineIndex = _lines.Count;
                    spacingDelta = 0;
                }

                currentLine.primarySize += spacingDelta + childPrimary;
                currentLine.crossSize = Mathf.Max(currentLine.crossSize, childCross);
                currentLine.activeCount++;
                activeInLine++;
            }

            // Close the last line (if it has active items or if no line has been added at all)
            if(activeInLine > 0 || _lines.Count == 0) {
                currentLine.lastChildIdx = _children.Count;
                _lines.Add(currentLine);
            }

            // For row direction, cross-grow items affect the packing of subsequent rows.
            // Here, we re-partition the lines to prevent items from falling on blocks and wrap if they overflow.
            if(primaryIsX && m_wrap) {
                RepackForCrossGrowBlocks();
            }
        }

        // Cross-grow (Y-Grow) items block X columns in subsequent rows.
        // This method re-partitions _lines: items that do not fit due to blocks are moved to a new row.
        // Only active for Row direction (primaryIsX=true).
        private void RepackForCrossGrowBlocks() {
            float containerPrimary = _rect.rect.size.x - m_padding.left - m_padding.right;
            if(containerPrimary <= 0) return;

            var newLines = new List<LineInfo>();
            var blocks = new List<BlockInfo>();

            float currentLineStart = 0f;
            int childIdx = 0;
            int totalChildren = _children.Count;

            while (childIdx < totalChildren) {
                var curLine = new LineInfo {
                    firstChildIdx = childIdx,
                    activeCount = 0,
                    growCount = 0,
                    primarySize = 0f,
                    crossSize = 0f
                };

                var activeBlocks = new List<BlockInfo>();
                foreach(BlockInfo b in blocks) {
                    if(b.endY > currentLineStart) {
                        activeBlocks.Add(b);
                    }
                }

                float xCursor = 0f;
                int activeInLine = 0;
                bool lineClosed = false;

                while(childIdx < totalChildren && !lineClosed) {
                    ChildInfo c = _children[childIdx];
                    c.lineIndex = newLines.Count;

                    if(CheckIgnoreElem(c)) {
                        childIdx++;
                        continue;
                    }

                    Vector2 scale = m_ignoreChildScale ? Vector2.one : c.rect.localScale;
                    Vector2 sz = c.rect.rect.size * scale;

                    bool pGrow = c.li && c.li.Sizing.x == SizingMode.Grow;
                    bool cGrow = c.li && (c.li.Sizing.y == SizingMode.Grow || c.li.OverflowsLineCross);
                    float childPrim = pGrow ? 0f : sz.x;
                    float childCross = cGrow ? 0f : sz.y;

                    if(pGrow) {
                        if(activeInLine > 0) {
                            lineClosed = true;
                            break;
                        }

                        curLine.growCount = 1;
                        curLine.activeCount = 1;
                        curLine.crossSize = childCross;
                        curLine.primarySize = 0f;
                        curLine.lastChildIdx = childIdx + 1;
                        newLines.Add(curLine);

                        if(cGrow) {
                            blocks.Add(new BlockInfo {
                                startX = 0f,
                                endX = containerPrimary,
                                startY = currentLineStart,
                                endY = currentLineStart + sz.y,
                                creatorLineIndex = newLines.Count - 1
                            });
                        }

                        currentLineStart += childCross + m_lineSpacing;
                        childIdx++;
                        lineClosed = true;
                        break;
                    }

                    float tentativeX = xCursor;
                    if(activeInLine > 0) {
                        tentativeX += m_innerSpacing;
                    }

                    bool jumped;
                    do {
                        jumped = false;
                        foreach (BlockInfo b in activeBlocks) {
                            if(tentativeX < b.endX && tentativeX + childPrim > b.startX) {
                                tentativeX = b.endX + Mathf.Max(0f, m_innerSpacing);
                                jumped = true;
                                break;
                            }
                        }
                    } while(jumped);

                    if(tentativeX + childPrim > containerPrimary) {
                        if (activeInLine > 0) {
                            lineClosed = true;
                            break;
                        }
                    }

                    c.lineIndex = newLines.Count;
                    curLine.primarySize = tentativeX + childPrim;
                    curLine.crossSize = Mathf.Max(curLine.crossSize, childCross);
                    curLine.activeCount++;
                    activeInLine++;

                    if(cGrow) {
                        blocks.Add(new BlockInfo {
                            startX = tentativeX,
                            endX = tentativeX + sz.x,
                            startY = currentLineStart,
                            endY = currentLineStart + sz.y,
                            creatorLineIndex = newLines.Count
                        });
                    }

                    xCursor = tentativeX + childPrim;
                    childIdx++;
                }

                if(activeInLine > 0) {
                    curLine.lastChildIdx = childIdx;
                    newLines.Add(curLine);
                    currentLineStart += curLine.crossSize + m_lineSpacing;

                    if(curLine.activeCount <= 1) {
                        blocks.RemoveAll(b => b.creatorLineIndex == newLines.Count - 1);
                    }
                }
            }

            if(newLines.Count == 0) {
                newLines.Add(new LineInfo {
                    firstChildIdx = 0,
                    lastChildIdx = totalChildren,
                    activeCount = 0,
                    growCount = 0,
                    primarySize = 0f,
                    crossSize = 0f
                });
            }

            _lines.Clear();
            _lines.AddRange(newLines);
        }
        
        // ==== WRAP IMPL ====
        private void GrowChildrenWrapped(RectTransform.Axis axis) {
            bool primaryIsX = IsRowDirection();
            bool axisIsPrimary = (axis == RectTransform.Axis.Horizontal) == primaryIsX;

            if(axisIsPrimary) {
                // PRIMARY AXIS GROW: per-line leftover dagit. PackLines grow child'i tek basina
                // satira aldigi icin leftover = containerPrimary (satir tamamen grow'a ait).
                float containerPrimary = primaryIsX ?
                    _rect.rect.size.x - m_padding.left - m_padding.right
                    : _rect.rect.size.y - m_padding.top - m_padding.bottom;

                foreach(LineInfo line in _lines) {
                    if (line.growCount == 0) continue;
                    float leftover = containerPrimary - line.primarySize;
                    if (leftover <= 0) continue;
                    float perGrow = leftover / line.growCount;

                    for(int i = line.firstChildIdx; i < line.lastChildIdx; i++) {
                        ChildInfo c = _children[i];
                        if (CheckIgnoreElem(c) || !c.li) continue;
                        bool grow = primaryIsX
                            ? c.li.Sizing.x == SizingMode.Grow
                            : c.li.Sizing.y == SizingMode.Grow;
                        if (!grow) continue;

                        if (primaryIsX)
                        {
                            c.size.x = perGrow;
                            if (!Mathf.Approximately(c.rect.rect.size.x, perGrow))
                                c.rect.SetSizeWithCurrentAnchors(axis, perGrow);
                        }
                        else
                        {
                            c.size.y = perGrow;
                            if (!Mathf.Approximately(c.rect.rect.size.y, perGrow))
                                c.rect.SetSizeWithCurrentAnchors(axis, perGrow);
                        }
                    }
                    line.primarySize += perGrow * line.growCount;
                }
            }
            else
            {
                // CROSS AXIS GROW: child cross boyutunu container cross'a esle.
                // Row direction'da cross=Y; Column direction'da cross=X.
                bool axisIsX = axis == RectTransform.Axis.Horizontal;
                float containerCross = axisIsX
                    ? _rect.rect.size.x - m_padding.left - m_padding.right
                    : _rect.rect.size.y - m_padding.top - m_padding.bottom;
                if (containerCross <= 0) return;

                foreach (var line in _lines)
                {
                    for (int i = line.firstChildIdx; i < line.lastChildIdx; i++)
                    {
                        ChildInfo c = _children[i];
                        if (CheckIgnoreElem(c) || !c.li) continue;
                        bool crossGrow = axisIsX
                            ? c.li.Sizing.x == SizingMode.Grow
                            : c.li.Sizing.y == SizingMode.Grow;
                        if (!crossGrow) continue;

                        // Cross-grow: SADECE child kendi rect'ini container cross'a stretch eder.
                        // line.crossSize DOKUNULMAZ — diger satirlar normal yer alir (overflow).
                        if (axisIsX)
                        {
                            c.size.x = containerCross;
                            if (!Mathf.Approximately(c.rect.rect.size.x, containerCross))
                                c.rect.SetSizeWithCurrentAnchors(axis, containerCross);
                        }
                        else
                        {
                            c.size.y = containerCross;
                            if (!Mathf.Approximately(c.rect.rect.size.y, containerCross))
                                c.rect.SetSizeWithCurrentAnchors(axis, containerCross);
                        }
                    }
                }
            }
        }
        
        // Tum line'larin cross axis toplam blok boyutu (sum line cross + (n-1)*lineSpacing)
        private float ComputeLinesCrossBlockSize()
        {
            float sum = 0;
            for (int i = 0; i < _lines.Count; i++)
            {
                sum += _lines[i].crossSize;
                if (i > 0) sum += m_lineSpacing;
            }
            return sum;
        }
        
        // AlignContent'a gore cross blok baslangic offset'i (container padding'i icinde)
        private float ComputeAlignContentOffset(float containerCross, float crossBlock)
        {
            float free = containerCross - crossBlock;
            switch (m_alignContent)
            {
                case Alignment.Start: return 0;
                case Alignment.Center: return free / 2;
                case Alignment.End: return free;
            }
            return 0;
        }
        
        private void HorizontalLayoutWrapped() {
            switch (m_direction) {
                case LayoutDirection.Row:
                case LayoutDirection.RowReverse:
                    // X = primary, per-line per-item positioning (JustifyContent)
                    LayoutLinesPrimaryX(reversed: m_direction == LayoutDirection.RowReverse);
                    break;
                case LayoutDirection.Column:
                case LayoutDirection.ColumnReverse:
                    // X = cross, lines stack along X with AlignContent
                    LayoutLinesCrossX();
                    break;
            }
        }
        
        private void VerticalLayoutWrapped()
        {
            switch (m_direction)
            {
                case LayoutDirection.Row:
                case LayoutDirection.RowReverse:
                    // Y = cross, lines stack along Y with AlignContent
                    LayoutLinesCrossY();
                    break;
                case LayoutDirection.Column:
                case LayoutDirection.ColumnReverse:
                    // Y = primary, per-line per-item positioning (JustifyContent)
                    LayoutLinesPrimaryY(reversed: m_direction == LayoutDirection.ColumnReverse);
                    break;
            }
        }
        
        // Row/RowReverse + Wrap: position each row's items on the X axis using JustifyContent.
        // If cross-grow (Y-Grow) items exist in previous rows, they "block" the X columns;
        // subsequent row items skip these blocks (like text wrapping around an image in Word).
        private void LayoutLinesPrimaryX(bool reversed)
        {
            float containerPrimary = _rect.rect.size.x - m_padding.left - m_padding.right;

            var blocks = new List<BlockInfo>();
            float currentLineStart = 0f;

            for (int li = 0; li < _lines.Count; li++)
            {
                var line = _lines[li];
                if (line.activeCount == 0) continue;

                var activeBlockedIntervals = new List<(float start, float end)>();
                foreach (var b in blocks)
                {
                    if (b.endY > currentLineStart)
                    {
                        activeBlockedIntervals.Add((b.startX, b.endX));
                    }
                }
                activeBlockedIntervals.Sort((a, b) => a.start.CompareTo(b.start));

                if (activeBlockedIntervals.Count == 0)
                {
                    PlaceLinePrimaryXFullJustify(line, containerPrimary, reversed);
                }
                else
                {
                    PlaceLinePrimaryXWithBlocks(line, activeBlockedIntervals, containerPrimary);
                }

                if (line.activeCount > 1)
                {
                    for (int i = line.firstChildIdx; i < line.lastChildIdx; i++)
                    {
                        ChildInfo c = _children[i];
                        if (CheckIgnoreElem(c) || !c.li) continue;
                        if (c.li.Sizing.y != SizingMode.Grow && !c.li.OverflowsLineCross) continue;

                        float pivot = c.size.x * c.rect.pivot.x;
                        float itemLeftFromPad;
                        if (!reversed)
                        {
                            itemLeftFromPad = c.rect.anchoredPosition.x - pivot - m_padding.left;
                        }
                        else
                        {
                            float anchorRightOffset = -c.rect.anchoredPosition.x;
                            float itemRightFromRight = anchorRightOffset - (c.size.x - pivot);
                            itemLeftFromPad = containerPrimary - itemRightFromRight - c.size.x;
                        }

                        blocks.Add(new BlockInfo
                        {
                            startX = itemLeftFromPad,
                            endX = itemLeftFromPad + c.size.x,
                            startY = currentLineStart,
                            endY = currentLineStart + c.size.y,
                            creatorLineIndex = li
                        });
                    }
                }

                currentLineStart += line.crossSize + m_lineSpacing;
            }
        }
        
        // NO block: full support for current JustifyContent behavior
        private void PlaceLinePrimaryXFullJustify(LineInfo line, float containerPrimary, bool reversed)
        {
            float lineUsed = line.primarySize;
            float free = Mathf.Max(0, containerPrimary - lineUsed);
            float spacing = m_innerSpacing;
            if (m_justifyContent == Justification.SpaceBetween && line.activeCount > 1)
            {
                spacing = m_innerSpacing + free / (line.activeCount - 1);
            }

            float offset;
            if (!reversed)
            {
                offset = m_padding.left;
                switch (m_justifyContent)
                {
                    case Justification.Center: offset += free / 2; break;
                    case Justification.End: offset += free; break;
                }
            }
            else
            {
                offset = m_padding.right;
                switch (m_justifyContent)
                {
                    case Justification.Center: offset += free / 2; break;
                    case Justification.End: offset += free; break;
                }
            }

            for (int i = line.firstChildIdx; i < line.lastChildIdx; i++)
            {
                ChildInfo c = _children[i];
                if (CheckIgnoreElem(c)) continue;

                float pivot = c.size.x * c.rect.pivot.x;
                if (!reversed)
                {
                    SetAnchorX(c.rect, 0);
                    offset += pivot;
                    c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(offset);
                    offset += (c.size.x - pivot) + spacing;
                }
                else
                {
                    SetAnchorX(c.rect, 1);
                    offset += (c.size.x - pivot);
                    c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(-offset);
                    offset += pivot + spacing;
                }
            }
        }
        
        // WITH block: Start-style placement, X cursor skips blocked intervals.
        // JustifyContent is ignored — v1 limitation (when cross-grow blocks exist).
        private void PlaceLinePrimaryXWithBlocks(LineInfo line, List<(float start, float end)> blocked, float containerPrimary)
        {
            float xCursor = 0; // relative to padding.left

            for (int i = line.firstChildIdx; i < line.lastChildIdx; i++)
            {
                ChildInfo c = _children[i];
                if (CheckIgnoreElem(c)) continue;

                // If item [xCursor, xCursor + size.x] overlaps with any block, jump cursor to block.end.
                // m_innerSpacing is added after skip so item doesn't stick to the block.
                // do-while is used to do this sequentially (could enter another block after jumping).
                bool advanced;
                do
                {
                    advanced = false;
                    foreach (var b in blocked)
                    {
                        if (xCursor < b.end && xCursor + c.size.x > b.start)
                        {
                            // Negative spacing could cause infinite loop; add at least 0
                            xCursor = b.end + Mathf.Max(0f, m_innerSpacing);
                            advanced = true;
                            break;
                        }
                    }
                } while (advanced);

                SetAnchorX(c.rect, 0);
                float pivot = c.size.x * c.rect.pivot.x;
                c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(m_padding.left + xCursor + pivot);
                xCursor += c.size.x + m_innerSpacing;
            }
        }
        
        // Column/ColumnReverse + Wrap: position each column's items on the Y axis using JustifyContent
        private void LayoutLinesPrimaryY(bool reversed)
        {
            float containerPrimary = _rect.rect.size.y - m_padding.top - m_padding.bottom;

            foreach (var line in _lines)
            {
                if (line.activeCount == 0) continue;
                float lineUsed = line.primarySize;
                float free = Mathf.Max(0, containerPrimary - lineUsed);
                float spacing = m_innerSpacing;
                if (m_justifyContent == Justification.SpaceBetween && line.activeCount > 1)
                {
                    spacing = m_innerSpacing + free / (line.activeCount - 1);
                }

                float offset;
                if (!reversed)
                {
                    // Column forward: items top->bottom. anchor Y=1, anchoredPos negatif. offset accumulates downward.
                    offset = m_padding.top;
                    switch (m_justifyContent)
                    {
                        case Justification.Center: offset += free / 2; break;
                        case Justification.End: offset += free; break;
                    }
                }
                else
                {
                    // ColumnReverse: items bottom->top, anchor Y=0, anchoredPos positive.
                    offset = m_padding.bottom;
                    switch (m_justifyContent)
                    {
                        case Justification.Center: offset += free / 2; break;
                        case Justification.End: offset += free; break;
                    }
                }

                for (int i = line.firstChildIdx; i < line.lastChildIdx; i++)
                {
                    ChildInfo c = _children[i];
                    if (CheckIgnoreElem(c)) continue;

                    float pivot = c.size.y * c.rect.pivot.y;
                    if (!reversed)
                    {
                        SetAnchorY(c.rect, 1);
                        offset += (c.size.y - pivot);
                        c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(-offset);
                        offset += pivot + spacing;
                    }
                    else
                    {
                        SetAnchorY(c.rect, 0);
                        offset += pivot;
                        c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(offset);
                        offset += (c.size.y - pivot) + spacing;
                    }
                }
            }
        }
        
        // Row/RowReverse + Wrap: lines stack along the Y axis, block is aligned with AlignContent
        private void LayoutLinesCrossY()
        {
            float containerCross = _rect.rect.size.y - m_padding.top - m_padding.bottom;
            float crossBlock = ComputeLinesCrossBlockSize();
            float blockStartOffset = ComputeAlignContentOffset(containerCross, crossBlock);
            float crossOffset = m_padding.top + blockStartOffset;

            foreach (var line in _lines)
            {
                for (int i = line.firstChildIdx; i < line.lastChildIdx; i++)
                {
                    ChildInfo c = _children[i];
                    if (CheckIgnoreElem(c)) continue;

                    SetAnchorY(c.rect, 1);
                    float pivot = c.size.y * c.rect.pivot.y;
                    // line start (top) + item offset from line top. No AlignItems support, all are aligned to line top.
                    c.rect.anchoredPosition = c.rect.anchoredPosition.SetY(-crossOffset - (c.size.y - pivot));
                }
                crossOffset += line.crossSize + m_lineSpacing;
            }
        }
        
        // Column/ColumnReverse + Wrap: lines stack along the X axis, block is aligned with AlignContent
        private void LayoutLinesCrossX()
        {
            float containerCross = _rect.rect.size.x - m_padding.left - m_padding.right;
            float crossBlock = ComputeLinesCrossBlockSize();
            float blockStartOffset = ComputeAlignContentOffset(containerCross, crossBlock);
            float crossOffset = m_padding.left + blockStartOffset;

            foreach (var line in _lines)
            {
                for (int i = line.firstChildIdx; i < line.lastChildIdx; i++)
                {
                    ChildInfo c = _children[i];
                    if (CheckIgnoreElem(c)) continue;

                    SetAnchorX(c.rect, 0);
                    float pivot = c.size.x * c.rect.pivot.x;
                    c.rect.anchoredPosition = c.rect.anchoredPosition.SetX(crossOffset + pivot);
                }
                crossOffset += line.crossSize + m_lineSpacing;
            }
        }
        #endregion

        public void GrowSizingXCallback(float yDiff) {
            Log($"X Grow Callback ({yDiff})");
            // remove grow items from calculated content size
            foreach(ChildInfo c in _children) {
                if(CheckIgnoreElem(c))
                    continue;

                if(c.li && c.li.Sizing.y == SizingMode.Grow) {
                    _contentSize.y -= c.rect.rect.size.y;
                }
                else {
                    c.size.y = c.rect.rect.size.y;
                }
            }

            float oldSize = _contentSize.y;
            float oldHeight = _rect.rect.size.y;

            // recalculate content size
            switch(m_direction)
            {
                case LayoutDirection.Row:
                case LayoutDirection.RowReverse:
                    _contentSize.y = 0;
                    foreach(ChildInfo c in _children) {
                        if(CheckIgnoreElem(c) || (c.li && c.li.Sizing.y == SizingMode.Grow))
                            continue;

                        _contentSize.y = Mathf.Max(_contentSize.y, c.size.y);
                    }
                    break;
                case LayoutDirection.Column:
                case LayoutDirection.ColumnReverse:
                    _contentSize.y += yDiff;
                    break;
            }
            bool sizeChanged = !Mathf.Approximately(_contentSize.y, oldSize);

            if(m_sizing.y == SizingMode.FitContent && sizeChanged) {
                _rect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    m_padding.top + m_padding.bottom + _contentSize.y
                );
            }

            Log($"old content: {oldSize}, old height: {oldHeight}\nnew content: {_contentSize.y}, new height: {_rect.rect.height}");
            
            if(_parent)
                _parent.GrowSizingXCallback(yDiff);

            if(!_dirty && sizeChanged) {
                Log("forcing vertical layout update from x grow callback");
                GrowChildren(RectTransform.Axis.Vertical);
                VerticalLayout();
            }
        }

        public int CompareTo(Layout other) {
            if(_depth < other._depth) {
                return 1;
            }
            if(_depth == other._depth) {
                return 0;
            }

            return -1;
        }

        public void RefreshChildCache() {
            _children.Clear();
            _lines.Clear();

            int childCount = transform.childCount;
            Log($"Refreshing child cache - {childCount} children detected");

            for(int i = 0; i < childCount; i++) {
                RectTransform rt = transform.GetChild(i).GetComponent<RectTransform>();
                if (rt == null) continue;
                
                LayoutItem li = rt.GetComponent<LayoutItem>();
                
                Log($"Adding child \"{rt.name}\" - size: {rt.rect.size}, layoutitem: {li != null}");
                
                _children.Add(
                    new ChildInfo
                    {
                        index = i,
                        rect = rt,
                        li = li,
                        margins = li ? li.Margins : default,
                        size = rt.rect.size * (m_ignoreChildScale ? Vector2.one : rt.localScale),
                        enabled = rt.gameObject.activeInHierarchy,
                        ignoreLayout = li && li.IgnoreLayout,
                        lineIndex = 0,
                    }
                );
            }

            LayoutRebuilder.MarkLayoutForRebuild(_rect);
        }
    }
}
