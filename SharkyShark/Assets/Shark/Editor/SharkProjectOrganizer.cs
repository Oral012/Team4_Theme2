using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class SharkProjectOrganizer
{
    private const string OrganizedSessionKey = "SharkCourseAssetsOrganizedV3";
    private static bool organizing;
    private static bool waitingForImport;

    [MenuItem("Tools/Shark/Organize Shark Course Assets")]
    private static void OrganizeFromMenu()
    {
        SessionState.SetBool(OrganizedSessionKey, false);
        EnsureOrganized();
    }

    internal static bool EnsureOrganized()
    {
        if (SessionState.GetBool(OrganizedSessionKey, false))
        {
            return true;
        }

        if (organizing || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            if (!waitingForImport)
            {
                waitingForImport = true;
                EditorApplication.update += RetryWhenIdle;
            }

            return false;
        }

        organizing = true;
        bool success = OrganizeAssets();
        organizing = false;
        if (!success)
        {
            return false;
        }

        SessionState.SetBool(OrganizedSessionKey, true);
        AssetDatabase.SaveAssets();
        EditorApplication.delayCall += RefreshAssetsAfterOrganization;
        return false;
    }

    private static void RetryWhenIdle()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        EditorApplication.update -= RetryWhenIdle;
        waitingForImport = false;
        EnsureOrganized();
    }

    private static void RefreshAssetsAfterOrganization()
    {
        AssetDatabase.Refresh();
        EditorApplication.update += RebuildCourseWhenReady;
    }

    private static void RebuildCourseWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        EditorApplication.update -= RebuildCourseWhenReady;
        if (SceneManager.GetActiveScene().path == "Assets/Scenes/SampleScene.unity"
            && GameObject.Find("Shark Course") != null)
        {
            SharkCourseBuilder.RebuildCourse();
        }
    }

    private static bool OrganizeAssets()
    {
        EnsureFolder("Assets/Shark/Models");
        EnsureFolder("Assets/Shark/Models/Player");
        EnsureFolder("Assets/Shark/Models/Environment");
        EnsureFolder("Assets/Shark/Models/Shark");
        EnsureFolder("Assets/Shark/Animations");
        EnsureFolder("Assets/Shark/Textures");
        EnsureFolder("Assets/Shark/Textures/Player");
        EnsureFolder("Assets/Shark/Textures/Shark");
        EnsureFolder("Assets/Shark/Runtime");
        EnsureFolder("Assets/Shark/Editor");
        EnsureFolder("Assets/Shark/Animations/Controllers");

        bool success = true;
        success &= MoveAsset("Assets/Shark/source/female_white_shark_rigged_9anims.glb",
            "Assets/Shark/Models/Shark/female_white_shark_rigged_9anims.glb");
        success &= MoveAsset("Assets/Shark/PlayerAnimations/character-oopi.fbx",
            "Assets/Shark/Models/Player/character-oopi.fbx");
        success &= MoveAsset("Assets/Shark/PlayerAnimations/block-grass-large.fbx",
            "Assets/Shark/Models/Environment/block-grass-large.fbx");
        success &= MoveAsset("Assets/Shark/PlayerAnimations/Textures/colormap.png",
            "Assets/Shark/Textures/Player/colormap.png");
        success &= MoveAsset("Assets/Shark/textures/white_shark_female_basecolor_0.jpeg",
            "Assets/Shark/Textures/Shark/white_shark_female_basecolor_0.jpeg");
        success &= MoveAsset("Assets/Shark/PlayerAnimations/OopiKenney.mat",
            "Assets/Shark/Materials/CoursePlayer.mat");
        success &= MoveAsset("Assets/Shark/Animation/SharkPlatform.controller",
            "Assets/Shark/Animations/Controllers/SharkPlatform.controller");
        success &= MoveAsset("Assets/Shark/Animation/OopiTestPlayer.controller",
            "Assets/Shark/Animations/Controllers/CoursePlayer.controller");
        success &= MoveAsset("Assets/Shark/Prefabs/oopiTestPlayer.prefab",
            "Assets/Shark/Prefabs/Player.prefab");
        success &= MoveAsset("Assets/Shark/Scripts/SharkTestPlayerController.cs",
            "Assets/Shark/Runtime/SharkCoursePlayerController.cs");
        success &= MoveAsset("Assets/Shark/Scripts/SharkBiteReceiver.cs",
            "Assets/Shark/Runtime/SharkBiteReceiver.cs");
        success &= MoveAsset("Assets/Shark/Scripts/SharkCourseManager.cs",
            "Assets/Shark/Runtime/SharkCourseManager.cs");
        success &= MoveAsset("Assets/Shark/Scripts/SharkFinishLine.cs",
            "Assets/Shark/Runtime/SharkFinishLine.cs");
        success &= MoveAsset("Assets/Shark/Scripts/SharkPlatformController.cs",
            "Assets/Shark/Runtime/SharkPlatformController.cs");
        success &= MoveAsset("Assets/Shark/Scripts/SmoothPlayerCamera.cs",
            "Assets/Shark/Runtime/SmoothPlayerCamera.cs");
        success &= MoveAsset("Assets/Editor/SharkTestSceneSetup.cs",
            "Assets/Shark/Editor/SharkCourseSceneSetup.cs");
        success &= MoveAsset("Assets/Editor/SharkCourseSetup.cs",
            "Assets/Shark/Editor/SharkCourseBuilder.cs");
        success &= MoveAsset("Assets/Editor/SharkPlayerAnimationSetup.cs",
            "Assets/Shark/Editor/SharkPlayerAnimationSetup.cs");
        success &= MoveAsset("Assets/Editor/SharkProjectOrganizer.cs",
            "Assets/Shark/Editor/SharkProjectOrganizer.cs");

        success &= NormalizeTextureFolderCase();
        DeleteIfEmpty("Assets/Shark/PlayerAnimations/Textures");
        DeleteIfEmpty("Assets/Shark/PlayerAnimations");
        DeleteIfEmpty("Assets/Shark/Animation");
        DeleteIfEmpty("Assets/Shark/Scripts");
        DeleteIfEmpty("Assets/Shark/source");
        DeleteIfEmpty("Assets/Editor");
        DeleteIfEmpty("Assets/Shark/textures");

        return success;
    }

    private static bool NormalizeTextureFolderCase()
    {
        string sharkFolder = GetAbsoluteAssetPath("Assets/Shark");
        string lowercaseFolder = Directory.GetDirectories(sharkFolder)
            .FirstOrDefault(path => Path.GetFileName(path) == "textures");
        string temporaryPath = "Assets/Shark/TexturesRename";
        string temporaryFolder = GetAbsoluteAssetPath(temporaryPath);
        if (lowercaseFolder == null && !Directory.Exists(temporaryFolder))
        {
            return true;
        }

        if (lowercaseFolder != null)
        {
            string error = AssetDatabase.MoveAsset("Assets/Shark/textures", temporaryPath);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError("Could not stage the shark texture folder casing change: " + error);
                return false;
            }
        }

        string finalError = AssetDatabase.MoveAsset(temporaryPath, "Assets/Shark/Textures");
        if (!string.IsNullOrEmpty(finalError))
        {
            Debug.LogError("Could not normalize the shark texture folder casing: " + finalError);
            return false;
        }

        return true;
    }

    private static bool MoveAsset(string source, string destination)
    {
        string absoluteSource = GetAbsoluteAssetPath(source);
        if (!File.Exists(absoluteSource) && !Directory.Exists(absoluteSource))
        {
            string absoluteDestination = GetAbsoluteAssetPath(destination);
            if (File.Exists(absoluteDestination) || Directory.Exists(absoluteDestination))
            {
                return true;
            }

            Debug.LogError("Expected project asset is missing at both '" + source + "' and '" + destination + "'.");
            return false;
        }

        string error = AssetDatabase.MoveAsset(source, destination);
        if (string.IsNullOrEmpty(error))
        {
            return true;
        }

        Debug.LogError("Could not organize asset '" + source + "' to '" + destination + "': " + error);
        return false;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string folderName = Path.GetFileName(path);
        EnsureFolder(parent);
        string guid = AssetDatabase.CreateFolder(parent, folderName);
        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogError("Could not create Unity asset folder: " + path);
        }
    }

    private static void DeleteIfEmpty(string assetPath)
    {
        string absolutePath = GetAbsoluteAssetPath(assetPath);
        if (Directory.Exists(absolutePath) && Directory.GetFiles(absolutePath).Length == 0
            && Directory.GetDirectories(absolutePath).Length == 0)
        {
            AssetDatabase.DeleteAsset(assetPath);
        }
    }

    private static string GetAbsoluteAssetPath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }
}
