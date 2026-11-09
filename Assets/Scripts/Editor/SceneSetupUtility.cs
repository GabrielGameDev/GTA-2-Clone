using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.InputSystem;
using GTA2Clone.Player;
using GTA2Clone.CameraSystem;
using GTA2Clone.Vehicles;
using GTA2Clone.UI;

namespace GTA2Clone.EditorTools
{
    public static class SceneSetupUtility
    {
        private readonly struct VehicleDefinition
        {
            public readonly string Key;
            public readonly string VehicleName;
            public readonly string FbxFileName;
            public readonly float Acceleration;
            public readonly float ReverseAcceleration;
            public readonly float MaxForwardSpeed;
            public readonly float MaxReverseSpeed;
            public readonly float BrakeStrength;
            public readonly float NaturalDrag;
            public readonly float TurnSpeed;
            public readonly float MinSpeedForFullTurn;
            public readonly float LateralFriction;

            public VehicleDefinition(
                string key,
                string vehicleName,
                string fbxFileName,
                float acceleration,
                float reverseAcceleration,
                float maxForwardSpeed,
                float maxReverseSpeed,
                float brakeStrength,
                float naturalDrag,
                float turnSpeed,
                float minSpeedForFullTurn,
                float lateralFriction)
            {
                Key = key;
                VehicleName = vehicleName;
                FbxFileName = fbxFileName;
                Acceleration = acceleration;
                ReverseAcceleration = reverseAcceleration;
                MaxForwardSpeed = maxForwardSpeed;
                MaxReverseSpeed = maxReverseSpeed;
                BrakeStrength = brakeStrength;
                NaturalDrag = naturalDrag;
                TurnSpeed = turnSpeed;
                MinSpeedForFullTurn = minSpeedForFullTurn;
                LateralFriction = lateralFriction;
            }
        }

