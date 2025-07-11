using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace UnityUtilsEditor
{
    public class FavoriteAssets : EditorWindow
    {
        [Serializable]
        public class DataWrapper
        {
            public List<AssetData> assets = new();
        }

        [Serializable]
        public class AssetData
        {
            public string path;
            public string name;
            public bool isFolder;
            public string type;
        }

        private DataWrapper _assetsData;

        private DataWrapper AssetsData
        {
            get
            {
                if (_assetsData == null) 
                    LoadData(false);

                return _assetsData;
            }
        }

        
        private static string GetDataDirectory => Path.Combine(Application.dataPath, "Editor");
        private static string GetDataPath => Path.Combine(GetDataDirectory, "favoriteAssets.json");
        
        private readonly EditorCommon _editorCommon = new();

        private Vector2 scrollView = Vector2.zero;
        private string _customPrefix;

        [MenuItem("Window/SolonityCore/Favorite Assets")]
        public static void ShowWindow()
        {
            GetWindow<FavoriteAssets>("★ Fav. Assets");
        }

        public void OnGUI()
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            _customPrefix = GUILayout.TextField(_customPrefix);
            if (GUILayout.Button("Pin Selected Assets", EditorStyles.miniButton))
            {
                foreach (string assetGUID in Selection.assetGUIDs)
                {
                    AssetData assetData = new AssetData();
                    assetData.path = AssetDatabase.GUIDToAssetPath(assetGUID);
                    Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetData.path);
                    assetData.name = $"{_customPrefix}{asset.name}";
                    assetData.type = asset.GetType().ToString();
                    AssetsData.assets.Add(assetData);
                }

                Sort();
                SaveData();
            }

            if (GUILayout.Button("Pin parent folder", EditorStyles.miniButton))
            {
                if (Selection.assetGUIDs.Length != 0)
                {
                    var assetGUID = Selection.assetGUIDs[0];

                    AssetData assetData = new AssetData();
                    assetData.path = AssetDatabase.GUIDToAssetPath(assetGUID);
                    Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetData.path);
                    var folderName = assetData.path.Split('/')[^2];
                    assetData.name = $"{_customPrefix}{folderName}";
                    assetData.isFolder = true;
                    assetData.type = "Folder";
                    AssetsData.assets.Add(assetData);
                }
                
                Sort();
                SaveData();
            }
            
            if (_editorCommon.RefreshButton("Reload fav assets", 40))
            {
                LoadData(true);
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                GUILayout.Label("Pinned Assets:");
                if (GUILayout.Button("▼ Sort Assets", EditorStyles.toolbarButton))
                {
                    Sort();
                    SaveData();
                }
            }
            GUILayout.EndHorizontal();

            scrollView = GUILayout.BeginScrollView(scrollView);
            for (var i = 0; i < AssetsData.assets.Count; i++)
            {
                var assetData = AssetsData.assets[i];
                var nextAssetData = i < AssetsData.assets.Count - 1 ? AssetsData.assets[i + 1] : null;
                GUILayout.BeginHorizontal();

                var extension = Path.GetExtension(assetData.path);
                var hasOpenBtn = extension.Equals(".unity");

                // const float baseSize = 150;
                const float openBtnSize = 50;

                var icon = assetData.isFolder ? _editorCommon.FolderIcon.Value : AssetDatabase.GetCachedIcon(assetData.path);
                GUI.skin.button.alignment = TextAnchor.MiddleLeft;
                if (GUILayout.Button(new GUIContent(" " + assetData.name, icon), GUILayout.MinWidth(1), GUILayout.Height(18)))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(assetData.path);
                    EditorGUIUtility.PingObject(asset);
                    Selection.activeObject = asset;
                }

                GUI.skin.button.alignment = TextAnchor.MiddleCenter;

                if (hasOpenBtn && GUILayout.Button(new GUIContent("Open", "Open scene"), GUILayout.Width(openBtnSize)))
                {
                    EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                    EditorSceneManager.OpenScene(assetData.path, OpenSceneMode.Single);
                }

                if (GUILayout.Button(new GUIContent("X", "Un-pin"), GUILayout.ExpandWidth(false)))
                {
                    RemovePin(assetData);
                    i--;
                }

                GUILayout.EndHorizontal();

                if (nextAssetData != null && assetData.type != nextAssetData.type)
                {
                    _editorCommon.DrawUILine(Color.gray);
                }
            }

            GUILayout.EndScrollView();
        }

        private void Sort()
        {
            AssetsData.assets.Sort(AssetDataComparer);
        }

        private void SaveData()
        {
            var directory = GetDataDirectory;
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string json = JsonUtility.ToJson(AssetsData, true);
            var dataPath = GetDataPath;
            File.WriteAllText(dataPath, json, Encoding.UTF8);
        }

        private void LoadData(bool log)
        {
            var path = GetDataPath;
            if (!File.Exists(path))
            {
                _assetsData = new DataWrapper();
                if (log)
                    Debug.Log($"DataWrapper created");
            }
            else
            {
                _assetsData = JsonUtility.FromJson<DataWrapper>(File.ReadAllText(path, Encoding.UTF8));

                if (log)
                    Debug.Log($"DataWrapper loaded from {path}");
            }
        }

        private void RemovePin(AssetData assetData)
        {
            AssetsData.assets.Remove(assetData);
            SaveData();
        }

        private int AssetDataComparer(AssetData left, AssetData right)
        {
            return string.Compare(left.type, right.type, StringComparison.Ordinal);
        }
    }
}