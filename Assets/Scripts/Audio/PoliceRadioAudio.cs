using UnityEngine;
using GTA2Clone.Police;

namespace GTA2Clone.Audio
{
    /// <summary>
    /// Componente desacoplado que escuta o evento OnWantedStarsChanged do PoliceManager
    /// e reproduz o efeito sonoro clássico de rádio policial (Squelch + Dispatch Alert + Voz de Rádio)
    /// sempre que o Nível de Procurado aumenta (+1 estrela ou mais).
    /// </summary>
    [DisallowMultipleComponent]
    public class PoliceRadioAudio : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PoliceManager policeManager;
        [SerializeField] private AudioSource radioAudioSource;
        [SerializeField] private AudioClip starIncreaseRadioClip;

        [Header("Radio Settings")]
        [SerializeField, Range(0f, 1f)] private float radioVolume = 0.72f;
        [SerializeField] private float basePitch = 0.98f;
        [SerializeField] private float pitchStepPerStar = 0.015f;

        private void Awake()
        {
            if (policeManager == null)
            {
                policeManager = GetComponent<PoliceManager>();
            }

            EnsureAudioSource();
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        /// <summary>
        /// Configura as referências do PoliceRadioAudio (usado pelas ferramentas de Editor).
        /// </summary>
        public void Configure(PoliceManager targetPoliceManager, AudioClip radioClip)
        {
            UnsubscribeEvents();
            policeManager = targetPoliceManager;
            starIncreaseRadioClip = radioClip;
            EnsureAudioSource();

            if (isActiveAndEnabled)
            {
                SubscribeEvents();
            }
        }

        /// <summary>
        /// Dispara imediatamente o som de transmissão de rádio policial referente ao aumento para `wantedStars` estrelas.
        /// </summary>
        public void PlayStarIncreaseRadio(int wantedStars)
        {
            EnsureAudioSource();
            if (radioAudioSource == null) return;

            AudioClip clipToPlay = starIncreaseRadioClip != null
                ? starIncreaseRadioClip
                : ProceduralAudioSynthesizer.GetOrCreatePoliceRadioStarClip();

            if (clipToPlay == null) return;

            // Leve variação de pitch conforme o nível de estrelas sobe, dando sensação de urgência crescente na central
            float starOffset = Mathf.Clamp(wantedStars - 1, 0, PoliceManager.MaxWantedStars) * pitchStepPerStar;
            radioAudioSource.pitch = basePitch + starOffset + Random.Range(-0.012f, 0.012f);
            radioAudioSource.PlayOneShot(clipToPlay, radioVolume);
        }

        private void SubscribeEvents()
        {
            if (policeManager == null)
            {
                policeManager = GetComponent<PoliceManager>();
            }

            if (policeManager != null)
            {
                policeManager.OnWantedStarsChanged -= HandleWantedStarsChanged;
                policeManager.OnWantedStarsChanged += HandleWantedStarsChanged;
            }
        }

        private void UnsubscribeEvents()
        {
            if (policeManager != null)
            {
                policeManager.OnWantedStarsChanged -= HandleWantedStarsChanged;
            }
        }

        private void HandleWantedStarsChanged(int oldStars, int newStars)
        {
            // Toca o rádio policial sempre que o nível de procurado aumentar
            if (newStars > oldStars && newStars > 0)
            {
                PlayStarIncreaseRadio(newStars);
            }
        }

        private void EnsureAudioSource()
        {
            if (radioAudioSource == null)
            {
                Transform existingChild = transform.Find("PoliceRadioAudioSource");
                if (existingChild != null)
                {
                    radioAudioSource = existingChild.GetComponent<AudioSource>();
                }

                if (radioAudioSource == null)
                {
                    GameObject sourceObj = new GameObject("PoliceRadioAudioSource");
                    sourceObj.transform.SetParent(transform, false);
                    radioAudioSource = sourceObj.AddComponent<AudioSource>();
                }
            }

            radioAudioSource.playOnAwake = false;
            radioAudioSource.loop = false;
            radioAudioSource.spatialBlend = 0f; // 2D (rádio direto para o jogador)
            radioAudioSource.priority = 32;     // Alta prioridade para nunca ser cortado
        }
    }
}