        private static readonly VehicleDefinition[] AllVehicles = new VehicleDefinition[]
        {
            // 1. Ambulance
            new VehicleDefinition("Ambulance", "Ambulance", "ambulance.fbx",
                acceleration: 18f, reverseAcceleration: 10f, maxForwardSpeed: 19f, maxReverseSpeed: 7.5f,
                brakeStrength: 30f, naturalDrag: 4.8f, turnSpeed: 145f, minSpeedForFullTurn: 3.8f, lateralFriction: 8.5f),

            // 2. Delivery
            new VehicleDefinition("Delivery", "Delivery", "delivery.fbx",
                acceleration: 15f, reverseAcceleration: 9f, maxForwardSpeed: 16f, maxReverseSpeed: 6.5f,
                brakeStrength: 26f, naturalDrag: 5.2f, turnSpeed: 135f, minSpeedForFullTurn: 4.0f, lateralFriction: 8.2f),

            // 3. Delivery Flat
            new VehicleDefinition("DeliveryFlat", "Delivery Flat", "delivery-flat.fbx",
                acceleration: 16f, reverseAcceleration: 9.5f, maxForwardSpeed: 16.5f, maxReverseSpeed: 7f,
                brakeStrength: 27f, naturalDrag: 5.0f, turnSpeed: 140f, minSpeedForFullTurn: 3.8f, lateralFriction: 8.4f),

            // 4. Firetruck
            new VehicleDefinition("Firetruck", "Firetruck", "firetruck.fbx",
                acceleration: 14f, reverseAcceleration: 8f, maxForwardSpeed: 18.5f, maxReverseSpeed: 6f,
                brakeStrength: 25f, naturalDrag: 5.5f, turnSpeed: 115f, minSpeedForFullTurn: 4.5f, lateralFriction: 8.0f),

            // 5. Garbage Truck
            new VehicleDefinition("GarbageTruck", "Garbage Truck", "garbage-truck.fbx",
                acceleration: 11f, reverseAcceleration: 7f, maxForwardSpeed: 14f, maxReverseSpeed: 5.5f,
                brakeStrength: 24f, naturalDrag: 6.0f, turnSpeed: 110f, minSpeedForFullTurn: 4.5f, lateralFriction: 7.8f),

            // 6. Hatchback Sports
            new VehicleDefinition("HatchbackSports", "Hatchback Sports", "hatchback-sports.fbx",
                acceleration: 23f, reverseAcceleration: 13f, maxForwardSpeed: 20f, maxReverseSpeed: 8.5f,
                brakeStrength: 35f, naturalDrag: 4.5f, turnSpeed: 185f, minSpeedForFullTurn: 3.0f, lateralFriction: 10.0f),

            // 7. Police
            new VehicleDefinition("Police", "Police", "police.fbx",
                acceleration: 24f, reverseAcceleration: 13.5f, maxForwardSpeed: 22f, maxReverseSpeed: 9f,
                brakeStrength: 36f, naturalDrag: 4.5f, turnSpeed: 175f, minSpeedForFullTurn: 3.2f, lateralFriction: 10.5f),

            // 8. Race
            new VehicleDefinition("Race", "Race", "race.fbx",
                acceleration: 27f, reverseAcceleration: 14f, maxForwardSpeed: 24f, maxReverseSpeed: 9.5f,
                brakeStrength: 38f, naturalDrag: 4.2f, turnSpeed: 180f, minSpeedForFullTurn: 3.2f, lateralFriction: 11.0f),

            // 9. Race Future
            new VehicleDefinition("RaceFuture", "Race Future", "race-future.fbx",
                acceleration: 30f, reverseAcceleration: 15f, maxForwardSpeed: 26f, maxReverseSpeed: 10f,
                brakeStrength: 42f, naturalDrag: 4.0f, turnSpeed: 190f, minSpeedForFullTurn: 3.0f, lateralFriction: 12.0f),

            // 10. Sedan
            new VehicleDefinition("Sedan", "Sedan", "sedan.fbx",
                acceleration: 18f, reverseAcceleration: 11f, maxForwardSpeed: 17.5f, maxReverseSpeed: 7.5f,
                brakeStrength: 30f, naturalDrag: 5.0f, turnSpeed: 160f, minSpeedForFullTurn: 3.5f, lateralFriction: 9.0f),

            // 11. Sedan Sports
            new VehicleDefinition("SedanSports", "Sedan Sports", "sedan-sports.fbx",
                acceleration: 22f, reverseAcceleration: 12.5f, maxForwardSpeed: 21f, maxReverseSpeed: 8.5f,
                brakeStrength: 34f, naturalDrag: 4.6f, turnSpeed: 170f, minSpeedForFullTurn: 3.3f, lateralFriction: 9.2f),

            // 12. SUV
            new VehicleDefinition("Suv", "SUV", "suv.fbx",
                acceleration: 17f, reverseAcceleration: 10.5f, maxForwardSpeed: 17f, maxReverseSpeed: 7.5f,
                brakeStrength: 29f, naturalDrag: 5.2f, turnSpeed: 150f, minSpeedForFullTurn: 3.6f, lateralFriction: 9.0f),

            // 13. SUV Luxury
            new VehicleDefinition("SuvLuxury", "SUV Luxury", "suv-luxury.fbx",
                acceleration: 19.5f, reverseAcceleration: 11.5f, maxForwardSpeed: 19f, maxReverseSpeed: 8f,
                brakeStrength: 32f, naturalDrag: 4.8f, turnSpeed: 155f, minSpeedForFullTurn: 3.5f, lateralFriction: 9.4f),

            // 14. Taxi
            new VehicleDefinition("Taxi", "Taxi", "taxi.fbx",
                acceleration: 20f, reverseAcceleration: 12f, maxForwardSpeed: 18.5f, maxReverseSpeed: 8f,
                brakeStrength: 33f, naturalDrag: 4.8f, turnSpeed: 175f, minSpeedForFullTurn: 3.2f, lateralFriction: 9.5f),

            // 15. Tractor
            new VehicleDefinition("Tractor", "Tractor", "tractor.fbx",
                acceleration: 12f, reverseAcceleration: 8f, maxForwardSpeed: 12f, maxReverseSpeed: 6f,
                brakeStrength: 28f, naturalDrag: 6.5f, turnSpeed: 160f, minSpeedForFullTurn: 2.5f, lateralFriction: 11.0f),

            // 16. Tractor Police
            new VehicleDefinition("TractorPolice", "Tractor Police", "tractor-police.fbx",
                acceleration: 15f, reverseAcceleration: 9f, maxForwardSpeed: 14.5f, maxReverseSpeed: 6.5f,
                brakeStrength: 30f, naturalDrag: 6.0f, turnSpeed: 165f, minSpeedForFullTurn: 2.5f, lateralFriction: 11.5f),

            // 17. Tractor Shovel
            new VehicleDefinition("TractorShovel", "Tractor Shovel", "tractor-shovel.fbx",
                acceleration: 11f, reverseAcceleration: 7.5f, maxForwardSpeed: 11.5f, maxReverseSpeed: 5.5f,
                brakeStrength: 28f, naturalDrag: 6.8f, turnSpeed: 150f, minSpeedForFullTurn: 2.5f, lateralFriction: 11.0f),

            // 18. Truck
            new VehicleDefinition("Truck", "Truck", "truck.fbx",
                acceleration: 13f, reverseAcceleration: 7.5f, maxForwardSpeed: 15.5f, maxReverseSpeed: 6f,
                brakeStrength: 24f, naturalDrag: 5.8f, turnSpeed: 120f, minSpeedForFullTurn: 4.2f, lateralFriction: 8.0f),

            // 19. Truck Flat
            new VehicleDefinition("TruckFlat", "Truck Flat", "truck-flat.fbx",
                acceleration: 14f, reverseAcceleration: 8f, maxForwardSpeed: 16f, maxReverseSpeed: 6.5f,
                brakeStrength: 25f, naturalDrag: 5.5f, turnSpeed: 125f, minSpeedForFullTurn: 4.0f, lateralFriction: 8.2f),

            // 20. Van
            new VehicleDefinition("Van", "Van", "van.fbx",
                acceleration: 16.5f, reverseAcceleration: 10f, maxForwardSpeed: 17f, maxReverseSpeed: 7f,
                brakeStrength: 28f, naturalDrag: 5.0f, turnSpeed: 148f, minSpeedForFullTurn: 3.7f, lateralFriction: 8.6f)
        };

