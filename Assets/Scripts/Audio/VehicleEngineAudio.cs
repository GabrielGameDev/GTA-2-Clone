using UnityEngine;
using GTA2Clone.Vehicles;

namespace GTA2Clone.Audio
{
    /// <summary>
    /// Controla o som dinâmico do motor do veículo (pitch, volume, simulação de marchas arcade e espacialização top-down).
    /// - Quando conduzido pelo jogador (IsOccupied): reproduz o motor em primeiro plano com curva de 4 marchas estilo GTA 2
    ///   e resposta imediata ao pedal do acelerador/ré.
    /// - Quando conduzido por IA (HasAIDriver): reproduz um ronco ambiente sutil atenuado pela distância planar XZ até a câmera
    ///   e pausa automaticamente o AudioSource quando distante (> aiAudibleRadius) ou estacionado.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VehicleController))]
    public class VehicleEngineAudio : MonoBehaviour
    {
        [Header("Audio References")]
        [SerializeField] private AudioSource engineAudioSource;
        [SerializeField] private AudioClip engineLoopClip;
        [SerializeField] private EngineSoundCategory engineCategory = EngineSoundCategory.Standard;
        [SerializeField] private bool autoInferCategory = true;

        [Header("Pitch & Arcade Gearbox")]
        [SerializeField] private float idlePitch = 0.78f;
        [SerializeField] private float maxForwardPitch = 1.92f;
        [SerializeField] private float maxReversePitch = 1.38f;
        [SerializeField] private float gearModulationDepth = 0.18f;
        [SerializeField] private float pitchLerpSpeed = 9.5f;

        [Header("Volume & Top-Down Attenuation")]
        [SerializeField] private float playerIdleVolume = 0.28f;
        [SerializeField] private float playerMaxVolume = 0.56f;
        [SerializeField] private float aiMaxVolume = 0.13f;
        [SerializeField] private float aiAudibleRadius = 28f;
        [SerializeField] private float volumeLerpSpeed = 10f;

        private VehicleController vehicleController;
        private Transform cachedCameraTransform;
        private float currentPitch;
        private float currentVolume;
        private float randomTimeOffset;

        public EngineSoundCategory Category => engineCategory;
        public AudioClip EngineClip => engineLoopClip;

        private void Awake()
        {
            vehicleController = GetComponent<VehicleController>();
            randomTimeOffset = Random.Range(0f, 1.5f);

            if (autoInferCategory && vehicleController != null)
            {
                engineCategory = ProceduralAudioSynthesizer.InferCategoryFromVehicle(
                    vehicleController.VehicleName,
                    vehicleController.MaxForwardSpeed
                );
            }

            ApplyCategoryPitchDefaults();
            EnsureAudioSource();
            currentPitch = idlePitch;
            currentVolume = 0f;
        }

        private void OnEnable()
        {
            EnsureAudioSource();
        }

        private void OnDisable()
        {
            if (engineAudioSource != null && engineAudioSource.isPlaying)
            {
                engineAudioSource.Stop();
            }
            currentVolume = 0f;
        }

        /// <summary>
        /// Configura a categoria e o AudioClip do motor (usado pelas ferramentas de Editor na geração dos Prefabs).
        /// </summary>
        public void Configure(EngineSoundCategory category, AudioClip clip)
        {
            engineCategory = category;
            autoInferCategory = false;
            engineLoopClip = clip;

            ApplyCategoryPitchDefaults();
            EnsureAudioSource();
            if (engineAudioSource != null && clip != null)
            {
                engineAudioSource.clip = clip;
            }
        }

        private void Update()
        {
            if (vehicleController == null || engineAudioSource == null)
                return;

            // Se o veículo explodiu ou está estacionado sem motorista, silencia e pausa o AudioSource
            if (vehicleController.IsDestroyed || !vehicleController.IsControlled)
            {
                FadeOutAndPause();
                return;
            }

            bool isPlayerDriving = vehicleController.IsOccupied;
            float distanceFactor = 1f;
            float stereoPan = 0f;

            if (!isPlayerDriving)
            {
                if (!TryEvaluatePlanarSpatialization(out distanceFactor, out stereoPan))
                {
                    FadeOutAndPause();
                    return;
                }
            }

            EnsurePlaying();

            float targetPitch = ComputeTargetPitch();
            float targetVolume = ComputeTargetVolume(isPlayerDriving, distanceFactor);

            currentPitch = Mathf.Lerp(currentPitch, targetPitch, pitchLerpSpeed * Time.deltaTime);
            currentVolume = Mathf.Lerp(currentVolume, targetVolume, volumeLerpSpeed * Time.deltaTime);

            engineAudioSource.pitch = currentPitch;
            engineAudioSource.volume = currentVolume;
            engineAudioSource.panStereo = isPlayerDriving ? 0f : stereoPan;
        }

        private float ComputeTargetPitch()
        {
            float forwardSpeed = vehicleController.ForwardSpeed;
            float throttleInput = vehicleController.CurrentThrottleInput;
            float normalizedSpeed = vehicleController.NormalizedSpeed;

            // Marcha à ré
            if (forwardSpeed < -0.25f)
            {
                float maxRev = Mathf.Max(4f, vehicleController.Data != null ? vehicleController.Data.MaxReverseSpeed : 8f);
                float revFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / maxRev);
                float revThrottleBoost = throttleInput < -0.1f ? 0.10f : 0f;
                return Mathf.Lerp(idlePitch, maxReversePitch, revFactor) + revThrottleBoost;
            }

