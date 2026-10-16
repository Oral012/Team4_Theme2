using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class SharkCourseBuilder
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string SetupRootName = "Shark Course";
    private const string LegacySetupRootName = "Shark Test Setup";
    private const string CourseFloorName = "Course Floor";
    private const string LegacyFloorName = "Test Floor";
    private const string StartPlatformPrefabPath = "Assets/Shark/Prefabs/StartPlatform.prefab";
    private const string SharkPrefabPath = "Assets/Shark/Prefabs/SharkPlatform.prefab";
    private const string PlayerPrefabPath = "Assets/Shark/Prefabs/Player.prefab";
    private const string MaterialFolder = "Assets/Shark/Materials";
    private const float StartPlatformTop = 0.7f;
    private const float SharkPlatformRootOffset = 1.05f;
    private const float StartPositionZ = -11f;
    private const float FinishPositionZ = 27f;
    private const float SharkFirstPositionZ = -2f;
    private const float SharkSpacing = 5f;
    private static readonly Vector3 PlatformSize = new Vector3(8f, 2f, 10f);

    static SharkCourseBuilder()
    {
        EditorApplication.delayCall += BuildIfNeeded;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("Tools/Shark/Build Jump Course")]
    private static void BuildFromMenu()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            Debug.LogError("Open Assets/Scenes/SampleScene before building the shark jump course.");
            return;
        }

        BuildCourse();
    }

    internal static void RebuildCourse()
    {
        BuildCourse();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += BuildIfNeeded;
        }
    }

    private static void BuildIfNeeded()
    {
        if (EditorApplication.isPlaying)
        {
            return;
        }

        if (!SharkProjectOrganizer.EnsureOrganized())
        {
            return;
        }

        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            return;
        }

        GameObject setupRoot = FindSetupRoot();
        if (setupRoot == null)
        {
            return;
        }

        SharkCourseManager course = setupRoot.GetComponent<SharkCourseManager>();
        if (course != null && course.CourseVersion >= SharkCourseManager.CurrentCourseVersion)
        {
            return;
        }

        BuildCourse();
    }

    private static void BuildCourse()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject setupRoot = FindSetupRoot();
        if (scene.path != ScenePath || setupRoot == null)
        {
            Debug.LogError("The shark setup is missing from SampleScene. Build the shark course setup before the course.");
            return;
        }

        setupRoot.name = SetupRootName;

        GameObject startPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StartPlatformPrefabPath);
        GameObject sharkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SharkPrefabPath);
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (startPrefab == null || sharkPrefab == null || playerPrefab == null)
        {
            Debug.LogError("The start platform, shark, or animated player prefab is missing. Refresh the shark course setup before building the course.");
            return;
        }

        if (!ConfigureSharkPrefab(sharkPrefab))
        {
            return;
        }

        EnsureFolder(MaterialFolder);
        Material platformMaterial = CreatePaletteMaterial(
            MaterialFolder + "/KenneyFactorySlate.mat",
            new Color(0.42f, 0.42f, 0.54f, 1f));
        Material accentMaterial = CreatePaletteMaterial(
            MaterialFolder + "/KenneyFactoryAmber.mat",
            new Color(0.88f, 0.64f, 0.36f, 1f));
        Material waterMaterial = CreatePaletteMaterial(
            MaterialFolder + "/KenneyFactoryWater.mat",
            new Color(0.30f, 0.43f, 0.68f, 1f));
        if (platformMaterial == null || accentMaterial == null || waterMaterial == null)
        {
            return;
        }

        SharkCourseManager courseManager = setupRoot.GetComponent<SharkCourseManager>();
        float groundHeight = GetGroundHeight(setupRoot, courseManager);

        for (int i = setupRoot.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = setupRoot.transform.GetChild(i);
            if (child.name != CourseFloorName && child.name != LegacyFloorName)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        ConfigureOcean(setupRoot.transform, groundHeight, waterMaterial);

        GameObject startPlatform = (GameObject)PrefabUtility.InstantiatePrefab(startPrefab);
        startPlatform.name = "Start Platform";
        startPlatform.transform.SetParent(setupRoot.transform, false);
        startPlatform.transform.localPosition = new Vector3(
            0f,
            groundHeight + StartPlatformTop - PlatformSize.y * 0.5f,
            StartPositionZ);
        ApplyMaterial(startPlatform, platformMaterial);
        CreateStripe(
            setupRoot.transform,
            "Start Line",
            new Vector3(PlatformSize.x - 0.4f, 0.08f, 0.3f),
            new Vector3(0f, groundHeight + StartPlatformTop + 0.05f, StartPositionZ + PlatformSize.z * 0.5f - 0.45f),
            accentMaterial);

        GameObject finishPlatform = (GameObject)PrefabUtility.InstantiatePrefab(startPrefab);
        finishPlatform.name = "Finish Platform";
        finishPlatform.transform.SetParent(setupRoot.transform, false);
        finishPlatform.transform.localPosition = new Vector3(
            0f,
            groundHeight + StartPlatformTop - PlatformSize.y * 0.5f,
            FinishPositionZ);
        ApplyMaterial(finishPlatform, platformMaterial);

        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        player.name = "Course Player";
        player.transform.SetParent(setupRoot.transform, false);
        player.transform.SetPositionAndRotation(
            new Vector3(0f, groundHeight + StartPlatformTop, StartPositionZ),
            Quaternion.identity);

        SharkPlatformController[] sharks = new SharkPlatformController[5];
        for (int i = 0; i < sharks.Length; i++)
        {
            GameObject shark = (GameObject)PrefabUtility.InstantiatePrefab(sharkPrefab);
            shark.name = "Shark " + (i + 1);
            shark.transform.SetParent(setupRoot.transform, false);
            shark.transform.localPosition = new Vector3(
                0f,
                groundHeight + SharkPlatformRootOffset,
                SharkFirstPositionZ + SharkSpacing * i);

            SharkPlatformController controller = shark.GetComponent<SharkPlatformController>();
            controller.BiteDelay = 2f;
            controller.ConfigureSwimming(1.6f, 1f, i * 1.25f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            EditorUtility.SetDirty(controller);
            sharks[i] = controller;
        }

        GameObject finishLine = new GameObject("Finish Line");
        finishLine.transform.SetParent(setupRoot.transform, false);
        finishLine.transform.localPosition = new Vector3(0f, groundHeight + StartPlatformTop, FinishPositionZ);
        BoxCollider finishTrigger = finishLine.AddComponent<BoxCollider>();
        finishTrigger.isTrigger = true;
        finishTrigger.center = new Vector3(0f, 1f, 0f);
        finishTrigger.size = new Vector3(PlatformSize.x + 2f, 2f, 1f);
        Rigidbody finishBody = finishLine.AddComponent<Rigidbody>();
        finishBody.isKinematic = true;
        finishBody.useGravity = false;
        SharkFinishLine finishBehavior = finishLine.AddComponent<SharkFinishLine>();

        CreateStripe(
            finishLine.transform,
            "Finish Stripe",
            new Vector3(PlatformSize.x - 0.4f, 0.08f, 0.35f),
            new Vector3(0f, 0.05f, 0f),
            accentMaterial);

        if (courseManager == null)
        {
            courseManager = setupRoot.AddComponent<SharkCourseManager>();
        }

        SmoothPlayerCamera cameraFollow = Camera.main != null
            ? Camera.main.GetComponent<SmoothPlayerCamera>()
            : null;
        if (cameraFollow != null)
        {
            cameraFollow.Target = player.transform;
            cameraFollow.InputEnabled = true;
        }

        courseManager.Configure(
            player,
            player.transform.position,
            player.transform.rotation,
            finishBehavior,
            sharks,
            cameraFollow,
            groundHeight);

        EditorUtility.SetDirty(courseManager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Built the shark jump course: start platform, five moving sharks, and a finish platform. WASD moves, Space jumps, Shift sprints, and R restarts.");
    }

    private static GameObject FindSetupRoot()
    {
        GameObject setupRoot = GameObject.Find(SetupRootName);
        return setupRoot != null ? setupRoot : GameObject.Find(LegacySetupRootName);
    }

    private static float GetGroundHeight(GameObject setupRoot, SharkCourseManager courseManager)
    {
        if (courseManager != null)
        {
            return courseManager.GroundHeight;
        }

        Transform floor = FindCourseFloor(setupRoot.transform);
        Collider floorCollider = floor != null ? floor.GetComponent<Collider>() : null;
        return floorCollider != null ? floorCollider.bounds.max.y : 0f;
    }

    private static bool ConfigureSharkPrefab(GameObject sharkPrefab)
    {
        SharkPlatformController controller = sharkPrefab.GetComponent<SharkPlatformController>();
        if (controller == null)
        {
            Debug.LogError("The shark platform prefab is missing its controller.", sharkPrefab);
            return false;
        }

        SerializedObject settings = new SerializedObject(controller);
        settings.FindProperty("biteDelay").floatValue = 2f;
        settings.FindProperty("bitePitchDegrees").floatValue = 60f;
        settings.FindProperty("swimRange").floatValue = 1.6f;
        settings.FindProperty("swimSpeed").floatValue = 1f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
        PrefabUtility.SavePrefabAsset(sharkPrefab);
        return true;
    }

    private static void ConfigureOcean(Transform setupRoot, float groundHeight, Material waterMaterial)
    {
        Transform floor = FindCourseFloor(setupRoot);
        if (floor == null)
        {
            GameObject ocean = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ocean.name = CourseFloorName;
            ocean.transform.SetParent(setupRoot, false);
            ocean.transform.localScale = new Vector3(50f, 1f, 100f);
            ocean.transform.localPosition = new Vector3(0f, groundHeight, 5f);
            floor = ocean.transform;
        }
        else
        {
            floor.name = CourseFloorName;
        }

        Collider collider = floor.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        MeshRenderer renderer = floor.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = waterMaterial;
        }
    }

    private static Transform FindCourseFloor(Transform setupRoot)
    {
        Transform floor = setupRoot.Find(CourseFloorName);
        return floor != null ? floor : setupRoot.Find(LegacyFloorName);
    }

    private static Material CreatePaletteMaterial(string path, Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                Debug.LogError("Neither the URP Lit shader nor the Standard shader is available.");
                return null;
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.name = System.IO.Path.GetFileNameWithoutExtension(path);
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ApplyMaterial(GameObject root, Material material)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            int materialCount = Mathf.Max(1, renderer.sharedMaterials.Length);
            renderer.sharedMaterials = Enumerable.Repeat(material, materialCount).ToArray();
        }
    }

    private static void CreateStripe(
        Transform parent,
        string stripeName,
        Vector3 size,
        Vector3 position,
        Material material)
    {
        GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stripe.name = stripeName;
        stripe.transform.SetParent(parent, false);
        stripe.transform.localPosition = position;
        stripe.transform.localScale = size;
        Collider collider = stripe.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        stripe.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
