using System;
using System.Collections;
using UnityEngine;

namespace GTA2Clone.Vehicles
{
    /// <summary>
    /// Gerencia a integridade estrutural (vida), dano por colisão física, efeitos visuais de avaria (fumaça/fogo)
    /// e explosão do veículo quando a vida chega a zero.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class VehicleHealth : MonoBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;

        [Header("Collision Damage")]
        [SerializeField] private float minImpactSpeed = 4.2f;
        [SerializeField] private float collisionDamageMultiplier = 1.65f;
        [SerializeField] private float minDamagePerHit = 3f;
        [SerializeField] private float maxSingleHitFraction = 0.35f;
        [SerializeField] private float damageCooldown = 0.42f;

        private VehicleController vehicleController;
        private Rigidbody rb;
        private Renderer[] bodyRenderers;
        private MaterialPropertyBlock propBlock;

        private Vector3 lastFixedVelocity;
        private float lastDamageTimestamp = -10f;
        private bool isDestroyed;

        private GameObject damageFxRoot;
        private Transform smokePuffTransform;
        private Transform firePuffTransform;
        private Light fireLight;
        private Coroutine hitFlashCoroutine;

        private static Material sharedSmokeMaterial;
        private static Material sharedFireMaterial;
        private static Material sharedExplosionCoreMaterial;

        public VehicleController Vehicle => vehicleController;
        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float NormalizedHealth => maxHealth > 0.01f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        public bool IsDestroyed => isDestroyed;

        /// <summary>
        /// Disparado sempre que a vida do veículo muda (currentHealth, maxHealth).
        /// </summary>
        public event Action<float, float> OnHealthChanged;

        /// <summary>
        /// Disparado quando a vida chega a zero e o veículo explode.
        /// </summary>
        public event Action<VehicleController> OnVehicleExploded;

        /// <summary>
        /// Evento global disparado quando qualquer veículo explode, informando se o jogador estava dentro dele.
        /// </summary>
        public static event Action<VehicleController, bool> OnAnyVehicleExploded;

        private void Awake()
        {
            vehicleController = GetComponent<VehicleController>();
            rb = GetComponent<Rigidbody>();
            bodyRenderers = GetComponentsInChildren<Renderer>(true);
            propBlock = new MaterialPropertyBlock();

            SyncMaxHealthFromData(resetCurrentHealth: true);
        }

        private void OnEnable()
        {
            if (vehicleController != null)
            {
                vehicleController.OnDriverEntered -= HandleDriverEntered;
                vehicleController.OnDriverEntered += HandleDriverEntered;
            }
        }

        private void OnDisable()
        {
            if (vehicleController != null)
            {
                vehicleController.OnDriverEntered -= HandleDriverEntered;
            }
        }

        private void FixedUpdate()
        {
            if (rb != null && !isDestroyed)
            {
                lastFixedVelocity = rb.linearVelocity;
            }
        }

        private void Update()
        {
            if (!isDestroyed && currentHealth < maxHealth * 0.55f)
            {
                UpdateDamageVisualsAnimation();
            }
        }

        /// <summary>
        /// Sincroniza a vida máxima com o ScriptableObject VehicleData do VehicleController.
        /// </summary>
        public void SyncMaxHealthFromData(bool resetCurrentHealth = true)
        {
            if (vehicleController == null)
            {
                vehicleController = GetComponent<VehicleController>();
            }

            if (vehicleController != null && vehicleController.Data != null)
            {
                maxHealth = Mathf.Max(20f, vehicleController.Data.MaxHealth);
            }
            else if (maxHealth < 20f)
            {
                maxHealth = 100f;
            }

            if (resetCurrentHealth)
            {
                currentHealth = maxHealth;
                isDestroyed = false;
            }
            else
            {
                currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
            }
        }

