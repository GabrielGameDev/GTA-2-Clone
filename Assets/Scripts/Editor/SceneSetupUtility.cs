using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using GTA2Clone.Player;
using GTA2Clone.CameraSystem;
using GTA2Clone.Vehicles;
using GTA2Clone.Traffic;
using GTA2Clone.Missions;
using GTA2Clone.Police;
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
            int createdContractCount = 0;

            foreach (VehicleDefinition def in AllVehicles)
            {
                VehicleData dataAsset = CreateOrUpdateVehicleDataAsset(def);
                if (dataAsset != null) createdDataCount++;

                GameObject prefab = CreateOrUpdateVehiclePrefab(def, dataAsset, carMat, inputAsset);
                if (prefab != null)
                {
                    createdPrefabCount++;
                    VehicleController prefabVc = prefab.GetComponent<VehicleController>();
                    VehicleContractData contractAsset = CreateOrUpdateVehicleContractAsset(def, dataAsset, prefabVc);
                    if (contractAsset != null) createdContractCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SceneSetupUtility] Gerados/atualizados {createdDataCount} VehicleData, {createdPrefabCount} Prefabs e {createdContractCount} Contratos em 'Assets/Data/Contracts'!");
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
            SetupTrafficSystemInternal(pc);
            SetupMissionSystemInternal(pc);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[SceneSetupUtility] Player, Veículos de Teste, Tráfego Urbano, Sistema de Missões, HUD, Ground e Câmera configurados com sucesso!");
        }

        [MenuItem("Tools/GTA2/Setup Mission System")]
        public static void SetupMissionSystemMenu()
        {
            EnsureFoldersExist();
            GameObject playerObj = GameObject.Find("Player");
            PlayerController pc = playerObj != null ? playerObj.GetComponent<PlayerController>() : null;

            SetupMissionSystemInternal(pc);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[SceneSetupUtility] Sistema de Missões Infinito (Arcade Loop) configurado com sucesso na cena!");
        }

        [MenuItem("Tools/GTA2/Setup Traffic System")]
        public static void SetupTrafficSystemMenu()
        {
            GameObject playerObj = GameObject.Find("Player");
            PlayerController pc = playerObj != null ? playerObj.GetComponent<PlayerController>() : null;

            SetupTrafficSystemInternal(pc);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[SceneSetupUtility] Sistema de Tráfego Urbano configurado com sucesso na cena!");
        }

        private static void SetupTrafficSystemInternal(PlayerController playerController)
        {
            GameObject existingTraffic = GameObject.Find("TrafficManager");
            if (existingTraffic != null)
            {
                Object.DestroyImmediate(existingTraffic);
            }

            TrafficRoadGraph roadGraph = null;
            GameObject lapaMapObj = GameObject.Find("LapaMap");
            if (lapaMapObj != null)
            {
                roadGraph = lapaMapObj.GetComponentInChildren<TrafficRoadGraph>(true);
            }

            if (roadGraph == null)
            {
                Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
                if (urpLit == null) urpLit = Shader.Find("Standard");
                LapaMapGenerator.BuildLapaMap(urpLit);

                lapaMapObj = GameObject.Find("LapaMap");
                if (lapaMapObj != null)
                {
                    roadGraph = lapaMapObj.GetComponentInChildren<TrafficRoadGraph>(true);
                }
            }

            var prefabsList = new List<VehicleController>();
            foreach (VehicleDefinition def in AllVehicles)
            {
                string prefabPath = $"Assets/Prefabs/Vehicles/Vehicle_{def.Key}.prefab";
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabAsset != null)
                {
                    VehicleController vc = prefabAsset.GetComponent<VehicleController>();
                    if (vc != null)
                    {
                        prefabsList.Add(vc);
                    }
                }
            }

            var sceneTrafficVehicles = new List<TrafficVehicleAI>();
            string[] initialSceneVehicleNames = { "Vehicle_SedanSports", "Vehicle_Taxi" };
            foreach (string vName in initialSceneVehicleNames)
            {
                GameObject vObj = GameObject.Find(vName);
                if (vObj != null)
                {
                    TrafficVehicleAI ai = vObj.GetComponent<TrafficVehicleAI>();
                    if (ai != null)
                    {
                        sceneTrafficVehicles.Add(ai);
                    }
                }
            }

            GameObject trafficObj = new GameObject("TrafficManager");
            TrafficManager manager = trafficObj.AddComponent<TrafficManager>();
            manager.Configure(playerController, roadGraph, prefabsList, sceneTrafficVehicles);
            EditorUtility.SetDirty(manager);
        }

        [MenuItem("Tools/GTA2/Setup Police System")]
        public static void SetupPoliceSystemMenu()
        {
            EnsureFoldersExist();
            GameObject playerObj = GameObject.Find("Player");
            PlayerController pc = playerObj != null ? playerObj.GetComponent<PlayerController>() : null;

            GameObject missionObj = GameObject.Find("MissionSystem");
            MissionManager mm = missionObj != null ? missionObj.GetComponent<MissionManager>() : null;

            PoliceManager pm = SetupPoliceSystemInternal(pc, mm);
            if (mm != null)
            {
                SetupMissionHudElements(mm, pm);
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[SceneSetupUtility] Sistema de Polícia (Wanted Level 0-6 Estrelas & Perseguição) configurado com sucesso na cena!");
        }

        private static PoliceManager SetupPoliceSystemInternal(PlayerController playerController, MissionManager missionManager)
        {
            GameObject existingPolice = GameObject.Find("PoliceSystem");
            if (existingPolice != null)
            {
                Object.DestroyImmediate(existingPolice);
            }

            TrafficRoadGraph roadGraph = null;
            GameObject lapaMapObj = GameObject.Find("LapaMap");
            if (lapaMapObj != null)
            {
                roadGraph = lapaMapObj.GetComponentInChildren<TrafficRoadGraph>(true);
            }

            GameObject policePrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Vehicles/Vehicle_Police.prefab");
            GameObject heavyPolicePrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Vehicles/Vehicle_TractorPolice.prefab");

            VehicleController stdPoliceVc = policePrefabAsset != null ? policePrefabAsset.GetComponent<VehicleController>() : null;
            VehicleController heavyPoliceVc = heavyPolicePrefabAsset != null ? heavyPolicePrefabAsset.GetComponent<VehicleController>() : null;

            var scenePoliceList = new List<VehicleController>();
            GameObject scenePoliceObj = GameObject.Find("Vehicle_Police");
            if (scenePoliceObj != null)
            {
                VehicleController vc = scenePoliceObj.GetComponent<VehicleController>();
                if (vc != null)
                {
                    if (scenePoliceObj.GetComponent<PoliceVehicleAI>() == null)
                    {
                        scenePoliceObj.AddComponent<PoliceVehicleAI>();
                    }
                    scenePoliceList.Add(vc);
                }
            }

            GameObject policeRoot = new GameObject("PoliceSystem");
            PoliceManager policeManager = policeRoot.AddComponent<PoliceManager>();
            policeManager.Configure(
                playerController,
                missionManager,
                roadGraph,
                stdPoliceVc,
                heavyPoliceVc,
                scenePoliceList
            );
            EditorUtility.SetDirty(policeManager);
            return policeManager;
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
            Debug.Log("[SceneSetupUtility] HUD de nome e barra de vida do veículo configurado no canto inferior direito!");
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
            containerRect.anchoredPosition = new Vector2(-42f, 36f);
            containerRect.sizeDelta = new Vector2(460f, 122f);

            CanvasGroup canvasGroup = containerObj.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            Image bgImage = containerObj.AddComponent<Image>();
            bgImage.color = new Color(0.05f, 0.06f, 0.08f, 0.82f);

            Font builtInFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtInFont == null)
            {
                builtInFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            // 1. Nome do Veículo (metade superior do painel)
            GameObject textObj = new GameObject("VehicleNameText");
            textObj.transform.SetParent(containerObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0.52f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(18f, 2f);
            textRect.offsetMax = new Vector2(-20f, -6f);

            Text uiText = textObj.AddComponent<Text>();
            uiText.font = builtInFont;
            uiText.fontSize = 34;
            uiText.fontStyle = FontStyle.BoldAndItalic;
            uiText.alignment = TextAnchor.MiddleRight;
            uiText.color = new Color(1f, 0.84f, 0.18f, 1f);
            uiText.horizontalOverflow = HorizontalWrapMode.Overflow;
            uiText.verticalOverflow = VerticalWrapMode.Overflow;
            uiText.text = string.Empty;
            AddTextShadowAndOutline(textObj);

            // 2. Barra de Vida / Integridade do Veículo (faixa central)
            GameObject healthBarBgObj = new GameObject("VehicleHealthBarBg");
            healthBarBgObj.transform.SetParent(containerObj.transform, false);
            RectTransform barBgRect = healthBarBgObj.AddComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0f, 0.28f);
            barBgRect.anchorMax = new Vector2(1f, 0.48f);
            barBgRect.offsetMin = new Vector2(18f, 0f);
            barBgRect.offsetMax = new Vector2(-20f, 0f);

            Image barBgImg = healthBarBgObj.AddComponent<Image>();
            barBgImg.color = new Color(0.16f, 0.18f, 0.22f, 0.95f);

            GameObject healthBarFillObj = new GameObject("VehicleHealthBarFill");
            healthBarFillObj.transform.SetParent(healthBarBgObj.transform, false);
            RectTransform barFillRect = healthBarFillObj.AddComponent<RectTransform>();
            barFillRect.anchorMin = new Vector2(0f, 0f);
            barFillRect.anchorMax = new Vector2(1f, 1f);
            barFillRect.offsetMin = new Vector2(2f, 2f);
            barFillRect.offsetMax = new Vector2(-2f, -2f);

            Image barFillImg = healthBarFillObj.AddComponent<Image>();
            barFillImg.color = new Color(0.20f, 0.94f, 0.42f, 1f);

            // 3. Texto de Status da Vida do Veículo (base do painel)
            GameObject statusObj = new GameObject("VehicleHealthStatusText");
            statusObj.transform.SetParent(containerObj.transform, false);
            RectTransform statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 0.04f);
            statusRect.anchorMax = new Vector2(1f, 0.26f);
            statusRect.offsetMin = new Vector2(18f, 0f);
            statusRect.offsetMax = new Vector2(-20f, 0f);

            Text statusText = statusObj.AddComponent<Text>();
            statusText.font = builtInFont;
            statusText.fontSize = 16;
            statusText.fontStyle = FontStyle.Bold;
            statusText.alignment = TextAnchor.MiddleRight;
            statusText.color = new Color(0.86f, 0.94f, 0.88f, 0.95f);
            statusText.text = "LATARIA / MOTOR: 100%";
            AddTextShadowAndOutline(statusObj);

            VehicleHudUI hudUI = canvasObj.AddComponent<VehicleHudUI>();
            hudUI.Configure(playerController, uiText, canvasGroup, barFillRect, barFillImg, statusText);
            EditorUtility.SetDirty(hudUI);
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
            if (!AssetDatabase.IsValidFolder("Assets/Data/Contracts"))
                AssetDatabase.CreateFolder("Assets/Data", "Contracts");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Vehicles"))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Vehicles");
        }

        private static VehicleContractData CreateOrUpdateVehicleContractAsset(
            VehicleDefinition def,
            VehicleData dataAsset,
            VehicleController prefabVc)
        {
            string assetPath = $"Assets/Data/Contracts/Contract_{def.Key}.asset";
            VehicleContractData contract = AssetDatabase.LoadAssetAtPath<VehicleContractData>(assetPath);

            // Recompensa e nível de procurado (heat) proporcionais à velocidade máxima e categoria do veículo
            int baseReward = Mathf.RoundToInt(def.MaxForwardSpeed * 125f / 100f) * 100;
            baseReward = Mathf.Clamp(baseReward, 1500, 4500);
            int speedBonus = Mathf.RoundToInt(baseReward * 0.4f / 50f) * 50;

            // Tempo bônus adicionado ao Banco de Tempo Global (veículos mais lentos dão alguns segundos a mais)
            float timeAwardSeconds = def.MaxForwardSpeed < 15.5f ? 30f : (def.MaxForwardSpeed > 21f ? 22f : 26f);

            int stealHeat = 1;
            if (def.Key.Contains("Police") || def.Key.Contains("Firetruck") || def.Key.Contains("Ambulance") || def.Key.Contains("Race"))
            {
                stealHeat = 2;
                baseReward += 800;
            }

            if (contract == null)
            {
                contract = ScriptableObject.CreateInstance<VehicleContractData>();
                contract.Initialize(
                    $"contract_{def.Key.ToLowerInvariant()}",
                    $"Encomenda: {def.VehicleName}",
                    dataAsset,
                    prefabVc,
                    baseReward,
                    targetTime: 45f,
                    speedBonus: speedBonus,
                    stealHeat: stealHeat,
                    deliveryHeat: 0,
                    canSpawnInTraffic: true,
                    timeAwardSeconds: timeAwardSeconds
                );
                AssetDatabase.CreateAsset(contract, assetPath);
            }
            else
            {
                contract.Initialize(
                    $"contract_{def.Key.ToLowerInvariant()}",
                    $"Encomenda: {def.VehicleName}",
                    dataAsset,
                    prefabVc,
                    baseReward,
                    targetTime: 45f,
                    speedBonus: speedBonus,
                    stealHeat: stealHeat,
                    deliveryHeat: 0,
                    canSpawnInTraffic: true,
                    timeAwardSeconds: timeAwardSeconds
                );
            }

            EditorUtility.SetDirty(contract);
            return contract;
        }

        private static void SetupMissionSystemInternal(PlayerController playerController)
        {
            EnsureFoldersExist();

            GameObject existingMissionSys = GameObject.Find("MissionSystem");
            if (existingMissionSys != null)
            {
                Object.DestroyImmediate(existingMissionSys);
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            // Garante que os VehicleData, Prefabs e Contratos existam
            var contractsList = new List<VehicleContractData>();
            foreach (VehicleDefinition def in AllVehicles)
            {
                string contractPath = $"Assets/Data/Contracts/Contract_{def.Key}.asset";
                VehicleContractData contract = AssetDatabase.LoadAssetAtPath<VehicleContractData>(contractPath);
                if (contract == null)
                {
                    GenerateAllVehicleAssetsAndPrefabs();
                    contract = AssetDatabase.LoadAssetAtPath<VehicleContractData>(contractPath);
                }
                if (contract != null)
                {
                    contractsList.Add(contract);
                }
            }

            TrafficManager trafficManager = null;
            GameObject trafficObj = GameObject.Find("TrafficManager");
            if (trafficObj != null)
            {
                trafficManager = trafficObj.GetComponent<TrafficManager>();
            }

            GameObject missionRoot = new GameObject("MissionSystem");

            // 1. Indicador Direcional 3D (Seta Verde -> Carro / Amarela -> Desmanche)
            GameObject indicatorObj = new GameObject("MissionTargetIndicator");
            indicatorObj.transform.SetParent(missionRoot.transform, false);
            MissionTargetIndicator indicator = indicatorObj.AddComponent<MissionTargetIndicator>();
            indicator.SetPlayer(playerController);
            indicator.EnsureArrowVisualExists();

            string arrowMatPath = "Assets/Materials/MissionArrow.mat";
            Material arrowMat = AssetDatabase.LoadAssetAtPath<Material>(arrowMatPath);
            if (arrowMat == null)
            {
                arrowMat = new Material(urpLit);
                AssetDatabase.CreateAsset(arrowMat, arrowMatPath);
            }
            arrowMat.SetColor("_BaseColor", new Color(0.12f, 0.95f, 0.32f, 1f));
            arrowMat.SetFloat("_Smoothness", 0.35f);
            arrowMat.EnableKeyword("_EMISSION");
            arrowMat.SetColor("_EmissionColor", new Color(0.12f, 0.95f, 0.32f, 1f) * 0.75f);
            EditorUtility.SetDirty(arrowMat);

            MeshRenderer arrowRenderer = indicatorObj.GetComponentInChildren<MeshRenderer>(true);
            if (arrowRenderer != null)
            {
                arrowRenderer.sharedMaterial = arrowMat;
            }

            string arrowMeshPath = "Assets/Data/MissionArrowMesh.asset";
            Mesh savedArrowMesh = AssetDatabase.LoadAssetAtPath<Mesh>(arrowMeshPath);
            if (savedArrowMesh == null)
            {
                savedArrowMesh = MissionTargetIndicator.BuildProceduralArrowMesh();
                AssetDatabase.CreateAsset(savedArrowMesh, arrowMeshPath);
            }
            MeshFilter arrowMf = indicatorObj.GetComponentInChildren<MeshFilter>(true);
            if (arrowMf != null)
            {
                arrowMf.sharedMesh = savedArrowMesh;
            }
            EditorUtility.SetDirty(indicator);

            // 2. Ponto de Spawn da 1ª Missão (sempre um veículo parado próximo ao jogador na Av. Mem de Sá)
            GameObject firstSpawnObj = new GameObject("FirstMissionSpawnPoint");
            firstSpawnObj.transform.SetParent(missionRoot.transform, false);
            firstSpawnObj.transform.SetPositionAndRotation(
                new Vector3(11.5f, 0.08f, -31.85f),
                Quaternion.Euler(0f, 90f, 0f)
            );

            // 3. Pontos de Spawn de Veículos Estacionados pelo Mapa da Lapa (vehicleSpawnPoints)
            GameObject spawnPointsGroup = new GameObject("VehicleSpawnPoints");
            spawnPointsGroup.transform.SetParent(missionRoot.transform, false);

            (Vector3 pos, float rotY, string label)[] mapSpawnDefs =
            {
                (new Vector3(-18.5f, 0.08f, -31.85f), 90f, "Spawn_MemDeSa_CentroOeste"),
                (new Vector3(48.5f, 0.08f, -28.15f), -90f, "Spawn_MemDeSa_Leste"),
                (new Vector3(21.85f, 0.08f, 12.0f), 0f, "Spawn_Lavradio_Centro"),
                (new Vector3(18.15f, 0.08f, 48.0f), 180f, "Spawn_Lavradio_Norte"),
                (new Vector3(-21.85f, 0.08f, 14.0f), 180f, "Spawn_GomesFreire_Centro"),
                (new Vector3(-58.15f, 0.08f, -12.0f), 0f, "Spawn_Invalidos_Sul"),
                (new Vector3(-45.0f, 0.08f, 28.15f), 90f, "Spawn_HenriqueValadares"),
                (new Vector3(45.0f, 0.08f, 31.85f), -90f, "Spawn_RepublicaDoChile"),
                (new Vector3(61.85f, 0.08f, -15.0f), 0f, "Spawn_RepDoParaguai"),
                (new Vector3(-35.0f, 0.08f, -61.85f), 90f, "Spawn_Riachuelo_Oeste"),
                (new Vector3(35.0f, 0.08f, -58.15f), -90f, "Spawn_Riachuelo_Leste"),
                (new Vector3(88.0f, 0.08f, -1.85f), 90f, "Spawn_EvaristoDaVeiga")
            };

            var spawnTransforms = new List<Transform>(mapSpawnDefs.Length);
            for (int i = 0; i < mapSpawnDefs.Length; i++)
            {
                GameObject sp = new GameObject(mapSpawnDefs[i].label);
                sp.transform.SetParent(spawnPointsGroup.transform, false);
                sp.transform.SetPositionAndRotation(
                    mapSpawnDefs[i].pos,
                    Quaternion.Euler(0f, mapSpawnDefs[i].rotY, 0f)
                );
                spawnTransforms.Add(sp.transform);
            }

            // 4. Zonas de Desmanche / Entrega (DeliveryZones)
            string deliveryMatPath = "Assets/Materials/DeliveryZoneHighlight.mat";
            Material deliveryMat = AssetDatabase.LoadAssetAtPath<Material>(deliveryMatPath);
            if (deliveryMat == null)
            {
                deliveryMat = new Material(urpLit);
                AssetDatabase.CreateAsset(deliveryMat, deliveryMatPath);
            }
            deliveryMat.SetColor("_BaseColor", new Color(1.0f, 0.85f, 0.08f, 1f));
            deliveryMat.SetFloat("_Smoothness", 0.25f);
            deliveryMat.EnableKeyword("_EMISSION");
            deliveryMat.SetColor("_EmissionColor", new Color(1.0f, 0.85f, 0.08f, 1f) * 0.55f);
            EditorUtility.SetDirty(deliveryMat);

            GameObject zonesGroup = new GameObject("DeliveryZones");
            zonesGroup.transform.SetParent(missionRoot.transform, false);

            (string name, Vector3 pos, float rotY, Vector3 localEjectOffset)[] zoneDefs =
            {
                ("Desmanche Arcos da Lapa", new Vector3(60f, 0.05f, 45f), 0f, new Vector3(4.2f, 0.05f, 0f)),
                ("Desmanche Armazém do Senado", new Vector3(-42f, 0.05f, 60f), 90f, new Vector3(4.2f, 0.05f, 0f)),
                ("Desmanche Largo do Passeio", new Vector3(100f, 0.05f, -45f), 0f, new Vector3(-4.2f, 0.05f, 0f)),
                ("Desmanche Mem de Sá Oeste", new Vector3(-82f, 0.05f, -30f), 90f, new Vector3(-4.2f, 0.05f, 0f))
            };

            var zonesList = new List<DeliveryZone>(zoneDefs.Length);
            for (int i = 0; i < zoneDefs.Length; i++)
            {
                DeliveryZone zone = BuildDeliveryZoneObject(
                    zonesGroup.transform,
                    $"DeliveryZone_{i + 1}",
                    zoneDefs[i].name,
                    zoneDefs[i].pos,
                    Quaternion.Euler(0f, zoneDefs[i].rotY, 0f),
                    zoneDefs[i].localEjectOffset,
                    deliveryMat
                );
                zonesList.Add(zone);
            }

            // 5. MissionManager
            MissionManager missionManager = missionRoot.AddComponent<MissionManager>();
            missionManager.Configure(
                playerController,
                trafficManager,
                indicator,
                contractsList,
                firstSpawnObj.transform,
                spawnTransforms,
                zonesList
            );
            EditorUtility.SetDirty(missionManager);

            // 6. Configura o Sistema de Polícia (Wanted Level 0-6 Estrelas & Perseguição)
            PoliceManager policeManager = SetupPoliceSystemInternal(playerController, missionManager);

            // 7. Integra o MissionHudUI e WantedStarsPanel ao HUD_Canvas
            SetupMissionHudElements(missionManager, policeManager);
        }

        private static DeliveryZone BuildDeliveryZoneObject(
            Transform parent,
            string objName,
            string displayZoneName,
            Vector3 position,
            Quaternion rotation,
            Vector3 localEjectOffset,
            Material highlightMaterial)
        {
            GameObject zoneObj = new GameObject(objName);
            zoneObj.transform.SetParent(parent, false);
            zoneObj.transform.SetPositionAndRotation(position, rotation);

            BoxCollider box = zoneObj.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 1.5f, 0f);
            box.size = new Vector3(5.6f, 3.0f, 8.6f);

            GameObject ejectObj = new GameObject("SafeEjectPoint");
            ejectObj.transform.SetParent(zoneObj.transform, false);
            ejectObj.transform.localPosition = localEjectOffset;
            Vector3 lookToZone = -localEjectOffset;
            lookToZone.y = 0f;
            ejectObj.transform.localRotation = lookToZone.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(lookToZone.normalized, Vector3.up)
                : Quaternion.identity;

            // Visual de baia amarela luminosa no chão (visível quando a DeliveryZone está ativa)
            GameObject visualRoot = new GameObject("VisualHighlight");
            visualRoot.transform.SetParent(zoneObj.transform, false);
            visualRoot.transform.localPosition = Vector3.zero;
            visualRoot.transform.localRotation = Quaternion.identity;

            const float width = 5.4f;
            const float length = 8.4f;
            const float borderThickness = 0.36f;
            const float barHeight = 0.08f;

            CreateHighlightBar(visualRoot.transform, "Border_North", new Vector3(0f, 0.04f, length * 0.5f), new Vector3(width, barHeight, borderThickness), highlightMaterial);
            CreateHighlightBar(visualRoot.transform, "Border_South", new Vector3(0f, 0.04f, -length * 0.5f), new Vector3(width, barHeight, borderThickness), highlightMaterial);
            CreateHighlightBar(visualRoot.transform, "Border_East", new Vector3(width * 0.5f, 0.04f, 0f), new Vector3(borderThickness, barHeight, length), highlightMaterial);
            CreateHighlightBar(visualRoot.transform, "Border_West", new Vector3(-width * 0.5f, 0.04f, 0f), new Vector3(borderThickness, barHeight, length), highlightMaterial);

            // Faixas diagonais/internas de doca
            CreateHighlightBar(visualRoot.transform, "CenterChevron_1", new Vector3(0f, 0.03f, -1.8f), new Vector3(2.8f, 0.04f, 0.28f), highlightMaterial);
            CreateHighlightBar(visualRoot.transform, "CenterChevron_2", new Vector3(0f, 0.03f, 0.0f), new Vector3(2.8f, 0.04f, 0.28f), highlightMaterial);
            CreateHighlightBar(visualRoot.transform, "CenterChevron_3", new Vector3(0f, 0.03f, 1.8f), new Vector3(2.8f, 0.04f, 0.28f), highlightMaterial);

            DeliveryZone zone = zoneObj.AddComponent<DeliveryZone>();
            zone.Configure(displayZoneName, ejectObj.transform, visualRoot);
            visualRoot.SetActive(false);
            EditorUtility.SetDirty(zone);
            return zone;
        }

        private static void CreateHighlightBar(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name;
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = localPos;
            bar.transform.localRotation = Quaternion.identity;
            bar.transform.localScale = localScale;

            Collider col = bar.GetComponent<Collider>();
            if (col != null)
            {
                Object.DestroyImmediate(col);
            }

            MeshRenderer mr = bar.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }

        private static void SetupMissionHudElements(MissionManager missionManager, PoliceManager policeManager = null)
        {
            if (policeManager == null)
            {
                GameObject policeObj = GameObject.Find("PoliceSystem");
                if (policeObj != null)
                {
                    policeManager = policeObj.GetComponent<PoliceManager>();
                }
            }

            GameObject canvasObj = GameObject.Find("HUD_Canvas");
            if (canvasObj == null)
            {
                GameObject playerObj = GameObject.Find("Player");
                PlayerController pc = playerObj != null ? playerObj.GetComponent<PlayerController>() : null;
                SetupVehicleHudInternal(pc);
                canvasObj = GameObject.Find("HUD_Canvas");
            }
            if (canvasObj == null) return;

            Transform existingMissionHud = canvasObj.transform.Find("MissionHudContainer");
            if (existingMissionHud != null)
            {
                Object.DestroyImmediate(existingMissionHud.gameObject);
            }

            Font builtInFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtInFont == null)
            {
                builtInFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            GameObject missionHudRoot = new GameObject("MissionHudContainer");
            missionHudRoot.transform.SetParent(canvasObj.transform, false);
            RectTransform rootRect = missionHudRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // 1. Painel de Objetivo no Topo Central
            GameObject objPanel = new GameObject("ObjectivePanel");
            objPanel.transform.SetParent(missionHudRoot.transform, false);
            RectTransform objRect = objPanel.AddComponent<RectTransform>();
            objRect.anchorMin = new Vector2(0.5f, 1f);
            objRect.anchorMax = new Vector2(0.5f, 1f);
            objRect.pivot = new Vector2(0.5f, 1f);
            objRect.anchoredPosition = new Vector2(0f, -26f);
            objRect.sizeDelta = new Vector2(720f, 64f);

            Image objBg = objPanel.AddComponent<Image>();
            objBg.color = new Color(0.05f, 0.06f, 0.08f, 0.80f);

            GameObject accentObj = new GameObject("ObjectiveAccentBar");
            accentObj.transform.SetParent(objPanel.transform, false);
            RectTransform accentRect = accentObj.AddComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(1f, 0f);
            accentRect.pivot = new Vector2(0.5f, 0f);
            accentRect.anchoredPosition = Vector2.zero;
            accentRect.sizeDelta = new Vector2(0f, 6f);
            Image accentImg = accentObj.AddComponent<Image>();
            accentImg.color = new Color(0.18f, 0.96f, 0.38f, 1f);

            GameObject objTextGo = new GameObject("ObjectiveText");
            objTextGo.transform.SetParent(objPanel.transform, false);
            RectTransform objTextRect = objTextGo.AddComponent<RectTransform>();
            objTextRect.anchorMin = Vector2.zero;
            objTextRect.anchorMax = Vector2.one;
            objTextRect.offsetMin = new Vector2(18f, 8f);
            objTextRect.offsetMax = new Vector2(-18f, -6f);

            Text objectiveText = objTextGo.AddComponent<Text>();
            objectiveText.font = builtInFont;
            objectiveText.fontSize = 30;
            objectiveText.fontStyle = FontStyle.Bold;
            objectiveText.alignment = TextAnchor.MiddleCenter;
            objectiveText.color = new Color(0.18f, 0.96f, 0.38f, 1f);
            objectiveText.text = "ROUBE O VEÍCULO ALVO";
            AddTextShadowAndOutline(objTextGo);

            // 2. Painel de Dinheiro e Entregas no Topo Direito
            GameObject scorePanel = new GameObject("ScorePanel");
            scorePanel.transform.SetParent(missionHudRoot.transform, false);
            RectTransform scoreRect = scorePanel.AddComponent<RectTransform>();
            scoreRect.anchorMin = new Vector2(1f, 1f);
            scoreRect.anchorMax = new Vector2(1f, 1f);
            scoreRect.pivot = new Vector2(1f, 1f);
            scoreRect.anchoredPosition = new Vector2(-42f, -26f);
            scoreRect.sizeDelta = new Vector2(340f, 92f);

            Image scoreBg = scorePanel.AddComponent<Image>();
            scoreBg.color = new Color(0.05f, 0.06f, 0.08f, 0.78f);

            GameObject moneyTextGo = new GameObject("MoneyText");
            moneyTextGo.transform.SetParent(scorePanel.transform, false);
            RectTransform moneyRect = moneyTextGo.AddComponent<RectTransform>();
            moneyRect.anchorMin = new Vector2(0f, 0.42f);
            moneyRect.anchorMax = new Vector2(1f, 1f);
            moneyRect.offsetMin = new Vector2(16f, 0f);
            moneyRect.offsetMax = new Vector2(-20f, -6f);

            Text moneyText = moneyTextGo.AddComponent<Text>();
            moneyText.font = builtInFont;
            moneyText.fontSize = 36;
            moneyText.fontStyle = FontStyle.Bold;
            moneyText.alignment = TextAnchor.MiddleRight;
            moneyText.color = new Color(0.25f, 0.98f, 0.42f, 1f);
            moneyText.text = "$000000";
            AddTextShadowAndOutline(moneyTextGo);

            GameObject deliveriesTextGo = new GameObject("DeliveriesCountText");
            deliveriesTextGo.transform.SetParent(scorePanel.transform, false);
            RectTransform delivRect = deliveriesTextGo.AddComponent<RectTransform>();
            delivRect.anchorMin = new Vector2(0f, 0f);
            delivRect.anchorMax = new Vector2(1f, 0.44f);
            delivRect.offsetMin = new Vector2(16f, 6f);
            delivRect.offsetMax = new Vector2(-20f, 0f);

            Text deliveriesText = deliveriesTextGo.AddComponent<Text>();
            deliveriesText.font = builtInFont;
            deliveriesText.fontSize = 22;
            deliveriesText.fontStyle = FontStyle.Bold;
            deliveriesText.alignment = TextAnchor.MiddleRight;
            deliveriesText.color = new Color(0.92f, 0.94f, 0.98f, 0.95f);
            deliveriesText.text = "ENTREGAS: 0";
            AddTextShadowAndOutline(deliveriesTextGo);

            // 2B. Painel de 6 Estrelas de Nível de Procurado (Wanted Level) logo abaixo do ScorePanel
            GameObject wantedPanel = new GameObject("WantedStarsPanel");
            wantedPanel.transform.SetParent(missionHudRoot.transform, false);
            RectTransform wantedRect = wantedPanel.AddComponent<RectTransform>();
            wantedRect.anchorMin = new Vector2(1f, 1f);
            wantedRect.anchorMax = new Vector2(1f, 1f);
            wantedRect.pivot = new Vector2(1f, 1f);
            wantedRect.anchoredPosition = new Vector2(-42f, -126f);
            wantedRect.sizeDelta = new Vector2(340f, 78f);

            Image wantedBg = wantedPanel.AddComponent<Image>();
            wantedBg.color = new Color(0.05f, 0.06f, 0.08f, 0.78f);

            GameObject wantedStarsGo = new GameObject("WantedStarsText");
            wantedStarsGo.transform.SetParent(wantedPanel.transform, false);
            RectTransform starsRect = wantedStarsGo.AddComponent<RectTransform>();
            starsRect.anchorMin = new Vector2(0f, 0.38f);
            starsRect.anchorMax = new Vector2(1f, 1f);
            starsRect.offsetMin = new Vector2(12f, 0f);
            starsRect.offsetMax = new Vector2(-16f, -4f);

            Text wantedStarsText = wantedStarsGo.AddComponent<Text>();
            wantedStarsText.font = builtInFont;
            wantedStarsText.fontSize = 32;
            wantedStarsText.fontStyle = FontStyle.Bold;
            wantedStarsText.alignment = TextAnchor.MiddleRight;
            wantedStarsText.supportRichText = true;
            wantedStarsText.color = Color.white;
            wantedStarsText.text = "<color=#48505E>☆ ☆ ☆ ☆ ☆ ☆</color>";
            AddTextShadowAndOutline(wantedStarsGo);

            GameObject wantedStatusGo = new GameObject("WantedStatusText");
            wantedStatusGo.transform.SetParent(wantedPanel.transform, false);
            RectTransform wStatusRect = wantedStatusGo.AddComponent<RectTransform>();
            wStatusRect.anchorMin = new Vector2(0f, 0f);
            wStatusRect.anchorMax = new Vector2(1f, 0.40f);
            wStatusRect.offsetMin = new Vector2(10f, 4f);
            wStatusRect.offsetMax = new Vector2(-14f, 0f);

            Text wantedStatusText = wantedStatusGo.AddComponent<Text>();
            wantedStatusText.font = builtInFont;
            wantedStatusText.fontSize = 14;
            wantedStatusText.fontStyle = FontStyle.Bold;
            wantedStatusText.alignment = TextAnchor.MiddleRight;
            wantedStatusText.horizontalOverflow = HorizontalWrapMode.Overflow;
            wantedStatusText.color = new Color(0.72f, 0.78f, 0.86f, 0.85f);
            wantedStatusText.text = "FICHA LIMPA (PATRULHA)";
            AddTextShadowAndOutline(wantedStatusGo);

            // 3. Painel do Banco de Tempo Global no Topo Esquerdo (Estilo Crazy Taxi)
            GameObject timerPanel = new GameObject("TimerPanel");
            timerPanel.transform.SetParent(missionHudRoot.transform, false);
            RectTransform timerRect = timerPanel.AddComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0f, 1f);
            timerRect.anchorMax = new Vector2(0f, 1f);
            timerRect.pivot = new Vector2(0f, 1f);
            timerRect.anchoredPosition = new Vector2(42f, -26f);
            timerRect.sizeDelta = new Vector2(280f, 92f);

            Image timerBg = timerPanel.AddComponent<Image>();
            timerBg.color = new Color(0.05f, 0.06f, 0.08f, 0.78f);

            GameObject timerLabelGo = new GameObject("TimerLabel");
            timerLabelGo.transform.SetParent(timerPanel.transform, false);
            RectTransform timerLabelRect = timerLabelGo.AddComponent<RectTransform>();
            timerLabelRect.anchorMin = new Vector2(0f, 0.62f);
            timerLabelRect.anchorMax = new Vector2(1f, 1f);
            timerLabelRect.offsetMin = new Vector2(16f, 0f);
            timerLabelRect.offsetMax = new Vector2(-16f, -6f);

            Text timerLabelText = timerLabelGo.AddComponent<Text>();
            timerLabelText.font = builtInFont;
            timerLabelText.fontSize = 18;
            timerLabelText.fontStyle = FontStyle.Bold;
            timerLabelText.alignment = TextAnchor.MiddleCenter;
            timerLabelText.color = new Color(0.85f, 0.88f, 0.94f, 0.9f);
            timerLabelText.text = "TEMPO RESTANTE";
            AddTextShadowAndOutline(timerLabelGo);

            GameObject timerValueGo = new GameObject("TimerValueText");
            timerValueGo.transform.SetParent(timerPanel.transform, false);
            RectTransform timerValRect = timerValueGo.AddComponent<RectTransform>();
            timerValRect.anchorMin = new Vector2(0f, 0f);
            timerValRect.anchorMax = new Vector2(1f, 0.65f);
            timerValRect.offsetMin = new Vector2(12f, 6f);
            timerValRect.offsetMax = new Vector2(-12f, 0f);

            Text timerValueText = timerValueGo.AddComponent<Text>();
            timerValueText.font = builtInFont;
            timerValueText.fontSize = 42;
            timerValueText.fontStyle = FontStyle.Bold;
            timerValueText.alignment = TextAnchor.MiddleCenter;
            timerValueText.color = new Color(1.0f, 0.92f, 0.28f, 1f);
            timerValueText.text = "01:00.0";
            AddTextShadowAndOutline(timerValueGo);

            // 4. Banner Popup de Entrega Concluída / Alerta Policial
            GameObject popupObj = new GameObject("RewardPopupBanner");
            popupObj.transform.SetParent(missionHudRoot.transform, false);
            RectTransform popupRect = popupObj.AddComponent<RectTransform>();
            popupRect.anchorMin = new Vector2(0.5f, 1f);
            popupRect.anchorMax = new Vector2(0.5f, 1f);
            popupRect.pivot = new Vector2(0.5f, 1f);
            popupRect.anchoredPosition = new Vector2(0f, -104f);
            popupRect.sizeDelta = new Vector2(760f, 54f);

            CanvasGroup popupGroup = popupObj.AddComponent<CanvasGroup>();
            popupGroup.alpha = 0f;
            popupGroup.interactable = false;
            popupGroup.blocksRaycasts = false;

            Image popupBg = popupObj.AddComponent<Image>();
            popupBg.color = new Color(0.08f, 0.22f, 0.10f, 0.88f);

            GameObject popupTextGo = new GameObject("RewardPopupText");
            popupTextGo.transform.SetParent(popupObj.transform, false);
            RectTransform popupTextRect = popupTextGo.AddComponent<RectTransform>();
            popupTextRect.anchorMin = Vector2.zero;
            popupTextRect.anchorMax = Vector2.one;
            popupTextRect.offsetMin = new Vector2(14f, 4f);
            popupTextRect.offsetMax = new Vector2(-14f, -4f);

            Text popupText = popupTextGo.AddComponent<Text>();
            popupText.font = builtInFont;
            popupText.fontSize = 23;
            popupText.fontStyle = FontStyle.BoldAndItalic;
            popupText.alignment = TextAnchor.MiddleCenter;
            popupText.color = new Color(1.0f, 0.90f, 0.22f, 1f);
            popupText.text = string.Empty;
            AddTextShadowAndOutline(popupTextGo);

            // 5. Tela de Game Over (Overlay Centralizado)
            GameObject gameOverOverlay = new GameObject("GameOverOverlay");
            gameOverOverlay.transform.SetParent(missionHudRoot.transform, false);
            RectTransform goOverlayRect = gameOverOverlay.AddComponent<RectTransform>();
            goOverlayRect.anchorMin = Vector2.zero;
            goOverlayRect.anchorMax = Vector2.one;
            goOverlayRect.offsetMin = Vector2.zero;
            goOverlayRect.offsetMax = Vector2.zero;

            CanvasGroup gameOverGroup = gameOverOverlay.AddComponent<CanvasGroup>();
            gameOverGroup.alpha = 0f;
            gameOverGroup.interactable = false;
            gameOverGroup.blocksRaycasts = false;

            Image goOverlayBg = gameOverOverlay.AddComponent<Image>();
            goOverlayBg.color = new Color(0.02f, 0.03f, 0.04f, 0.86f);

            GameObject goCard = new GameObject("GameOverCard");
            goCard.transform.SetParent(gameOverOverlay.transform, false);
            RectTransform goCardRect = goCard.AddComponent<RectTransform>();
            goCardRect.anchorMin = new Vector2(0.5f, 0.5f);
            goCardRect.anchorMax = new Vector2(0.5f, 0.5f);
            goCardRect.pivot = new Vector2(0.5f, 0.5f);
            goCardRect.anchoredPosition = Vector2.zero;
            goCardRect.sizeDelta = new Vector2(720f, 480f);

            Image goCardBg = goCard.AddComponent<Image>();
            goCardBg.color = new Color(0.08f, 0.09f, 0.12f, 0.96f);

            GameObject goTitleObj = new GameObject("GameOverTitle");
            goTitleObj.transform.SetParent(goCard.transform, false);
            RectTransform goTitleRect = goTitleObj.AddComponent<RectTransform>();
            goTitleRect.anchorMin = new Vector2(0f, 0.76f);
            goTitleRect.anchorMax = new Vector2(1f, 0.95f);
            goTitleRect.offsetMin = new Vector2(20f, 0f);
            goTitleRect.offsetMax = new Vector2(-20f, 0f);

            Text goTitleText = goTitleObj.AddComponent<Text>();
            goTitleText.font = builtInFont;
            goTitleText.fontSize = 42;
            goTitleText.fontStyle = FontStyle.Bold;
            goTitleText.alignment = TextAnchor.MiddleCenter;
            goTitleText.color = new Color(1.0f, 0.22f, 0.22f, 1f);
            goTitleText.text = "FIM DE TEMPO! GAME OVER";
            AddTextShadowAndOutline(goTitleObj);

            GameObject goStatsObj = new GameObject("GameOverStatsText");
            goStatsObj.transform.SetParent(goCard.transform, false);
            RectTransform goStatsRect = goStatsObj.AddComponent<RectTransform>();
            goStatsRect.anchorMin = new Vector2(0.06f, 0.25f);
            goStatsRect.anchorMax = new Vector2(0.94f, 0.74f);
            goStatsRect.offsetMin = Vector2.zero;
            goStatsRect.offsetMax = Vector2.zero;

            Text goStatsText = goStatsObj.AddComponent<Text>();
            goStatsText.font = builtInFont;
            goStatsText.fontSize = 24;
            goStatsText.fontStyle = FontStyle.Bold;
            goStatsText.alignment = TextAnchor.MiddleCenter;
            goStatsText.lineSpacing = 1.25f;
            goStatsText.color = new Color(0.95f, 0.96f, 0.98f, 1f);
            goStatsText.text = "DINHEIRO ACUMULADO: $0\nVEÍCULOS ENTREGUES: 0\nTEMPO DE CORRIDA: 00:00";
            AddTextShadowAndOutline(goStatsObj);

            GameObject restartBtnObj = new GameObject("RestartButton");
            restartBtnObj.transform.SetParent(goCard.transform, false);
            RectTransform restartBtnRect = restartBtnObj.AddComponent<RectTransform>();
            restartBtnRect.anchorMin = new Vector2(0.5f, 0.06f);
            restartBtnRect.anchorMax = new Vector2(0.5f, 0.06f);
            restartBtnRect.pivot = new Vector2(0.5f, 0f);
            restartBtnRect.anchoredPosition = Vector2.zero;
            restartBtnRect.sizeDelta = new Vector2(440f, 64f);

            Image restartBtnImg = restartBtnObj.AddComponent<Image>();
            restartBtnImg.color = new Color(0.16f, 0.78f, 0.32f, 1f);
            Button restartBtn = restartBtnObj.AddComponent<Button>();
            restartBtn.targetGraphic = restartBtnImg;

            GameObject restartBtnTextObj = new GameObject("RestartButtonText");
            restartBtnTextObj.transform.SetParent(restartBtnObj.transform, false);
            RectTransform restartBtnTextRect = restartBtnTextObj.AddComponent<RectTransform>();
            restartBtnTextRect.anchorMin = Vector2.zero;
            restartBtnTextRect.anchorMax = Vector2.one;
            restartBtnTextRect.offsetMin = Vector2.zero;
            restartBtnTextRect.offsetMax = Vector2.zero;

            Text restartBtnText = restartBtnTextObj.AddComponent<Text>();
            restartBtnText.font = builtInFont;
            restartBtnText.fontSize = 24;
            restartBtnText.fontStyle = FontStyle.Bold;
            restartBtnText.alignment = TextAnchor.MiddleCenter;
            restartBtnText.color = Color.white;
            restartBtnText.text = "JOGAR NOVAMENTE (ENTER / R)";
            AddTextShadowAndOutline(restartBtnTextObj);

            // Garante que exista um EventSystem na cena para cliques na UI
            if (GameObject.Find("EventSystem") == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<InputSystemUIInputModule>();
            }

            MissionHudUI missionHud = canvasObj.GetComponent<MissionHudUI>();
            if (missionHud == null)
            {
                missionHud = canvasObj.AddComponent<MissionHudUI>();
            }
            missionHud.Configure(
                missionManager,
                objectiveText,
                accentImg,
                timerValueText,
                moneyText,
                deliveriesText,
                popupGroup,
                popupText,
                gameOverGroup,
                goStatsText,
                restartBtn,
                policeManager,
                wantedBg,
                wantedStarsText,
                wantedStatusText,
                goTitleText,
                popupBg
            );
            EditorUtility.SetDirty(missionHud);
        }

        private static void AddTextShadowAndOutline(GameObject textGo)
        {
            Outline outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);

            Shadow shadow = textGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(3f, -3f);
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

        private static float CalculateVehicleMaxHealth(VehicleDefinition def)
        {
            if (def.Key.Contains("Firetruck") || def.Key.Contains("GarbageTruck") || def.Key.Contains("Truck") || def.Key.Contains("Tractor"))
                return 175f;
            if (def.Key.Contains("Police") || def.Key.Contains("Ambulance") || def.Key.Contains("Suv") || def.Key.Contains("Delivery") || def.Key.Contains("Van"))
                return 135f;
            if (def.Key.Contains("Race"))
                return 95f;
            return 115f;
        }

        private static VehicleData CreateOrUpdateVehicleDataAsset(VehicleDefinition def)
        {
            string assetPath = $"Assets/Data/Vehicles/VehicleData_{def.Key}.asset";
            VehicleData dataAsset = AssetDatabase.LoadAssetAtPath<VehicleData>(assetPath);
            float maxHealth = CalculateVehicleMaxHealth(def);

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
                    def.LateralFriction,
                    maxHealth
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
                    def.LateralFriction,
                    maxHealth
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

            // Veículos posicionados nas faixas corretas (mão direita) da Av. Mem de Sá (z = -30) e R. do Lavradio (x = 20)
            // A vaga (11.5f, 0.08f, -31.85f) fica reservada para o FirstMissionSpawnPoint do MissionManager
            SpawnVehicleFromPrefab("Vehicle_SedanSports", new Vector3(-14.5f, 0.08f, -31.85f), Quaternion.Euler(0f, 90f, 0f));
            SpawnVehicleFromPrefab("Vehicle_Taxi", new Vector3(21.85f, 0.08f, -21.5f), Quaternion.Euler(0f, 0f, 0f));
            SpawnVehicleFromPrefab("Vehicle_Police", new Vector3(-48.5f, 0.08f, -28.15f), Quaternion.Euler(0f, -90f, 0f));
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

            VehicleHealth vh = vehicleRoot.GetComponent<VehicleHealth>();
            if (vh == null)
            {
                vh = vehicleRoot.AddComponent<VehicleHealth>();
            }
            vh.SyncMaxHealthFromData(resetCurrentHealth: true);
            EditorUtility.SetDirty(vh);

            TrafficVehicleAI trafficAI = vehicleRoot.AddComponent<TrafficVehicleAI>();
            trafficAI.enabled = false;
            EditorUtility.SetDirty(trafficAI);

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