        [MenuItem("Tools/GTA2/Generate All Vehicle Data & Prefabs")]
        public static void GenerateAllVehicleAssetsAndPrefabs()
        {
            EnsureFoldersExist();

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            Material carMat = EnsureCarMaterial(urpLit);
            InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");

            int createdDataCount = 0;
            int createdPrefabCount = 0;

            foreach (VehicleDefinition def in AllVehicles)
            {
                VehicleData dataAsset = CreateOrUpdateVehicleDataAsset(def);
                if (dataAsset != null) createdDataCount++;

                GameObject prefab = CreateOrUpdateVehiclePrefab(def, dataAsset, carMat, inputAsset);
                if (prefab != null) createdPrefabCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SceneSetupUtility] Gerados/atualizados {createdDataCount} ScriptableObjects em 'Assets/Data/Vehicles' e {createdPrefabCount} Prefabs em 'Assets/Prefabs/Vehicles'!");
        }

        [MenuItem("Tools/GTA2/Setup Player Scene")]
        public static void SetupPlayerScene()
        {
            EnsureFoldersExist();

            string charModelPath = "Assets/Models/Characters/Model/characterMedium.fbx";
            string idleAnimPath = "Assets/Models/Characters/Animations/idle.fbx";
            string runAnimPath = "Assets/Models/Characters/Animations/run.fbx";

            ModelImporter charImporter = AssetImporter.GetAtPath(charModelPath) as ModelImporter;
            if (charImporter != null)
            {
                charImporter.animationType = ModelImporterAnimationType.Human;
                charImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                charImporter.SaveAndReimport();
            }

            Avatar charAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(charModelPath);

            ConfigureAnimationFBX(idleAnimPath, charAvatar, "Idle");
            ConfigureAnimationFBX(runAnimPath, charAvatar, "Run");

            string skinTexPath = "Assets/Models/Characters/Skins/criminalMaleA.png";
            Texture2D skinTex = AssetDatabase.LoadAssetAtPath<Texture2D>(skinTexPath);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            string playerMatPath = "Assets/Materials/PlayerSkin.mat";
            Material playerMat = AssetDatabase.LoadAssetAtPath<Material>(playerMatPath);
            if (playerMat == null)
            {
                playerMat = new Material(urpLit);
                AssetDatabase.CreateAsset(playerMat, playerMatPath);
            }
            playerMat.SetTexture("_BaseMap", skinTex);
            playerMat.SetColor("_BaseColor", Color.white);
            playerMat.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(playerMat);

            string groundMatPath = "Assets/Materials/Ground.mat";
            Material groundMat = AssetDatabase.LoadAssetAtPath<Material>(groundMatPath);
            if (groundMat == null)
            {
                groundMat = new Material(urpLit);
                AssetDatabase.CreateAsset(groundMat, groundMatPath);
            }
            groundMat.SetColor("_BaseColor", new Color(0.22f, 0.24f, 0.27f, 1f));
            groundMat.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(groundMat);

            string controllerPath = "Assets/Animations/PlayerAnimator.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            {
                AssetDatabase.DeleteAsset(controllerPath);
            }
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

