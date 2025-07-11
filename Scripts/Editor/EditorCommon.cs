using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnityUtilsEditor
{
    public class EditorCommon
    {
        public readonly Cache<Texture> FolderIcon = new(() => EditorGUIUtility.IconContent("d_Folder Icon").image);
        public readonly Cache<Texture> ReloadIcon = new(() => EditorGUIUtility.IconContent("TreeEditor.Refresh").image);
        public readonly Cache<Texture> CreateAddNewIcon = new(() => EditorGUIUtility.IconContent("CreateAddNew").image);
        public readonly Cache<Texture> DeleteIcon = new(() => EditorGUIUtility.IconContent("d_winbtn_win_close").image);

        public readonly Cache<GUIStyle> RichLabel = new(() =>
        {
            var s = new GUIStyle(EditorStyles.label);
            s.richText = true;
            return s;
        });

        public readonly Cache<GUIStyle> RichHelp = new(() =>
        {
            var s = new GUIStyle(GUI.skin.GetStyle("HelpBox"));
            s.richText = true;
            return s;
        });

        public readonly Cache<GUIStyle> RichFoldout = new(() =>
        {
            var s = new GUIStyle(EditorStyles.foldout);
            s.richText = true;
            return s;
        });

        public readonly Cache<GUIStyle> RichBoldLabel = new(() =>
        {
            var s = new GUIStyle(EditorStyles.boldLabel);
            s.richText = true;
            return s;
        });

        public readonly Cache<GUIStyle> ErrorText = new(() =>
        {
            var guiStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 12,
            };

            ColorUtility.TryParseHtmlString("#FF4251", out var color);
            guiStyle.normal.textColor = color;

            return guiStyle;
        });

        public static Scene GetSceneState(out PrefabStage prefabStage)
        {
            Scene scene;
            prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

            if (prefabStage != null)
                scene = prefabStage.scene;
            else
                scene = SceneManager.GetActiveScene();
            return scene;
        }

        public static List<T> FindAllFromScene<T>(Scene scene)
        {
            var allComponentns = scene.GetRootGameObjects()
                .SelectMany(q => q.GetComponentsInChildren<T>(includeInactive: true)).ToList();
            return allComponentns;
        }


        public bool AddButton(string tooltip, int width)
        {
            return GUILayout.Button(new GUIContent(CreateAddNewIcon.Value, tooltip), GUILayout.Width(width));
        }

        public bool DeleteButton(string tooltip, int width)
        {
            return GUILayout.Button(new GUIContent(DeleteIcon.Value, tooltip), GUILayout.Width(width));
        }

        public bool RefreshButton(string tooltip, int width)
        {
            return GUILayout.Button(new GUIContent(ReloadIcon.Value, tooltip), GUILayout.Width(width));
        }

        public void SaveState(Object obj, string str)
        {
            Undo.RegisterCompleteObjectUndo(obj, str);
            Undo.FlushUndoRecordObjects();
        }

        public void DrawUILine(Color color, int thickness = 2, int padding = 10)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
            r.height = thickness;
            r.y += padding / 2;
            r.x -= 2;
            r.width += 6;
            EditorGUI.DrawRect(r, color);
        }

        public void DrawVerticalLine(Color color, int thickness = 2, int padding = 10)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Width(padding + thickness));
            r.width = thickness;
            r.x += padding / 2;
            r.y -= 2;
            r.height += 6;
            EditorGUI.DrawRect(r, color);
        }

        public T Property<T>(Func<T> f, Object target, string str)
        {
            EditorGUI.BeginChangeCheck();
            var cache = f();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, str);
            }

            return cache;
        }

        public static void OffsetBlock(Action contentRenderer, bool needBorder = false)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space(10, false);

            if (needBorder)
                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(false));
            else
                EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(false));

            contentRenderer.Invoke();
            EditorGUILayout.Space(2, false);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        public static T[] FindAllAssetsOf<T>() where T : Object
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            var assets = new T[guids.Length];

            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                assets[i] = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            }

            return assets;
        }
    }
}