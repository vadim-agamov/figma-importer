using System;
using UnityEditor;
using UnityEngine;

namespace FigmaImporter.Editor
{
    [Serializable]
    public class FigmaNodeConfig
    {
        [SerializeField]
        private string _unityExportPath;
        
        [SerializeField]
        private string _nodeToken;
        
        [SerializeField]
        private bool _expandToPot;
        
        [SerializeField]
        private int _padding;
        
        [SerializeField]
        private bool _autoCrop;
        
        [SerializeField]
        private TextureImporterFormat _iosTextureImporterFormat = TextureImporterFormat.Automatic;
        
        [SerializeField]
        private TextureImporterFormat _androidTextureImporterFormat = TextureImporterFormat.Automatic;

        [SerializeField]
        private bool _isReadable;
        
        public string UnityExportPath => _unityExportPath;
        public string NodeToken => _nodeToken;
        
        public bool ExpandToPot => _expandToPot;
        public TextureImporterFormat IosTextureImporterFormat => _iosTextureImporterFormat;
        public TextureImporterFormat AndroidTextureImporterFormat => _androidTextureImporterFormat;
        public bool IsReadable => _isReadable;
        public int Padding => _padding;
        public bool AutoCrop => _autoCrop;
    }
}