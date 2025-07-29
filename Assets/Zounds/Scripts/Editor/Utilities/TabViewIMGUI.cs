using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Zounds {
    
    public class TabContent {
        public virtual string name => null;
        public virtual string tooltip => null;
        public virtual void OnTabOpened() { }
        public virtual void OnGUI(SerializedObject serializedObject, Rect contentRect) { }
    }

    public class TabViewIMGUI {

        private List<TabContent> tabElements;
        private GUIContent[] tabNameContents;

        public int tabCount => tabElements.Count;

        public TTabContent GetTab<TTabContent>(int index) where TTabContent : TabContent {
            return tabElements[index] as TTabContent;
        }

        public TabViewIMGUI(params TabContent[] tabElements) {
            this.tabElements = new List<TabContent>(tabElements);
            tabNameContents = new GUIContent[tabElements.Length];
            for (int i = 0; i < tabElements.Length; i++) {
                tabNameContents[i] = new GUIContent(tabElements[i].name, tabElements[i].tooltip);
            }
        }

        public int DrawLayout(int tabIndex, SerializedObject serializedObject, Rect viewportRect) {
            var selectedTab = GUILayout.Toolbar(tabIndex, tabNameContents);
            if (tabIndex != selectedTab) {
                tabElements[selectedTab].OnTabOpened();
            }
            tabElements[selectedTab].OnGUI(serializedObject, viewportRect);
            return selectedTab;
        }

    }

}