            // Curva de 4 marchas arcade estilo GTA 2 para aceleração frontal
            // Cada marcha sobe o RPM e dá uma leve queda ao trocar para a próxima faixa de velocidade
            const int gearCount = 4;
            float scaledSpeed = normalizedSpeed * gearCount;
            int currentGear = Mathf.Clamp(Mathf.FloorToInt(scaledSpeed), 0, gearCount - 1);
            float inGearProgress = scaledSpeed - currentGear;

            // Nas primeiras marchas o RPM sobe rápido; na última marcha vai até o topo
            float baseSpeedPitch = Mathf.Lerp(idlePitch, maxForwardPitch, Mathf.Pow(normalizedSpeed, 0.85f));
            float sawtoothGear = (inGearProgress - 0.35f) * gearModulationDepth * Mathf.Clamp01(normalizedSpeed * 3.5f);

            // Resposta imediata ao pisar no acelerador mesmo em baixa velocidade
            float throttleBoost = throttleInput > 0.05f
                ? throttleInput * 0.14f * (1f - normalizedSpeed * 0.35f)
                : -0.04f * normalizedSpeed;

            return Mathf.Clamp(baseSpeedPitch + sawtoothGear + throttleBoost, idlePitch * 0.92f, maxForwardPitch + 0.25f);
        }

        private float ComputeTargetVolume(bool isPlayerDriving, float aiDistanceFactor)
        {
            float normalizedSpeed = vehicleController.NormalizedSpeed;
            float absThrottle = Mathf.Abs(vehicleController.CurrentThrottleInput);
            float loadFactor = Mathf.Clamp01(normalizedSpeed * 0.65f + absThrottle * 0.45f);

            if (isPlayerDriving)
            {
                return Mathf.Lerp(playerIdleVolume, playerMaxVolume, loadFactor);
            }

            float baseAiVol = Mathf.Lerp(aiMaxVolume * 0.45f, aiMaxVolume, loadFactor);
            return baseAiVol * aiDistanceFactor;
        }

        private bool TryEvaluatePlanarSpatialization(out float distanceFactor, out float stereoPan)
        {
            distanceFactor = 0f;
            stereoPan = 0f;

            if (cachedCameraTransform == null)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    cachedCameraTransform = mainCam.transform;
                }
            }

            if (cachedCameraTransform == null)
                return false;

            Vector3 camPos = cachedCameraTransform.position;
            Vector3 myPos = transform.position;
            float dx = myPos.x - camPos.x;
            float dz = myPos.z - camPos.z;
            float distSq = dx * dx + dz * dz;
            float maxDistSq = aiAudibleRadius * aiAudibleRadius;

            if (distSq >= maxDistSq)
                return false;

            float dist = Mathf.Sqrt(distSq);
            float t = 1f - Mathf.Clamp01(dist / aiAudibleRadius);
            distanceFactor = t * t;
            stereoPan = Mathf.Clamp(dx / 24f, -0.6f, 0.6f);
            return true;
        }

        private void EnsurePlaying()
        {
            if (engineAudioSource.clip == null)
            {
                engineAudioSource.clip = engineLoopClip != null
                    ? engineLoopClip
                    : ProceduralAudioSynthesizer.GetOrCreateEngineLoopClip(engineCategory);
            }

            if (!engineAudioSource.isPlaying && engineAudioSource.clip != null)
            {
                if (engineAudioSource.clip.length > 0.1f)
                {
                    engineAudioSource.time = Mathf.Repeat(randomTimeOffset, engineAudioSource.clip.length);
                }
                engineAudioSource.Play();
            }
        }

        private void FadeOutAndPause()
        {
            if (engineAudioSource == null || !engineAudioSource.isPlaying)
                return;

            currentVolume = Mathf.MoveTowards(currentVolume, 0f, volumeLerpSpeed * 1.5f * Time.deltaTime);
            engineAudioSource.volume = currentVolume;

            if (currentVolume <= 0.005f)
            {
                engineAudioSource.Pause();
            }
        }

        private void ApplyCategoryPitchDefaults()
        {
            switch (engineCategory)
            {
                case EngineSoundCategory.Sports:
                    idlePitch = 0.84f;
                    maxForwardPitch = 2.08f;
                    maxReversePitch = 1.48f;
                    gearModulationDepth = 0.21f;
                    break;

                case EngineSoundCategory.Heavy:
                    idlePitch = 0.72f;
                    maxForwardPitch = 1.62f;
                    maxReversePitch = 1.22f;
                    gearModulationDepth = 0.15f;
                    break;

                default:
                    idlePitch = 0.78f;
                    maxForwardPitch = 1.88f;
                    maxReversePitch = 1.36f;
                    gearModulationDepth = 0.18f;
                    break;
            }
        }

        private void EnsureAudioSource()
        {
            if (engineAudioSource == null)
            {
                engineAudioSource = GetComponent<AudioSource>();
                if (engineAudioSource == null)
                {
                    engineAudioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            engineAudioSource.playOnAwake = false;
            engineAudioSource.loop = true;
            engineAudioSource.spatialBlend = 0f; // Atenuação e pan calculados no plano XZ top-down
            engineAudioSource.volume = 0f;
            engineAudioSource.priority = 110;

            if (engineLoopClip == null && Application.isPlaying)
            {
                engineLoopClip = ProceduralAudioSynthesizer.GetOrCreateEngineLoopClip(engineCategory);
            }

            if (engineLoopClip != null)
            {
                engineAudioSource.clip = engineLoopClip;
            }
        }
    }
}