            AnimationClip idleClip = LoadFirstAnimationClip(idleAnimPath);
            AnimationClip runClip = LoadFirstAnimationClip(runAnimPath);

            AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = rootStateMachine.AddState("Idle");
            idleState.motion = idleClip;
            rootStateMachine.defaultState = idleState;

            AnimatorState runState = rootStateMachine.AddState("Run");
            runState.motion = runClip;

            AnimatorStateTransition toRun = idleState.AddTransition(runState);
            toRun.hasExitTime = false;
            toRun.duration = 0.15f;
            toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            AnimatorStateTransition toIdle = runState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            EditorUtility.SetDirty(controller);

            LapaMapGenerator.BuildLapaMap(urpLit);

            Vector3 playerSpawnPos = new Vector3(16.5f, 0.08f, -26.5f);

            GameObject existingPlayer = GameObject.Find("Player");
            if (existingPlayer != null) Object.DestroyImmediate(existingPlayer);

            GameObject player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = playerSpawnPos;

            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(charModelPath);
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, player.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            SkinnedMeshRenderer smr = visual.GetComponentInChildren<SkinnedMeshRenderer>();
            float modelHeight = 1.8f;
            if (smr != null)
            {
                smr.sharedMaterial = playerMat;
                float rawHeight = smr.bounds.size.y;
                if (rawHeight > 2.5f)
                {
                    float scaleFactor = 1.8f / rawHeight;
                    visual.transform.localScale = Vector3.one * scaleFactor;
                }
                modelHeight = smr.bounds.size.y;
            }

            Animator anim = visual.GetComponent<Animator>();
            if (anim == null) anim = visual.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.avatar = charAvatar;
            anim.applyRootMotion = false;

            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = Mathf.Clamp(modelHeight, 1.0f, 2.2f);
            cc.radius = cc.height * 0.25f;
            cc.center = new Vector3(0f, cc.height * 0.5f, 0f);

            PlayerController pc = player.AddComponent<PlayerController>();
            InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");

