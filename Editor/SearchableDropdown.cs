using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.UIElements;

namespace AwAVR.VRCSDKPlus {
    /// <summary>A searchable popup list (Unity's AdvancedDropdown), opened under a UI Toolkit element.</summary>
    internal sealed class SearchableDropdown<T> : AdvancedDropdown {
        private readonly string _title;
        private readonly List<(string label, T value)> _items;
        private readonly Action<T> _onSelected;

        internal SearchableDropdown(string title, IEnumerable<(string label, T value)> items, Action<T> onSelected)
            : base(new AdvancedDropdownState()) {
            _title = title;
            _items = items.ToList();
            _onSelected = onSelected;
            minimumSize = new Vector2(220, Mathf.Clamp(_items.Count * 20 + 60, 120, 380));
        }

        protected override AdvancedDropdownItem BuildRoot() {
            var root = new AdvancedDropdownItem(_title);
            for (int i = 0; i < _items.Count; i++)
                root.AddChild(new AdvancedDropdownItem(_items[i].label) { id = i });
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item) {
            if (item.id >= 0 && item.id < _items.Count)
                _onSelected(_items[item.id].value);
        }

        internal void ShowUnder(VisualElement anchor) {
            var rect = anchor.worldBound;
            // Open at least as wide as the field the user clicked next to.
            var field = anchor.parent ?? anchor;
            rect.xMin = Mathf.Min(rect.xMin, field.worldBound.xMin);
            Show(rect);
        }
    }
}
