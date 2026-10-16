using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class SharkPlayerAnimationSetup
{
    private const string PlayerModelPath = "Assets/Shark/Models/Player/character-oopi.fbx";
    private const string StartPlatformModelPath = "Assets/Shark/Models/Environment/block-grass-large.fbx";
    private const string SetupSessionKey = "SharkCoursePlayerAnimationsAppliedV8";

    static SharkPlayerAnimationSetup()
    {
        EditorApplication.delayCall += TryApplyOnce;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("Tools/Shark/Refresh Course Player, Start Platform, and Camera")]
    private static void RefreshFromMenu()
    {
        SharkCourseSceneSetup.RefreshCourseSetup();
    }

    private static void TryApplyOnce()
    {
        if (EditorApplication.isPlaying
            || SessionState.GetBool(SetupSessionKey, false)
            || SceneManager.GetActiveScene().path != "Assets/Scenes/SampleScene.unity")
        {
            return;
        }

        GameObject setupRoot = GameObject.Find("Shark Course");
        if (setupRoot == null
            || AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath) == null
            || AssetDatabase.LoadAssetAtPath<GameObject>(StartPlatformModelPath) == null)
        {
            return;
        }

        string[] requiredClips = { "idle", "walk", "sprint", "jump", "fall", "die" };
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(PlayerModelPath).OfType<AnimationClip>().ToArray();
        if (requiredClips.Any(name => !clips.Any(clip => clip.name == name || clip.name.EndsWith("|" + name))))
        {
            return;
        }

        if (setupRoot.GetComponent<SharkCourseManager>() == null)
        {
            SharkCourseSceneSetup.RefreshCourseSetup();
        }

        SessionState.SetBool(SetupSessionKey, true);
        ConfigurePlayerAnimationLoops();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += TryApplyOnce;
        }
    }

    private static void ConfigurePlayerAnimationLoops()
    {
        ModelImporter importer = AssetImporter.GetAtPath(PlayerModelPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError("Could not configure loop settings for the imported oopi animations.");
            return;
        }

        ModelImporterClipAnimation[] clipSettings = importer.clipAnimations;
        if (clipSettings.Length == 0)
        {
            clipSettings = importer.defaultClipAnimations;
        }

        bool changed = false;
        foreach (ModelImporterClipAnimation clip in clipSettings)
        {
            string name = clip.name;
            bool shouldLoop = name == "idle" || name == "walk" || name == "sprint" || name == "fall";
            if (clip.loopTime != shouldLoop || clip.loopPose != shouldLoop)
            {
                clip.loopTime = shouldLoop;
                clip.loopPose = shouldLoop;
                changed = true;
            }
        }

        if (changed)
        {
            importer.clipAnimations = clipSettings;
            importer.SaveAndReimport();
        }
    }
}
