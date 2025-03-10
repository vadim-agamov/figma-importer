using UnityEditor;
using UnityEngine;

namespace FigmaImporter.Editor
{
    [CustomEditor(typeof(FigmaNodeImporter))]
    public class FigmaImporterEditor : UnityEditor.Editor
    {
        private GUIStyle _leftAlignedButtonStyle;
        
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var importer = (FigmaNodeImporter)target;

            _leftAlignedButtonStyle ??= new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft
            };
            
            // Ensure there are nodes to display
            if (importer.Nodes != null && importer.Nodes.Length > 0)
            {
                foreach (var node in importer.Nodes)
                {
                    if (GUILayout.Button($"Download {node.UnityExportPath}", _leftAlignedButtonStyle))
                    {
                        importer.ImportNode(node);
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No nodes available.", MessageType.Info);
            }
            
            if (GUILayout.Button("Download All"))
            {
                importer.Import().Forget();
            }
        }
    }
}