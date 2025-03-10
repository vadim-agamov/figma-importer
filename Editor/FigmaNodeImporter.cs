using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        public FigmaNodeConfig[] Nodes => _figmaNodeConfig;

        private const string TEXTURE_FORMAT = "png";
        private const int BATCH_SIZE = 20;
        private const int MAX_TEXTURE_SIZE = 1024;

        private static CancellationTokenSource _cancellationTokenSource;

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
            EditorUtility.ClearProgressBar();
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

                EditorUtility.DisplayProgressBar("Downloading nodes", $"{figmaNodeConfig.UnityExportPath}", 0);
                var nodeObject = await DownloadNode(figmaNodeConfig.NodeToken);
                Debug.Log($"---- Downloaded node  {figmaNodeConfig.NodeToken}, {figmaNodeConfig.UnityExportPath}");

                var documentNode = nodeObject["nodes"].Children<JProperty>().Select(p => p.Value["document"]).First();

                var batches = documentNode
                    .ToObject<FigmaDocument>()
                    .Map(ExtractNodeIds)
                    .SplitIntoBatches(BATCH_SIZE)
                    .ToList();

                EditorUtility.DisplayProgressBar(
                    "Downloading..",
                    $"{figmaNodeConfig.UnityExportPath}, batch {0}/{batches.Count}",
                    0);

                for (var index = 0; index < batches.Count; index++)
                {
                    EditorUtility.DisplayProgressBar(
                        "Downloading..",
                        $"{figmaNodeConfig.UnityExportPath}, batch {index}/{batches.Count}",
                        index / (float)batches.Count);

                    var batch = batches[index];
                    Debug.Log($"---- Processing batch {figmaNodeConfig.NodeToken} {index}");
                    var savedTextures = await ProcessNodes(batch, figmaNodeConfig);
                    Debug.Log($"----Processing batch done {figmaNodeConfig.NodeToken} {index}, {string.Join(", ", savedTextures)}");
                    importedAssets.AddRange(savedTextures);
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
                EditorUtility.ClearProgressBar();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
                Debug.Log($"---- Downloaded all images {figmaNodeConfig.NodeToken}, {figmaNodeConfig.UnityExportPath}");
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

        private async UniTask<IReadOnlyList<string>> SaveTextures(UniTask<IEnumerable<(string NodeName, Texture2D Texture)>> nodes,
            FigmaNodeConfig exportConfig) =>
            (await nodes).Select(x => SaveTexture(x.NodeName, x.Texture, exportConfig)).ToList();

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

                var progress = index / (float)importedAssets.Count;
                EditorUtility.DisplayProgressBar($"Setup import settings", $"{(int)(100 * progress)}% {importedAssetPath}", progress);
                Debug.Log($"---- Setup import settings {importedAssetPath}");

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.maxTextureSize = 1024;
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
            var response = await FetchUrl($"https://api.figma.com/v1/images/{_figmaProjectId}?ids={commaSeparatedVisibleNodeIds}&format={TEXTURE_FORMAT}");
            var figmaImages = response.ToObject<FigmaImages>();
            return batch.Select(node => (NodeUrl: figmaImages.Images[node.NodeId], NodeName: node.NodeName));
        }

        private async UniTask<IEnumerable<(string NodeName, Texture2D Texture)>> DownloadTextures(UniTask<IEnumerable<(string NodeUrl, string NodeName)>> nodes)
        {
            var tasks = new List<UniTask<(string NodeName, Texture2D Texture)>>();
            foreach (var (nodeUrl, nodeName) in await nodes)
            {
                tasks.Add(DownloadTexture(nodeName, nodeUrl));
            }

            return await UniTask.WhenAll(tasks);
        }

        private async UniTask<(string NodeName, Texture2D Texture)> DownloadTexture(string nodeName, string nodeUrl)
        {
            using var request = UnityWebRequestTexture.GetTexture(nodeUrl);
            await request.SendWebRequest().ToUniTask(cancellationToken: _cancellationTokenSource.Token);
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"---- Downloaded image: {nodeUrl} {nodeName}");
                return (NodeName: nodeName, Texture: DownloadHandlerTexture.GetContent(request));
            }

            throw new Exception($"Error downloading image: {nodeUrl} {nodeName} {request.error}");
        }

        private static string SaveTexture(string nodeName, Texture2D texture, FigmaNodeConfig config) =>
            Save(Path.Combine(config.UnityExportPath, nodeName), 
                TEXTURE_FORMAT,
                config.ExpandToPot, 
                config.Padding,
                config.AutoCrop,
                texture.EncodeToPNG());

        private IReadOnlyList<(string NodeId, string NodeName)> ExtractNodeIds(FigmaDocument figmaDocument) =>
            figmaDocument.Children
                .SelectMany(node => node.Type == "COMPONENT_SET"
                    ? node.Children.Select(child => (NodeId: child.Id, NodeName: child.Name))
                    : new[] { (NodeId: node.Id, NodeName: node.Name) })
                .ToList();

        private static string Save(string fullPath, string extension, bool extendToPot, int padding, bool autoCrop, byte[] bytes)
        {
            var directory = Path.GetDirectoryName(fullPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var data = (extendToPot, padding, autoCrop) switch
            {
                (true, _, false) => ExpandToPot(bytes),
                (true, _, true) => bytes.Map(AutoCrop).Map(ExpandToPot),
                (false, > 0, false) => Expand(bytes, padding),
                (false, > 0, true) => bytes.Map(AutoCrop).Map(x => Expand(x, padding)),
                (false, 0, true) => AutoCrop(bytes),
                _ => bytes
            };

            var fullPathWithExtension = $"{fullPath}.{extension}";
            File.WriteAllBytes(fullPathWithExtension, data);
            Debug.Log($"---- Saved {fullPathWithExtension}");
            return fullPathWithExtension;
        }
    }
}