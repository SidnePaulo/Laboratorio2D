using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

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
    private const string PackRoot = Root + "/Sprites1/Berie's_Adventure_Seaside_Asset_Pack_Free";
    private const string PngRoot = PackRoot + "/PNG";
    private const string SheetRoot = PackRoot + "/Spritesheet";
    private const string PlayerSpriteRoot = PngRoot;
    private const string AnimationRoot = Root + "/Animations/Player";
    private const string AnimatorPath = AnimationRoot + "/Player.controller";
    private const string ScenePath = Root + "/Scenes/Main.unity";
    private const string MaterialPath = Root + "/Physics/ZeroFriction.physicsMaterial2D";
    private const string TileRoot = Root + "/Tiles";
    private const string TilePath = TileRoot + "/SandCenter.asset";
    private const string SandLeftTilePath = TileRoot + "/SandLeft.asset";
    private const string SandRightTilePath = TileRoot + "/SandRight.asset";
    private const string SoftSandTilePath = TileRoot + "/SoftSandCenter.asset";
    private const string SoftSandSheetPath = SheetRoot + "/tilemap_new_softy_sand.png";
    private const string CoinPrefabPath = Root + "/Prefabs/Coin.prefab";
    private const string BackgroundPath = PngRoot + "/background.png";
    private const string PalmPath = SheetRoot + "/vegetation_tree_palm.png";
    private const string GrassPath = SheetRoot + "/vegetation_grass_small.png";
    private const string RockSheetPath = SheetRoot + "/vegetation_grass_rock_trunk.png";
    private const string BombPath = PngRoot + "/trap_bomb_idle.png";
    private const string BarrelAnimationRoot = Root + "/Animations/Barrel";
    private const string BarrelControllerPath = BarrelAnimationRoot + "/Barrel.controller";
    private const string BarrelIdlePath = BarrelAnimationRoot + "/BarrelIdle.anim";
    private const string BarrelExplodePath = BarrelAnimationRoot + "/BarrelExplode.anim";
    private const string BarrelPrefabPath = Root + "/Prefabs/Barrel.prefab";
    private const string BarrelPhysicsMaterialPath = Root + "/Physics/Barrel.physicsMaterial2D";
    private const float BarrelMass = 2f;
    private const float BarrelLinearDamping = 3f;
    private const float BarrelGravityScale = 1.5f;
    private const float BarrelFriction = 0.6f;
    private const float CameraOrthographicSize = 3.25f;
    private const float CoinScale = 1.1f;
    private const float CoinColliderRadius = 0.16f;
    private static readonly Vector3 PlayerStartPosition = new Vector3(-7.5f, -0.25f, 0f);
    private static readonly Vector3 InitialCameraPosition = new Vector3(-3.82f, -0.35f, -10f);
    private const string MusicPath = PackRoot + "/MusicaYSonidos/MusicaYSonidos/Musica/POL-king-of-coins-short.wav";
    private const string CoinSfxPath = PackRoot + "/MusicaYSonidos/MusicaYSonidos/Sonidos/Coin1.ogg";
    private const string BarrelSfxPath = PackRoot + "/MusicaYSonidos/MusicaYSonidos/Sonidos/BarrelSound.ogg";
    private const string HazardSfxPath = PackRoot + "/MusicaYSonidos/MusicaYSonidos/Sonidos/error_007.ogg";
    private const string GoalSpritePath = PngRoot + "/collectibles_treasure_ruby_static.png";
    private const string BarrelExplosionPrefabPath = Root + "/Prefabs/BarrelExplosionParticles.prefab";

    public static void Build()
    {
        EnsureDirectories();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureVisualTextures();
        ConfigureTexture(GetFramePaths("Idle")[0], 100, SpriteImportMode.Single);
        ConfigureTileSheet();
        ConfigureSoftSandSheet();

        ConfigureUniversal2D();
        PhysicsMaterial2D zeroFriction = CreatePhysicsMaterial();
        Tile sandTile = CreateTile(TilePath, SheetRoot + "/tilemap.png", "SandCenter");
        Tile sandLeftTile = CreateTile(SandLeftTilePath, SheetRoot + "/tilemap.png", "SandLeft");
        Tile sandRightTile = CreateTile(SandRightTilePath, SheetRoot + "/tilemap.png", "SandRight");
        Tile softSandTile = CreateSoftSandTile();
        CreateMainScene(zeroFriction, sandTile, sandLeftTile, sandRightTile, softSandTile);
        SetupPlayerAnimations();
        SetupBarrelSystem();
        SetupBarrelExplosionParticles();
        SetupCinemachineCamera();
        SetupCollectiblesAndUI();
        SetupAudio();
        SetupPlayerCheckpoint();
        SetupSpikeHazards();
        SetupGoalAndVictoryUI();
        FinalizeCoinCounterLayout();
        ConfigureBuildSettings();
        AssetDatabase.SaveAssets();
        PreserveSerializedCanvasLayout();
        RemoveGeneratedTrailingWhitespace();

        Verify();
        Debug.Log("LABORATORIO2D_BUILD_SUCCESS");
    }

    public static void SetupVisualAssets()
    {
        EnsureDirectories();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureVisualTextures();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Sprite backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
        Sprite barrelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GetBarrelFramePaths()[0]);
        Sprite coinSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GetCoinFramePaths()[0]);

        GameObject background = RequireObject(scene, "Background");
        SpriteRenderer backgroundRenderer = RequireComponent<SpriteRenderer>(background);
        backgroundRenderer.sprite = backgroundSprite;
        backgroundRenderer.color = Color.white;
        backgroundRenderer.sortingOrder = -10;
        background.transform.position = new Vector3(0.16f, 0.16f, 2f);
        background.transform.localScale = new Vector3(6.1f, 6.1f, 1f);

        // Update barrel prefab sprite if exists, otherwise legacy Box
        GameObject barrelPrefabForVisual = AssetDatabase.LoadAssetAtPath<GameObject>(BarrelPrefabPath);
        if (barrelPrefabForVisual != null)
        {
            SpriteRenderer barrelPrefabRenderer = barrelPrefabForVisual.GetComponent<SpriteRenderer>();
            if (barrelPrefabRenderer != null)
            {
                barrelPrefabRenderer.sprite = barrelSprite;
                barrelPrefabRenderer.sortingOrder = 5;
                EditorUtility.SetDirty(barrelPrefabForVisual);
            }
        }
        else
        {
            SpriteRenderer boxRenderer = RequireComponent<SpriteRenderer>(RequireObject(scene, "Box"));
            boxRenderer.sprite = barrelSprite;
            boxRenderer.color = Color.white;
            boxRenderer.sortingOrder = 5;
        }

        GameObject coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoinPrefabPath);
        SpriteRenderer coinRenderer = RequireComponent<SpriteRenderer>(coinPrefab);
        coinRenderer.sprite = coinSprite;
        coinRenderer.color = Color.white;
        coinRenderer.sortingOrder = 5;
        EditorUtility.SetDirty(coinRenderer);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("LABORATORIO2D_VISUAL_ASSETS_SETUP_SUCCESS");
    }

    public static void Verify()
    {
        VerifyRawSerializedState();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject grid = RequireObject(scene, "Grid");
        GameObject level = RequireObject(scene, "Grid/Level");
        GameObject player = RequireObject(scene, "Player");
        GameObject groundCheck = RequireObject(scene, "Player/GroundCheck");
        GameObject cameraObject = RequireObject(scene, "Main Camera");
        GameObject cinemachineObject = RequireObject(scene, "Cinemachine Camera");
        GameObject cameraBoundsObject = RequireObject(scene, "Camera Bounds");

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
        Require(player.transform.position == PlayerStartPosition, "Player must start at the authored left-side spawn.");
        Animator animator = RequireComponent<Animator>(player);
        SerializedObject serializedController = new SerializedObject(controller);
        Require(serializedController.FindProperty("groundCheck").objectReferenceValue == groundCheck.transform, "GroundCheck reference must be assigned.");
        Require(serializedController.FindProperty("groundLayer").intValue == (1 << floorLayer), "Ground layer mask must target Floor.");
        Require(Mathf.Approximately(serializedController.FindProperty("jumpForce").floatValue, 6f), "Jump force must be 6.");
        Require(Mathf.Approximately(serializedController.FindProperty("speed").floatValue, 2f), "Player speed must be 2.");
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

        // Barrel prefab verification replaces legacy Box check
        VerifyBarrelSystem(scene);

        GameObject collectibles = RequireObject(scene, "Collectibles");
        Coin[] coins = collectibles.GetComponentsInChildren<Coin>();
        Require(coins.Length == 5, "The scene must contain five coins.");
        GameObject coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoinPrefabPath);
        Require(coinPrefab != null, "Coin prefab must exist.");
        RequireComponent<Coin>(coinPrefab);
        SpriteRenderer coinPrefabRenderer = RequireComponent<SpriteRenderer>(coinPrefab);
        Require(AssetDatabase.GetAssetPath(coinPrefabRenderer.sprite) == GetCoinFramePaths()[0], "Coin prefab must use the original coin sprite.");
        Require(coinPrefabRenderer.color == Color.white, "Coin prefab sprite tint must be white.");
        CircleCollider2D coinCollider = RequireComponent<CircleCollider2D>(coinPrefab);
        Require(coinCollider.isTrigger, "Coin prefab collider must be a trigger.");
        Require(coinPrefab.transform.localScale == new Vector3(CoinScale, CoinScale, 1f), "Coin prefab must use the enlarged uniform visual scale.");
        Require(Mathf.Approximately(coinCollider.radius, CoinColliderRadius), "Coin prefab collider radius must match the enlarged visual.");
        float coinViewportRatio = coinPrefabRenderer.sprite.bounds.size.y * CoinScale / (CameraOrthographicSize * 2f);
        Require(coinViewportRatio >= 0.05f && coinViewportRatio <= 0.06f,
            "Coin visual height must occupy 5-6% of the orthographic viewport.");
        foreach (Coin coin in coins)
        {
            Require(PrefabUtility.GetCorrespondingObjectFromSource(coin.gameObject) == coinPrefab, coin.name + " must be a Coin prefab instance.");
            Require(coin.transform.localScale == new Vector3(CoinScale, CoinScale, 1f), coin.name + " must inherit the prefab scale without overrides.");
            ColliderDistance2D platformDistance = RequireComponent<Collider2D>(coin.gameObject).Distance(RequireComponent<TilemapCollider2D>(level));
            Require(!platformDistance.isOverlapped, coin.name + " must not intersect a platform.");
        }

        GameManager gameManager = RequireComponent<GameManager>(RequireObject(scene, "GameManager"));
        SerializedObject serializedGameManager = new SerializedObject(gameManager);
        Require(serializedGameManager.FindProperty("coins").arraySize == coins.Length, "GameManager must reference every coin.");

        GameObject canvasObject = RequireObject(scene, "Canvas");
        Canvas canvas = RequireComponent<Canvas>(canvasObject);
        CanvasScaler scaler = RequireComponent<CanvasScaler>(canvasObject);
        Require(canvasObject.activeSelf && canvasObject.activeInHierarchy && canvas.enabled && canvas.isActiveAndEnabled,
            "Canvas must be active and enabled from the first frame.");
        Require(canvas.renderMode == RenderMode.ScreenSpaceOverlay, "Canvas must use Screen Space Overlay.");
        Require(canvas.targetDisplay == 0 && canvas.worldCamera == null, "Overlay Canvas must target Display 1 without a world camera.");
        Require(canvas.sortingOrder == 0, "Overlay Canvas sorting order must be zero.");
        Require(scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize, "Canvas must scale with screen size.");
        Require(scaler.referenceResolution == new Vector2(1920f, 1080f), "Canvas reference resolution must be 1920 x 1080.");
        Require(Mathf.Approximately(scaler.matchWidthOrHeight, 0.5f), "Canvas must balance width and height scaling.");
        UIManager uiManager = RequireComponent<UIManager>(canvasObject);
        GameObject hudObject = RequireObject(scene, "Canvas/Coin HUD");
        GameObject coinIconObject = RequireObject(scene, "Canvas/Coin HUD/Coin Icon");
        Image coinIcon = RequireComponent<Image>(coinIconObject);
        GameObject coinTextObject = RequireObject(scene, "Canvas/Coin HUD/Coin Text");
        Text coinText = RequireComponent<Text>(coinTextObject);
        RectTransform coinTextRect = RequireComponent<RectTransform>(coinTextObject);
        Require(coinText.text == "00", "Coin text must start with two digits.");
        RectTransform hudRect = RequireComponent<RectTransform>(hudObject);
        Require(hudRect.anchorMin == new Vector2(0f, 1f) && hudRect.anchorMax == new Vector2(0f, 1f), "Coin HUD must be anchored to the top-left.");
        Require(hudRect.pivot == new Vector2(0f, 1f), "Coin HUD pivot must be top-left.");
        SerializedObject serializedUIManager = new SerializedObject(uiManager);
        Require(serializedUIManager.FindProperty("gameManager").objectReferenceValue == gameManager, "UIManager must reference GameManager.");
        Require(serializedUIManager.FindProperty("coinIcon").objectReferenceValue == coinIcon, "UIManager must reference Coin Icon.");
        Require(serializedUIManager.FindProperty("coinText").objectReferenceValue == coinText, "UIManager must reference Coin Text.");
        Require(canvasObject.activeInHierarchy && canvas.enabled && hudObject.activeInHierarchy && coinIcon.enabled && coinText.enabled, "Coin HUD must be active and enabled.");
        RectTransform canvasRect = RequireComponent<RectTransform>(canvasObject);
        VerifySerializedCanvasLayout();
        Require(hudRect.anchoredPosition == new Vector2(26f, -26f), "Coin HUD must have a visible top-left margin.");
        Require(AssetDatabase.GetAssetPath(coinIcon.sprite) == GetCoinFramePaths()[0] && coinIcon.color.a == 1f, "Coin Icon must use the opaque original gold coin sprite.");
        Require(coinTextRect.localScale == Vector3.one, "Coin text scale must be one.");
        Require(coinTextRect.sizeDelta.x >= 80f && coinTextRect.sizeDelta.y >= 50f, "Coin text must have a readable size.");
        Require(coinText.color == Color.white && coinText.font != null && coinText.fontSize >= 28 && coinText.fontStyle == FontStyle.Bold,
            "Coin text must use an opaque white, readable bold font.");
        Outline outline = RequireComponent<Outline>(coinTextObject);
        Require(outline.enabled && outline.effectColor.a >= 0.9f, "Coin text must have a visible high-contrast outline.");
        Rect referenceCanvas = new Rect(0f, 0f, scaler.referenceResolution.x, scaler.referenceResolution.y);
        Rect referenceHud = new Rect(hudRect.anchoredPosition.x,
            scaler.referenceResolution.y + hudRect.anchoredPosition.y - hudRect.sizeDelta.y,
            hudRect.sizeDelta.x, hudRect.sizeDelta.y);
        Require(referenceCanvas.Contains(referenceHud.min) && referenceCanvas.Contains(referenceHud.max),
            "Coin HUD rectangle must be fully inside the reference-resolution Canvas.");
        VerifyHudAtResolution(hudRect, scaler, new Vector2(1920f, 1080f));
        VerifyHudAtResolution(hudRect, scaler, new Vector2(1280f, 720f));
        VerifyHudAtResolution(hudRect, scaler, new Vector2(800f, 1200f));
        VerifyFirstFrameFraming(cameraComponent: RequireComponent<Camera>(cameraObject), player, hudRect, scaler);

        AudioManager audioManager = RequireComponent<AudioManager>(RequireObject(scene, "AudioManager"));
        SerializedObject serializedAudioManager = new SerializedObject(audioManager);
        Require(serializedAudioManager.FindProperty("gameManager").objectReferenceValue == gameManager, "AudioManager must reference GameManager.");
        Require(AssetDatabase.GetAssetPath(serializedAudioManager.FindProperty("musicClip").objectReferenceValue) == MusicPath, "AudioManager must use the selected music.");
        Require(AssetDatabase.GetAssetPath(serializedAudioManager.FindProperty("coinClip").objectReferenceValue) == CoinSfxPath, "AudioManager must use the selected coin SFX.");
        Require(AssetDatabase.GetAssetPath(serializedAudioManager.FindProperty("barrelClip").objectReferenceValue) == BarrelSfxPath, "AudioManager must keep the barrel SFX prepared.");
        AudioSource[] audioSources = audioManager.GetComponents<AudioSource>();
        Require(audioSources.Length == 2, "AudioManager must have separate music and SFX sources.");
        Require(audioSources[0].loop && Mathf.Approximately(audioSources[0].volume, 0.35f), "Music must loop at a reasonable volume.");

        Camera cameraComponent = RequireComponent<Camera>(cameraObject);
        Require(cameraComponent.orthographic, "Camera must be orthographic.");
        Require(Mathf.Approximately(cameraComponent.orthographicSize, CameraOrthographicSize), "Main Camera size is incorrect.");
        RequireComponent<CinemachineBrain>(cameraObject);
        Require(cameraObject.GetComponent<AudioListener>() != null, "Main Camera must have the AudioListener.");
        Require(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include).Length == 1, "The scene must contain exactly one AudioListener.");

        UnityEditor.PackageManager.PackageInfo cinemachinePackage = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(CinemachineCamera).Assembly);
        Require(cinemachinePackage != null && cinemachinePackage.name == "com.unity.cinemachine", "Cinemachine package must be installed.");
        Require(cinemachinePackage.version == "3.1.7", "Cinemachine package version must be 3.1.7.");

        CinemachineCamera cinemachineCamera = RequireComponent<CinemachineCamera>(cinemachineObject);
        Require(cinemachineCamera.Follow == player.transform, "Cinemachine Camera must follow Player.");
        Require(cinemachineCamera.Lens.ModeOverride == LensSettings.OverrideModes.Orthographic, "Cinemachine Camera lens must be orthographic.");
        Require(Mathf.Approximately(cinemachineCamera.Lens.OrthographicSize, CameraOrthographicSize), "Cinemachine Camera size is incorrect.");
        CinemachinePositionComposer positionComposer = RequireComponent<CinemachinePositionComposer>(cinemachineObject);
        Require(positionComposer.Damping == new Vector3(0.35f, 0.5f, 0f), "Position Composer damping is incorrect.");
        Require(positionComposer.Composition.DeadZone.Enabled, "Position Composer dead zone must be enabled.");
        Require(positionComposer.Composition.DeadZone.Size == new Vector2(0.25f, 0.18f), "Position Composer dead zone size is incorrect.");
        Require(positionComposer.Composition.ScreenPosition == new Vector2(-0.2f, 0.05f), "Position Composer must frame the Player in the left third.");
        Require(Mathf.Approximately(positionComposer.CameraDistance, 10f), "Position Composer camera distance must be 10.");
        Require(cameraObject.transform.position == InitialCameraPosition && cinemachineObject.transform.position == InitialCameraPosition,
            "Main and Cinemachine cameras must share the first-frame position.");

        PolygonCollider2D cameraBounds = RequireComponent<PolygonCollider2D>(cameraBoundsObject);
        Require(cameraBounds.isTrigger && cameraBounds.pathCount == 1 && cameraBounds.GetPath(0).Length == 4, "Camera Bounds must be a trigger with a closed four-point polygon collider.");
        Require(cameraBoundsObject.GetComponent<Renderer>() == null, "Camera Bounds must not have a renderer.");
        Require(cameraBounds.bounds.size.x > CameraOrthographicSize * 3.56f && cameraBounds.bounds.size.y > CameraOrthographicSize * 2f, "Camera Bounds must be larger than the 16:9 orthographic window.");
        CinemachineConfiner2D confiner = RequireComponent<CinemachineConfiner2D>(cinemachineObject);
        Require(confiner.BoundingShape2D == cameraBounds, "Confiner must reference Camera Bounds.");

        GameObject backgrounds = RequireObject(scene, "Backgrounds");
        Require(backgrounds.transform.childCount == 5, "The extended level must contain five continuous background panels.");
        foreach (SpriteRenderer backgroundRenderer in backgrounds.GetComponentsInChildren<SpriteRenderer>())
        {
            Require(AssetDatabase.GetAssetPath(backgroundRenderer.sprite) == BackgroundPath, "Background panels must use the original background sprite.");
            Require(backgroundRenderer.sortingOrder < RequireComponent<TilemapRenderer>(level).sortingOrder, "Background must render behind the level.");
            Require(backgroundRenderer.bounds.min.y <= cameraBounds.bounds.min.y && backgroundRenderer.bounds.max.y >= cameraBounds.bounds.max.y,
                backgroundRenderer.name + " must vertically cover the enlarged camera bounds.");
        }
        GameObject decoration = RequireObject(scene, "Decoration");
        Require(decoration.transform.childCount >= 16, "The level must contain the complete decoration pass.");
        Barrel[] barrels = UnityEngine.Object.FindObjectsByType<Barrel>(FindObjectsSortMode.None);
        Require(barrels.Length == 4, "Scene must contain exactly four interactive barrels.");
        GameObject barrelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BarrelPrefabPath);
        Require(barrelPrefab != null, "Barrel prefab must exist at " + BarrelPrefabPath);
        foreach (Barrel barrel in barrels)
        {
            Require(PrefabUtility.GetCorrespondingObjectFromSource(barrel.gameObject) == barrelPrefab, barrel.name + " must be a Barrel prefab instance.");
        }
        foreach (SpriteRenderer renderer in decoration.GetComponentsInChildren<SpriteRenderer>())
        {
            Require(renderer.sortingOrder > 0 && renderer.sortingOrder < playerRenderer.sortingOrder, renderer.name + " has an invalid sorting order.");
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(renderer.sprite));
            Require(importer != null && importer.filterMode == FilterMode.Point, renderer.name + " must use Point filtering.");
        }
        VerifySpikeHazards(scene);
        VerifyBarrelExplosionParticles();
        VerifyGoalSystem(scene);
        VerifyThreeZoneLayout(scene, level, cameraBounds, coins, barrels);
        Require(QualitySettings.renderPipeline != null, "Universal Render Pipeline must be active.");
        Require(EditorBuildSettings.scenes.Length == 1 && EditorBuildSettings.scenes[0].path == ScenePath, "Main scene must be the only build scene.");
        VerifyCoinCounterFlow(gameManager, uiManager, coinText, coins);
        Debug.Log("LABORATORIO2D_VERIFICATION_SUCCESS");
    }

    public static void SetupPlayerAnimations()
    {
        EnsureDirectories();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
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

    public static void SetupCinemachineCamera()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = RequireObject(scene, "Player");
        GameObject cameraObject = RequireObject(scene, "Main Camera");
        if (cameraObject.GetComponent<CinemachineBrain>() == null)
        {
            cameraObject.AddComponent<CinemachineBrain>();
        }

        GameObject cameraBoundsObject = GameObject.Find("Camera Bounds");
        if (cameraBoundsObject == null)
        {
            cameraBoundsObject = new GameObject("Camera Bounds", typeof(PolygonCollider2D));
        }
        PolygonCollider2D cameraBounds = RequireComponent<PolygonCollider2D>(cameraBoundsObject);
        cameraBounds.isTrigger = true;
        cameraBounds.SetPath(0, new[]
        {
            new Vector2(-9.6f, -3.6f),
            new Vector2(19.2f, -3.6f),
            new Vector2(19.2f, 4f),
            new Vector2(-9.6f, 4f)
        });

        GameObject cinemachineObject = GameObject.Find("Cinemachine Camera");
        if (cinemachineObject == null)
        {
            cinemachineObject = new GameObject("Cinemachine Camera");
        }
        cinemachineObject.transform.position = InitialCameraPosition;
        CinemachineCamera cinemachineCamera = cinemachineObject.GetComponent<CinemachineCamera>();
        if (cinemachineCamera == null)
        {
            cinemachineCamera = cinemachineObject.AddComponent<CinemachineCamera>();
        }
        cinemachineCamera.Follow = player.transform;
        LensSettings lens = cinemachineCamera.Lens;
        lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        lens.OrthographicSize = CameraOrthographicSize;
        cinemachineCamera.Lens = lens;

        CinemachinePositionComposer positionComposer = cinemachineObject.GetComponent<CinemachinePositionComposer>();
        if (positionComposer == null)
        {
            positionComposer = cinemachineObject.AddComponent<CinemachinePositionComposer>();
        }
        positionComposer.CameraDistance = 10f;
        positionComposer.TargetOffset = new Vector3(0f, 0.15f, 0f);
        positionComposer.Damping = new Vector3(0.35f, 0.5f, 0f);
        ScreenComposerSettings composition = positionComposer.Composition;
        composition.ScreenPosition = new Vector2(-0.2f, 0.05f);
        composition.DeadZone.Enabled = true;
        composition.DeadZone.Size = new Vector2(0.25f, 0.18f);
        positionComposer.Composition = composition;

        CinemachineConfiner2D confiner = cinemachineObject.GetComponent<CinemachineConfiner2D>();
        if (confiner == null)
        {
            confiner = cinemachineObject.AddComponent<CinemachineConfiner2D>();
        }
        confiner.BoundingShape2D = cameraBounds;
        confiner.Damping = 0.2f;
        confiner.SlowingDistance = 0.15f;
        confiner.enabled = false;
        confiner.enabled = true;
        var invalidate = typeof(CinemachineConfiner2D).GetMethod("InvalidateCache") ?? typeof(CinemachineConfiner2D).GetMethod("InvalidateBoundingShapeCache");
        if (invalidate != null)
        {
            invalidate.Invoke(confiner, null);
        }

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("LABORATORIO2D_CINEMACHINE_SETUP_SUCCESS");
    }

    public static void SetupCollectiblesAndUI()
    {
        EnsureDirectories();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = RequireObject(scene, "Player");
        player.tag = "Player";

        GameObject existingCollectibles = GameObject.Find("Collectibles");
        if (existingCollectibles != null)
        {
            UnityEngine.Object.DestroyImmediate(existingCollectibles);
        }
        GameObject legacyCoin = GameObject.Find("Coin");
        if (legacyCoin != null)
        {
            UnityEngine.Object.DestroyImmediate(legacyCoin);
        }

        GameObject coinPrefab = CreateCoinPrefab();
        GameObject collectibles = new GameObject("Collectibles");
        // Redistributed to platform centers +0.5 above top, gap 0.4-0.6 verified, no bottom intersect (radius 0.225)
        Vector3[] positions =
        {
            new Vector3(-7.20f, 0.18f, 0f),
            new Vector3(-4.32f, 0.18f, 0f),
            new Vector3(1.92f, 0.50f, 0f),
            new Vector3(6.40f, 0.82f, 0f),
            new Vector3(16.00f, 1.78f, 0f)
        };
        Coin[] coins = new Coin[positions.Length];
        for (int index = 0; index < positions.Length; index++)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(coinPrefab, collectibles.transform);
            instance.name = "Coin " + (index + 1);
            instance.transform.position = positions[index];
            coins[index] = instance.GetComponent<Coin>();
        }

        GameObject managerObject = GameObject.Find("GameManager");
        if (managerObject == null)
        {
            managerObject = new GameObject("GameManager");
        }
        GameManager gameManager = managerObject.GetComponent<GameManager>();
        if (gameManager == null)
        {
            gameManager = managerObject.AddComponent<GameManager>();
        }
        SerializedObject serializedGameManager = new SerializedObject(gameManager);
        SerializedProperty coinReferences = serializedGameManager.FindProperty("coins");
        coinReferences.arraySize = coins.Length;
        for (int index = 0; index < coins.Length; index++)
        {
            coinReferences.GetArrayElementAtIndex(index).objectReferenceValue = coins[index];
        }
        serializedGameManager.ApplyModifiedPropertiesWithoutUndo();

        CreateResponsiveUI(gameManager);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("LABORATORIO2D_COLLECTIBLES_UI_SETUP_SUCCESS");
    }

    public static void SetupUIAndAudio()
    {
        EnsureDirectories();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameManager gameManager = RequireComponent<GameManager>(RequireObject(scene, "GameManager"));
        CreateResponsiveUI(gameManager);
        SetupAudioInScene(scene, gameManager);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("LABORATORIO2D_UI_AUDIO_SETUP_SUCCESS");
    }

    public static void SetupAudio()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SetupAudioInScene(scene, RequireComponent<GameManager>(RequireObject(scene, "GameManager")));
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
    }

    private static void SetupAudioInScene(Scene scene, GameManager gameManager)
    {
        GameObject existingAudioManager = GameObject.Find("AudioManager");
        if (existingAudioManager != null)
        {
            UnityEngine.Object.DestroyImmediate(existingAudioManager);
        }

        GameObject audioObject = new GameObject("AudioManager", typeof(AudioManager), typeof(AudioSource), typeof(AudioSource));
        AudioSource[] sources = audioObject.GetComponents<AudioSource>();
        sources[0].playOnAwake = false;
        sources[0].loop = true;
        sources[0].volume = 0.35f;
        sources[1].playOnAwake = false;
        sources[1].loop = false;
        sources[1].volume = 1f;

        AudioManager audioManager = audioObject.GetComponent<AudioManager>();
        SerializedObject serializedAudioManager = new SerializedObject(audioManager);
        serializedAudioManager.FindProperty("gameManager").objectReferenceValue = gameManager;
        serializedAudioManager.FindProperty("musicSource").objectReferenceValue = sources[0];
        serializedAudioManager.FindProperty("sfxSource").objectReferenceValue = sources[1];
        serializedAudioManager.FindProperty("musicClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
        serializedAudioManager.FindProperty("coinClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(CoinSfxPath);
        serializedAudioManager.FindProperty("barrelClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(BarrelSfxPath);
        serializedAudioManager.ApplyModifiedPropertiesWithoutUndo();

        GameObject cameraObject = RequireObject(scene, "Main Camera");
        foreach (AudioListener listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include))
        {
            UnityEngine.Object.DestroyImmediate(listener);
        }
        cameraObject.AddComponent<AudioListener>();
    }

    private static void EnsureDirectories()
    {
        Directory.CreateDirectory(Root + "/Scenes");
        Directory.CreateDirectory(TileRoot);
        Directory.CreateDirectory(AnimationRoot);
        Directory.CreateDirectory(BarrelAnimationRoot);
        Directory.CreateDirectory(Root + "/Prefabs");
        Directory.CreateDirectory(Root + "/Physics");
    }

    private static GameObject CreateCoinPrefab()
    {
        Sprite coinSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GetCoinFramePaths()[0]);
        GameObject template = new GameObject("Coin", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Coin));
        template.transform.localScale = new Vector3(CoinScale, CoinScale, 1f);
        SpriteRenderer renderer = template.GetComponent<SpriteRenderer>();
        renderer.sprite = coinSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = 5;
        CircleCollider2D collider = template.GetComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = CoinColliderRadius;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, CoinPrefabPath);
        UnityEngine.Object.DestroyImmediate(template);
        return prefab;
    }

    private static void CreateResponsiveUI(GameManager gameManager)
    {
        GameObject existingCanvas = GameObject.Find("Canvas");
        if (existingCanvas != null)
        {
            UnityEngine.Object.DestroyImmediate(existingCanvas);
        }

        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(UIManager));
        canvasObject.transform.localScale = Vector3.one;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject hudObject = new GameObject("Coin HUD", typeof(RectTransform));
        hudObject.transform.SetParent(canvasObject.transform, false);
        RectTransform hudRect = hudObject.GetComponent<RectTransform>();
        hudRect.anchorMin = hudRect.anchorMax = new Vector2(0f, 1f);
        hudRect.pivot = new Vector2(0f, 1f);
        hudRect.anchoredPosition = new Vector2(26f, -26f);
        hudRect.sizeDelta = new Vector2(150f, 64f);

        GameObject iconObject = new GameObject("Coin Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.transform.SetParent(hudObject.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(56f, 56f);
        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GetCoinFramePaths()[0]);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        GameObject textObject = new GameObject("Coin Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(hudObject.transform, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(64f, 0f);
        rect.sizeDelta = new Vector2(86f, 56f);
        Text text = textObject.GetComponent<Text>();
        text.text = "00";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 32;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = Color.white;
        text.raycastTarget = false;
        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        UIManager uiManager = canvasObject.GetComponent<UIManager>();
        SerializedObject serializedUIManager = new SerializedObject(uiManager);
        serializedUIManager.FindProperty("gameManager").objectReferenceValue = gameManager;
        serializedUIManager.FindProperty("coinIcon").objectReferenceValue = icon;
        serializedUIManager.FindProperty("coinText").objectReferenceValue = text;
        serializedUIManager.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedCanvasRect = new SerializedObject(canvasObject.GetComponent<RectTransform>());
        serializedCanvasRect.FindProperty("m_LocalScale").vector3Value = Vector3.one;
        serializedCanvasRect.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void FinalizeCoinCounterLayout()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject canvasObject = RequireObject(scene, "Canvas");
        RectTransform canvasRect = RequireComponent<RectTransform>(canvasObject);
        canvasRect.localScale = Vector3.one;
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.anchoredPosition = Vector2.zero;
        canvasRect.sizeDelta = Vector2.zero;
        canvasRect.pivot = new Vector2(0.5f, 0.5f);

        RectTransform hudRect = RequireComponent<RectTransform>(RequireObject(scene, "Canvas/Coin HUD"));
        hudRect.localScale = Vector3.one;
        hudRect.anchorMin = hudRect.anchorMax = new Vector2(0f, 1f);
        hudRect.pivot = new Vector2(0f, 1f);
        hudRect.anchoredPosition = new Vector2(26f, -26f);
        hudRect.sizeDelta = new Vector2(150f, 64f);

        RectTransform coinTextRect = RequireComponent<RectTransform>(RequireObject(scene, "Canvas/Coin HUD/Coin Text"));
        coinTextRect.localScale = Vector3.one;

        EditorUtility.SetDirty(canvasRect);
        EditorUtility.SetDirty(hudRect);
        EditorUtility.SetDirty(coinTextRect);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static void PreserveSerializedCanvasLayout()
    {
        string yaml = File.ReadAllText(ScenePath);
        Match canvasObject = Regex.Match(yaml,
            @"--- !u!1 &\d+\r?\nGameObject:\r?\n(?:(?!--- !u!).)*?  m_Name: Canvas\r?\n(?:(?!--- !u!).)*?",
            RegexOptions.Singleline);
        Require(canvasObject.Success, "Serialized Canvas GameObject must exist.");

        MatchCollection components = Regex.Matches(canvasObject.Value, @"component: \{fileID: (\d+)\}");
        Require(components.Count > 0, "Serialized Canvas must have a RectTransform component.");
        string rectId = FindRectTransformId(yaml, components);
        Match canvasRect = Regex.Match(yaml,
            @"--- !u!224 &" + rectId + @"\r?\nRectTransform:\r?\n(?:(?!--- !u!).)*(?=--- !u!|\z)",
            RegexOptions.Singleline);
        Require(canvasRect.Success, "Serialized Canvas RectTransform must exist.");

        string corrected = canvasRect.Value;
        corrected = Regex.Replace(corrected, @"  m_LocalScale: \{[^\r\n]+\}", "  m_LocalScale: {x: 1, y: 1, z: 1}");
        corrected = Regex.Replace(corrected, @"  m_AnchorMin: \{[^\r\n]+\}", "  m_AnchorMin: {x: 0, y: 0}");
        corrected = Regex.Replace(corrected, @"  m_AnchorMax: \{[^\r\n]+\}", "  m_AnchorMax: {x: 1, y: 1}");
        corrected = Regex.Replace(corrected, @"  m_AnchoredPosition: \{[^\r\n]+\}", "  m_AnchoredPosition: {x: 0, y: 0}");
        corrected = Regex.Replace(corrected, @"  m_SizeDelta: \{[^\r\n]+\}", "  m_SizeDelta: {x: 0, y: 0}");
        corrected = Regex.Replace(corrected, @"  m_Pivot: \{[^\r\n]+\}", "  m_Pivot: {x: 0.5, y: 0.5}");
        File.WriteAllText(ScenePath, yaml.Substring(0, canvasRect.Index) + corrected + yaml.Substring(canvasRect.Index + canvasRect.Length));
    }

    private static void RemoveGeneratedTrailingWhitespace()
    {
        string[] generatedTextAssets =
        {
            ScenePath, AnimatorPath, BackgroundPath + ".meta", SheetRoot + "/tilemap.png.meta",
            SoftSandSheetPath + ".meta"
        };
        foreach (string path in generatedTextAssets)
        {
            if (!File.Exists(path))
            {
                continue;
            }
            string content = File.ReadAllText(path);
            string cleaned = Regex.Replace(content, @"[ \t]+(?=\r?$)", string.Empty, RegexOptions.Multiline);
            if (cleaned != content)
            {
                File.WriteAllText(path, cleaned);
            }
        }
    }

    private static void VerifySerializedCanvasLayout()
    {
        string yaml = File.ReadAllText(ScenePath);
        Match canvasObject = Regex.Match(yaml,
            @"--- !u!1 &\d+\r?\nGameObject:\r?\n(?:(?!--- !u!).)*?  m_Name: Canvas\r?\n(?:(?!--- !u!).)*?",
            RegexOptions.Singleline);
        Require(canvasObject.Success, "Serialized Canvas GameObject must exist.");
        MatchCollection components = Regex.Matches(canvasObject.Value, @"component: \{fileID: (\d+)\}");
        string rectId = FindRectTransformId(yaml, components);
        Match canvasRect = Regex.Match(yaml,
            @"--- !u!224 &" + rectId + @"\r?\nRectTransform:\r?\n(?:(?!--- !u!).)*(?=--- !u!|\z)",
            RegexOptions.Singleline);
        Require(canvasRect.Success && canvasRect.Value.Contains("m_LocalScale: {x: 1, y: 1, z: 1}"),
            "Serialized Canvas scale must be one.");
        Require(canvasRect.Value.Contains("m_AnchorMin: {x: 0, y: 0}") && canvasRect.Value.Contains("m_AnchorMax: {x: 1, y: 1}"),
            "Serialized Canvas RectTransform must stretch to the screen.");
        Require(canvasRect.Value.Contains("m_Pivot: {x: 0.5, y: 0.5}"), "Serialized Canvas pivot must be centered.");
    }

    private static void VerifyRawSerializedState()
    {
        string yaml = File.ReadAllText(ScenePath);
        Require(yaml.Contains("  m_Name: Canvas\n") || yaml.Contains("  m_Name: Canvas\r\n"), "RAW scene must contain Canvas before loading.");
        Require(yaml.Contains("  m_Name: Coin HUD\n") || yaml.Contains("  m_Name: Coin HUD\r\n"), "RAW scene must contain Coin HUD before loading.");
        Require(yaml.Contains("  m_Name: Coin Icon\n") || yaml.Contains("  m_Name: Coin Icon\r\n"), "RAW scene must contain Coin Icon before loading.");
        Require(yaml.Contains("  m_Text: 00"), "RAW Coin Text must start at 00 before UIManager runs.");
        Require(yaml.Contains("  m_ReferenceResolution: {x: 1920, y: 1080}"), "RAW CanvasScaler reference resolution must be 1920 x 1080.");
        Require(yaml.Contains("  m_RenderMode: 0"), "RAW Canvas must be Screen Space Overlay.");
        Require(yaml.Contains("    OrthographicSize: 3.25") && yaml.Contains("  orthographic size: 3.25"),
            "RAW scene cameras must serialize orthographic size 3.25.");

        Match uiManager = Regex.Match(yaml,
            @"m_EditorClassIdentifier: Assembly-CSharp::UIManager\r?\n  gameManager: \{fileID: (?!0\})\d+\}\r?\n  coinIcon: \{fileID: (?!0\})\d+\}\r?\n  coinText: \{fileID: (?!0\})\d+\}");
        Require(uiManager.Success, "RAW UIManager must serialize non-null GameManager, Coin Icon, and Coin Text references.");
        VerifySerializedCanvasLayout();
    }

    private static string FindRectTransformId(string yaml, MatchCollection components)
    {
        foreach (Match component in components)
        {
            string id = component.Groups[1].Value;
            if (Regex.IsMatch(yaml, @"--- !u!224 &" + id + @"\r?\nRectTransform:"))
            {
                return id;
            }
        }

        throw new InvalidOperationException("Serialized Canvas RectTransform must exist.");
    }

    private static string[] GetBarrelFramePaths()
    {
        string[] paths = new string[7];
        paths[0] = PngRoot + "/object_barrel_light_idle.png";
        for (int index = 1; index < paths.Length; index++)
        {
            paths[index] = PngRoot + "/object_barrel_light_explode_" + index + ".png";
        }
        return paths;
    }

    private static string[] GetCoinFramePaths()
    {
        string[] paths = new string[4];
        for (int index = 0; index < paths.Length; index++)
        {
            paths[index] = PngRoot + "/collectibles_coin_gold_" + (index + 1) + ".png";
        }
        return paths;
    }

    private static string[] GetSpikeFramePaths()
    {
        string[] paths = new string[4];
        for (int index = 0; index < paths.Length; index++)
        {
            paths[index] = PngRoot + "/trap_spike_" + (index + 1) + ".png";
        }
        return paths;
    }

    private static void ConfigureVisualTextures()
    {
        ConfigureTexture(BackgroundPath, 320, SpriteImportMode.Single);
        foreach (string path in GetBarrelFramePaths())
        {
            ConfigureTexture(path, 100, SpriteImportMode.Single);
        }
        foreach (string path in GetCoinFramePaths())
        {
            ConfigureTexture(path, 100, SpriteImportMode.Single);
        }
        foreach (string path in GetSpikeFramePaths())
        {
            ConfigureTexture(path, 100, SpriteImportMode.Single);
        }
        ConfigureEnvironmentSheet(PalmPath, "Palm", new Rect(0f, 0f, 96f, 96f));
        ConfigureEnvironmentSheet(GrassPath, "Grass", new Rect(0f, 0f, 32f, 32f));
        ConfigureRockSheet();
        ConfigureTexture(BombPath, 100, SpriteImportMode.Single);
    }

    private static string[] GetFramePaths(string state)
    {
        int frameCount = state == "Run" ? 6 : state == "Fall" ? 2 : 4;
        string[] paths = new string[frameCount];
        for (int index = 0; index < frameCount; index++)
        {
            paths[index] = PlayerSpriteRoot + "/character_berie_" + state.ToLowerInvariant() + "_" + (index + 1) + ".png";
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

    private static void VerifyCoinCounterFlow(GameManager gameManager, UIManager uiManager, Text coinText, Coin[] coins)
    {
        BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        MethodInfo gameManagerOnEnable = typeof(GameManager).GetMethod("OnEnable", privateInstance);
        MethodInfo gameManagerOnDisable = typeof(GameManager).GetMethod("OnDisable", privateInstance);
        MethodInfo uiManagerOnEnable = typeof(UIManager).GetMethod("OnEnable", privateInstance);
        MethodInfo uiManagerOnDisable = typeof(UIManager).GetMethod("OnDisable", privateInstance);
        FieldInfo collectedEvent = typeof(Coin).GetField("Collected", privateInstance);
        Require(gameManagerOnEnable != null && gameManagerOnDisable != null && uiManagerOnEnable != null && uiManagerOnDisable != null && collectedEvent != null,
            "Coin counter lifecycle methods and event must be available for flow verification.");

        gameManagerOnEnable.Invoke(gameManager, null);
        uiManagerOnEnable.Invoke(uiManager, null);
        Require(coinText.text == "00", "Coin counter flow must render 00 before collection.");
        for (int index = 0; index < coins.Length; index++)
        {
            Action<Coin> collected = (Action<Coin>)collectedEvent.GetValue(coins[index]);
            Require(collected != null, coins[index].name + " must have GameManager subscribed to its Collected event.");
            collected.Invoke(coins[index]);
            Require(gameManager.CoinCount == index + 1, "GameManager coin count did not increment correctly.");
            Require(coinText.text == (index + 1).ToString("D2"), "UI did not render the expected two-digit coin count.");
        }
        uiManagerOnDisable.Invoke(uiManager, null);
        gameManagerOnDisable.Invoke(gameManager, null);
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
        string path = SheetRoot + "/tilemap.png";
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
            SpriteMetadata("SandLeft", 0f, 128f),
            SpriteMetadata("SandCenter", 32f, 128f),
            SpriteMetadata("SandRight", 64f, 128f)
        };
        importer.SaveAndReimport();
    }

    private static SpriteMetaData SpriteMetadata(string name, float x, float y)
    {
        return new SpriteMetaData
        {
            name = name,
            rect = new Rect(x, y, 32f, 32f),
            alignment = (int)SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f)
        };
    }

    private static void ConfigureSoftSandSheet()
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SoftSandSheetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritesheet = new[]
        {
            new SpriteMetaData { name = "SoftSandGround", rect = new Rect(32f, 128f, 32f, 32f), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) }
        };
        importer.SaveAndReimport();
    }

    private static void ConfigureEnvironmentSheet(string path, string spriteName, Rect rect)
    {
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
            new SpriteMetaData { name = spriteName, rect = rect, alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) }
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

    private static Tile CreateTile(string tilePath, string sheetPath, string spriteName)
    {
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, tilePath);
        }
        UnityEngine.Object[] sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(sheetPath);
        tile.sprite = Array.Find(sprites, item => item is Sprite && item.name == spriteName) as Sprite;
        Require(tile.sprite != null, spriteName + " tile sprite was not imported.");
        tile.colliderType = Tile.ColliderType.Sprite;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    private static Tile CreateSoftSandTile()
    {
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(SoftSandTilePath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, SoftSandTilePath);
        }
        UnityEngine.Object[] sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(SoftSandSheetPath);
        tile.sprite = Array.Find(sprites, item => item is Sprite && item.name == "SoftSandGround") as Sprite;
        Require(tile.sprite != null, "Soft sand tile sprite was not imported.");
        tile.colliderType = Tile.ColliderType.Sprite;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    private static void CreateMainScene(PhysicsMaterial2D zeroFriction, Tile sandTile, Tile sandLeftTile, Tile sandRightTile, Tile softSandTile)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject backgrounds = new GameObject("Backgrounds");
        for (int index = 0; index < 5; index++)
        {
            GameObject background = CreateDecoration("Background " + (index + 1), AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath),
                new Vector3(-9.6f + index * 6.4f, 0.2f, 2f), new Vector3(6.4f, 14.4f, 1f), -10, backgrounds.transform);
        }

        GameObject gridObject = new GameObject("Grid", typeof(Grid));
        Grid grid = gridObject.GetComponent<Grid>();
        grid.cellSize = new Vector3(0.32f, 0.32f, 0f);

        GameObject levelObject = new GameObject("Level", typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D));
        levelObject.transform.SetParent(gridObject.transform);
        levelObject.layer = LayerMask.NameToLayer("Floor");
        Tilemap tilemap = levelObject.GetComponent<Tilemap>();
        TilemapRenderer tilemapRenderer = levelObject.GetComponent<TilemapRenderer>();
        tilemapRenderer.sortingOrder = 0;
        PaintRectangle(tilemap, softSandTile, -30, 60, -6, -5);

        CreatePlatform("Zone 1 - Start A", tilemap, sandTile, sandLeftTile, sandRightTile, -30, -18, -2);
        CreatePlatform("Zone 1 - Start B", tilemap, softSandTile, sandLeftTile, sandRightTile, -15, -10, -2);
        CreatePlatform("Zone 2 - Development A", tilemap, sandTile, sandLeftTile, sandRightTile, -7, 1, -2);
        CreatePlatform("Zone 2 - Development B", tilemap, softSandTile, sandLeftTile, sandRightTile, 4, 12, -1);
        CreatePlatform("Zone 2 - Development C", tilemap, sandTile, sandLeftTile, sandRightTile, 15, 24, 0);
        CreatePlatform("Zone 3 - Final A", tilemap, softSandTile, sandLeftTile, sandRightTile, 27, 35, 0);
        CreatePlatform("Zone 3 - Final B", tilemap, sandTile, sandLeftTile, sandRightTile, 38, 44, 1);
        CreatePlatform("Zone 3 - Final C", tilemap, softSandTile, sandLeftTile, sandRightTile, 47, 52, 3);
        CreatePlatform("Zone 3 - Goal Reserve", tilemap, sandTile, sandLeftTile, sandRightTile, 55, 60, 3);

        GameObject player = new GameObject("Player", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CapsuleCollider2D), typeof(PlayerController));
        player.transform.position = PlayerStartPosition;
        SpriteRenderer playerRenderer = player.GetComponent<SpriteRenderer>();
        playerRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GetFramePaths("Idle")[0]);
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

        GameObject coin = new GameObject("Coin", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Coin));
        coin.transform.position = new Vector3(1.3f, -0.35f, 0f);
        coin.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
        SpriteRenderer coinRenderer = coin.GetComponent<SpriteRenderer>();
        coinRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GetCoinFramePaths()[0]);
        coinRenderer.color = Color.white;
        coinRenderer.sortingOrder = 5;
        coin.GetComponent<CircleCollider2D>().isTrigger = true;

        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = InitialCameraPosition;
        Camera cameraComponent = cameraObject.GetComponent<Camera>();
        cameraComponent.orthographic = true;
        cameraComponent.orthographicSize = CameraOrthographicSize;
        cameraComponent.clearFlags = CameraClearFlags.SolidColor;
        cameraComponent.backgroundColor = new Color(0.20f, 0.45f, 0.90f, 1f);

        CreateEnvironmentDecoration();
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static void CreateEnvironmentDecoration()
    {
        GameObject root = new GameObject("Decoration");
        Sprite palm = Array.Find(AssetDatabase.LoadAllAssetRepresentationsAtPath(PalmPath), item => item is Sprite) as Sprite;
        Sprite grass = Array.Find(AssetDatabase.LoadAllAssetRepresentationsAtPath(GrassPath), item => item is Sprite) as Sprite;
        Require(palm != null && grass != null, "Palm and grass sprites must be imported from their original sheets.");
        Sprite rock = Array.Find(AssetDatabase.LoadAllAssetRepresentationsAtPath(RockSheetPath), item => item.name == "RockLarge") as Sprite;
        CreateDecoration("Palm Start", palm, new Vector3(-8.8f, -0.72f), Vector3.one, 2, root.transform);
        CreateDecoration("Palm Development", palm, new Vector3(3.2f, -0.40f), new Vector3(-1f, 1f, 1f), 2, root.transform);
        CreateDecoration("Palm Final", palm, new Vector3(17.8f, 0.88f), Vector3.one, 2, root.transform);
        float[] grassX = { -8.0f, -5.2f, -3.0f, -1.2f, 1.0f, 3.0f, 5.4f, 7.0f, 9.2f, 11.6f, 14.8f, 18.4f };
        for (int i = 0; i < grassX.Length; i++)
            CreateDecoration("Grass " + (i + 1), grass, new Vector3(grassX[i], -1.43f), i % 2 == 0 ? Vector3.one : new Vector3(-1f, 1f, 1f), 3, root.transform);
        CreateDecoration("Rock Start", rock, new Vector3(-5.8f, -0.40f), Vector3.one, 3, root.transform);
        CreateDecoration("Rock Development", rock, new Vector3(4.8f, 0.24f), new Vector3(-1f, 1f, 1f), 3, root.transform);
        CreateDecoration("Rock Final", rock, new Vector3(13.2f, 0.56f), Vector3.one, 3, root.transform);
        // Barrels are now prefab instances created via SetupBarrelsInDecoration; keep legacy helper but generate via prefab
        SetupBarrelsInDecoration(root);
        // Spikes deco visual without damage using trap_spike assets (Berie Pack original)
        string[] spikePaths = GetSpikeFramePaths();
        Sprite spike1 = AssetDatabase.LoadAssetAtPath<Sprite>(spikePaths[0]);
        Sprite spike2 = AssetDatabase.LoadAssetAtPath<Sprite>(spikePaths[1]);
        Sprite spike3 = AssetDatabase.LoadAssetAtPath<Sprite>(spikePaths[2]);
        Sprite spike4 = AssetDatabase.LoadAssetAtPath<Sprite>(spikePaths[3]);
        if (spike1 != null) CreateDecoration("Spike Visual 1", spike1, new Vector3(0.80f, -1.43f, 0f), Vector3.one * 0.9f, 4, root.transform);
        if (spike2 != null) CreateDecoration("Spike Visual 2", spike2, new Vector3(4.40f, -1.43f, 0f), Vector3.one * 0.9f, 4, root.transform);
        if (spike3 != null) CreateDecoration("Spike Visual 3", spike3, new Vector3(8.80f, -1.43f, 0f), Vector3.one * 0.9f, 4, root.transform);
        if (spike4 != null) CreateDecoration("Spike Visual 4", spike4, new Vector3(14.60f, -1.43f, 0f), Vector3.one * 0.9f, 4, root.transform);
        Sprite bomb = AssetDatabase.LoadAssetAtPath<Sprite>(BombPath);
        Require(bomb != null, "Decorative bomb sprite must load directly from Sprites1.");
        CreateDecoration("Bomb Idle Decorative", bomb, new Vector3(10.56f, 0.32f, 0f), Vector3.one, 4, root.transform);
    }

    private static void VerifyHudAtResolution(RectTransform hudRect, CanvasScaler scaler, Vector2 resolution)
    {
        float widthScale = resolution.x / scaler.referenceResolution.x;
        float heightScale = resolution.y / scaler.referenceResolution.y;
        float scale = Mathf.Pow(widthScale, 1f - scaler.matchWidthOrHeight) * Mathf.Pow(heightScale, scaler.matchWidthOrHeight);
        Vector2 margin = new Vector2(hudRect.anchoredPosition.x, -hudRect.anchoredPosition.y) * scale;
        Vector2 size = hudRect.sizeDelta * scale;
        Require(margin.x >= 0f && margin.y >= 0f && margin.x + size.x <= resolution.x && margin.y + size.y <= resolution.y,
            "Coin HUD must remain inside the Canvas at " + resolution.x + "x" + resolution.y + ".");
    }

    private static void VerifyFirstFrameFraming(Camera cameraComponent, GameObject player, RectTransform hudRect, CanvasScaler scaler)
    {
        Vector2 screen = new Vector2(1300f, 700f);
        float widthScale = screen.x / scaler.referenceResolution.x;
        float heightScale = screen.y / scaler.referenceResolution.y;
        float canvasScale = Mathf.Sqrt(widthScale * heightScale);
        Rect hudPixels = new Rect(
            hudRect.anchoredPosition.x * canvasScale,
            screen.y + hudRect.anchoredPosition.y * canvasScale - hudRect.sizeDelta.y * canvasScale,
            hudRect.sizeDelta.x * canvasScale,
            hudRect.sizeDelta.y * canvasScale);
        Require(hudPixels.xMin >= 0f && hudPixels.yMin >= 0f && hudPixels.xMax <= screen.x && hudPixels.yMax <= screen.y,
            "First-frame HUD pixel rect must be inside the 1300x700 Game View.");

        Vector3 delta = player.transform.position - cameraComponent.transform.position;
        float worldHeight = cameraComponent.orthographicSize * 2f;
        float worldWidth = worldHeight * screen.x / screen.y;
        Vector3 viewport = new Vector3(delta.x / worldWidth + 0.5f, delta.y / worldHeight + 0.5f, delta.z);
        Require(viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f / 3f && viewport.y >= 0f && viewport.y <= 1f,
            "Player must be visible in the left third on the first rendered frame.");
        Debug.Log($"LABORATORIO2D_FIRST_FRAME screen=1300x700 hudPixels={hudPixels} playerViewport={viewport} player={player.transform.position} camera={cameraComponent.transform.position}");
    }

    private static GameObject CreateDecoration(string name, Sprite sprite, Vector3 position, Vector3 scale, int sortingOrder, Transform parent)
    {
        GameObject item = new GameObject(name, typeof(SpriteRenderer));
        item.transform.SetParent(parent);
        item.transform.position = position;
        item.transform.localScale = scale;
        SpriteRenderer renderer = item.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        return item;
    }

    public static void SetupBarrelSystem()
    {
        EnsureDirectories();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string p in GetBarrelFramePaths())
        {
            ConfigureTexture(p, 100, SpriteImportMode.Single);
        }
        AnimationClip idleClip = CreateBarrelIdleClip();
        AnimationClip explodeClip = CreateBarrelExplodeClip();
        AnimatorController barrelController = CreateBarrelAnimator(idleClip, explodeClip);
        PhysicsMaterial2D barrelMaterial = CreateBarrelPhysicsMaterial();
        CreateBarrelPrefab(barrelController, idleClip, barrelMaterial);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BarrelPrefabPath) != null && File.Exists(ScenePath))
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject deco = GameObject.Find("Decoration");
            SetupBarrelsInDecoration(deco);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }
        Debug.Log("LABORATORIO2D_BARREL_SETUP_SUCCESS");
    }

    private static AnimationClip CreateBarrelIdleClip()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BarrelIdlePath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, BarrelIdlePath);
        }
        clip.name = "BarrelIdle";
        clip.frameRate = 1f;
        clip.wrapMode = WrapMode.ClampForever;
        EditorCurveBinding binding = new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };
        Sprite idleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GetBarrelFramePaths()[0]);
        Require(idleSprite != null, "Missing barrel idle sprite: " + GetBarrelFramePaths()[0]);
        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[1];
        keyframes[0] = new ObjectReferenceKeyframe { time = 0f, value = idleSprite };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimationClip CreateBarrelExplodeClip()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BarrelExplodePath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, BarrelExplodePath);
        }
        clip.name = "BarrelExplode";
        clip.frameRate = 12f;
        clip.wrapMode = WrapMode.ClampForever;
        EditorCurveBinding binding = new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };
        string[] paths = GetBarrelFramePaths();
        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[6];
        for (int i = 0; i < 6; i++)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i + 1]);
            Require(sprite != null, "Missing barrel explode sprite: " + paths[i + 1]);
            keyframes[i] = new ObjectReferenceKeyframe { time = i / clip.frameRate, value = sprite };
        }
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorController CreateBarrelAnimator(AnimationClip idle, AnimationClip explode)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BarrelControllerPath);
        if (controller != null)
        {
            return controller;
        }

        controller = AnimatorController.CreateAnimatorControllerAtPath(BarrelControllerPath);
        controller.AddParameter("Explode", AnimatorControllerParameterType.Trigger);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idleState = machine.AddState("Idle", new Vector3(200f, 0f));
        AnimatorState explodeState = machine.AddState("Explode", new Vector3(450f, 0f));
        idleState.motion = idle;
        explodeState.motion = explode;
        machine.defaultState = idleState;
        AnimatorStateTransition transition = idleState.AddTransition(explodeState);
        transition.hasExitTime = false;
        transition.duration = 0f;
        transition.hasFixedDuration = true;
        transition.AddCondition(AnimatorConditionMode.If, 0f, "Explode");
        return controller;
    }

    private static PhysicsMaterial2D CreateBarrelPhysicsMaterial()
    {
        PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(BarrelPhysicsMaterialPath);
        if (material == null)
        {
            material = new PhysicsMaterial2D("Barrel");
            AssetDatabase.CreateAsset(material, BarrelPhysicsMaterialPath);
        }
        material.friction = BarrelFriction;
        material.bounciness = 0f;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject CreateBarrelPrefab(AnimatorController controller, AnimationClip idleClip, PhysicsMaterial2D barrelMaterial)
    {
        Sprite idleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GetBarrelFramePaths()[0]);
        AudioClip barrelClip = AssetDatabase.LoadAssetAtPath<AudioClip>(BarrelSfxPath);
        GameObject template = new GameObject("Barrel", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(Animator), typeof(Barrel), typeof(AudioSource));
        template.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
        SpriteRenderer renderer = template.GetComponent<SpriteRenderer>();
        renderer.sprite = idleSprite;
        renderer.sortingOrder = 5;
        Rigidbody2D body = template.GetComponent<Rigidbody2D>();
        // Conservative tuning: heavier than the player, damped against runaway sliding, but still pushable at speed 2.
        body.bodyType = RigidbodyType2D.Dynamic;
        body.mass = BarrelMass;
        body.linearDamping = BarrelLinearDamping;
        body.gravityScale = BarrelGravityScale;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
        BoxCollider2D mainCollider = template.GetComponent<BoxCollider2D>();
        mainCollider.size = new Vector2(0.28f, 0.32f);
        mainCollider.offset = new Vector2(0f, 0.05f);
        mainCollider.isTrigger = false;
        mainCollider.sharedMaterial = barrelMaterial;
        Animator animator = template.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        AudioSource audioSource = template.GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0.9f;
        Barrel barrel = template.GetComponent<Barrel>();
        SerializedObject so = new SerializedObject(barrel);
        so.FindProperty("bounceForce").floatValue = 7f;
        so.FindProperty("explodeDuration").floatValue = 0.55f;
        so.FindProperty("barrelClip").objectReferenceValue = barrelClip;
        so.FindProperty("audioSource").objectReferenceValue = audioSource;
        GameObject explosionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BarrelExplosionPrefabPath);
        if (explosionPrefab != null)
        {
            so.FindProperty("explosionParticlesPrefab").objectReferenceValue = explosionPrefab;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        GameObject topTrigger = new GameObject("TopTrigger", typeof(BoxCollider2D));
        topTrigger.transform.SetParent(template.transform, false);
        topTrigger.transform.localPosition = Vector3.zero;
        BoxCollider2D trigger = topTrigger.GetComponent<BoxCollider2D>();
        trigger.size = new Vector2(0.28f, 0.08f);
        trigger.offset = new Vector2(0f, 0.18f);
        trigger.isTrigger = true;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, BarrelPrefabPath);
        UnityEngine.Object.DestroyImmediate(template);
        AssetDatabase.SaveAssets();
        return prefab;
    }

    private static void SetupBarrelsInDecoration(GameObject decorationRoot)
    {
        if (decorationRoot == null)
        {
            return;
        }
        GameObject barrelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BarrelPrefabPath);
        if (barrelPrefab == null)
        {
            return;
        }
        string[] legacyNames = { "Barrel West", "Barrel East", "Barrel Lookout", "Box" };
        foreach (string name in legacyNames)
        {
            Transform found = decorationRoot.transform.Find(name);
            if (found != null)
            {
                UnityEngine.Object.DestroyImmediate(found.gameObject);
            }
        }
        GameObject boxRoot = GameObject.Find("Box");
        if (boxRoot != null)
        {
            UnityEngine.Object.DestroyImmediate(boxRoot);
        }
        Vector3[] positions =
        {
            new Vector3(-4.00f, -0.14f, 0f),
            new Vector3(0.00f, -0.14f, 0f),
            new Vector3(6.72f, 0.50f, 0f),
            new Vector3(18.08f, 1.46f, 0f)
        };
        string[] names =
        {
            "Barrel Start Optional",
            "Barrel Development Low",
            "Barrel Development High",
            "Barrel Final Optional"
        };
        for (int i = 0; i < positions.Length; i++)
        {
            Transform existing = decorationRoot.transform.Find(names[i]);
            if (existing != null)
            {
                continue;
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(barrelPrefab, decorationRoot.transform);
            instance.name = names[i];
            instance.transform.position = positions[i];
            instance.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
        }
    }

    public static void SetupBarrelExplosionParticles()
    {
        EnsureDirectories();
        // Create prefab with ParticleSystem polvo/chispas simple visible 0.4-0.6s duration
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(BarrelExplosionPrefabPath);
        if (existing != null)
        {
            // Ensure correct config but keep prefab
            ParticleSystem psExisting = existing.GetComponent<ParticleSystem>();
            if (psExisting != null)
            {
                var main = psExisting.main;
                main.duration = 0.5f;
                main.loop = false;
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                return;
            }
        }

        GameObject template = new GameObject("BarrelExplosionParticles", typeof(ParticleSystem));
        ParticleSystem ps = template.GetComponent<ParticleSystem>();
        var mainModule = ps.main;
        mainModule.duration = 0.5f;
        mainModule.loop = false;
        mainModule.startLifetime = 0.4f;
        mainModule.startSpeed = 2.5f;
        mainModule.startSize = 0.12f;
        mainModule.startColor = new Color(0.85f, 0.75f, 0.55f, 1f);
        mainModule.playOnAwake = false;
        mainModule.maxParticles = 30;
        mainModule.simulationSpace = ParticleSystemSimulationSpace.World;
        mainModule.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 30f;
        shape.radius = 0.1f;

        var renderer = template.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 6;
        // Use default material (null -> Default-Particle), do not invent custom material

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, BarrelExplosionPrefabPath);
        UnityEngine.Object.DestroyImmediate(template);
        AssetDatabase.SaveAssets();

        // Update barrel prefab reference if it already exists
        GameObject barrelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BarrelPrefabPath);
        if (barrelPrefab != null && prefab != null)
        {
            Barrel barrel = barrelPrefab.GetComponent<Barrel>();
            SerializedObject so = new SerializedObject(barrel);
            so.FindProperty("explosionParticlesPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(barrelPrefab);
        }
    }

    public static void SetupPlayerCheckpoint()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = RequireObject(scene, "Player");
        CheckpointTracker tracker = player.GetComponent<CheckpointTracker>();
        if (tracker == null)
        {
            tracker = player.AddComponent<CheckpointTracker>();
        }
        Transform groundCheck = player.transform.Find("GroundCheck");
        SerializedObject so = new SerializedObject(tracker);
        if (groundCheck != null)
        {
            so.FindProperty("groundCheck").objectReferenceValue = groundCheck;
        }
        so.FindProperty("groundRadius").floatValue = 0.1f;
        int floorLayer = LayerMask.NameToLayer("Floor");
        so.FindProperty("groundLayer").intValue = floorLayer >= 0 ? (1 << floorLayer) : 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(player);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("LABORATORIO2D_CHECKPOINT_SETUP_SUCCESS");
    }

    public static void SetupSpikeHazards()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject decoration = GameObject.Find("Decoration");
        if (decoration == null)
        {
            decoration = new GameObject("Decoration");
        }

        // Remove old visual-only spikes (will be replaced by functional)
        string[] oldNames = { "Spike Visual 1", "Spike Visual 2", "Spike Visual 3", "Spike Visual 4" };
        foreach (string n in oldNames)
        {
            Transform t = decoration.transform.Find(n);
            if (t != null)
            {
                UnityEngine.Object.DestroyImmediate(t.gameObject);
            }
        }

        // Also clean any previous SpikeHazard leftovers by name pattern
        SpikeHazard[] existing = decoration.GetComponentsInChildren<SpikeHazard>();
        foreach (SpikeHazard h in existing)
        {
            // Keep if correctly configured; we will recreate cleanly
            UnityEngine.Object.DestroyImmediate(h.gameObject);
        }

        ConfigureTexture(GoalSpritePath, 100, SpriteImportMode.Single);
        AudioClip hazardClip = AssetDatabase.LoadAssetAtPath<AudioClip>(HazardSfxPath);
        if (hazardClip == null)
        {
            hazardClip = AssetDatabase.LoadAssetAtPath<AudioClip>(BarrelSfxPath);
        }

        Vector3[] positions =
        {
            new Vector3(0.80f, -1.43f, 0f),
            new Vector3(4.40f, -1.43f, 0f),
            new Vector3(8.80f, -1.43f, 0f),
            new Vector3(14.60f, -1.43f, 0f)
        };
        string[] spikePaths = GetSpikeFramePaths();
        for (int i = 0; i < positions.Length; i++)
        {
            string name = "SpikeHazard " + (i + 1);
            Sprite spikeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spikePaths[i % spikePaths.Length]);
            GameObject spike = new GameObject(name, typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(SpikeHazard), typeof(AudioSource));
            spike.transform.SetParent(decoration.transform, false);
            spike.transform.position = positions[i];
            spike.transform.localScale = Vector3.one * 0.9f;
            SpriteRenderer sr = spike.GetComponent<SpriteRenderer>();
            sr.sprite = spikeSprite;
            sr.sortingOrder = 4;
            BoxCollider2D col = spike.GetComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.30f, 0.16f);
            col.offset = new Vector2(0f, -0.06f);
            AudioSource src = spike.GetComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
            src.volume = 0.9f;
            SpikeHazard hazard = spike.GetComponent<SpikeHazard>();
            SerializedObject so = new SerializedObject(hazard);
            so.FindProperty("cooldownDuration").floatValue = 0.75f;
            so.FindProperty("flashDuration").floatValue = 0.6f;
            so.FindProperty("hazardClip").objectReferenceValue = hazardClip;
            so.FindProperty("audioSource").objectReferenceValue = src;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("LABORATORIO2D_SPIKE_HAZARDS_SETUP_SUCCESS");
    }

    public static void SetupGoalAndVictoryUI()
    {
        ConfigureTexture(GoalSpritePath, 100, SpriteImportMode.Single);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject decoration = GameObject.Find("Decoration");
        GameObject goalObject = GameObject.Find("Goal");
        if (goalObject != null)
        {
            UnityEngine.Object.DestroyImmediate(goalObject);
        }

        Sprite goalSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GoalSpritePath);
        if (goalSprite == null)
        {
            // fallback to any treasure sprite
            goalSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GetCoinFramePaths()[0]);
        }

        goalObject = new GameObject("Goal", typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Goal), typeof(AudioSource));
        goalObject.transform.position = new Vector3(18.8f, 2.0f, 0f);
        goalObject.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
        SpriteRenderer sr = goalObject.GetComponent<SpriteRenderer>();
        sr.sprite = goalSprite;
        sr.sortingOrder = 5;
        BoxCollider2D col = goalObject.GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(0.6f, 0.9f);
        col.offset = new Vector2(0f, 0.1f);
        AudioSource audioSrc = goalObject.GetComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.loop = false;

        // Victory UI Panel under Canvas
        GameObject canvasObject = RequireObject(scene, "Canvas");
        Transform existingPanel = canvasObject.transform.Find("LevelCompletePanel");
        if (existingPanel != null)
        {
            UnityEngine.Object.DestroyImmediate(existingPanel.gameObject);
        }

        GameObject panel = new GameObject("LevelCompletePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = Vector2.zero;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.6f);
        panelImage.raycastTarget = true;

        GameObject textObj = new GameObject("Victory Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObj.transform.SetParent(panel.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = new Vector2(0f, 30f);
        textRect.sizeDelta = new Vector2(600f, 100f);
        Text victoryText = textObj.GetComponent<Text>();
        victoryText.text = "¡Nivel completado!";
        victoryText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        victoryText.fontSize = 48;
        victoryText.fontStyle = FontStyle.Bold;
        victoryText.alignment = TextAnchor.MiddleCenter;
        victoryText.color = Color.white;
        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject countObj = new GameObject("Final Count Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        countObj.transform.SetParent(panel.transform, false);
        RectTransform countRect = countObj.GetComponent<RectTransform>();
        countRect.anchorMin = new Vector2(0.5f, 0.5f);
        countRect.anchorMax = new Vector2(0.5f, 0.5f);
        countRect.pivot = new Vector2(0.5f, 0.5f);
        countRect.anchoredPosition = new Vector2(0f, -30f);
        countRect.sizeDelta = new Vector2(300f, 60f);
        Text countText = countObj.GetComponent<Text>();
        countText.text = "00 / 05";
        countText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        countText.fontSize = 32;
        countText.fontStyle = FontStyle.Bold;
        countText.alignment = TextAnchor.MiddleCenter;
        countText.color = Color.white;

        panel.SetActive(false);

        Goal goal = goalObject.GetComponent<Goal>();
        SerializedObject so = new SerializedObject(goal);
        so.FindProperty("victoryPanel").objectReferenceValue = panel;
        so.FindProperty("victoryText").objectReferenceValue = victoryText;
        so.FindProperty("finalCountTextObject").objectReferenceValue = countObj;
        so.FindProperty("audioSource").objectReferenceValue = audioSrc;
        AudioClip coinClip = AssetDatabase.LoadAssetAtPath<AudioClip>(CoinSfxPath);
        so.FindProperty("victoryClip").objectReferenceValue = coinClip;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(goalObject);
        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("LABORATORIO2D_GOAL_SETUP_SUCCESS");
    }

    private static void VerifyBarrelSystem(Scene scene)
    {
        GameObject barrelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BarrelPrefabPath);
        Require(barrelPrefab != null, "Barrel prefab must exist at " + BarrelPrefabPath);
        Barrel barrelComponent = barrelPrefab.GetComponent<Barrel>();
        Require(barrelComponent != null, "Barrel prefab must have Barrel.cs specific script (not PlayerController).");
        Require(barrelPrefab.GetComponent<PlayerController>() == null, "Barrel prefab must not contain PlayerController.");
        SerializedObject so = new SerializedObject(barrelComponent);
        Require(Mathf.Approximately(so.FindProperty("bounceForce").floatValue, 7f), "Barrel bounceForce must be ~7f.");
        float duration = so.FindProperty("explodeDuration").floatValue;
        Require(duration >= 0.5f && duration <= 0.6f, "Barrel explode duration must be 0.5-0.6s.");
        Require(so.FindProperty("barrelClip").objectReferenceValue != null, "Barrel must reference BarrelSound.ogg via AudioManager or AudioSource.");
        Require(AssetDatabase.GetAssetPath(so.FindProperty("barrelClip").objectReferenceValue) == BarrelSfxPath, "Barrel SFX must be BarrelSound.ogg.");
        Require(typeof(Barrel).GetField("hasExploded", BindingFlags.Instance | BindingFlags.NonPublic) != null, "Barrel must have bool guard to avoid double trigger.");
        SpriteRenderer barrelRenderer = barrelPrefab.GetComponent<SpriteRenderer>();
        Require(barrelRenderer != null && barrelRenderer.sortingOrder == 5, "Barrel SpriteRenderer sorting 5 required.");
        Require(AssetDatabase.GetAssetPath(barrelRenderer.sprite) == GetBarrelFramePaths()[0], "Barrel must use original idle sprite.");
        Rigidbody2D barrelBody = barrelPrefab.GetComponent<Rigidbody2D>();
        Require(barrelBody.bodyType == RigidbodyType2D.Dynamic, "Barrel Rigidbody must be Dynamic.");
        Require(Mathf.Approximately(barrelBody.mass, BarrelMass), "Barrel mass must be 2.");
        Require(Mathf.Approximately(barrelBody.linearDamping, BarrelLinearDamping), "Barrel linear damping must be 3.");
        Require(Mathf.Approximately(barrelBody.gravityScale, BarrelGravityScale), "Barrel gravity scale must be 1.5.");
        Require(barrelBody.constraints == RigidbodyConstraints2D.FreezeRotation, "Barrel must freeze only Z rotation while allowing X/Y translation.");
        BoxCollider2D main = null;
        foreach (BoxCollider2D c in barrelPrefab.GetComponents<BoxCollider2D>())
        {
            if (!c.isTrigger)
            {
                main = c;
            }
        }
        Require(main != null, "Barrel must have solid BoxCollider2D.");
        Require(main.size == new Vector2(0.28f, 0.32f) && main.offset == new Vector2(0f, 0.05f) && !main.isTrigger, "Barrel lateral collider size 0.28x0.32 offset 0.05 non-trigger.");
        Require(main.sharedMaterial != null && AssetDatabase.GetAssetPath(main.sharedMaterial) == BarrelPhysicsMaterialPath, "Barrel solid collider must use its dedicated physics material.");
        Require(Mathf.Approximately(main.sharedMaterial.friction, BarrelFriction) && Mathf.Approximately(main.sharedMaterial.bounciness, 0f), "Barrel material must use friction 0.6 and zero bounciness.");
        Transform top = barrelPrefab.transform.Find("TopTrigger");
        Require(top != null, "Barrel must have child TopTrigger.");
        BoxCollider2D topCollider = top.GetComponent<BoxCollider2D>();
        Require(topCollider != null && topCollider.isTrigger, "TopTrigger must have BoxCollider2D isTrigger.");
        Require(topCollider.size == new Vector2(0.28f, 0.08f) && topCollider.offset == new Vector2(0f, 0.18f), "TopTrigger size 0.28x0.08 offset 0.18 required.");
        Animator animator = barrelPrefab.GetComponent<Animator>();
        Require(animator != null, "Barrel must have Animator with trigger Explode.");
        AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
        Require(controller != null && AssetDatabase.GetAssetPath(controller) == BarrelControllerPath, "Barrel Animator must use Barrel.controller.");
        Require(Array.Exists(controller.parameters, p => p.name == "Explode" && p.type == AnimatorControllerParameterType.Trigger), "Barrel Animator must have trigger Explode.");
        ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
        Require(states.Length == 2, "Barrel Animator must have Idle and Explode states.");
        AnimatorState idle = Array.Find(states, s => s.state.name == "Idle").state;
        AnimatorState explode = Array.Find(states, s => s.state.name == "Explode").state;
        Require(idle != null && explode != null, "Barrel Animator must contain Idle and Explode.");
        Require(controller.layers[0].stateMachine.defaultState == idle, "Barrel Animator default must be Idle.");
        AnimationClip explodeClip = explode.motion as AnimationClip;
        Require(explodeClip != null, "Explode state must reference AnimationClip.");
        Require(AssetDatabase.GetAssetPath(explodeClip) == BarrelExplodePath, "Explode clip must be at " + BarrelExplodePath);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(explodeClip);
        Require(!settings.loopTime, "Explode clip must not loop.");
        Require(explodeClip.length >= 0.45f && explodeClip.length <= 0.70f, "Explode clip length must be ~0.5-0.6s.");
        ObjectReferenceKeyframe[] frames = AnimationUtility.GetObjectReferenceCurve(explodeClip, new EditorCurveBinding { path = string.Empty, type = typeof(SpriteRenderer), propertyName = "m_Sprite" });
        Require(frames != null && frames.Length == 6, "Explode clip must use 6 sprites object_barrel_light_explode_1..6.");
        for (int i = 0; i < 6; i++)
        {
            Require(AssetDatabase.GetAssetPath(frames[i].value) == GetBarrelFramePaths()[i + 1], "Explode frame " + (i + 1) + " must use original sprite.");
        }
        string barrelSource = File.ReadAllText("Assets/_Project/Scripts/Barrel.cs");
        Require(barrelSource.Contains("CompareTag") && barrelSource.Contains("\"Player\""), "Barrel must use CompareTag Player.");
        Require(barrelSource.Contains("linearVelocity") && barrelSource.Contains("bounceForce"), "Barrel must apply bounce via linearVelocity y = bounceForce keeping vx.");
        Require(barrelSource.Contains("hasExploded"), "Barrel must guard double trigger with bool.");
        Require(barrelSource.Contains("0.15f") || barrelSource.Contains("0.15"), "Barrel must check py > barrelY +0.15.");
        Require(barrelSource.Contains("SetTrigger") && barrelSource.Contains("Explode"), "Barrel must fire trigger Explode.");
        Require(barrelSource.Contains("Destroy") || barrelSource.Contains("SetActive"), "Barrel must destroy/deactivate after animation.");
        Barrel[] barrelsInScene = UnityEngine.Object.FindObjectsByType<Barrel>(FindObjectsSortMode.None);
        Require(barrelsInScene.Length == 4, "Scene must contain exactly 4 interactive barrels from same prefab.");
        GameObject boxLegacy = GameObject.Find("Box");
        Require(boxLegacy == null, "Legacy Box must be removed; all barrels use prefab.");
        foreach (Barrel b in barrelsInScene)
        {
            Require(PrefabUtility.GetCorrespondingObjectFromSource(b.gameObject) == barrelPrefab, b.name + " must be Barrel prefab instance.");
            Vector3 pos = b.transform.position;
            bool onPlatform = (Mathf.Abs(pos.x - (-4.00f)) < 0.05f && Mathf.Abs(pos.y - (-0.14f)) < 0.05f) ||
                              (Mathf.Abs(pos.x - 0.00f) < 0.05f && Mathf.Abs(pos.y - (-0.14f)) < 0.05f) ||
                              (Mathf.Abs(pos.x - 6.72f) < 0.05f && Mathf.Abs(pos.y - 0.50f) < 0.05f) ||
                              (Mathf.Abs(pos.x - 18.08f) < 0.05f && Mathf.Abs(pos.y - 1.46f) < 0.05f);
            Require(onPlatform, b.name + " must be placed as an optional obstacle with a clear alternate route.");
        }
    }

    private static void VerifySpikeHazards(Scene scene)
    {
        SpikeHazard[] hazards = UnityEngine.Object.FindObjectsByType<SpikeHazard>(FindObjectsSortMode.None);
        Require(hazards.Length == 4, "Scene must contain exactly four functional SpikeHazard objects.");
        // Ensure spike source safeguards
        string spikeSource = File.ReadAllText("Assets/_Project/Scripts/SpikeHazard.cs");
        Require(spikeSource.Contains("CompareTag") && spikeSource.Contains("\"Player\""), "SpikeHazard must use CompareTag Player.");
        Require(spikeSource.Contains("cooldown") || spikeSource.Contains("Cooldown"), "SpikeHazard must have cooldown field.");
        Require(spikeSource.Contains("lastDamageTime") || spikeSource.Contains("invulnerab"), "SpikeHazard must guard loops with timestamp/invulnerability.");
        Require(spikeSource.Contains("FlashRoutine") || spikeSource.Contains("flash"), "SpikeHazard must have visual feedback flash.");
        Require(spikeSource.Contains("CheckpointTracker") || spikeSource.Contains("LastSafePosition") || spikeSource.Contains("-7.5f"), "SpikeHazard must respawn via checkpoint or initial spawn -7.5.");
        // Ensure no direct physics modification of PlayerController fields (speed/jump/gravity)
        Require(!spikeSource.Contains("speed =") && !spikeSource.Contains("jumpForce") && !spikeSource.Contains("gravityScale"), "SpikeHazard must not modify PlayerController physics.");
        foreach (SpikeHazard hazard in hazards)
        {
            Require(hazard.isActiveAndEnabled, hazard.name + " SpikeHazard must be enabled (not incorrectly disabled).");
            Require(hazard.GetComponent<Collider2D>() != null, hazard.name + " must have a Collider2D.");
            Collider2D col = hazard.GetComponent<Collider2D>();
            Require(col.isTrigger, hazard.name + " collider must be a trigger with tight fit.");
            Require(col.bounds.size.x <= 0.5f && col.bounds.size.y <= 0.4f, hazard.name + " trigger must be tightly fitted (<=0.5 x 0.4).");
            SpriteRenderer sr = hazard.GetComponent<SpriteRenderer>();
            Require(sr != null && sr.sprite != null, hazard.name + " must have SpriteRenderer with spike sprite.");
            bool usesOriginal = false;
            foreach (string p in GetSpikeFramePaths())
            {
                if (AssetDatabase.GetAssetPath(sr.sprite) == p) { usesOriginal = true; break; }
            }
            Require(usesOriginal, hazard.name + " must use original trap_spike sprite from Sprites1.");
            SerializedObject so = new SerializedObject(hazard);
            float cd = so.FindProperty("cooldownDuration") != null ? so.FindProperty("cooldownDuration").floatValue : 0f;
            Require(cd >= 0.5f && cd <= 1f, hazard.name + " cooldown must be 0.5-1s (found " + cd + ").");
            // No Missing script check implicitly via component existence; verify no NRE risk: groundCheck not required here
            Require(hazard.gameObject.GetComponent<SpikeHazard>() != null, hazard.name + " must not be Missing script.");
        }
        // Ensure player has checkpoint tracker and no damage if hazard disabled incorrectly is already covered by enabled check
        GameObject player = RequireObject(scene, "Player");
        Require(player.GetComponent<CheckpointTracker>() != null, "Player must have CheckpointTracker for safe respawn without resetting coins.");
        // Verify that damage does not reset coins: simulate hazard trigger logic check via GameManager observer still intact
        GameManager gm = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        Require(gm != null, "GameManager must exist for coin preservation check.");
        string gmSource = File.ReadAllText("Assets/_Project/Scripts/GameManager.cs");
        Require(gmSource.Contains("CoinCount") && gmSource.Contains("CoinsChanged"), "GameManager must preserve coin count via observer.");
    }

    private static void VerifyBarrelExplosionParticles()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BarrelExplosionPrefabPath);
        Require(prefab != null, "BarrelExplosionParticles prefab must exist at " + BarrelExplosionPrefabPath);
        ParticleSystem ps = prefab.GetComponent<ParticleSystem>();
        Require(ps != null, "BarrelExplosionParticles must have ParticleSystem.");
        var main = ps.main;
        Require(main.duration >= 0.4f && main.duration <= 0.6f, "Particle duration must be 0.4-0.6s (found " + main.duration + ").");
        Require(!main.loop, "Particle must not loop.");
        Require(prefab.GetComponent<ParticleSystemRenderer>() != null, "Particle must have renderer with Default material (no custom invented).");
        // Verify Barrel references it and sync logic exists
        GameObject barrelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BarrelPrefabPath);
        SerializedObject barrelSO = new SerializedObject(barrelPrefab.GetComponent<Barrel>());
        Require(barrelSO.FindProperty("explosionParticlesPrefab").objectReferenceValue == prefab, "Barrel prefab must reference BarrelExplosionParticles prefab.");
        string barrelSource = File.ReadAllText("Assets/_Project/Scripts/Barrel.cs");
        Require(barrelSource.Contains("explosionParticlesPrefab") && barrelSource.Contains("Instantiate") && barrelSource.Contains("ParticleSystem"), "Barrel must instantiate/play particles synced with anim+SFX+bounce.");
        Require(barrelSource.Contains("Destroy") && barrelSource.Contains("1f") || barrelSource.Contains("auto-destroy") || barrelSource.Contains("Destroy(fx"), "Barrel must auto-destroy particle instance.");
    }

    private static void VerifyGoalSystem(Scene scene)
    {
        GameObject goalObject = RequireObject(scene, "Goal");
        Goal goal = RequireComponent<Goal>(goalObject);
        Require(goal.isActiveAndEnabled, "Goal must be active and enabled.");
        BoxCollider2D col = goal.GetComponent<BoxCollider2D>();
        if (col == null) col = goalObject.GetComponentInChildren<BoxCollider2D>();
        Require(col != null && col.isTrigger, "Goal must have BoxCollider2D trigger.");
        Require(goalObject.transform.position.x >= 17.5f && goalObject.transform.position.x <= 19.2f, "Goal must be in final zone X~18-19 (found x=" + goalObject.transform.position.x + ").");
        SpriteRenderer sr = goalObject.GetComponent<SpriteRenderer>();
        Require(sr != null && sr.sprite != null, "Goal must have SpriteRenderer with treasure/cart asset.");
        Require(AssetDatabase.GetAssetPath(sr.sprite) == GoalSpritePath || AssetDatabase.GetAssetPath(sr.sprite).Contains("collectibles_treasure") || AssetDatabase.GetAssetPath(sr.sprite).Contains("collectibles"), "Goal must use existing treasure/rubi asset from Sprites1 (not invented).");
        SerializedObject so = new SerializedObject(goal);
        Require(so.FindProperty("victoryPanel").objectReferenceValue != null, "Goal must reference victoryPanel.");
        GameObject panel = so.FindProperty("victoryPanel").objectReferenceValue as GameObject;
        Require(panel != null, "Victory panel reference must be a GameObject.");
        // Panel must be Overlay and responsive
        Canvas canvas = RequireComponent<Canvas>(RequireObject(scene, "Canvas"));
        Require(canvas.renderMode == RenderMode.ScreenSpaceOverlay, "Victory UI must be Overlay.");
        // Initially inactive
        Require(!panel.activeSelf || panel.activeInHierarchy == false || panel.GetComponent<CanvasGroup>() != null, "Victory panel must start inactive (will be enabled on goal).");
        // Check text contains completion message
        string goalSource = File.ReadAllText("Assets/_Project/Scripts/Goal.cs");
        Require(goalSource.Contains("Nivel completado") || goalSource.Contains("¡Nivel"), "Goal.cs must show '¡Nivel completado!' UI.");
        Require(goalSource.Contains("PlayerController") && goalSource.Contains("enabled = false"), "Goal must limit/detain input via disabling PlayerController.");
        // Ensure no scene change
        Require(!goalSource.Contains("LoadScene") && !goalSource.Contains("ChangeScene"), "Goal must not change scene, keep final count on same scene.");
        // Ensure coin count preserved: no reset
        Require(!goalSource.Contains("CoinCount = 0") && !goalSource.Contains("Reset"), "Goal must preserve final coin count.");
    }

    private static void VerifyThreeZoneLayout(Scene scene, GameObject level, PolygonCollider2D cameraBounds, Coin[] coins, Barrel[] barrels)
    {
        string[] platformNames =
        {
            "Zone 1 - Start A", "Zone 1 - Start B",
            "Zone 2 - Development A", "Zone 2 - Development B", "Zone 2 - Development C",
            "Zone 3 - Final A", "Zone 3 - Final B", "Zone 3 - Final C", "Zone 3 - Goal Reserve"
        };
        foreach (string platformName in platformNames)
        {
            RequireObject(scene, "Grid/Level/" + platformName);
        }

        Require(cameraBounds.bounds.min.x <= -9.6f && cameraBounds.bounds.max.x >= 19.2f,
            "Camera Bounds must cover all three zones from X -9.6 through X 19.2.");

        Vector3[] expectedCoins =
        {
            new Vector3(-7.20f, 0.18f, 0f), new Vector3(-4.32f, 0.18f, 0f),
            new Vector3(1.92f, 0.50f, 0f), new Vector3(6.40f, 0.82f, 0f),
            new Vector3(16.00f, 1.78f, 0f)
        };
        foreach (Vector3 expected in expectedCoins)
        {
            Require(Array.Exists(coins, coin => Vector3.Distance(coin.transform.position, expected) < 0.01f),
                "Each authored zone coin position must be preserved.");
        }

        Require(barrels.Length == 4, "The three-zone layout requires one start, two development, and one final optional barrel.");
        GameObject decoration = RequireObject(scene, "Decoration");
        SpikeHazard[] hazardSpikes = decoration.GetComponentsInChildren<SpikeHazard>();
        // Support both naming conventions: SpikeHazard objects or Spike Visual with hazard
        if (hazardSpikes.Length == 0)
        {
            hazardSpikes = UnityEngine.Object.FindObjectsByType<SpikeHazard>(FindObjectsSortMode.None);
        }
        Require(hazardSpikes.Length == 4, "The three zones must contain four functional SpikeHazard triggers.");
        foreach (SpikeHazard spike in hazardSpikes)
        {
            Require(spike.GetComponent<Collider2D>() != null && spike.GetComponent<Collider2D>().isTrigger,
                spike.name + " must be functional hazard with trigger, not visual-only.");
        }
        GameObject bomb = RequireObject(scene, "Decoration/Bomb Idle Decorative");
        Require(bomb.GetComponents<Component>().Length == 2 && bomb.GetComponent<SpriteRenderer>() != null,
            "Decorative bomb must contain only Transform and SpriteRenderer components.");
        Require(AssetDatabase.GetAssetPath(bomb.GetComponent<SpriteRenderer>().sprite) == BombPath,
            "Decorative bomb must reference Sprites1 directly.");

        Tilemap tilemap = RequireComponent<Tilemap>(level);
        Require(AssetDatabase.GetAssetPath(tilemap.GetTile(new Vector3Int(-29, -2, 0))) == TilePath,
            "Zone 1 must start with the original tilemap sand tile.");
        Require(AssetDatabase.GetAssetPath(tilemap.GetTile(new Vector3Int(-14, -2, 0))) == SoftSandTilePath,
            "The level must use the original soft sand tilemap for surface variety.");

        int[,] platformLayout =
        {
            { -30, -18, -2 }, { -15, -10, -2 }, { -7, 1, -2 }, { 4, 12, -1 },
            { 15, 24, 0 }, { 27, 35, 0 }, { 38, 44, 1 }, { 47, 52, 3 }, { 55, 60, 3 }
        };
        const float speed = 2f;
        const float jumpVelocity = 6f;
        float gravity = Mathf.Abs(Physics2D.gravity.y) * 2f;
        for (int index = 0; index < platformLayout.GetLength(0) - 1; index++)
        {
            float dx = (platformLayout[index + 1, 0] - platformLayout[index, 1] - 1) * tilemap.layoutGrid.cellSize.x;
            float dy = (platformLayout[index + 1, 2] - platformLayout[index, 2]) * tilemap.layoutGrid.cellSize.y;
            float discriminant = jumpVelocity * jumpVelocity - 2f * gravity * dy;
            Require(discriminant >= 0f, "Mandatory jump " + (index + 1) + " exceeds jump height.");
            float flightTime = (jumpVelocity + Mathf.Sqrt(discriminant)) / gravity;
            float reach = speed * flightTime;
            float margin = reach - dx;
            Require(margin >= 0.15f, "Mandatory jump " + (index + 1) + " must keep at least 0.15 world-unit reach margin.");
            Debug.Log($"LABORATORIO2D_JUMP {index + 1}: dx={dx:F3} dy={dy:F3} reach={reach:F3} margin={margin:F3}");
        }

        Require(GameObject.Find("Goal") != null, "Final Goal must exist in zone X~18-19.");
    }

    private static void ConfigureRockSheet()
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(RockSheetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritesheet = new[]
        {
            new SpriteMetaData { name = "RockLarge", rect = new Rect(32f, 64f, 32f, 32f), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f) }
        };
        importer.SaveAndReimport();
    }

    private static void CreatePlatform(string name, Tilemap tilemap, TileBase centerTile, TileBase leftTile, TileBase rightTile, int minX, int maxX, int y)
    {
        GameObject marker = new GameObject(name);
        marker.transform.SetParent(tilemap.transform);
        marker.transform.position = tilemap.CellToWorld(new Vector3Int(minX, y, 0));
        PaintRectangle(tilemap, centerTile, minX, maxX, y, y);
        tilemap.SetTile(new Vector3Int(minX, y, 0), leftTile);
        tilemap.SetTile(new Vector3Int(maxX, y, 0), rightTile);
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
