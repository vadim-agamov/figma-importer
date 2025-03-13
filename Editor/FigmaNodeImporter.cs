using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Android.Gradle.Manifest;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using static FigmaImporter.Editor.TextureUtils;

namespace FigmaImporter.Editor
{
    [CreateAssetMenu(menuName = "FigmaImporter/Node Importer", fileName = "FigmaNodeImporter", order = 0)]
    public class FigmaNodeImporter : ScriptableObject
    {
        [SerializeField]
        private FigmaToken _figmaToken;

        [SerializeField]
        private string _figmaProjectId;

        [SerializeField]
        private FigmaNodeConfig[] _figmaNodeConfig;

        private ProgressWindow _progressWindow;

        public FigmaNodeConfig[] Nodes => _figmaNodeConfig;

        private const string TEXTURE_FORMAT = "png";
        private const int MAX_TEXTURE_SIZE = 1024;
        private static CancellationTokenSource _cancellationTokenSource;
        private const int MAX_CONCURRENT_TEXTURE_DOWNLOADS = 5;
        private int _concurrentTextureDownloads = 0;

        private void OnValidate()
        {
            _figmaToken = AssetDatabase.FindAssets("t:FigmaToken")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<FigmaToken>)
                .FirstOrDefault();
        }

        private static void Cancel()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = null;
        }

        public async UniTaskVoid Import()
        {
            Cancel();

            foreach (var nodeConfig in _figmaNodeConfig)
            {
                await Do(nodeConfig);
            }

            // reimport all assets
            foreach (var nodeConfig in _figmaNodeConfig)
            {
                Debug.Log($"---- Re-Import directory {nodeConfig.UnityExportPath}");
                AssetDatabase.ImportAsset(nodeConfig.UnityExportPath,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ImportRecursive);
            }
        }

        public async UniTaskVoid ImportNode(FigmaNodeConfig figmaNodeConfig)
        {
            Cancel();

            await Do(figmaNodeConfig);

            Debug.Log($"---- Re-Import directory {figmaNodeConfig.UnityExportPath}");
            AssetDatabase.ImportAsset(figmaNodeConfig.UnityExportPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ImportRecursive);
        }

        private async UniTask Do(FigmaNodeConfig figmaNodeConfig)
        {
            _progressWindow = ProgressWindow.ShowWindow(figmaNodeConfig.UnityExportPath, Cancel);

            _cancellationTokenSource = new CancellationTokenSource();
            Debug.Log($"---- Begin {figmaNodeConfig.NodeToken}, {figmaNodeConfig.UnityExportPath}");
            var importedAssets = new List<string>();

            try
            {
                AssetDatabase.StartAssetEditing();

                if (!Directory.Exists(figmaNodeConfig.UnityExportPath))
                {
                    Debug.Log($"---- Creating directory {figmaNodeConfig.UnityExportPath}");
                    Directory.CreateDirectory(figmaNodeConfig.UnityExportPath);
                }

                var existingFiles = Directory
                    .GetFiles(figmaNodeConfig.UnityExportPath, $"*.{TEXTURE_FORMAT}", SearchOption.AllDirectories)
                    .ToList();

                _progressWindow.SetStatus("Downloading project..");
                _progressWindow.ReportTotalProgress(0);
                _progressWindow.ReportCurrentProgress(0);
                
                var nodeObject = await DownloadNode(figmaNodeConfig.NodeToken);

                var documentNode = nodeObject["nodes"].Children<JProperty>().Select(p => p.Value["document"]).First();

                var batches = documentNode
                    .ToObject<FigmaDocument>()
                    .Map(ExtractNodeIds)
                    .SplitIntoBatches(figmaNodeConfig.BatchSize)
                    .ToList();

                for (var index = 0; index < batches.Count; index++)
                {
                    var batch = batches[index];
                    var savedTextures = await ProcessNodes(batch, figmaNodeConfig);
                    importedAssets.AddRange(savedTextures);
                    _progressWindow.ReportTotalProgress((index+1) / (float)batches.Count);
                }

                Debug.Log($"importedAssets: {string.Join(", ", importedAssets)}");

                foreach (var path in existingFiles.Except(importedAssets))
                {
                    Debug.LogWarning($"DELETE {path}");
                    File.Delete(path);
                    File.Delete($"{path}.meta");
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                Debug.Log($"---- Downloaded all images {figmaNodeConfig.NodeToken}, {figmaNodeConfig.UnityExportPath}");

                _progressWindow.HideWindow();
                _progressWindow = null;
            }

            Debug.Log($"---- Setup import settings {figmaNodeConfig.NodeToken}, {figmaNodeConfig.UnityExportPath}");
            SetupImportSettings(figmaNodeConfig, importedAssets);

            Debug.Log($"---- End {figmaNodeConfig.NodeToken}, {figmaNodeConfig.UnityExportPath}");
        }

        private UniTask<IReadOnlyList<string>> ProcessNodes(IReadOnlyList<(string NodeId, string NodeName)> nodes, FigmaNodeConfig exportConfig) =>
            nodes
                .Map(FetchNodesUrls) // Fetch URLs for the nodes
                .Map(DownloadTextures) // Download textures for the nodes
                .Map(textures => SaveTextures(textures, exportConfig)); // Save textures and return paths

        private async UniTask<IReadOnlyList<string>> SaveTextures(UniTask<IEnumerable<(string NodeName, Texture2D Texture)>> nodes, FigmaNodeConfig exportConfig)
        {
            var textures = await nodes;
            _progressWindow.SetStatus("Saving images..");
            return textures.Select(x => SaveTexture(x.NodeName, x.Texture, exportConfig)).ToList();
        }

        private static void SetupImportSettings(FigmaNodeConfig figmaNodeConfig, List<string> importedAssets)
        {
            AssetDatabase.StartAssetEditing();
            for (var index = 0; index < importedAssets.Count; index++)
            {
                var importedAssetPath = importedAssets[index];
                var importer = AssetImporter.GetAtPath(importedAssetPath) as TextureImporter;
                Debug.Assert(importer != null, $"importer != null {importedAssetPath}");
                if (importer == null)
                {
                    continue;
                }
                
                Debug.Log($"---- Setup import settings {importedAssetPath}");

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.maxTextureSize = MAX_TEXTURE_SIZE;
                importer.isReadable = figmaNodeConfig.IsReadable;
                importer.textureCompression = TextureImporterCompression.Uncompressed;

                var importerSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(importerSettings);
                importerSettings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(importerSettings);

                var iosSettings = importer.GetPlatformTextureSettings("iPhone");
                iosSettings.overridden = true;
                iosSettings.maxTextureSize = MAX_TEXTURE_SIZE;
                iosSettings.format = figmaNodeConfig.IosTextureImporterFormat;
                importer.SetPlatformTextureSettings(iosSettings);

                var androidSettings = importer.GetPlatformTextureSettings("Android");
                androidSettings.overridden = true;
                androidSettings.maxTextureSize = MAX_TEXTURE_SIZE;
                androidSettings.format = figmaNodeConfig.AndroidTextureImporterFormat;
                importer.SetPlatformTextureSettings(androidSettings);

                EditorUtility.SetDirty(importer);
            }

            AssetDatabase.StopAssetEditing();
        }

        private UniTask<JObject> DownloadNode(string node)
        {
            var url = $"https://api.figma.com/v1/files/{_figmaProjectId}/nodes?ids={node}&depth=2";
            return FetchUrl(url);
        }

        private async UniTask<JObject> FetchUrl(string url)
        {
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("X-Figma-Token", _figmaToken.Token);

            Debug.Log($"---- Fetch: {url}");
            await request.SendWebRequest().ToUniTask(cancellationToken: _cancellationTokenSource.Token);
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"---- Fetch done: {url}");
                return JsonConvert.DeserializeObject<JObject>(request.downloadHandler.text);
            }

            throw new Exception($"Error downloading Figma project: {request.error}");
        }

        private async UniTask<IEnumerable<(string NodeUrl, string NodeName)>> FetchNodesUrls(IEnumerable<(string NodeId, string NodeName)> batch)
        {
            var commaSeparatedVisibleNodeIds = string.Join(',', batch.Select(x => x.NodeId));
            _progressWindow.SetStatus($"Requesting {batch.Count()} images..");
            _progressWindow.ReportCurrentProgress(0);
            var response = await FetchUrl($"https://api.figma.com/v1/images/{_figmaProjectId}?ids={commaSeparatedVisibleNodeIds}&format={TEXTURE_FORMAT}");
            var figmaImages = response.ToObject<FigmaImages>();
            return batch.Select(node =>
                (
                    NodeUrl: figmaImages.Images[node.NodeId],
                    NodeName: node.NodeName
                )
            );
        }

        private async UniTask<IEnumerable<(string NodeName, Texture2D Texture)>> DownloadTextures(UniTask<IEnumerable<(string NodeUrl, string NodeName)>> nodes)
        {
            var tasks = new List<UniTask<(string NodeName, Texture2D Texture)>>();
            foreach (var (nodeUrl, nodeName) in await nodes)
            {
                tasks.Add(DownloadTexture(nodeName, nodeUrl));
            }

            _progressWindow.SetStatus("Downloading images..");
            _progressWindow.ReportCurrentProgress(0);
            return await tasks.WhenAll(new Progress<float>(_progressWindow.ReportCurrentProgress));
        }
        
        private async UniTask<(string NodeName, Texture2D Texture)> DownloadTexture(string nodeName, string nodeUrl)
        {
            using var request = UnityWebRequestTexture.GetTexture(nodeUrl);
            
            await UniTask.WaitWhile(() => _concurrentTextureDownloads >= MAX_CONCURRENT_TEXTURE_DOWNLOADS);

            try
            {
                _concurrentTextureDownloads++;
                await request.SendWebRequest().ToUniTask(cancellationToken: _cancellationTokenSource.Token).SuppressCancellationThrow();
            }
            finally
            {
                _concurrentTextureDownloads--;
            }
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                _progressWindow.SetStatus($"Downloaded {nodeName}");
                Debug.Log($"---- Downloaded image: {nodeUrl} {nodeName}");
                return (NodeName: nodeName, Texture: DownloadHandlerTexture.GetContent(request));
            }
            throw new Exception($"Error downloading image: {nodeUrl} {nodeName} {request.error}");
        }

        private static string SaveTexture(string nodeName, Texture2D texture, FigmaNodeConfig config) =>
            Save(Path.Combine(config.UnityExportPath, nodeName), TEXTURE_FORMAT, config, texture.EncodeToPNG());

        private IReadOnlyList<(string NodeId, string NodeName)> ExtractNodeIds(FigmaDocument figmaDocument) =>
            figmaDocument.Children
                .SelectMany(node => node.Type == "COMPONENT_SET"
                    ? node.Children.Select(child => (NodeId: child.Id, NodeName: Path.Combine(node.Name, child.Name.Split('=').Skip(1).First())))
                    : new[] { (NodeId: node.Id, NodeName: node.Name) })
                .ToList();

        private static string Save(string fullPath, string extension, FigmaNodeConfig config, byte[] bytes)
        {
            var directory = Path.GetDirectoryName(fullPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var data = bytes
                .MapIf(AutoCrop, config.AutoCrop)
                .MapIf(d => Expand(d, config.Padding), config.Padding > 0)
                .MapIf(d => Resize(d, config.Size), config.Size.x > 0 && config.Size.y > 0)
                .MapIf(ExpandToPot, config.ExpandToPot);
            
            var fullPathWithExtension = $"{fullPath}.{extension}";
            File.WriteAllBytes(fullPathWithExtension, data);
            Debug.Log($"---- Saved {fullPathWithExtension}");
            return fullPathWithExtension;
        }
    }
}