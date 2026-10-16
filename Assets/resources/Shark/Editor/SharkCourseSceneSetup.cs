using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class SharkCourseSceneSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string SharkModelPath = "Assets/Shark/Models/Shark/female_white_shark_rigged_9anims.glb";
    private const string PlayerModelPath = "Assets/Shark/Models/Player/character-oopi.fbx";
    private const string StartPlatformModelPath = "Assets/Shark/Models/Environment/block-grass-large.fbx";
    private const string PlayerAnimationFolder = "Assets/Shark/Models/Player";
    private const string PrefabFolder = "Assets/Shark/Prefabs";
    private const string AnimationFolder = "Assets/Shark/Animations/Controllers";
    private const string SetupRootName = "Shark Course";
    private const string CalmAnimationName = "WS_Swim_Cruise_MouthClosed_Loop";
    private const string BiteAnimationName = "WS_Attack_Bite";
    private static readonly Vector3 StartPlatformSize = new Vector3(8f, 2f, 10f);
    private const float StartPlatformTopHeight = 0.7f;
    private const float StartPlatformZ = -7f;

    [MenuItem("Tools/Shark/Build Shark Course Setup")]
    private static void BuildFromMenu()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            Debug.LogError("Open Assets/Scenes/SampleScene before building the shark course setup.");
            return;
        }

        BuildCourseSetup();
    }

    [MenuItem("Tools/Shark/Align Shark Course to Ground")]
    private static void AlignFromMenu()
    {
        AlignCourseToGround();
    }

    private static void BuildCourseSetup()
    {
        GameObject existingSetup = GameObject.Find(SetupRootName)
            ?? GameObject.Find("Shark Test Setup");
        if (existingSetup != null)
        {
            if (existingSetup.GetComponent<SharkCourseManager>() != null)
            {
                SharkCourseBuilder.RebuildCourse();
                return;
            }

            RefreshCourseSetup();
            return;
        }

        GameObject sharkModel = AssetDatabase.LoadAssetAtPath<GameObject>(SharkModelPath);
        GameObject playerModel = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath);
        GameObject startPlatformModel = AssetDatabase.LoadAssetAtPath<GameObject>(StartPlatformModelPath);
        if (sharkModel == null || playerModel == null || startPlatformModel == null)
        {
            Debug.LogError("The shark, animated player, or FallGuyClone platform model has not finished importing. Run Tools/Shark/Build Shark Course Setup after imports finish.");
            return;
        }

        EnsureFolder(PrefabFolder);
        EnsureFolder(AnimationFolder);
        EnsureFolder(PlayerAnimationFolder);
        AnimatorController controller = BuildAnimationController();
        if (controller == null)
        {
            return;
        }

        AnimatorController playerController = BuildPlayerAnimationController();
        if (playerController == null)
        {
            return;
        }

        GameObject sharkPrefab = BuildSharkPrefab(sharkModel, controller);
        GameObject playerPrefab = BuildPlayerPrefab(playerModel, playerController);
        GameObject startPlatformPrefab = BuildStartPlatformPrefab(startPlatformModel);
        if (sharkPrefab == null || playerPrefab == null || startPlatformPrefab == null)
        {
            return;
        }

        GameObject setupRoot = new GameObject(SetupRootName);
        Undo.RegisterCreatedObjectUndo(setupRoot, "Build shark course");

        float floorHeight = GetOrCreateCourseFloor(setupRoot);

        GameObject startPlatform = (GameObject)PrefabUtility.InstantiatePrefab(startPlatformPrefab);
        startPlatform.transform.SetParent(setupRoot.transform, false);
        startPlatform.transform.localPosition = new Vector3(
            0f,
            floorHeight + StartPlatformTopHeight - StartPlatformSize.y * 0.5f,
            StartPlatformZ);
        Undo.RegisterCreatedObjectUndo(startPlatform, "Build shark course");

        GameObject shark = (GameObject)PrefabUtility.InstantiatePrefab(sharkPrefab);
        shark.transform.SetParent(setupRoot.transform, false);
        shark.transform.localPosition = new Vector3(0f, floorHeight + 1.05f, 0f);
        Undo.RegisterCreatedObjectUndo(shark, "Build shark course");

        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        player.name = "Course Player";
        player.transform.SetParent(setupRoot.transform, false);
        player.transform.localPosition = new Vector3(0f, floorHeight + StartPlatformTopHeight, StartPlatformZ);
        Undo.RegisterCreatedObjectUndo(player, "Build shark course");

        SetCameraTarget(Camera.main, player.transform);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = shark;
        Debug.Log("Built the shark course setup. Move with WASD, hold Shift to sprint, jump with Space, and stay aboard a shark for 2 seconds to trigger its bite.", shark);
    }

    internal static void RefreshCourseSetup()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject setupRoot = GameObject.Find(SetupRootName);
        if (scene.path != ScenePath || setupRoot == null)
        {
            return;
        }

        if (setupRoot.GetComponent<SharkCourseManager>() != null)
        {
            SharkCourseBuilder.RebuildCourse();
            return;
        }

        GameObject sharkModel = AssetDatabase.LoadAssetAtPath<GameObject>(SharkModelPath);
        GameObject playerModel = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath);
        GameObject startPlatformModel = AssetDatabase.LoadAssetAtPath<GameObject>(StartPlatformModelPath);
        if (sharkModel == null || playerModel == null || startPlatformModel == null)
        {
            return;
        }

        EnsureFolder(PrefabFolder);
        EnsureFolder(AnimationFolder);
        EnsureFolder(PlayerAnimationFolder);

        AnimatorController sharkController = BuildAnimationController();
        AnimatorController playerController = BuildPlayerAnimationController();
        if (sharkController == null || playerController == null)
        {
            return;
        }

        GameObject sharkPrefab = BuildSharkPrefab(sharkModel, sharkController);
        GameObject playerPrefab = BuildPlayerPrefab(playerModel, playerController);
        GameObject startPlatformPrefab = BuildStartPlatformPrefab(startPlatformModel);
        if (sharkPrefab == null || playerPrefab == null || startPlatformPrefab == null)
        {
            return;
        }

        Transform oldShark = setupRoot.transform.Find("SharkPlatform");
        Transform oldPlayer = setupRoot.transform.Find("Course Player");
        if (oldShark == null || oldPlayer == null)
        {
            Debug.LogError("The shark course scene is missing its shark or player instance.", setupRoot);
            return;
        }

        float floorHeight = GetOrCreateCourseFloor(setupRoot);
        GameObject shark = ReplaceScenePrefab(oldShark, sharkPrefab, "SharkPlatform");
        GameObject player = ReplaceScenePrefab(oldPlayer, playerPrefab, "Course Player");
        Transform oldStartPlatform = setupRoot.transform.Find("Start Platform");
        GameObject startPlatform = oldStartPlatform != null
            ? ReplaceScenePrefab(oldStartPlatform, startPlatformPrefab, "Start Platform")
            : (GameObject)PrefabUtility.InstantiatePrefab(startPlatformPrefab);
        startPlatform.transform.SetParent(setupRoot.transform, false);
        startPlatform.transform.localPosition = new Vector3(
            0f,
            floorHeight + StartPlatformTopHeight - StartPlatformSize.y * 0.5f,
            StartPlatformZ);
        player.transform.localPosition = new Vector3(0f, floorHeight + StartPlatformTopHeight, StartPlatformZ);
        SetCameraTarget(Camera.main, player.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Updated the shark course player, start platform, and follow camera.", player);
    }

    private static GameObject ReplaceScenePrefab(Transform oldInstance, GameObject prefab, string instanceName)
    {
        Transform parent = oldInstance.parent;
        Vector3 localPosition = oldInstance.localPosition;
        Quaternion localRotation = oldInstance.localRotation;
        Vector3 localScale = oldInstance.localScale;
        int siblingIndex = oldInstance.GetSiblingIndex();
        Object.DestroyImmediate(oldInstance.gameObject);

        GameObject replacement = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        replacement.name = instanceName;
        replacement.transform.SetParent(parent, false);
        replacement.transform.localPosition = localPosition;
        replacement.transform.localRotation = localRotation;
        replacement.transform.localScale = localScale;
        replacement.transform.SetSiblingIndex(siblingIndex);
        return replacement;
    }

    private static void SetCameraTarget(Camera camera, Transform target)
    {
        if (camera == null || target == null)
        {
            return;
        }

        SmoothPlayerCamera follow = camera.GetComponent<SmoothPlayerCamera>();
        if (follow == null)
        {
            follow = camera.gameObject.AddComponent<SmoothPlayerCamera>();
        }

        SerializedObject cameraSettings = new SerializedObject(follow);
        cameraSettings.FindProperty("pivotOffset").vector3Value = new Vector3(0f, 1.5f, 0f);
        cameraSettings.FindProperty("distance").floatValue = 7f;
        cameraSettings.FindProperty("minimumDistance").floatValue = 3f;
        cameraSettings.FindProperty("maximumDistance").floatValue = 12f;
        cameraSettings.FindProperty("mouseSensitivity").floatValue = 0.12f;
        cameraSettings.FindProperty("minimumPitch").floatValue = -25f;
        cameraSettings.FindProperty("maximumPitch").floatValue = 70f;
        cameraSettings.FindProperty("pivotSharpness").floatValue = 14f;
        cameraSettings.FindProperty("zoomSharpness").floatValue = 5f;
        cameraSettings.FindProperty("collisionRadius").floatValue = 0.3f;
        cameraSettings.FindProperty("collisionMask").intValue = 1;
        cameraSettings.ApplyModifiedPropertiesWithoutUndo();
        follow.Target = target;
        camera.transform.position = target.position + new Vector3(0f, 3.4f, -9f);
        Vector3 focusPoint = target.position + new Vector3(0f, 0.85f, 0f);
        camera.transform.rotation = Quaternion.LookRotation(focusPoint - camera.transform.position, Vector3.up);
        follow.SnapToTarget();
    }

    private static void AlignCourseToGround()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            return;
        }

        GameObject setupRoot = GameObject.Find(SetupRootName);
        if (setupRoot == null)
        {
            return;
        }

        float floorHeight = GetOrCreateCourseFloor(setupRoot);
        Transform shark = setupRoot.transform.Find("SharkPlatform");
        Transform player = setupRoot.transform.Find("Course Player");
        if (shark == null || player == null)
        {
            Debug.LogError("The shark course scene is missing its shark or player instance.", setupRoot);
            return;
        }

        Undo.RecordObject(shark, "Align shark course to ground");
        Undo.RecordObject(player, "Align shark course to ground");
        shark.localPosition = new Vector3(0f, floorHeight + 1.05f, 0f);
        player.localPosition = new Vector3(0f, floorHeight + StartPlatformTopHeight, StartPlatformZ);

        Transform startPlatform = setupRoot.transform.Find("Start Platform");
        if (startPlatform != null)
        {
            Undo.RecordObject(startPlatform, "Align start platform to ground");
            startPlatform.localPosition = new Vector3(
                0f,
                floorHeight + StartPlatformTopHeight - StartPlatformSize.y * 0.5f,
                StartPlatformZ);
        }

        SetCameraTarget(Camera.main, player);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Aligned the shark and player above the scene ground.", setupRoot);
    }

    private static float GetOrCreateCourseFloor(GameObject setupRoot)
    {
        GameObject ground = GameObject.Find("Ground");
        if (ground != null && ground.activeInHierarchy)
        {
            Transform oldFloor = setupRoot.transform.Find("Course Floor")
                ?? setupRoot.transform.Find("Test Floor");
            if (oldFloor != null)
            {
                oldFloor.gameObject.SetActive(false);
            }

            return ground.transform.position.y;
        }

        Transform courseFloor = setupRoot.transform.Find("Course Floor")
            ?? setupRoot.transform.Find("Test Floor");
        if (courseFloor == null)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Course Floor";
            floor.transform.SetParent(setupRoot.transform, false);
            floor.transform.localScale = new Vector3(5f, 1f, 5f);
        }
        else
        {
            courseFloor.name = "Course Floor";
        }

        courseFloor = setupRoot.transform.Find("Course Floor");
        courseFloor.gameObject.SetActive(true);
        courseFloor.localPosition = new Vector3(0f, -1f, 0f);
        return courseFloor.GetComponent<Collider>().bounds.max.y;
    }

    private static AnimatorController BuildAnimationController()
    {
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(SharkModelPath).OfType<AnimationClip>().ToArray();
        AnimationClip calmClip = clips.FirstOrDefault(clip => clip.name == CalmAnimationName);
        AnimationClip biteClip = clips.FirstOrDefault(clip => clip.name == BiteAnimationName);
        if (calmClip == null || biteClip == null)
        {
            Debug.LogError("The required closed-mouth and bite animations were not found in the imported shark model.");
            return null;
        }

        const string controllerPath = AnimationFolder + "/SharkPlatform.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }

        for (int i = controller.parameters.Length - 1; i >= 0; i--)
        {
            if (controller.parameters[i].name == "Bite")
            {
                controller.RemoveParameter(i);
            }
        }

        controller.AddParameter("Bite", AnimatorControllerParameterType.Trigger);
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState childState in stateMachine.states.ToArray())
        {
            stateMachine.RemoveState(childState.state);
        }

        AnimatorState calmState = stateMachine.AddState(CalmAnimationName);
        calmState.motion = calmClip;
        stateMachine.defaultState = calmState;

        AnimatorState biteState = stateMachine.AddState(BiteAnimationName);
        biteState.motion = biteClip;

        AnimatorStateTransition biteTransition = calmState.AddTransition(biteState);
        biteTransition.hasExitTime = false;
        biteTransition.duration = 0.15f;
        biteTransition.AddCondition(AnimatorConditionMode.If, 0f, "Bite");

        AnimatorStateTransition calmTransition = biteState.AddTransition(calmState);
        calmTransition.hasExitTime = true;
        calmTransition.exitTime = 0.95f;
        calmTransition.duration = 0.2f;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static AnimatorController BuildPlayerAnimationController()
    {
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(PlayerModelPath).OfType<AnimationClip>().ToArray();
        AnimationClip idleClip = FindPlayerClip(clips, "idle");
        AnimationClip walkClip = FindPlayerClip(clips, "walk");
        AnimationClip runClip = FindPlayerClip(clips, "sprint");
        AnimationClip jumpClip = FindPlayerClip(clips, "jump");
        AnimationClip fallClip = FindPlayerClip(clips, "fall");
        AnimationClip dieClip = FindPlayerClip(clips, "die");
        if (idleClip == null || walkClip == null || runClip == null || jumpClip == null || fallClip == null || dieClip == null)
        {
            Debug.LogError("The FallGuyClone oopi FBX must import idle, walk, sprint, jump, fall, and die animation clips. Found: "
                + string.Join(", ", clips.Select(clip => clip.name)));
            return null;
        }

        const string controllerPath = AnimationFolder + "/CoursePlayer.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }

        for (int i = controller.parameters.Length - 1; i >= 0; i--)
        {
            if (controller.parameters[i].name == "Pose")
            {
                controller.RemoveParameter(i);
            }
        }

        controller.AddParameter("Pose", AnimatorControllerParameterType.Int);
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState childState in stateMachine.states.ToArray())
        {
            stateMachine.RemoveState(childState.state);
        }

        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions.ToArray())
        {
            stateMachine.RemoveAnyStateTransition(transition);
        }

        AnimatorState idle = AddPlayerState(stateMachine, "Idle", idleClip);
        AddPlayerPoseTransition(stateMachine, idle, 0);
        AddPlayerPoseTransition(stateMachine, AddPlayerState(stateMachine, "Walk", walkClip), 1);
        AddPlayerPoseTransition(stateMachine, AddPlayerState(stateMachine, "Run", runClip), 2);
        AddPlayerPoseTransition(stateMachine, AddPlayerState(stateMachine, "Jump", jumpClip), 3);
        AddPlayerPoseTransition(stateMachine, AddPlayerState(stateMachine, "Fall", fallClip), 4);
        AddPlayerPoseTransition(stateMachine, AddPlayerState(stateMachine, "Die", dieClip), 5);
        stateMachine.defaultState = idle;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static AnimationClip FindPlayerClip(AnimationClip[] clips, string clipName)
    {
        return clips.FirstOrDefault(clip => clip.name == clipName || clip.name.EndsWith("|" + clipName));
    }

    private static AnimatorState AddPlayerState(AnimatorStateMachine stateMachine, string stateName, AnimationClip clip)
    {
        AnimatorState state = stateMachine.AddState(stateName);
        state.motion = clip;
        return state;
    }

    private static void AddPlayerPoseTransition(AnimatorStateMachine stateMachine, AnimatorState state, int pose)
    {
        AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(state);
        transition.hasExitTime = false;
        transition.duration = pose == 5 ? 0.08f : 0.15f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.Equals, pose, "Pose");
    }

    private static GameObject BuildStartPlatformPrefab(GameObject model)
    {
        GameObject instance = new GameObject("Start Platform");
        BoxCollider platformCollider = instance.AddComponent<BoxCollider>();
        platformCollider.size = StartPlatformSize;

        GameObject modelInstance = PrefabUtility.InstantiatePrefab(model) as GameObject;
        if (modelInstance == null)
        {
            Object.DestroyImmediate(instance);
            Debug.LogError("Could not instantiate the FallGuyClone grass platform.");
            return null;
        }

        Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(modelInstance);
            Debug.LogError("The FallGuyClone grass platform has no renderers.");
            return null;
        }

        Bounds modelBounds = renderers[0].bounds;
        foreach (Renderer modelRenderer in renderers.Skip(1))
        {
            modelBounds.Encapsulate(modelRenderer.bounds);
        }

        if (modelBounds.size.x <= 0f || modelBounds.size.y <= 0f || modelBounds.size.z <= 0f)
        {
            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(modelInstance);
            Debug.LogError("The FallGuyClone grass platform has no measurable size.");
            return null;
        }

        Transform visualRoot = new GameObject("Visual").transform;
        visualRoot.SetParent(instance.transform, false);
        modelInstance.transform.SetParent(visualRoot, false);
        Vector3 modelScale = new Vector3(
            StartPlatformSize.x / modelBounds.size.x,
            StartPlatformSize.y / modelBounds.size.y,
            StartPlatformSize.z / modelBounds.size.z);
        modelInstance.transform.localScale = modelScale;
        modelInstance.transform.localPosition = -Vector3.Scale(modelBounds.center, modelScale);

        Material platformMaterial = GetPlayerMaterial();
        foreach (Renderer modelRenderer in modelInstance.GetComponentsInChildren<Renderer>(true))
        {
            modelRenderer.sharedMaterials = Enumerable.Repeat(platformMaterial, modelRenderer.sharedMaterials.Length).ToArray();
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabFolder + "/StartPlatform.prefab");
        Object.DestroyImmediate(instance);
        return prefab;
    }

    private static GameObject BuildSharkPrefab(GameObject model, AnimatorController controller)
    {
        GameObject instance = new GameObject("SharkPlatform");
        GameObject visual = new GameObject("Shark Visual");
        visual.transform.SetParent(instance.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.69f, 0f);

        GameObject modelInstance = PrefabUtility.InstantiatePrefab(model) as GameObject;
        if (modelInstance == null)
        {
            Object.DestroyImmediate(instance);
            Debug.LogError("Could not instantiate the imported shark model.");
            return null;
        }

        modelInstance.name = model.name;
        modelInstance.transform.SetParent(visual.transform, false);
        modelInstance.transform.localPosition = new Vector3(0f, -0.69f, 0f);

        Animator animator = modelInstance.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            animator = modelInstance.AddComponent<Animator>();
        }

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        Rigidbody body = instance.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        GameObject surface = new GameObject("Standing Surface");
        surface.transform.SetParent(instance.transform, false);
        surface.transform.localPosition = new Vector3(0f, 0.69f, 0f);
        BoxCollider surfaceCollider = surface.AddComponent<BoxCollider>();
        surfaceCollider.size = new Vector3(1.8f, 0.2f, 2.7f);

        GameObject trigger = new GameObject("Boarding Trigger");
        trigger.transform.SetParent(instance.transform, false);
        trigger.transform.localPosition = new Vector3(0f, 1.35f, 0f);
        BoxCollider triggerCollider = trigger.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector3(2.4f, 1.2f, 3.5f);

        SharkPlatformController platform = instance.AddComponent<SharkPlatformController>();
        SerializedObject platformData = new SerializedObject(platform);
        platformData.FindProperty("sharkAnimator").objectReferenceValue = animator;
        platformData.FindProperty("visualRoot").objectReferenceValue = visual.transform;
        platformData.FindProperty("platformCollider").objectReferenceValue = surfaceCollider;
        platformData.FindProperty("biteDelay").floatValue = 2f;
        platformData.FindProperty("bitePitchDegrees").floatValue = 60f;
        platformData.FindProperty("swimSpeed").floatValue = 1f;
        platformData.ApplyModifiedPropertiesWithoutUndo();

        const string prefabPath = PrefabFolder + "/SharkPlatform.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);
        return prefab;
    }

    private static GameObject BuildPlayerPrefab(GameObject model, AnimatorController playerController)
    {
        GameObject modelInstance = PrefabUtility.InstantiatePrefab(model) as GameObject;
        if (modelInstance == null)
        {
            Debug.LogError("Could not instantiate the animated FallGuyClone oopi FBX.");
            return null;
        }

        Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Object.DestroyImmediate(modelInstance);
            Debug.LogError("The animated FallGuyClone oopi FBX does not contain any renderers.");
            return null;
        }

        Bounds modelBounds = renderers[0].bounds;
        foreach (Renderer modelRenderer in renderers.Skip(1))
        {
            modelBounds.Encapsulate(modelRenderer.bounds);
        }

        if (modelBounds.size.y <= 0f)
        {
            Object.DestroyImmediate(modelInstance);
            Debug.LogError("The animated FallGuyClone oopi FBX has no measurable model height.");
            return null;
        }

        const float playerHeight = 1.7f;
        float scale = playerHeight / modelBounds.size.y;
        GameObject player = new GameObject("Course Player");
        player.tag = "Player";
        modelInstance.name = "oopi Model";
        modelInstance.transform.SetParent(player.transform, false);
        modelInstance.transform.localScale = Vector3.one * scale;
        modelInstance.transform.localPosition = new Vector3(
            -modelBounds.center.x * scale,
            -modelBounds.min.y * scale,
            -modelBounds.center.z * scale);

        Animator animator = modelInstance.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            animator = modelInstance.AddComponent<Animator>();
        }

        Avatar playerAvatar = AssetDatabase.LoadAllAssetsAtPath(PlayerModelPath)
            .OfType<Avatar>()
            .FirstOrDefault(avatar => avatar.isValid);
        if (playerAvatar != null)
        {
            animator.avatar = playerAvatar;
        }

        animator.runtimeAnimatorController = playerController;
        animator.applyRootMotion = false;

        Material playerMaterial = GetPlayerMaterial();
        foreach (Renderer modelRenderer in modelInstance.GetComponentsInChildren<Renderer>(true))
        {
            modelRenderer.sharedMaterials = Enumerable.Repeat(playerMaterial, modelRenderer.sharedMaterials.Length).ToArray();
            if (modelRenderer is SkinnedMeshRenderer skinnedRenderer)
            {
                skinnedRenderer.updateWhenOffscreen = true;
            }
        }

        CharacterController characterController = player.AddComponent<CharacterController>();
        characterController.height = playerHeight;
        characterController.radius = 0.3f;
        characterController.center = new Vector3(0f, playerHeight * 0.5f, 0f);
        characterController.stepOffset = 0.25f;

        SharkCoursePlayerController movement = player.AddComponent<SharkCoursePlayerController>();
        SharkBiteReceiver receiver = player.AddComponent<SharkBiteReceiver>();
        SerializedObject receiverData = new SerializedObject(receiver);
        receiverData.FindProperty("movementBehaviour").objectReferenceValue = movement;
        AnimationClip dieClip = AssetDatabase.LoadAllAssetsAtPath(PlayerModelPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => clip.name == "die" || clip.name.EndsWith("|die"));
        if (dieClip != null)
        {
            receiverData.FindProperty("deathAnimationDuration").floatValue = Mathf.Clamp(dieClip.length, 0.25f, 5f);
        }

        receiverData.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(player, PrefabFolder + "/Player.prefab");
        Object.DestroyImmediate(player);
        return prefab;
    }

    private static Material GetPlayerMaterial()
    {
        const string materialPath = "Assets/Shark/Materials/CoursePlayer.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader) { name = "CoursePlayer" };
            AssetDatabase.CreateAsset(material, materialPath);
        }

        Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Shark/Textures/Player/colormap.png");
        if (atlas != null)
        {
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", atlas);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", atlas);
            }
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent) || !AssetDatabase.IsValidFolder(parent))
        {
            throw new System.InvalidOperationException("Cannot create Unity asset folder: " + path);
        }

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
