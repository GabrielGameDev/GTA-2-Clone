using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.InputSystem;
using GTA2Clone.Player;
using GTA2Clone.CameraSystem;
using GTA2Clone.Vehicles;

namespace GTA2Clone.EditorTools
{
    public static class SceneSetupUtility
    {
        [MenuItem("Tools/GTA2/Setup Player Scene")]
        public static void SetupPlayerScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder("Assets/Animations"))
                AssetDatabase.CreateFolder("Assets", "Animations");

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
                soCam.ApplyModifiedPropertiesWithoutUndo();
            }

            SerializedObject soPc = new SerializedObject(pc);
            soPc.FindProperty("inputActions").objectReferenceValue = inputAsset;
            soPc.FindProperty("animator").objectReferenceValue = anim;
            soPc.FindProperty("visualRoot").objectReferenceValue = visual;
            soPc.FindProperty("cameraFollow").objectReferenceValue = camFollow;
            soPc.ApplyModifiedPropertiesWithoutUndo();

            SpawnTestVehiclesInternal(inputAsset, urpLit);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[SceneSetupUtility] Player, Veículos de Teste, Ground e Câmera configurados com sucesso!");
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

        private static void SpawnTestVehiclesInternal(InputActionAsset inputAsset, Shader urpLit)
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

            // Veículos posicionados nas faixas da Av. Mem de Sá (z = -30) e R. do Lavradio (x = 20)
            CreateVehicleInstance(
                "Vehicle_SedanSports",
                "Assets/Models/Car kit/Models/FBX format/sedan-sports.fbx",
                new Vector3(12f, 0.08f, -28.2f),
                Quaternion.Euler(0f, 90f, 0f),
                carMat,
                inputAsset
            );

            CreateVehicleInstance(
                "Vehicle_Taxi",
                "Assets/Models/Car kit/Models/FBX format/taxi.fbx",
                new Vector3(21.8f, 0.08f, -21f),
                Quaternion.Euler(0f, 0f, 0f),
                carMat,
                inputAsset
            );

            CreateVehicleInstance(
                "Vehicle_Police",
                "Assets/Models/Car kit/Models/FBX format/police.fbx",
                new Vector3(28f, 0.08f, -31.8f),
                Quaternion.Euler(0f, -90f, 0f),
                carMat,
                inputAsset
            );
        }

        private static GameObject CreateVehicleInstance(
            string objectName,
            string fbxPath,
            Vector3 position,
            Quaternion rotation,
            Material carMat,
            InputActionAsset inputAsset)
        {
            GameObject existing = GameObject.Find(objectName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

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
            soVc.FindProperty("inputActions").objectReferenceValue = inputAsset;
            soVc.FindProperty("exitPointLeft").objectReferenceValue = exitLeft.transform;
            soVc.FindProperty("exitPointRight").objectReferenceValue = exitRight.transform;
            if (fl != null) soVc.FindProperty("frontLeftWheel").objectReferenceValue = fl;
            if (fr != null) soVc.FindProperty("frontRightWheel").objectReferenceValue = fr;
            if (bl != null) soVc.FindProperty("backLeftWheel").objectReferenceValue = bl;
            if (br != null) soVc.FindProperty("backRightWheel").objectReferenceValue = br;
            soVc.ApplyModifiedPropertiesWithoutUndo();

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
