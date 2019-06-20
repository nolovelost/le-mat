using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public class EditorLevelLoader
{
    static EditorLevelLoader()
    {
        EditorSceneManager.sceneOpened += OnSceneOpen;
        EditorSceneManager.sceneClosed += OnSceneClose;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += OnUpdate;
    }

    static void OnSceneOpen(Scene scene, OpenSceneMode mode)
    {
        GameDebug.Log("OPENED Scene: " + scene.name + "\nPath: " + scene.path);
    }

    static void OnSceneClose(Scene scene)
    {
        GameDebug.Log("CLOSED Scene: " + scene.name + "\nPath: " + scene.path);
    }

    static void OnPlayModeStateChanged(PlayModeStateChange mode)
    {
        GameDebug.Log("PlayMode is changed to: " + mode.ToString());
    }

    static void OnUpdate()
    {

    }
}
