using System;
using UnityEditor;
using UnityEngine;


namespace FigmaImporter.Editor
{
    public class ProgressWindow : EditorWindow
    {
        private float _totalProgress = 0f;
        private float _currentProgress = 0f;
        private string _statusText;
        private string _title;
        private Action _onCancel;
        private static readonly Vector2 WindowSize = new Vector2(300, 150);

        public static ProgressWindow ShowWindow( string title, Action onCancel)
        {
            var window = CreateInstance<ProgressWindow>();
            CenterWindow(window);
            window._onCancel = onCancel;
            window._title = title;
            window.ShowPopup();
            return window;
        }

        private static void CenterWindow(EditorWindow window)
        {
            var main = EditorGUIUtility.GetMainWindowPosition();
            var x = main.x + (main.width - WindowSize.x) * 0.5f;
            var y = main.y + (main.height - WindowSize.y) * 0.5f;
            window.position = new Rect(x, y, WindowSize.x, WindowSize.y);
        }

        public void HideWindow()
        {
            Debug.Log("---- HideWindow");
            Close();
        }

        public void SetStatus(string status)
        {
            _statusText = status;
            Repaint();
        }

        public void ReportTotalProgress(float p)
        {
            _totalProgress = p;
            _currentProgress = 0;
            Repaint();
        }
        
        public void ReportCurrentProgress(float p)
        {
            _currentProgress = p;
            Repaint();
        }
        
        public IProgress<float> TotalReporter() => new Progress<float>(ReportTotalProgress);
        public IProgress<float> CurrentProgressReporter() => new Progress<float>(ReportCurrentProgress);

        private void OnGUI()
        {
            GUILayout.Label(_title, EditorStyles.boldLabel);

            GUILayout.Label(_statusText, EditorStyles.wordWrappedLabel);
            
            EditorGUI.ProgressBar(new Rect(10, 50, position.width - 20, 20), _totalProgress, $"Batches {_totalProgress * 100:F1}%");

            EditorGUI.ProgressBar(new Rect(10, 80, position.width - 20, 20), _currentProgress, $"Textures {_currentProgress * 100:F1}%");
            
            if (GUI.Button(new Rect(10, 120, position.width - 20, 20),"Cancel"))
            {
                _onCancel.Invoke();
            }
        }
    }
}