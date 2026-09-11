using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class ProjectBuilder
{
    private readonly struct TransitionCondition
    {
        public readonly AnimatorConditionMode Mode;
        public readonly float Threshold;
        public readonly string Parameter;

        public TransitionCondition(AnimatorConditionMode mode, float threshold, string parameter)
        {
            Mode = mode;
            Threshold = threshold;
            Parameter = parameter;
        }
    }

    private const string Root = "Assets/_Project";
    private const string SpriteRoot = Root + "/Sprites";
    private const string PlayerSpriteRoot = SpriteRoot + "/Player";
    private const string AnimationRoot = Root + "/Animations/Player";
    private const string AnimatorPath = AnimationRoot + "/Player.controller";
    private const string ScenePath = Root + "/Scenes/Main.unity";
    private const string MaterialPath = Root + "/Sprites/ZeroFriction.physicsMaterial2D";
    private const string TilePath = Root + "/Sprites/SandTile.asset";
    private const string SourceRoot = @"C:\Users\sidne\OneDrive\Escritorio\DESARROLLO DE VIDEOJUEGOS-38689\Berie's_Adventure_Seaside_Asset_Pack_Free";

    public static void Build()
    {
        EnsureDirectories();
        CopyMinimumSourceAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureTexture(SpriteRoot + "/background.png", 320, SpriteImportMode.Single);
        ConfigureTexture(PlayerSpriteRoot + "/Idle/character_berie_idle_1.png", 100, SpriteImportMode.Single);
        ConfigureTileSheet();

        ConfigureUniversal2D();
        PhysicsMaterial2D zeroFriction = CreatePhysicsMaterial();
        Tile sandTile = CreateSandTile();
        CreateMainScene(zeroFriction, sandTile);
        ConfigureBuildSettings();
        AssetDatabase.SaveAssets();

        Verify();
        Debug.Log("LABORATORIO2D_BUILD_SUCCESS");
    }

    public static void Verify()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject grid = RequireObject(scene, "Grid");
        GameObject level = RequireObject(scene, "Grid/Level");
        GameObject player = RequireObject(scene, "Player");
        GameObject groundCheck = RequireObject(scene, "Player/GroundCheck");
        GameObject cameraObject = RequireObject(scene, "Main Camera");

        Grid gridComponent = RequireComponent<Grid>(grid);
        Require(gridComponent.cellSize == new Vector3(0.32f, 0.32f, 0f), "Grid cell size must be 0.32 x 0.32.");
        RequireComponent<Tilemap>(level);
        RequireComponent<TilemapCollider2D>(level);
        int floorLayer = LayerMask.NameToLayer("Floor");
        Require(floorLayer >= 0, "Floor layer must exist.");
        Require(level.layer == floorLayer, "Level must use the Floor layer.");

        Rigidbody2D body = RequireComponent<Rigidbody2D>(player);
        RequireComponent<CapsuleCollider2D>(player);
        PlayerController controller = RequireComponent<PlayerController>(player);
        SpriteRenderer playerRenderer = RequireComponent<SpriteRenderer>(player);
        Animator animator = RequireComponent<Animator>(player);
        SerializedObject serializedController = new SerializedObject(controller);
        Require(serializedController.FindProperty("groundCheck").objectReferenceValue == groundCheck.transform, "GroundCheck reference must be assigned.");
        Require(serializedController.FindProperty("groundLayer").intValue == (1 << floorLayer), "Ground layer mask must target Floor.");
        Require(Mathf.Approximately(serializedController.FindProperty("jumpForce").floatValue, 6f), "Jump force must be 6.");
        Require(Mathf.Approximately(serializedController.FindProperty("groundRadius").floatValue, 0.1f), "Ground radius must be 0.1.");
        Require(Mathf.Approximately(body.gravityScale, 2f), "Player gravity scale must be 2.");
        Require(Mathf.Approximately(body.mass, 1f), "Player mass must be 1.");
        Require(Mathf.Approximately(body.linearDamping, 0f), "Player linear drag must be 0.");
        Require(body.collisionDetectionMode == CollisionDetectionMode2D.Continuous, "Player collision detection must be Continuous.");
        Require(body.interpolation == RigidbodyInterpolation2D.Interpolate, "Player interpolation must be enabled.");
        Require((body.constraints & RigidbodyConstraints2D.FreezeRotation) != 0, "Player Z rotation must be frozen.");
        Require(body.sharedMaterial != null && Mathf.Approximately(body.sharedMaterial.friction, 0f), "Player friction must be zero.");
        Require(playerRenderer.sortingOrder > RequireComponent<TilemapRenderer>(level).sortingOrder, "Player must render in front of the level.");
        VerifyPlayerAnimations(animator);

        GameObject box = RequireObject(scene, "Box");
        Rigidbody2D boxBody = RequireComponent<Rigidbody2D>(box);
        RequireComponent<BoxCollider2D>(box);
        RequireComponent<SpriteRenderer>(box);
        Require(boxBody.bodyType == RigidbodyType2D.Dynamic, "Box must be dynamic.");
        Require(boxBody.gravityScale > 0f, "Box must use gravity.");
        Require((boxBody.constraints & RigidbodyConstraints2D.FreezeRotation) != 0, "Box Z rotation must be frozen.");

        GameObject coin = RequireObject(scene, "Coin");
        CircleCollider2D coinCollider = RequireComponent<CircleCollider2D>(coin);
        RequireComponent<SpriteRenderer>(coin);
        RequireComponent<Coin>(coin);
        Require(coinCollider.isTrigger, "Coin collider must be a trigger.");

        Camera cameraComponent = RequireComponent<Camera>(cameraObject);
        Require(cameraComponent.orthographic, "Camera must be orthographic.");
        Require(Mathf.Approximately(cameraComponent.orthographicSize, 1.25f), "Camera size must be 1.25.");

        RequireObject(scene, "Background");
        RequireObject(scene, "Grid/Level/Platform Left");
        RequireObject(scene, "Grid/Level/Platform Center");
        RequireObject(scene, "Grid/Level/Platform Right");
        Require(QualitySettings.renderPipeline != null, "Universal Render Pipeline must be active.");
        Require(EditorBuildSettings.scenes.Length == 1 && EditorBuildSettings.scenes[0].path == ScenePath, "Main scene must be the only build scene.");
        Debug.Log("LABORATORIO2D_VERIFICATION_SUCCESS");
    }

    public static void SetupPlayerAnimations()
    {
        EnsureDirectories();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        MoveLegacyIdleFrame();
        CopyPlayerAnimationFrames();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        foreach (string state in new[] { "Idle", "Run", "Jump", "Fall" })
        {
            string[] framePaths = GetFramePaths(state);
            foreach (string framePath in framePaths)
            {
                ConfigureTexture(framePath, 100, SpriteImportMode.Single);
            }
        }

        AnimationClip idle = CreateSpriteClip("Idle", 8f, true);
        AnimationClip run = CreateSpriteClip("Run", 12f, true);
        AnimationClip jump = CreateSpriteClip("Jump", 10f, false);
        AnimationClip fall = CreateSpriteClip("Fall", 8f, true);
        AnimatorController animatorController = CreatePlayerAnimator(idle, run, jump, fall);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = RequireObject(scene, "Player");
        Animator animator = player.GetComponent<Animator>();
        if (animator == null)
        {
            animator = player.AddComponent<Animator>();
        }
        animator.runtimeAnimatorController = animatorController;
        RequireComponent<SpriteRenderer>(player).sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GetFramePaths("Idle")[0]);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("LABORATORIO2D_PLAYER_ANIMATIONS_SETUP_SUCCESS");
    }

    private static void EnsureDirectories()
    {
        Directory.CreateDirectory(SpriteRoot);
        Directory.CreateDirectory(Root + "/Scenes");
        Directory.CreateDirectory(PlayerSpriteRoot + "/Idle");
        Directory.CreateDirectory(PlayerSpriteRoot + "/Run");
        Directory.CreateDirectory(PlayerSpriteRoot + "/Jump");
        Directory.CreateDirectory(PlayerSpriteRoot + "/Fall");
        Directory.CreateDirectory(AnimationRoot);
    }

    private static void CopyMinimumSourceAssets()
    {
        File.Copy(Path.Combine(SourceRoot, "PNG", "background.png"), SpriteRoot + "/background.png", true);
        File.Copy(Path.Combine(SourceRoot, "Spritesheet", "tilemap.png"), SpriteRoot + "/tilemap.png", true);
        CopyPlayerAnimationFrames();
    }

    private static void MoveLegacyIdleFrame()
    {
        const string legacyPath = SpriteRoot + "/character_berie_idle_1.png";
        string destinationPath = GetFramePaths("Idle")[0];
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(legacyPath) != null && AssetDatabase.LoadAssetAtPath<Texture2D>(destinationPath) == null)
        {
            string error = AssetDatabase.MoveAsset(legacyPath, destinationPath);
            Require(string.IsNullOrEmpty(error), "Could not move the existing idle frame: " + error);
        }
    }

    private static void CopyPlayerAnimationFrames()
    {
        foreach (string state in new[] { "Idle", "Run", "Jump", "Fall" })
        {
            string[] destinations = GetFramePaths(state);
            for (int index = 0; index < destinations.Length; index++)
            {
                string sourceName = "character_berie_" + state.ToLowerInvariant() + "_" + (index + 1) + ".png";
                File.Copy(Path.Combine(SourceRoot, "PNG", sourceName), destinations[index], true);
            }
        }
    }

    private static string[] GetFramePaths(string state)
    {
        int frameCount = state == "Run" ? 6 : state == "Fall" ? 2 : 4;
        string[] paths = new string[frameCount];
        for (int index = 0; index < frameCount; index++)
        {
            paths[index] = PlayerSpriteRoot + "/" + state + "/character_berie_" + state.ToLowerInvariant() + "_" + (index + 1) + ".png";
        }
        return paths;
    }

    private static AnimationClip CreateSpriteClip(string state, float frameRate, bool loop)
    {
        string clipPath = AnimationRoot + "/Player" + state + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, clipPath);
        }
        clip.name = "Player" + state;
        clip.frameRate = frameRate;
        EditorCurveBinding binding = new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };
        string[] framePaths = GetFramePaths(state);
        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[framePaths.Length];
        for (int index = 0; index < framePaths.Length; index++)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(framePaths[index]);
            Require(sprite != null, "Missing animation sprite: " + framePaths[index]);
            keyframes[index] = new ObjectReferenceKeyframe { time = index / frameRate, value = sprite };
        }
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorController CreatePlayerAnimator(AnimationClip idle, AnimationClip run, AnimationClip jump, AnimationClip fall)
    {
        AssetDatabase.DeleteAsset(AnimatorPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idleState = machine.AddState("Idle", new Vector3(200f, 0f));
        AnimatorState runState = machine.AddState("Run", new Vector3(450f, 0f));
        AnimatorState jumpState = machine.AddState("Jump", new Vector3(200f, 150f));
        AnimatorState fallState = machine.AddState("Fall", new Vector3(450f, 150f));
        idleState.motion = idle;
        runState.motion = run;
        jumpState.motion = jump;
        fallState.motion = fall;
        machine.defaultState = idleState;

        AddTransition(idleState, runState, new TransitionCondition(AnimatorConditionMode.Greater, 0.01f, "Speed"));
        AddTransition(runState, idleState, new TransitionCondition(AnimatorConditionMode.Less, 0.01f, "Speed"));
        AddAirTransitions(idleState, jumpState, fallState);
        AddAirTransitions(runState, jumpState, fallState);
        AddTransition(jumpState, fallState, new TransitionCondition(AnimatorConditionMode.Less, 0f, "VerticalVelocity"));
        AddTransition(fallState, idleState,
            new TransitionCondition(AnimatorConditionMode.If, 0f, "IsGrounded"),
            new TransitionCondition(AnimatorConditionMode.Less, 0.01f, "Speed"));
        AddTransition(fallState, runState,
            new TransitionCondition(AnimatorConditionMode.If, 0f, "IsGrounded"),
            new TransitionCondition(AnimatorConditionMode.Greater, 0.01f, "Speed"));
        return controller;
    }

    private static void AddAirTransitions(AnimatorState source, AnimatorState jump, AnimatorState fall)
    {
        AddTransition(source, jump,
            new TransitionCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded"),
            new TransitionCondition(AnimatorConditionMode.Greater, 0f, "VerticalVelocity"));
        AddTransition(source, fall,
            new TransitionCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded"),
            new TransitionCondition(AnimatorConditionMode.Less, 0f, "VerticalVelocity"));
    }

    private static void AddTransition(AnimatorState source, AnimatorState destination, params TransitionCondition[] conditions)
    {
        AnimatorStateTransition transition = source.AddTransition(destination);
        transition.hasExitTime = false;
        transition.duration = 0f;
        transition.hasFixedDuration = true;
        foreach (TransitionCondition condition in conditions)
        {
            transition.AddCondition(condition.Mode, condition.Threshold, condition.Parameter);
        }
    }

    private static void VerifyPlayerAnimations(Animator animator)
    {
        AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
        Require(controller != null && AssetDatabase.GetAssetPath(controller) == AnimatorPath, "Player Animator must reference Player.controller.");
        Require(controller.parameters.Length == 3, "Player Animator must have exactly three parameters.");
        Require(Array.Exists(controller.parameters, item => item.name == "Speed" && item.type == AnimatorControllerParameterType.Float), "Player Animator requires float Speed.");
        Require(Array.Exists(controller.parameters, item => item.name == "VerticalVelocity" && item.type == AnimatorControllerParameterType.Float), "Player Animator requires float VerticalVelocity.");
        Require(Array.Exists(controller.parameters, item => item.name == "IsGrounded" && item.type == AnimatorControllerParameterType.Bool), "Player Animator requires bool IsGrounded.");

        ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
        Require(states.Length == 4, "Player Animator must have exactly four states.");
        foreach (string stateName in new[] { "Idle", "Run", "Jump", "Fall" })
        {
            AnimatorState state = Array.Find(states, item => item.state.name == stateName).state;
            Require(state != null, "Missing Animator state: " + stateName);
            Require(state.motion == AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationRoot + "/Player" + stateName + ".anim"), stateName + " must reference its matching clip.");
            foreach (AnimatorStateTransition transition in state.transitions)
            {
                Require(!transition.hasExitTime && Mathf.Approximately(transition.duration, 0f), stateName + " transitions must be immediate and have no exit time.");
            }

            ObjectReferenceKeyframe[] frames = AnimationUtility.GetObjectReferenceCurve((AnimationClip)state.motion,
                new EditorCurveBinding { path = string.Empty, type = typeof(SpriteRenderer), propertyName = "m_Sprite" });
            string[] expectedPaths = GetFramePaths(stateName);
            Require(frames != null && frames.Length == expectedPaths.Length, stateName + " clip has an incorrect frame count.");
            for (int index = 0; index < expectedPaths.Length; index++)
            {
                Require(AssetDatabase.GetAssetPath(frames[index].value) == expectedPaths[index], stateName + " clip has an incorrect frame at index " + index + ".");
            }
        }
    }

    private static void ConfigureTexture(string path, int pixelsPerUnit, SpriteImportMode mode)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = mode;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    private static void ConfigureTileSheet()
    {
        string path = SpriteRoot + "/tilemap.png";
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritesheet = new[]
        {
            new SpriteMetaData
            {
                name = "SandGround",
                rect = new Rect(32f, 128f, 32f, 32f),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f)
            }
        };
        importer.SaveAndReimport();
    }

    private static void ConfigureUniversal2D()
    {
        const string rendererPath = Root + "/Universal2DRenderer.asset";
        const string pipelinePath = Root + "/Universal2D.asset";
        Renderer2DData renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(rendererPath);
        if (renderer == null)
        {
            renderer = ScriptableObject.CreateInstance<Renderer2DData>();
            AssetDatabase.CreateAsset(renderer, rendererPath);
        }

        UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
        if (pipeline == null)
        {
            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, pipelinePath);
        }

        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
    }

    private static PhysicsMaterial2D CreatePhysicsMaterial()
    {
        PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(MaterialPath);
        if (material == null)
        {
            material = new PhysicsMaterial2D("Zero Friction");
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.friction = 0f;
        material.bounciness = 0f;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Tile CreateSandTile()
    {
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(TilePath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, TilePath);
        }
        UnityEngine.Object[] sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(SpriteRoot + "/tilemap.png");
        tile.sprite = Array.Find(sprites, item => item is Sprite && item.name == "SandGround") as Sprite;
        Require(tile.sprite != null, "Sand tile sprite was not imported.");
        tile.colliderType = Tile.ColliderType.Sprite;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    private static void CreateMainScene(PhysicsMaterial2D zeroFriction, Tile sandTile)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject background = new GameObject("Background", typeof(SpriteRenderer));
        SpriteRenderer backgroundRenderer = background.GetComponent<SpriteRenderer>();
        backgroundRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + "/background.png");
        backgroundRenderer.sortingOrder = -10;
        background.transform.position = new Vector3(0f, 0f, 2f);
        background.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

        GameObject gridObject = new GameObject("Grid", typeof(Grid));
        Grid grid = gridObject.GetComponent<Grid>();
        grid.cellSize = new Vector3(0.32f, 0.32f, 0f);

        GameObject levelObject = new GameObject("Level", typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D));
        levelObject.transform.SetParent(gridObject.transform);
        levelObject.layer = LayerMask.NameToLayer("Floor");
        Tilemap tilemap = levelObject.GetComponent<Tilemap>();
        TilemapRenderer tilemapRenderer = levelObject.GetComponent<TilemapRenderer>();
        tilemapRenderer.sortingOrder = 0;
        PaintRectangle(tilemap, sandTile, -9, 9, -4, -3);
        PaintRectangle(tilemap, sandTile, -9, -8, -2, 4);
        PaintRectangle(tilemap, sandTile, 8, 9, -2, 4);

        CreatePlatform("Platform Left", tilemap, sandTile, -7, -4, -1);
        CreatePlatform("Platform Center", tilemap, sandTile, -2, 1, 0);
        CreatePlatform("Platform Right", tilemap, sandTile, 3, 6, 1);

        GameObject player = new GameObject("Player", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CapsuleCollider2D), typeof(PlayerController));
        player.transform.position = new Vector3(-1f, -0.65f, 0f);
        SpriteRenderer playerRenderer = player.GetComponent<SpriteRenderer>();
        playerRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + "/character_berie_idle_1.png");
        playerRenderer.sortingOrder = 10;
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.gravityScale = 2f;
        body.mass = 1f;
        body.linearDamping = 0f;
        body.sharedMaterial = zeroFriction;
        CapsuleCollider2D capsule = player.GetComponent<CapsuleCollider2D>();
        capsule.direction = CapsuleDirection2D.Vertical;
        capsule.size = new Vector2(0.30f, 0.42f);
        capsule.offset = new Vector2(0f, -0.02f);
        capsule.sharedMaterial = zeroFriction;

        GameObject groundCheck = new GameObject("GroundCheck");
        groundCheck.transform.SetParent(player.transform);
        groundCheck.transform.localPosition = new Vector3(0f, -0.24f, 0f);
        PlayerController controller = player.GetComponent<PlayerController>();
        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("groundCheck").objectReferenceValue = groundCheck.transform;
        serializedController.FindProperty("groundLayer").intValue = 1 << LayerMask.NameToLayer("Floor");
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        GameObject box = new GameObject("Box", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(BoxCollider2D));
        box.transform.position = new Vector3(0f, -0.55f, 0f);
        box.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
        SpriteRenderer boxRenderer = box.GetComponent<SpriteRenderer>();
        boxRenderer.sprite = sandTile.sprite;
        boxRenderer.sortingOrder = 5;
        Rigidbody2D boxBody = box.GetComponent<Rigidbody2D>();
        boxBody.constraints = RigidbodyConstraints2D.FreezeRotation;
        boxBody.gravityScale = 1f;

        GameObject coin = new GameObject("Coin", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Coin));
        coin.transform.position = new Vector3(1.3f, -0.35f, 0f);
        coin.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
        SpriteRenderer coinRenderer = coin.GetComponent<SpriteRenderer>();
        coinRenderer.sprite = sandTile.sprite;
        coinRenderer.color = Color.yellow;
        coinRenderer.sortingOrder = 5;
        coin.GetComponent<CircleCollider2D>().isTrigger = true;

        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        Camera cameraComponent = cameraObject.GetComponent<Camera>();
        cameraComponent.orthographic = true;
        cameraComponent.orthographicSize = 1.25f;
        cameraComponent.clearFlags = CameraClearFlags.SolidColor;
        cameraComponent.backgroundColor = new Color(0.20f, 0.45f, 0.90f, 1f);

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static void CreatePlatform(string name, Tilemap tilemap, TileBase tile, int minX, int maxX, int y)
    {
        GameObject marker = new GameObject(name);
        marker.transform.SetParent(tilemap.transform);
        marker.transform.position = tilemap.CellToWorld(new Vector3Int(minX, y, 0));
        PaintRectangle(tilemap, tile, minX, maxX, y, y);
    }

    private static void PaintRectangle(Tilemap tilemap, TileBase tile, int minX, int maxX, int minY, int maxY)
    {
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                tilemap.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }
    }

    private static void ConfigureBuildSettings()
    {
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.colorSpace = ColorSpace.Linear;
    }

    private static GameObject RequireObject(Scene scene, string path)
    {
        string[] segments = path.Split('/');
        GameObject current = Array.Find(scene.GetRootGameObjects(), item => item.name == segments[0]);
        for (int i = 1; i < segments.Length && current != null; i++)
        {
            Transform child = current.transform.Find(segments[i]);
            current = child == null ? null : child.gameObject;
        }
        Require(current != null, "Missing scene object: " + path);
        return current;
    }

    private static T RequireComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        Require(component != null, gameObject.name + " must have " + typeof(T).Name + ".");
        return component;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