            TopDownCameraFollow camFollow = null;
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.transform.position = new Vector3(playerSpawnPos.x, 22f, playerSpawnPos.z);
                mainCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                camFollow = mainCam.GetComponent<TopDownCameraFollow>();
                if (camFollow == null)
                {
                    camFollow = mainCam.gameObject.AddComponent<TopDownCameraFollow>();
                }
                SerializedObject soCam = new SerializedObject(camFollow);
                soCam.FindProperty("target").objectReferenceValue = player.transform;
                soCam.FindProperty("height").floatValue = 22f;
                soCam.FindProperty("smoothTime").floatValue = 0.12f;
                soCam.FindProperty("maxSpeedExtraHeight").floatValue = 14f;
                soCam.FindProperty("zoomOutSmoothTime").floatValue = 0.35f;
                soCam.FindProperty("zoomInSmoothTime").floatValue = 0.5f;
                soCam.ApplyModifiedPropertiesWithoutUndo();
            }

            SerializedObject soPc = new SerializedObject(pc);
            soPc.FindProperty("inputActions").objectReferenceValue = inputAsset;
            soPc.FindProperty("animator").objectReferenceValue = anim;
            soPc.FindProperty("visualRoot").objectReferenceValue = visual;
            soPc.FindProperty("cameraFollow").objectReferenceValue = camFollow;
            soPc.ApplyModifiedPropertiesWithoutUndo();

