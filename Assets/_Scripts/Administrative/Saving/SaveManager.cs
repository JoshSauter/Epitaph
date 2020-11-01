﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Saving.SaveManagerForScene;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEditor;
#endif

namespace Saving {
    public static class SaveManager {
        public static Dictionary<string, SaveManagerForScene> activeScenes = new Dictionary<string, SaveManagerForScene>();
        public const string temp = "temp";

        public static SaveManagerForScene GetSaveManagerForScene(string sceneName) {
            if (sceneName == "") {
                return null;
			}

            if (!activeScenes.ContainsKey(sceneName)) {
                AddSaveManagerForScene(sceneName);
			}
            return activeScenes[sceneName];
		}

        public static SaveManagerForScene AddSaveManagerForScene(string sceneName) {
            if (!activeScenes.ContainsKey(sceneName)) {
                SaveManagerForScene saveForScene = new GameObject($"{sceneName} Save Manager").AddComponent<SaveManagerForScene>();
                SceneManager.MoveGameObjectToScene(saveForScene.gameObject, SceneManager.GetSceneByName(sceneName));
                activeScenes[sceneName] = saveForScene;
            }
            return activeScenes[sceneName];
		}

        public static void RemoveSaveManagerForScene(string sceneName) {
            activeScenes.Remove(sceneName);
		}

		static string SavePath(string saveFileName) {
            return $"{Application.persistentDataPath}/Saves/{saveFileName}";
        }

        public static void Save(string saveName) {
            Directory.CreateDirectory(SavePath(saveName));

            CopyDirectory(SavePath(temp), SavePath(saveName));

            // Make sure we initialize SaveManagerForScene for each loaded scene
            foreach (var sceneName in LevelManager.instance.loadedSceneNames) {
                GetSaveManagerForScene(sceneName);
			}

            DynamicObjectManager.SaveAllDynamicObjectsToDisk(saveName);

            foreach (var saveForScene in activeScenes.Values) {
                saveForScene.SaveScene(saveName);
            }
        }

        public async static void Load(string saveName) {
            CopyDirectory(SavePath(saveName), SavePath(temp));

            MainCanvas.instance.blackOverlayState = MainCanvas.BlackOverlayState.On;
            Time.timeScale = 0f;

            DynamicObjectManager.DeleteAllExistingDynamicObjects();

            SaveManagerForScene saveManagerForManagerScene = GetSaveManagerForScene(LevelManager.managerScene);
            SaveFileForScene saveFileForManagerScene = saveManagerForManagerScene.GetSaveFromDisk(saveName);
            saveManagerForManagerScene.LoadSceneFromSaveFile(saveFileForManagerScene);

            Debug.Log("Waiting for scenes to be loaded...");
            await TaskEx.WaitUntil(() => !LevelManager.instance.isCurrentlyLoadingScenes);
            Debug.Log("All scenes loaded into memory, loading save...");

            DynamicObjectManager.LoadDynamicObjectsFromDisk(saveName);

            Dictionary<SaveManagerForScene, SaveFileForScene> savesForScenes = LevelManager.instance.loadedSceneNames
                .Select(activeScene => GetSaveManagerForScene(activeScene))
                .ToDictionary(saveManagerForScene => saveManagerForScene.GetSaveFromDisk(saveName))
                // Swap Keys and Values
                .ToDictionary(kp => kp.Value, kp => kp.Key);

            // Step 2: Initialize SaveableObjects Dict for every scene
            foreach (var save in savesForScenes) {
                SaveManagerForScene saveManager = save.Key;
                saveManager.InitializeSaveableObjectsDict();
			}

            // Step 3: Load data for every objects in each scene (starting with the ManagerScene)
            saveManagerForManagerScene.LoadSceneFromSaveFile(saveFileForManagerScene);

            foreach (var save in savesForScenes) {
                SaveManagerForScene saveManager = save.Key;
                SaveFileForScene saveFile = save.Value;
                saveManager.LoadSceneFromSaveFile(saveFile);
            }

            // Play the level change banner and remove the black overlay
            LevelChangeBanner.instance.PlayBanner(LevelManager.instance.activeScene);
            Time.timeScale = 1f;
            MainCanvas.instance.blackOverlayState = MainCanvas.BlackOverlayState.FadingOut;
        }

        public static void DeleteSave(string saveName) {
            string path = SavePath(saveName);
            if (Directory.Exists(path)) {
                Directory.Delete(path, true);
            }
        }

        private static void CopyDirectory(string sourcePath, string targetPath) {
            if (Directory.Exists(sourcePath)) {
                Directory.CreateDirectory(targetPath);
                string[] files = Directory.GetFiles(sourcePath);

                // Copy the files and overwrite destination files if they already exist.
                foreach (string s in files) {
                    // Use static Path methods to extract only the file name from the path.
                    string fileName = Path.GetFileName(s);
                    string destFile = Path.Combine(targetPath, fileName);
                    File.Copy(s, destFile, true);
                }
            }
        }

#if UNITY_EDITOR
        [MenuItem("Saving/Add UniqueIds where needed (in loaded scenes)")]
        public static void AddUniqueIdsToAllSaveableObjectsLackingOne() {
            List<MonoBehaviour> test = Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                // Find all script instances which require a UniqueId
                .Where(s => s.GetType().GetCustomAttributes<RequireComponent>().Any(a => a.m_Type0 == typeof(UniqueId)))
                // Find all non-dynamic objects which lack a uniqueId
                .Where(s => s.GetComponent<UniqueId>() == null && s.GetComponent<DynamicObject>() == null)
                .ToList();

            int count = 0;
            foreach (var script in test) {
                // Do this check again so we don't double-add for multiple scripts on same GO
                if (script.GetComponent<UniqueId>() == null) {
                    script.gameObject.AddComponent<UniqueId>();
                    EditorSceneManager.MarkSceneDirty(script.gameObject.scene);
                    count++;
				}
			}

            if (count > 0) {
                Debug.Log($"Added UniqueIds to {count} objects:\n{string.Join("\n", test)}");
            }
            else {
                Debug.Log("Nothing to add a UniqueId to. All set.");
			}
		}

        [MenuItem("Saving/Clear Saves")]
        public static void ClearSaves() {
            Directory.Delete($"{Application.persistentDataPath}/Saves/", true);
        }
#endif
    }
}

public static class TaskEx {
    /// <summary>
    /// Blocks while condition is true or timeout occurs.
    /// </summary>
    /// <param name="condition">The condition that will perpetuate the block.</param>
    /// <param name="frequency">The frequency at which the condition will be check, in milliseconds.</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <exception cref="TimeoutException"></exception>
    /// <returns></returns>
    public static async Task WaitWhile(Func<bool> condition, int frequency = 25, int timeout = -1) {
        var waitTask = Task.Run(async () => {
            while (condition()) await Task.Delay(frequency);
        });

        if (waitTask != await Task.WhenAny(waitTask, Task.Delay(timeout)))
            throw new TimeoutException();
    }

    /// <summary>
    /// Blocks until condition is true or timeout occurs.
    /// </summary>
    /// <param name="condition">The break condition.</param>
    /// <param name="frequency">The frequency at which the condition will be checked.</param>
    /// <param name="timeout">The timeout in milliseconds.</param>
    /// <returns></returns>
    public static async Task WaitUntil(Func<bool> condition, int frequency = 25, int timeout = -1) {
        var waitTask = Task.Run(async () => {
            while (!condition()) await Task.Delay(frequency);
        });

        if (waitTask != await Task.WhenAny(waitTask,
                Task.Delay(timeout)))
            throw new TimeoutException();
    }
}