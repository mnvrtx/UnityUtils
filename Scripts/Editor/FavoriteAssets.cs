using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public class ProfileSettings
        {
            public string activeProfile = "Default";
        }

        [Serializable]
        public class Profile
        {
            public string name = "Default";
            public List<AssetData> assets = new();
            public DateTime lastModified;
        }

        [Serializable]
        public class AssetData
        {
            public string path;
            public string name;
            public bool isFolder;
            public string type;
        }

        private ProfileSettings _settings;
        private Dictionary<string, Profile> _profiles = new();
        private Profile _currentProfile;
        private string _newProfileName = "";
        private bool _showProfileManager = false;
        private bool _needsRefresh = false;

        // Rename feature variables
        private string _renamingProfile = null;
        private string _renameText = "";

        private ProfileSettings Settings
        {
            get
            {
                if (_settings == null)
                    LoadSettings();
                return _settings;
            }
        }

        private Dictionary<string, Profile> Profiles
        {
            get
            {
                if (_profiles == null || _profiles.Count == 0 || _needsRefresh)
                {
                    LoadAllProfiles();
                    _needsRefresh = false;
                }
                return _profiles;
            }
        }

        private Profile CurrentProfile
        {
            get
            {
                if (_currentProfile == null || _currentProfile.name != Settings.activeProfile)
                {
                    if (Profiles.ContainsKey(Settings.activeProfile))
                    {
                        _currentProfile = Profiles[Settings.activeProfile];
                    }
                    else if (Profiles.Count > 0)
                    {
                        _currentProfile = Profiles.Values.First();
                        Settings.activeProfile = _currentProfile.name;
                        SaveSettings();
                    }
                    else
                    {
                        // Create default profile if none exist
                        _currentProfile = new Profile { name = "Default" };
                        SaveProfile(_currentProfile);
                        _profiles[_currentProfile.name] = _currentProfile;
                        Settings.activeProfile = _currentProfile.name;
                        SaveSettings();
                    }
                }
                return _currentProfile;
            }
        }

        private static string GetDataDirectory => Path.Combine(Application.dataPath, "Editor", "FavoriteAssetsProfiles");
        private static string GetSettingsPath => Path.Combine(Application.dataPath, "Editor", "favoriteAssetsSettings.json");
        private static string GetProfilePath(string profileName) => Path.Combine(GetDataDirectory, $"{SanitizeFileName(profileName)}.json");

        private readonly EditorCommon _editorCommon = new();
        private Vector2 scrollView = Vector2.zero;
        private string _customPrefix;

        [MenuItem("Window/SolonityCore/Favorite Assets")]
        public static void ShowWindow()
        {
            GetWindow<FavoriteAssets>("★ Fav. Assets");
        }

        private void OnEnable()
        {
            LoadSettings();
            LoadAllProfiles();
        }

        private void OnFocus()
        {
            // Reload profiles in case they were modified externally
            // _needsRefresh = true;
        }

        public void OnGUI()
        {
            // Profile selector header
            DrawProfileHeader();

            // Profile manager panel
            if (_showProfileManager)
            {
                DrawProfileManager();
            }

            // Main content
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
                    CurrentProfile.assets.Add(assetData);
                }

                Sort();
                SaveProfile(CurrentProfile);
            }

            if (GUILayout.Button("Pin parent folder", EditorStyles.miniButton))
            {
                if (Selection.assetGUIDs.Length != 0)
                {
                    var assetGUID = Selection.assetGUIDs[0];
                    AssetData assetData = new AssetData();
                    assetData.path = AssetDatabase.GUIDToAssetPath(assetGUID);
                    Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetData.path);
                    var pathParts = assetData.path.Split('/');
                    var folderName = pathParts.Length >= 2 ? pathParts[^2] : "Root";
                    assetData.name = $"{_customPrefix}{folderName}";
                    assetData.isFolder = true;
                    assetData.type = "Folder";
                    CurrentProfile.assets.Add(assetData);
                }

                Sort();
                SaveProfile(CurrentProfile);
            }

            if (_editorCommon.RefreshButton("Load Profiles", 40))
            {
                LoadAllProfiles();
                Debug.Log($"Loaded {Profiles.Count} profiles from disk");
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                GUILayout.Label($"Pinned Assets:");
                if (GUILayout.Button("Sort Assets", EditorStyles.toolbarButton))
                {
                    Sort();
                    SaveProfile(CurrentProfile);
                }

                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();

            // Assets list
            scrollView = GUILayout.BeginScrollView(scrollView);
            for (var i = 0; i < CurrentProfile.assets.Count; i++)
            {
                var assetData = CurrentProfile.assets[i];
                var nextAssetData = i < CurrentProfile.assets.Count - 1 ? CurrentProfile.assets[i + 1] : null;
                GUILayout.BeginHorizontal();

                var extension = Path.GetExtension(assetData.path);
                var hasOpenBtn = extension.Equals(".unity");

                const float openBtnSize = 50;

                var icon = assetData.isFolder ? _editorCommon.FolderIcon.Value : AssetDatabase.GetCachedIcon(assetData.path);
                GUI.skin.button.alignment = TextAnchor.MiddleLeft;
                if (GUILayout.Button(new GUIContent(" " + assetData.name, icon), GUILayout.MinWidth(1), GUILayout.Height(18)))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(assetData.path);
                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                        Selection.activeObject = asset;
                    }
                    else
                    {
                        Debug.LogWarning($"Asset not found at path: {assetData.path}");
                    }
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

        private void DrawProfileHeader()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Profile:", GUILayout.Width(50));

            // Profile dropdown
            var profileNames = Profiles.Keys.OrderBy(x => x).ToArray();
            var currentIndex = Array.IndexOf(profileNames, Settings.activeProfile);
            if (currentIndex < 0) currentIndex = 0;

            if (profileNames.Length > 0)
            {
                var newIndex = EditorGUILayout.Popup(currentIndex, profileNames, EditorStyles.toolbarDropDown, GUILayout.Width(150));
                if (newIndex != currentIndex && newIndex >= 0 && newIndex < profileNames.Length)
                {
                    Settings.activeProfile = profileNames[newIndex];
                    _currentProfile = null; // Reset to force reload
                    SaveSettings();
                }
            }
            else
            {
                GUILayout.Label("No profiles", EditorStyles.toolbarDropDown, GUILayout.Width(150));
            }

            // Profile management button
            var managerButtonContent = new GUIContent(_showProfileManager ? "▲" : "▼", "Manage Profiles");
            if (GUILayout.Button(managerButtonContent, EditorStyles.toolbarButton, GUILayout.Width(25)))
            {
                _showProfileManager = !_showProfileManager;
                // Cancel any ongoing rename when closing the manager
                if (!_showProfileManager)
                {
                    _renamingProfile = null;
                    _renameText = "";
                }
            }

            // Export/Import buttons
            if (GUILayout.Button(new GUIContent("Export", "Export current profile"), EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                ExportProfile(CurrentProfile);
            }

            if (GUILayout.Button(new GUIContent("Import", "Import profile from file"), EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                ImportProfile();
            }

            GUILayout.FlexibleSpace();

            GUILayout.EndHorizontal();
        }

        private void DrawProfileManager()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);

            // Create new profile
            GUILayout.BeginHorizontal();
            GUILayout.Label("New Profile:", GUILayout.Width(80));
            _newProfileName = GUILayout.TextField(_newProfileName);

            GUI.enabled = !string.IsNullOrWhiteSpace(_newProfileName) &&
                         !Profiles.ContainsKey(_newProfileName);

            if (GUILayout.Button("Create", GUILayout.Width(60)))
            {
                CreateProfile(_newProfileName);
                _newProfileName = "";
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Profile list management
            GUILayout.Label("Manage Profiles:", EditorStyles.boldLabel);

            var sortedProfiles = Profiles.OrderBy(p => p.Key).ToList();

            foreach (var kvp in sortedProfiles)
            {
                var profile = kvp.Value;
                GUILayout.BeginHorizontal();

                var isActive = profile.name == Settings.activeProfile;
                var isRenaming = _renamingProfile == profile.name;

                if (isActive)
                {
                    GUI.enabled = false;
                    GUILayout.Label("►", GUILayout.Width(20));
                    GUI.enabled = true;
                }
                else
                {
                    GUILayout.Space(24);
                }

                // Profile name display or rename field
                if (isRenaming)
                {
                    // Handle Enter and Escape keys for rename
                    var currentEvent = Event.current;
                    if (currentEvent.type == EventType.KeyDown)
                    {
                        if (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter)
                        {
                            FinishRename(profile);
                            currentEvent.Use();
                        }
                        else if (currentEvent.keyCode == KeyCode.Escape)
                        {
                            CancelRename();
                            currentEvent.Use();
                        }
                    }

                    GUI.SetNextControlName("RenameField");
                    _renameText = GUILayout.TextField(_renameText, GUILayout.MinWidth(100));

                    // Focus the text field when starting rename
                    if (GUI.GetNameOfFocusedControl() != "RenameField")
                    {
                        GUI.FocusControl("RenameField");
                        EditorGUI.FocusTextInControl("RenameField");
                    }

                    // Confirm rename button
                    GUI.enabled = !string.IsNullOrWhiteSpace(_renameText) &&
                                 _renameText != profile.name &&
                                 !Profiles.ContainsKey(_renameText);

                    if (GUILayout.Button("✓", EditorStyles.miniButton, GUILayout.Width(20)))
                    {
                        FinishRename(profile);
                    }
                    GUI.enabled = true;

                    // Cancel rename button
                    if (GUILayout.Button("✗", EditorStyles.miniButton, GUILayout.Width(20)))
                    {
                        CancelRename();
                    }
                }
                else
                {
                    GUILayout.Label(profile.name);
                    GUILayout.Label($"({profile.assets.Count} assets)", EditorStyles.miniLabel);

                    GUILayout.FlexibleSpace();

                    // Rename button
                    if (GUILayout.Button("Rename", EditorStyles.miniButton, GUILayout.Width(55)))
                    {
                        StartRename(profile);
                    }

                    // Show file button
                    if (GUILayout.Button("Show File", EditorStyles.miniButton, GUILayout.Width(65)))
                    {
                        var filePath = GetProfilePath(profile.name);
                        EditorUtility.RevealInFinder(filePath);
                    }

                    // Duplicate button
                    if (GUILayout.Button("Duplicate", EditorStyles.miniButton, GUILayout.Width(60)))
                    {
                        DuplicateProfile(profile);
                    }

                    // Delete button (disabled for active profile)
                    GUI.enabled = !isActive && Profiles.Count > 1;
                    if (GUILayout.Button("Delete", EditorStyles.miniButton, GUILayout.Width(50)))
                    {
                        if (EditorUtility.DisplayDialog("Delete Profile",
                            $"Are you sure you want to delete profile '{profile.name}'?\nThis will delete the file: {GetProfilePath(profile.name)}",
                            "Delete", "Cancel"))
                        {
                            DeleteProfile(profile);
                        }
                    }
                    GUI.enabled = true;
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(5);
            GUILayout.Label($"Profiles folder: {GetDataDirectory}", EditorStyles.miniLabel);

            GUILayout.EndVertical();
            GUILayout.Space(5);
        }

        private void StartRename(Profile profile)
        {
            _renamingProfile = profile.name;
            _renameText = profile.name;
        }

        private void CancelRename()
        {
            _renamingProfile = null;
            _renameText = "";
        }

        private void FinishRename(Profile profile)
        {
            if (string.IsNullOrWhiteSpace(_renameText) || _renameText == profile.name || Profiles.ContainsKey(_renameText))
            {
                CancelRename();
                return;
            }

            var oldName = profile.name;
            var newName = _renameText.Trim();

            try
            {
                // Update the profile object
                profile.name = newName;
                profile.lastModified = DateTime.Now;

                // Remove old file
                var oldFilePath = GetProfilePath(oldName);
                if (File.Exists(oldFilePath))
                {
                    File.Delete(oldFilePath);
                }

                // Save with new name
                SaveProfile(profile);

                // Update profiles dictionary
                _profiles.Remove(oldName);
                _profiles[newName] = profile;

                // Update active profile setting if this was the active profile
                if (Settings.activeProfile == oldName)
                {
                    Settings.activeProfile = newName;
                    _currentProfile = null; // Force reload
                    SaveSettings();
                }

                Debug.Log($"Profile renamed from '{oldName}' to '{newName}'");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to rename profile: {e.Message}");
                // Revert profile name if something went wrong
                profile.name = oldName;
            }

            CancelRename();
        }

        private void CreateProfile(string name)
        {
            var newProfile = new Profile { name = name, lastModified = DateTime.Now };
            SaveProfile(newProfile);
            _profiles[name] = newProfile;
            Settings.activeProfile = name;
            _currentProfile = null;
            SaveSettings();
        }

        private void DuplicateProfile(Profile original)
        {
            var baseName = original.name + " Copy";
            var newName = baseName;
            var counter = 1;

            while (Profiles.ContainsKey(newName))
            {
                newName = $"{baseName} {counter++}";
            }

            var newProfile = new Profile
            {
                name = newName,
                lastModified = DateTime.Now,
                assets = new List<AssetData>(original.assets.Select(a => new AssetData
                {
                    path = a.path,
                    name = a.name,
                    isFolder = a.isFolder,
                    type = a.type
                }))
            };

            SaveProfile(newProfile);
            _profiles[newName] = newProfile;
            Settings.activeProfile = newName;
            _currentProfile = null;
            SaveSettings();
        }

        private void DeleteProfile(Profile profile)
        {
            var filePath = GetProfilePath(profile.name);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            _profiles.Remove(profile.name);

            if (Settings.activeProfile == profile.name)
            {
                if (_profiles.Count > 0)
                {
                    Settings.activeProfile = _profiles.Keys.First();
                }
                else
                {
                    CreateProfile("Default");
                }
                _currentProfile = null;
            }
            SaveSettings();
        }

        private void ExportProfile(Profile profile)
        {
            var path = EditorUtility.SaveFilePanel(
                "Export Profile",
                Application.dataPath,
                $"{profile.name}_export.json",
                "json"
            );

            if (!string.IsNullOrEmpty(path))
            {
                var json = JsonUtility.ToJson(profile, true);
                File.WriteAllText(path, json, Encoding.UTF8);
                Debug.Log($"Profile '{profile.name}' exported to: {path}");
            }
        }

        private void ImportProfile()
        {
            var path = EditorUtility.OpenFilePanel(
                "Import Profile",
                Application.dataPath,
                "json"
            );

            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path, Encoding.UTF8);
                    var importedProfile = JsonUtility.FromJson<Profile>(json);

                    // Generate unique name if needed
                    var originalName = importedProfile.name;
                    var counter = 1;
                    while (Profiles.ContainsKey(importedProfile.name))
                    {
                        importedProfile.name = $"{originalName} ({counter++})";
                    }

                    importedProfile.lastModified = DateTime.Now;
                    SaveProfile(importedProfile);
                    _profiles[importedProfile.name] = importedProfile;
                    Settings.activeProfile = importedProfile.name;
                    _currentProfile = null;
                    SaveSettings();

                    Debug.Log($"Profile imported as '{importedProfile.name}'");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to import profile: {e.Message}");
                }
            }
        }

        private void Sort()
        {
            CurrentProfile.assets.Sort(AssetDataComparer);
        }

        private void SaveProfile(Profile profile)
        {
            var directory = GetDataDirectory;
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            profile.lastModified = DateTime.Now;
            string json = JsonUtility.ToJson(profile, true);
            var profilePath = GetProfilePath(profile.name);
            File.WriteAllText(profilePath, json, Encoding.UTF8);
        }

        private void SaveSettings()
        {
            var directory = Path.GetDirectoryName(GetSettingsPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string json = JsonUtility.ToJson(Settings, true);
            File.WriteAllText(GetSettingsPath, json, Encoding.UTF8);
        }

        private void LoadSettings()
        {
            var path = GetSettingsPath;
            if (File.Exists(path))
            {
                _settings = JsonUtility.FromJson<ProfileSettings>(File.ReadAllText(path, Encoding.UTF8));
            }
            else
            {
                _settings = new ProfileSettings();
            }
        }

        private void LoadAllProfiles()
        {
            _profiles.Clear();

            var directory = GetDataDirectory;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                // Create default profile if no profiles exist
                CreateProfile("Default");
                return;
            }

            var profileFiles = Directory.GetFiles(directory, "*.json");

            foreach (var file in profileFiles)
            {
                try
                {
                    var json = File.ReadAllText(file, Encoding.UTF8);
                    var profile = JsonUtility.FromJson<Profile>(json);
                    if (profile != null && !string.IsNullOrEmpty(profile.name))
                    {
                        _profiles[profile.name] = profile;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to load profile from {file}: {e.Message}");
                }
            }

            _currentProfile = null; // Reset to force reload

            // Create default profile if none exist
            if (_profiles.Count == 0)
            {
                CreateProfile("Default");
            }
        }

        private void MigrateOldData()
        {
            // Try to migrate from single file format
            var oldPath = Path.Combine(Application.dataPath, "Editor", "favoriteAssetsProfiles.json");
            if (File.Exists(oldPath))
            {
                try
                {
                    var oldDataJson = File.ReadAllText(oldPath, Encoding.UTF8);

                    // First try to parse as ProfilesData (multi-profile format)
                    if (oldDataJson.Contains("\"profiles\""))
                    {
                        var oldData = JsonUtility.FromJson<OldProfilesData>(oldDataJson);
                        if (oldData != null && oldData.profiles != null)
                        {
                            foreach (var oldProfile in oldData.profiles)
                            {
                                var newProfile = new Profile
                                {
                                    name = oldProfile.name,
                                    assets = oldProfile.assets,
                                    lastModified = DateTime.Now
                                };
                                SaveProfile(newProfile);
                                _profiles[newProfile.name] = newProfile;
                            }

                            Settings.activeProfile = oldData.activeProfile;
                            SaveSettings();

                            Debug.Log($"Migrated {oldData.profiles.Count} profiles from old format");
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to migrate old data: {e.Message}");
                }
            }

            // Try to migrate from even older single-list format
            var veryOldPath = Path.Combine(Application.dataPath, "Editor", "favoriteAssets.json");
            if (File.Exists(veryOldPath) && _profiles.Count == 0)
            {
                try
                {
                    var oldDataJson = File.ReadAllText(veryOldPath, Encoding.UTF8);
                    var oldData = JsonUtility.FromJson<DataWrapper>(oldDataJson);

                    if (oldData != null && oldData.assets != null && oldData.assets.Count > 0)
                    {
                        var defaultProfile = new Profile
                        {
                            name = "Default",
                            assets = oldData.assets,
                            lastModified = DateTime.Now
                        };
                        SaveProfile(defaultProfile);
                        _profiles["Default"] = defaultProfile;
                        Settings.activeProfile = "Default";
                        SaveSettings();

                        Debug.Log($"Migrated {oldData.assets.Count} assets from legacy format to Default profile");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to migrate legacy data: {e.Message}");
                }
            }
        }

        private void RemovePin(AssetData assetData)
        {
            CurrentProfile.assets.Remove(assetData);
            SaveProfile(CurrentProfile);
        }

        private int AssetDataComparer(AssetData left, AssetData right)
        {
            return string.Compare(left.type, right.type, StringComparison.Ordinal);
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        }

        // Legacy data structures for migration
        [Serializable]
        private class DataWrapper
        {
            public List<AssetData> assets = new();
        }

        [Serializable]
        private class OldProfilesData
        {
            public string activeProfile = "Default";
            public List<Profile> profiles = new();
        }
    }
}