            SpawnTestVehiclesInternal(inputAsset, urpLit);
            SetupVehicleHudInternal(pc);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[SceneSetupUtility] Player, Veículos de Teste, HUD, Ground e Câmera configurados com sucesso!");
        }

        [MenuItem("Tools/GTA2/Setup Vehicle HUD")]
        public static void SetupVehicleHudMenu()
        {
            GameObject playerObj = GameObject.Find("Player");
            PlayerController pc = playerObj != null ? playerObj.GetComponent<PlayerController>() : null;

            SetupVehicleHudInternal(pc);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[SceneSetupUtility] HUD de nome do veículo configurado no canto inferior direito!");
        }

        private static void SetupVehicleHudInternal(PlayerController playerController)
        {
            GameObject existingHud = GameObject.Find("HUD_Canvas");
            if (existingHud != null)
            {
                Object.DestroyImmediate(existingHud);
            }

            GameObject canvasObj = new GameObject("HUD_Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            GameObject containerObj = new GameObject("VehicleNameContainer");
            containerObj.transform.SetParent(canvasObj.transform, false);

            RectTransform containerRect = containerObj.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(1f, 0f);
            containerRect.anchorMax = new Vector2(1f, 0f);
            containerRect.pivot = new Vector2(1f, 0f);
            containerRect.anchoredPosition = new Vector2(-48f, 42f);
            containerRect.sizeDelta = new Vector2(520f, 76f);

            CanvasGroup canvasGroup = containerObj.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            Image bgImage = containerObj.AddComponent<Image>();
            bgImage.color = new Color(0.05f, 0.06f, 0.08f, 0.72f);

            GameObject textObj = new GameObject("VehicleNameText");
            textObj.transform.SetParent(containerObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20f, 6f);
            textRect.offsetMax = new Vector2(-24f, -6f);

            Text uiText = textObj.AddComponent<Text>();
            Font builtInFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtInFont == null)
            {
                builtInFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            uiText.font = builtInFont;
            uiText.fontSize = 40;
            uiText.fontStyle = FontStyle.BoldAndItalic;
            uiText.alignment = TextAnchor.MiddleRight;
            uiText.color = new Color(1f, 0.84f, 0.18f, 1f);
            uiText.horizontalOverflow = HorizontalWrapMode.Overflow;
            uiText.verticalOverflow = VerticalWrapMode.Overflow;
            uiText.text = string.Empty;

            Outline outline = textObj.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);

            Shadow shadow = textObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(4f, -4f);

            VehicleHudUI hudUI = canvasObj.AddComponent<VehicleHudUI>();
            SerializedObject soHud = new SerializedObject(hudUI);
            soHud.FindProperty("player").objectReferenceValue = playerController;
            soHud.FindProperty("vehicleNameText").objectReferenceValue = uiText;
            soHud.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            soHud.FindProperty("autoHideAfterDelay").boolValue = true;
            soHud.FindProperty("displayDuration").floatValue = 3.5f;
            soHud.FindProperty("fadeInDuration").floatValue = 0.2f;
            soHud.FindProperty("fadeOutDuration").floatValue = 0.5f;
            soHud.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Tools/GTA2/Spawn Test Vehicles")]
        public static void SpawnTestVehiclesMenu()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");
            InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");

            SpawnTestVehiclesInternal(inputAsset, urpLit);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        private static void EnsureFoldersExist()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder("Assets/Animations"))
                AssetDatabase.CreateFolder("Assets", "Animations");
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
                AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder("Assets/Data/Vehicles"))
                AssetDatabase.CreateFolder("Assets/Data", "Vehicles");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Vehicles"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Vehicles");
        }

        private static Material EnsureCarMaterial(Shader urpLit)
        {
            string carTexPath = "Assets/Models/Car kit/Models/FBX format/Textures/colormap.png";
            Texture2D carTex = AssetDatabase.LoadAssetAtPath<Texture2D>(carTexPath);

            string carMatPath = "Assets/Materials/CarColormap.mat";
            Material carMat = AssetDatabase.LoadAssetAtPath<Material>(carMatPath);
            if (carMat == null)
            {
                carMat = new Material(urpLit);
                AssetDatabase.CreateAsset(carMat, carMatPath);
            }
            if (carTex != null)
            {
                carMat.SetTexture("_BaseMap", carTex);
            }
            carMat.SetColor("_BaseColor", Color.white);
            carMat.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(carMat);
            return carMat;
        }

        private static VehicleData CreateOrUpdateVehicleDataAsset(VehicleDefinition def)
        {
            string assetPath = $"Assets/Data/Vehicles/VehicleData_{def.Key}.asset";
            VehicleData dataAsset = AssetDatabase.LoadAssetAtPath<VehicleData>(assetPath);
            if (dataAsset == null)
            {
                dataAsset = ScriptableObject.CreateInstance<VehicleData>();
                dataAsset.Initialize(
                    def.VehicleName,
                    def.Acceleration,
                    def.ReverseAcceleration,
                    def.MaxForwardSpeed,
                    def.MaxReverseSpeed,
                    def.BrakeStrength,
                    def.NaturalDrag,
                    def.TurnSpeed,
                    def.MinSpeedForFullTurn,
                    def.LateralFriction
                );
                AssetDatabase.CreateAsset(dataAsset, assetPath);
            }
            else
            {
                dataAsset.Initialize(
                    def.VehicleName,
                    def.Acceleration,
                    def.ReverseAcceleration,
                    def.MaxForwardSpeed,
                    def.MaxReverseSpeed,
                    def.BrakeStrength,
                    def.NaturalDrag,
                    def.TurnSpeed,
                    def.MinSpeedForFullTurn,
                    def.LateralFriction
                );
            }

            EditorUtility.SetDirty(dataAsset);
            return dataAsset;
        }

        private static GameObject CreateOrUpdateVehiclePrefab(
            VehicleDefinition def,
            VehicleData dataAsset,
            Material carMat,
            InputActionAsset inputAsset)
        {
            string fbxPath = $"Assets/Models/Car kit/Models/FBX format/{def.FbxFileName}";
            string prefabPath = $"Assets/Prefabs/Vehicles/Vehicle_{def.Key}.prefab";

            GameObject tempInstance = BuildConfiguredVehicleObject(
                $"Vehicle_{def.Key}",
                fbxPath,
                Vector3.zero,
                Quaternion.identity,
                carMat,
                inputAsset,
                dataAsset
            );

            if (tempInstance == null)
                return null;

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempInstance, prefabPath);
            Object.DestroyImmediate(tempInstance);
            return savedPrefab;
        }

        private static void SpawnTestVehiclesInternal(InputActionAsset inputAsset, Shader urpLit)
        {
            EnsureFoldersExist();
            GenerateAllVehicleAssetsAndPrefabs();

            // Veículos posicionados nas faixas da Av. Mem de Sá (z = -30) e R. do Lavradio (x = 20)
            SpawnVehicleFromPrefab("Vehicle_SedanSports", new Vector3(12f, 0.08f, -28.2f), Quaternion.Euler(0f, 90f, 0f));
            SpawnVehicleFromPrefab("Vehicle_Taxi", new Vector3(21.8f, 0.08f, -21f), Quaternion.Euler(0f, 0f, 0f));
            SpawnVehicleFromPrefab("Vehicle_Police", new Vector3(28f, 0.08f, -31.8f), Quaternion.Euler(0f, -90f, 0f));
        }

        private static GameObject SpawnVehicleFromPrefab(string prefabName, Vector3 position, Quaternion rotation)
        {
            GameObject existing = GameObject.Find(prefabName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            string prefabPath = $"Assets/Prefabs/Vehicles/{prefabName}.prefab";
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                Debug.LogWarning($"[SceneSetupUtility] Prefab não encontrado em: {prefabPath}");
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
            instance.name = prefabName;
            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }

        private static GameObject BuildConfiguredVehicleObject(
            string objectName,
            string fbxPath,
            Vector3 position,
            Quaternion rotation,
            Material carMat,
            InputActionAsset inputAsset,
            VehicleData vehicleData)
        {
            GameObject carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (carPrefab == null)
            {
                Debug.LogWarning($"[SceneSetupUtility] Modelo de veículo não encontrado em: {fbxPath}");
                return null;
            }

            GameObject vehicleRoot = new GameObject(objectName);
            vehicleRoot.transform.SetPositionAndRotation(position, Quaternion.identity);

            GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(carPrefab, vehicleRoot.transform);
            modelInstance.name = "Model";
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one;

            Transform fl = null, fr = null, bl = null, br = null;
            foreach (Transform child in modelInstance.GetComponentsInChildren<Transform>(true))
            {
                string n = child.name.ToLowerInvariant();
                if (n.Contains("wheel-front-left")) fl = child;
                else if (n.Contains("wheel-front-right")) fr = child;
                else if (n.Contains("wheel-back-left")) bl = child;
                else if (n.Contains("wheel-back-right")) br = child;
            }

            // Garante que as rodas dianteiras fiquem em +Z local do veículo (frente)
            if (fl != null && bl != null)
            {
                Renderer flRen = fl.GetComponentInChildren<Renderer>();
                Renderer blRen = bl.GetComponentInChildren<Renderer>();
                if (flRen != null && blRen != null)
                {
                    float frontLocalZ = vehicleRoot.transform.InverseTransformPoint(flRen.bounds.center).z;
                    float backLocalZ = vehicleRoot.transform.InverseTransformPoint(blRen.bounds.center).z;
                    if (frontLocalZ < backLocalZ)
                    {
                        modelInstance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    }
                }
            }

            MeshRenderer[] renderers = modelInstance.GetComponentsInChildren<MeshRenderer>(true);
            Bounds combinedBounds = new Bounds(vehicleRoot.transform.position, Vector3.zero);
            bool hasBounds = false;

            foreach (MeshRenderer mr in renderers)
            {
                if (carMat != null)
                {
                    mr.sharedMaterial = carMat;
                }
                if (!hasBounds)
                {
                    combinedBounds = mr.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(mr.bounds);
                }
            }

            Rigidbody rb = vehicleRoot.AddComponent<Rigidbody>();
            rb.mass = 1200f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            BoxCollider boxCol = vehicleRoot.AddComponent<BoxCollider>();
            if (hasBounds)
            {
                Vector3 localCenter = vehicleRoot.transform.InverseTransformPoint(combinedBounds.center);
                Vector3 size = combinedBounds.size;
                // Eleva levemente a base do BoxCollider para não raspar no chão
                float bottomTrim = 0.08f;
                boxCol.center = new Vector3(localCenter.x, localCenter.y + bottomTrim * 0.5f, localCenter.z);
                boxCol.size = new Vector3(
                    Mathf.Max(1.2f, size.x * 0.92f),
                    Mathf.Max(0.8f, size.y - bottomTrim),
                    Mathf.Max(2.2f, size.z * 0.94f)
                );
            }
            else
            {
                boxCol.center = new Vector3(0f, 0.65f, 0f);
                boxCol.size = new Vector3(1.5f, 1.1f, 3.0f);
            }

            GameObject exitLeft = new GameObject("ExitPointLeft");
            exitLeft.transform.SetParent(vehicleRoot.transform, false);
            exitLeft.transform.localPosition = new Vector3(-(boxCol.size.x * 0.5f + 0.7f), 0.05f, 0f);

            GameObject exitRight = new GameObject("ExitPointRight");
            exitRight.transform.SetParent(vehicleRoot.transform, false);
            exitRight.transform.localPosition = new Vector3(boxCol.size.x * 0.5f + 0.7f, 0.05f, 0f);

            VehicleController vc = vehicleRoot.AddComponent<VehicleController>();
            vc.AutoFindWheels();

            SerializedObject soVc = new SerializedObject(vc);
            soVc.FindProperty("vehicleData").objectReferenceValue = vehicleData;
            soVc.FindProperty("inputActions").objectReferenceValue = inputAsset;
            soVc.FindProperty("exitPointLeft").objectReferenceValue = exitLeft.transform;
            soVc.FindProperty("exitPointRight").objectReferenceValue = exitRight.transform;
            if (fl != null) soVc.FindProperty("frontLeftWheel").objectReferenceValue = fl;
            if (fr != null) soVc.FindProperty("frontRightWheel").objectReferenceValue = fr;
            if (bl != null) soVc.FindProperty("backLeftWheel").objectReferenceValue = bl;
            if (br != null) soVc.FindProperty("backRightWheel").objectReferenceValue = br;
            soVc.ApplyModifiedPropertiesWithoutUndo();

            vc.ApplyVehicleData();
            EditorUtility.SetDirty(vc);

            vehicleRoot.transform.rotation = rotation;
            return vehicleRoot;
        }

        private static void ConfigureAnimationFBX(string path, Avatar sourceAvatar, string clipName)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            ModelImporterClipAnimation[] defaultClips = importer.defaultClipAnimations;
            if (defaultClips != null && defaultClips.Length > 0)
            {
                var validClips = new System.Collections.Generic.List<ModelImporterClipAnimation>();
                for (int i = 0; i < defaultClips.Length; i++)
                {
                    if (defaultClips[i].takeName.Contains("Targeting Pose"))
                        continue;

                    defaultClips[i].name = clipName;
                    defaultClips[i].loopTime = true;
                    defaultClips[i].loopPose = true;
                    defaultClips[i].lockRootRotation = true;
                    defaultClips[i].lockRootHeightY = true;
                    defaultClips[i].lockRootPositionXZ = true;
                    validClips.Add(defaultClips[i]);
                }

                if (validClips.Count > 0)
                {
                    importer.clipAnimations = validClips.ToArray();
                }
            }
            importer.SaveAndReimport();
        }

        private static AnimationClip LoadFirstAnimationClip(string path)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (Object asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__") && !clip.name.Contains("Targeting"))
                {
                    return clip;
                }
            }
            return null;
        }
    }
}
