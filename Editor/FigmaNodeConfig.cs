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
     
        [Header("Figma Node Settings")]
        [SerializeField]
        private string _nodeToken;
        
        [SerializeField]
        private BatchSizeEnum _batchSize = BatchSizeEnum.Batch_10;
        
        [Header("Post Processing")]
        [SerializeField]
        private bool _autoCrop;
        
        [SerializeField]
        private int _padding;
        
        [SerializeField]
        private Vector2Int _resizeTo = Vector2Int.zero;
        
        [SerializeField]
        private bool _expandToPot;
        
        [Header("Texture Importer Settings")]
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
        public int BatchSize => (int)_batchSize;
        public Vector2Int Size => _resizeTo;
        
        private enum BatchSizeEnum
        {
            Batch_10 = 10,
            Batch_20 = 20,
            Batch_30 = 30,
            Batch_40 = 40,
            Batch_50 = 50,
            Batch_60 = 60,
            Batch_70 = 70,
            Batch_80 = 80,
            Batch_90 = 90,
            Batch_100 = 100
        }
    }
}