        /// <summary>
        /// Restaura completamente a vida do veículo.
        /// </summary>
        public void RepairFull()
        {
            if (isDestroyed) return;
            currentHealth = maxHealth;
            if (damageFxRoot != null)
            {
                damageFxRoot.SetActive(false);
            }
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>
        /// Aplica uma quantidade de dano diretamente ao veículo. Se a vida zerar, causa explosão imediata.
        /// </summary>
        public void ApplyDamage(float amount)
        {
            if (isDestroyed || amount <= 0.01f)
                return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            UpdateDamageVisualState();

            if (currentHealth <= 0f)
            {
                Explode();
            }
            else if (gameObject.activeInHierarchy)
            {
                if (hitFlashCoroutine != null)
                {
                    StopCoroutine(hitFlashCoroutine);
                }
                hitFlashCoroutine = StartCoroutine(FlashDamageRoutine());
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (isDestroyed || vehicleController == null)
                return;

            if (Time.time - lastDamageTimestamp < damageCooldown)
                return;

            Collider otherCol = collision.collider;
            if (otherCol == null || IsIgnoredSurface(otherCol.gameObject.name))
                return;

            VehicleController otherVehicle = otherCol.GetComponentInParent<VehicleController>();

            // Só computa dano de colisão se este veículo estiver com o jogador, ou se colidiu com o veículo do jogador
            bool playerInvolved = vehicleController.IsOccupied || (otherVehicle != null && otherVehicle.IsOccupied);
            if (!playerInvolved)
                return;

            Vector3 relVel = collision.relativeVelocity;
            relVel.y = 0f;
            float impactSpeed = relVel.magnitude;

            // Garante leitura precisa da velocidade pré-impacto mesmo contra paredes estáticas
            if (collision.contactCount > 0)
            {
                Vector3 contactNormal = collision.GetContact(0).normal;
                contactNormal.y = 0f;
                if (contactNormal.sqrMagnitude > 0.001f)
                {
                    contactNormal.Normalize();
                    Vector3 otherVel = otherVehicle != null && otherVehicle.GetComponent<Rigidbody>() != null
                        ? otherVehicle.GetComponent<Rigidbody>().linearVelocity
                        : Vector3.zero;
                    otherVel.y = 0f;

                    float normalApproachSpeed = Mathf.Abs(Vector3.Dot(lastFixedVelocity - otherVel, contactNormal));
                    impactSpeed = Mathf.Max(impactSpeed, normalApproachSpeed);
                }
            }

            if (impactSpeed < minImpactSpeed)
                return;

            lastDamageTimestamp = Time.time;

            float rawDamage = (impactSpeed - minImpactSpeed) * collisionDamageMultiplier;

            // Se colidiu contra uma viatura policial enquanto o jogador dirige, aumenta levemente o impacto
            if (vehicleController.IsOccupied && otherVehicle != null && IsPoliceVehicle(otherVehicle))
            {
                rawDamage *= 1.2f;
            }

            float maxSingleHit = maxHealth * maxSingleHitFraction;
            float finalDamage = Mathf.Clamp(rawDamage, minDamagePerHit, maxSingleHit);

            ApplyDamage(finalDamage);
        }

        private void HandleDriverEntered(Player.PlayerController driver)
        {
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private void Explode()
        {
            if (isDestroyed) return;
            isDestroyed = true;
            currentHealth = 0f;

            bool wasOccupiedByPlayer = vehicleController != null && vehicleController.IsOccupied;

            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
                hitFlashCoroutine = null;
            }

            ApplyCharredVisuals();
            EnsureDamageFxCreated();
            if (damageFxRoot != null)
            {
                damageFxRoot.SetActive(true);
                if (smokePuffTransform != null) smokePuffTransform.gameObject.SetActive(true);
                if (firePuffTransform != null) firePuffTransform.gameObject.SetActive(true);
            }

            SpawnExplosionEffect(transform.position + Vector3.up * 0.8f);

            if (vehicleController != null)
            {
                vehicleController.NotifyDestroyedByExplosion();
            }

            if (rb != null)
            {
                rb.constraints = RigidbodyConstraints.None;
                rb.linearDamping = 1.4f;
                rb.angularDamping = 1.8f;
                rb.AddForce(Vector3.up * 6.8f + UnityEngine.Random.insideUnitSphere * 2.0f, ForceMode.VelocityChange);
                rb.AddTorque(new Vector3(
                    UnityEngine.Random.Range(-3.5f, 3.5f),
                    UnityEngine.Random.Range(-4.5f, 4.5f),
                    UnityEngine.Random.Range(-3.5f, 3.5f)),
                    ForceMode.VelocityChange);
            }

            OnHealthChanged?.Invoke(0f, maxHealth);
            OnVehicleExploded?.Invoke(vehicleController);
            OnAnyVehicleExploded?.Invoke(vehicleController, wasOccupiedByPlayer);
        }

        private void UpdateDamageVisualState()
        {
            float ratio = NormalizedHealth;
            if (ratio >= 0.55f)
            {
                if (damageFxRoot != null)
                {
                    damageFxRoot.SetActive(false);
                }
                return;
            }

            EnsureDamageFxCreated();
            if (damageFxRoot == null) return;

            damageFxRoot.SetActive(true);
            bool showFire = ratio <= 0.28f;

            if (smokePuffTransform != null)
            {
                smokePuffTransform.gameObject.SetActive(true);
            }
            if (firePuffTransform != null)
            {
                firePuffTransform.gameObject.SetActive(showFire);
            }
            if (fireLight != null)
            {
                fireLight.enabled = showFire;
            }
        }

        private void UpdateDamageVisualsAnimation()
        {
            if (damageFxRoot == null || !damageFxRoot.activeSelf)
                return;

            float t = Time.time;

            if (smokePuffTransform != null && smokePuffTransform.gameObject.activeSelf)
            {
                float cycle = Mathf.Repeat(t * 1.8f, 1f);
                smokePuffTransform.localPosition = new Vector3(
                    Mathf.Sin(t * 4.2f) * 0.12f,
                    0.35f + cycle * 1.35f,
                    0f
                );
                float scale = Mathf.Lerp(0.32f, 0.95f, cycle);
                smokePuffTransform.localScale = new Vector3(scale, scale * 0.85f, scale);
            }

            if (firePuffTransform != null && firePuffTransform.gameObject.activeSelf)
            {
                float pulse = 0.42f + Mathf.Abs(Mathf.Sin(t * 16f)) * 0.28f;
                firePuffTransform.localPosition = new Vector3(
                    Mathf.Cos(t * 11f) * 0.08f,
                    0.22f + Mathf.Abs(Mathf.Sin(t * 9f)) * 0.25f,
                    0f
                );
                firePuffTransform.localScale = new Vector3(pulse, pulse * 1.3f, pulse);

                if (fireLight != null)
                {
                    fireLight.intensity = 2.2f + Mathf.Sin(t * 22f) * 0.9f;
                }
            }
        }

        private void EnsureDamageFxCreated()
        {
            if (damageFxRoot != null) return;

            EnsureSharedFxMaterials();

            damageFxRoot = new GameObject("DamageFX_Hood");
            damageFxRoot.transform.SetParent(transform, false);
            damageFxRoot.transform.localPosition = new Vector3(0f, 0.65f, 1.05f);

            GameObject smokeObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            smokeObj.name = "SmokePuff";
            smokeObj.transform.SetParent(damageFxRoot.transform, false);
            Collider smokeCol = smokeObj.GetComponent<Collider>();
            if (smokeCol != null) Destroy(smokeCol);

            MeshRenderer smokeRen = smokeObj.GetComponent<MeshRenderer>();
            if (smokeRen != null)
            {
                smokeRen.sharedMaterial = sharedSmokeMaterial;
                smokeRen.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            smokePuffTransform = smokeObj.transform;

            GameObject fireObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fireObj.name = "FirePuff";
            fireObj.transform.SetParent(damageFxRoot.transform, false);
            Collider fireCol = fireObj.GetComponent<Collider>();
            if (fireCol != null) Destroy(fireCol);

            MeshRenderer fireRen = fireObj.GetComponent<MeshRenderer>();
            if (fireRen != null)
            {
                fireRen.sharedMaterial = sharedFireMaterial;
                fireRen.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            firePuffTransform = fireObj.transform;

            GameObject lightObj = new GameObject("FirePointLight");
            lightObj.transform.SetParent(damageFxRoot.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            fireLight = lightObj.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1.0f, 0.45f, 0.08f, 1f);
            fireLight.range = 5.5f;
            fireLight.intensity = 2.5f;
            fireLight.shadows = LightShadows.None;
            fireLight.enabled = false;
        }

        private IEnumerator FlashDamageRoutine()
        {
            SetChassisColorTint(new Color(1.0f, 0.42f, 0.38f, 1f));
            yield return new WaitForSeconds(0.09f);
            if (!isDestroyed)
            {
                ClearChassisColorTint();
            }
            hitFlashCoroutine = null;
        }

        private void ApplyCharredVisuals()
        {
            SetChassisColorTint(new Color(0.16f, 0.15f, 0.15f, 1f));
        }

        private void SetChassisColorTint(Color tint)
        {
            if (bodyRenderers == null) return;
            if (propBlock == null) propBlock = new MaterialPropertyBlock();

            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                Renderer r = bodyRenderers[i];
                if (r == null || (damageFxRoot != null && r.transform.IsChildOf(damageFxRoot.transform)))
                    continue;

                r.GetPropertyBlock(propBlock);
                propBlock.SetColor("_BaseColor", tint);
                propBlock.SetColor("_Color", tint);
                r.SetPropertyBlock(propBlock);
            }
        }

        private void ClearChassisColorTint()
        {
            if (bodyRenderers == null) return;

            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                Renderer r = bodyRenderers[i];
                if (r == null || (damageFxRoot != null && r.transform.IsChildOf(damageFxRoot.transform)))
                    continue;

                r.SetPropertyBlock(null);
            }
        }

        private static void SpawnExplosionEffect(Vector3 worldPos)
        {
            EnsureSharedFxMaterials();

            GameObject fxHost = new GameObject("VehicleExplosionFX");
            fxHost.transform.position = worldPos;
            ExplosionVisualRunner runner = fxHost.AddComponent<ExplosionVisualRunner>();
            runner.Initialize(sharedExplosionCoreMaterial, sharedFireMaterial, sharedSmokeMaterial);
        }

        private static void EnsureSharedFxMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Standard");

            if (sharedSmokeMaterial == null)
            {
                sharedSmokeMaterial = new Material(urpLit)
                {
                    name = "VehicleSmokeMat"
                };
                sharedSmokeMaterial.SetColor("_BaseColor", new Color(0.22f, 0.22f, 0.24f, 1f));
                sharedSmokeMaterial.SetFloat("_Smoothness", 0.05f);
            }

            if (sharedFireMaterial == null)
            {
                sharedFireMaterial = new Material(urpLit)
                {
                    name = "VehicleFireMat"
                };
                Color fireCol = new Color(1.0f, 0.42f, 0.05f, 1f);
                sharedFireMaterial.SetColor("_BaseColor", fireCol);
                sharedFireMaterial.EnableKeyword("_EMISSION");
                sharedFireMaterial.SetColor("_EmissionColor", fireCol * 2.4f);
            }

            if (sharedExplosionCoreMaterial == null)
            {
                sharedExplosionCoreMaterial = new Material(urpLit)
                {
                    name = "VehicleExplosionCoreMat"
                };
                Color coreCol = new Color(1.0f, 0.85f, 0.25f, 1f);
                sharedExplosionCoreMaterial.SetColor("_BaseColor", coreCol);
                sharedExplosionCoreMaterial.EnableKeyword("_EMISSION");
                sharedExplosionCoreMaterial.SetColor("_EmissionColor", coreCol * 3.5f);
            }
        }

        private static bool IsIgnoredSurface(string objName)
        {
            if (string.IsNullOrEmpty(objName)) return false;
            return objName == "Ground" ||
                   objName.StartsWith("Road_", StringComparison.Ordinal) ||
                   objName.StartsWith("SidewalkLot_", StringComparison.Ordinal);
        }

        private static bool IsPoliceVehicle(VehicleController vc)
        {
            if (vc == null) return false;
            string vName = vc.VehicleName;
            return !string.IsNullOrEmpty(vName) && vName.IndexOf("Police", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Anima e destrói automaticamente a bola de fogo e fumaça da explosão do veículo.
        /// </summary>
        private sealed class ExplosionVisualRunner : MonoBehaviour
        {
            private Transform coreSphere;
            private Transform outerFireSphere;
            private Transform smokeRingSphere;
            private Light blastLight;
            private float elapsed;
            private const float Duration = 1.35f;

            public void Initialize(Material coreMat, Material fireMat, Material smokeMat)
            {
                coreSphere = CreateSphere("BlastCore", coreMat, 0.5f);
                outerFireSphere = CreateSphere("BlastFire", fireMat, 0.8f);
                smokeRingSphere = CreateSphere("BlastSmoke", smokeMat, 1.0f);

                GameObject lightGo = new GameObject("BlastLight");
                lightGo.transform.SetParent(transform, false);
                lightGo.transform.localPosition = Vector3.up * 1.2f;
                blastLight = lightGo.AddComponent<Light>();
                blastLight.type = LightType.Point;
                blastLight.color = new Color(1.0f, 0.62f, 0.15f, 1f);
                blastLight.range = 18f;
                blastLight.intensity = 8f;
                blastLight.shadows = LightShadows.None;
            }

            private Transform CreateSphere(string objName, Material mat, float initialScale)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = objName;
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * initialScale;
                Collider col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);

                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                return go.transform;
            }

            private void Update()
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Duration);

                if (coreSphere != null)
                {
                    float coreScale = t < 0.35f
                        ? Mathf.Lerp(0.6f, 4.2f, t / 0.35f)
                        : Mathf.Lerp(4.2f, 0.05f, (t - 0.35f) / 0.65f);
                    coreSphere.localScale = Vector3.one * coreScale;
                }

                if (outerFireSphere != null)
                {
                    float fireScale = t < 0.55f
                        ? Mathf.Lerp(0.8f, 5.6f, t / 0.55f)
                        : Mathf.Lerp(5.6f, 0.1f, (t - 0.55f) / 0.45f);
                    outerFireSphere.localPosition = Vector3.up * (t * 1.4f);
                    outerFireSphere.localScale = new Vector3(fireScale, fireScale * 0.8f, fireScale);
                }

                if (smokeRingSphere != null)
                {
                    float smokeScale = Mathf.Lerp(1.2f, 6.8f, t);
                    smokeRingSphere.localPosition = Vector3.up * (t * 3.2f);
                    float shrink = t > 0.7f ? Mathf.Lerp(1f, 0.05f, (t - 0.7f) / 0.3f) : 1f;
                    smokeRingSphere.localScale = new Vector3(smokeScale, smokeScale * 0.65f, smokeScale) * shrink;
                }

                if (blastLight != null)
                {
                    blastLight.intensity = Mathf.Lerp(9f, 0f, t * t);
                }

                if (elapsed >= Duration)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}

