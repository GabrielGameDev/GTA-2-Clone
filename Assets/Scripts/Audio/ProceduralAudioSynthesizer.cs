using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace GTA2Clone.Audio
{
    public enum EngineSoundCategory
    {
        Standard = 0,
        Sports = 1,
        Heavy = 2
    }

    /// <summary>
    /// Sintetizador procedural de áudio (PCM 44.1 kHz) para os efeitos sonoros estilo GTA 2:
    /// - Loops perfeitos (sem cliques de transição) de motores por categoria (Standard, Sports, Heavy)
    /// - Loop de sirene policial (Wail com harmônicos de corneta e modulação de fase)
    /// - Som de rádio policial (Squelch VHF + Beep Motorola de 2 tons + modulação vocal de despacho + Squelch final)
    /// Permite tanto salvar arquivos .wav reais no projeto (via Editor) quanto gerar AudioClips em memória como fallback.
    /// </summary>
    public static class ProceduralAudioSynthesizer
    {
        public const int DefaultSampleRate = 44100;

        private static AudioClip cachedStandardEngineClip;
        private static AudioClip cachedSportsEngineClip;
        private static AudioClip cachedHeavyEngineClip;
        private static AudioClip cachedPoliceSirenClip;
        private static AudioClip cachedPoliceRadioClip;

        /// <summary>
        /// Retorna (ou sintetiza em cache) o AudioClip de loop do motor para a categoria solicitada.
        /// </summary>
        public static AudioClip GetOrCreateEngineLoopClip(EngineSoundCategory category)
        {
            switch (category)
            {
                case EngineSoundCategory.Sports:
                    if (cachedSportsEngineClip == null)
                    {
                        float[] samples = GenerateEngineLoopSamples(EngineSoundCategory.Sports, DefaultSampleRate);
                        cachedSportsEngineClip = CreateClipFromSamples("EngineLoop_Sports_Procedural", samples, DefaultSampleRate);
                    }
                    return cachedSportsEngineClip;

                case EngineSoundCategory.Heavy:
                    if (cachedHeavyEngineClip == null)
                    {
                        float[] samples = GenerateEngineLoopSamples(EngineSoundCategory.Heavy, DefaultSampleRate);
                        cachedHeavyEngineClip = CreateClipFromSamples("EngineLoop_Heavy_Procedural", samples, DefaultSampleRate);
                    }
                    return cachedHeavyEngineClip;

                default:
                    if (cachedStandardEngineClip == null)
                    {
                        float[] samples = GenerateEngineLoopSamples(EngineSoundCategory.Standard, DefaultSampleRate);
                        cachedStandardEngineClip = CreateClipFromSamples("EngineLoop_Standard_Procedural", samples, DefaultSampleRate);
                    }
                    return cachedStandardEngineClip;
            }
        }

        /// <summary>
        /// Retorna (ou sintetiza em cache) o AudioClip de loop da sirene policial.
        /// </summary>
        public static AudioClip GetOrCreatePoliceSirenClip()
        {
            if (cachedPoliceSirenClip == null)
            {
                float[] samples = GeneratePoliceSirenSamples(DefaultSampleRate);
                cachedPoliceSirenClip = CreateClipFromSamples("PoliceSiren_Loop_Procedural", samples, DefaultSampleRate);
            }
            return cachedPoliceSirenClip;
        }

        /// <summary>
        /// Retorna (ou sintetiza em cache) o AudioClip de despacho de rádio policial ao subir uma estrela.
        /// </summary>
        public static AudioClip GetOrCreatePoliceRadioStarClip()
        {
            if (cachedPoliceRadioClip == null)
            {
                float[] samples = GeneratePoliceRadioStarSamples(DefaultSampleRate);
                cachedPoliceRadioClip = CreateClipFromSamples("PoliceRadio_StarIncrease_Procedural", samples, DefaultSampleRate);
            }
            return cachedPoliceRadioClip;
        }

        /// <summary>
        /// Infere a categoria sonora do motor a partir do nome e da velocidade máxima do veículo.
        /// </summary>
        public static EngineSoundCategory InferCategoryFromVehicle(string vehicleName, float maxForwardSpeed)
        {
            if (!string.IsNullOrEmpty(vehicleName))
            {
                if (vehicleName.IndexOf("Race", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    vehicleName.IndexOf("Sports", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (vehicleName.IndexOf("Police", StringComparison.OrdinalIgnoreCase) >= 0 &&
                     vehicleName.IndexOf("Tractor", StringComparison.OrdinalIgnoreCase) < 0))
                {
                    return EngineSoundCategory.Sports;
                }

                if (vehicleName.IndexOf("Truck", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    vehicleName.IndexOf("Tractor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    vehicleName.IndexOf("Firetruck", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    vehicleName.IndexOf("Garbage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    vehicleName.IndexOf("Ambulance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    vehicleName.IndexOf("Delivery", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    vehicleName.IndexOf("Van", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return EngineSoundCategory.Heavy;
                }
            }

            if (maxForwardSpeed >= 20.5f)
                return EngineSoundCategory.Sports;
            if (maxForwardSpeed <= 16.5f)
                return EngineSoundCategory.Heavy;

            return EngineSoundCategory.Standard;
        }

        /// <summary>
        /// Sintetiza 2.0 segundos perfeitamente cíclicos (sem clique no ponto de loop) de ronco de motor.
        /// </summary>
        public static float[] GenerateEngineLoopSamples(EngineSoundCategory category, int sampleRate = DefaultSampleRate)
        {
            const float durationSeconds = 2.0f;
            int totalSamples = Mathf.RoundToInt(sampleRate * durationSeconds);
            float[] samples = new float[totalSamples];

            // Frequências fundamentais inteiras para garantir continuidade de fase exata em 2.0s
            float baseFreq = category switch
            {
                EngineSoundCategory.Sports => 38.0f,
                EngineSoundCategory.Heavy => 23.0f,
                _ => 30.0f
            };

            float lowPassAlpha = category switch
            {
                EngineSoundCategory.Sports => 0.20f,
                EngineSoundCategory.Heavy => 0.09f,
                _ => 0.14f
            };

            float filterState1 = 0f;
            float filterState2 = 0f;
            float dcBlockerX = 0f;
            float dcBlockerY = 0f;

            // Fazemos 1 passagem de aquecimento dos filtros nos últimos 2048 samples antes de gravar
            int warmupCount = 2048;
            for (int i = -warmupCount; i < totalSamples; i++)
            {
                int sampleIdx = (i + totalSamples) % totalSamples;
                float t = sampleIdx / (float)sampleRate;
                float twoPiF = Mathf.PI * 2f * baseFreq * t;

                // Combinação de harmônicos de combustão interna (ordem de ignição assimétrica de 4 tempos)
                float subCycle = Mathf.Sin(twoPiF * 0.5f);
                float fundamental = Mathf.Sin(twoPiF);
                float secondHarm = Mathf.Sin(twoPiF * 2.0f + 0.4f);
                float thirdHarm = Mathf.Sin(twoPiF * 3.0f + 1.1f);
                float fourthHarm = Mathf.Sin(twoPiF * 4.0f + 0.7f);
                float sixthHarm = Mathf.Sin(twoPiF * 6.0f + 1.9f);

                // Pulso periódico de explosão nos cilindros (2 pulsos por rotação principal)
                float cylinderPhase = (baseFreq * 2.0f * t) % 1.0f;
                float combustionPulse = Mathf.Exp(-cylinderPhase * 6.5f) * Mathf.Sin(cylinderPhase * Mathf.PI * 6f);

                // Pequena assimetria entre cilindros pares e ímpares (dá o "chug-chug" autêntico de motor V8 / 4 cilindros)
                float firingMod = 0.84f + 0.16f * Mathf.Sin(twoPiF * 0.5f + 0.3f);

                // Textura de escapamento periódica (determinística e cíclica para não estalar no loop)
                float exhaustRasp = Mathf.Sin(twoPiF * 9.0f + Mathf.Sin(twoPiF * 2.0f) * 2.2f) * 0.18f;

                float raw;
                if (category == EngineSoundCategory.Sports)
                {
                    raw = (subCycle * 0.18f +
                           fundamental * 0.48f +
                           secondHarm * 0.42f +
                           thirdHarm * 0.28f +
                           fourthHarm * 0.22f +
                           sixthHarm * 0.15f +
                           combustionPulse * 0.45f +
                           exhaustRasp * 1.25f) * firingMod;
                }
                else if (category == EngineSoundCategory.Heavy)
                {
                    raw = (subCycle * 0.45f +
                           fundamental * 0.65f +
                           secondHarm * 0.36f +
                           thirdHarm * 0.20f +
                           fourthHarm * 0.10f +
                           combustionPulse * 0.62f +
                           exhaustRasp * 0.65f) * firingMod;
                }
                else
                {
                    raw = (subCycle * 0.28f +
                           fundamental * 0.56f +
                           secondHarm * 0.38f +
                           thirdHarm * 0.24f +
                           fourthHarm * 0.14f +
                           sixthHarm * 0.08f +
                           combustionPulse * 0.50f +
                           exhaustRasp * 0.90f) * firingMod;
                }

                // Saturação suave de válvula/escapamento
                float saturated = (float)Math.Tanh(raw * 1.45f);

                // Filtro passa-baixa de 2 polos em cascata para encorpar o som e tirar qualquer aspereza digital
                filterState1 += lowPassAlpha * (saturated - filterState1);
                filterState2 += lowPassAlpha * (filterState1 - filterState2);

                // Bloqueador DC para centrar a onda em zero
                float y = filterState2 - dcBlockerX + 0.995f * dcBlockerY;
                dcBlockerX = filterState2;
                dcBlockerY = y;

                if (i >= 0)
                {
                    samples[i] = Mathf.Clamp(y * 0.78f, -0.95f, 0.95f);
                }
            }

            return samples;
        }

        /// <summary>
        /// Sintetiza um loop de 3.2 segundos perfeitamente contínuo da sirene policial (Wail + harmônicos de corneta).
        /// </summary>
        public static float[] GeneratePoliceSirenSamples(int sampleRate = DefaultSampleRate)
        {
            const float durationSeconds = 3.2f;
            int totalSamples = Mathf.RoundToInt(sampleRate * durationSeconds);
            float[] samples = new float[totalSamples];

            // 2 ciclos completos de Wail em 3.2s (1.6s por ciclo) para loop perfeito
            const float wailCycles = 2.0f;
            const float minFreq = 610f;
            const float maxFreq = 1260f;

            double phase = 0.0;
            float lpState = 0f;
            const float lpAlpha = 0.26f;

            for (int i = 0; i < totalSamples; i++)
            {
                float normTime = i / (float)totalSamples;
                float cycleAngle = normTime * wailCycles * Mathf.PI * 2f;

                // Curva clássica de sirene (sobe rápido e desce levemente curvada)
                float rawMod = 0.5f - 0.5f * Mathf.Cos(cycleAngle);
                float shapedMod = Mathf.Pow(rawMod, 0.82f);

                float currentFreq = Mathf.Lerp(minFreq, maxFreq, shapedMod);
                phase += (Mathf.PI * 2.0 * currentFreq) / sampleRate;
                if (phase > Math.PI * 2.0)
                {
                    phase -= Math.PI * 2.0;
                }

                float fPhase = (float)phase;

                // Timbre de corneta policial (fundamental + 2º e 3º harmônicos com leve saturação)
                float hornWave =
                    Mathf.Sin(fPhase) * 0.68f +
                    Mathf.Sin(fPhase * 2f + 0.25f) * 0.24f +
                    Mathf.Sin(fPhase * 3f + 0.6f) * 0.11f;

                float saturated = (float)Math.Tanh(hornWave * 1.35f);
                lpState += lpAlpha * (saturated - lpState);

                // Pequena modulação de amplitude conforme a frequência sobe (corneta projeta mais nos agudos)
                float env = Mathf.Lerp(0.76f, 0.96f, shapedMod);
                samples[i] = Mathf.Clamp(lpState * env * 0.72f, -0.95f, 0.95f);
            }

            // Micro cross-fade de fase nos extremos (64 samples) para zero clique absoluto no loop
            const int fadeSamples = 64;
            for (int i = 0; i < fadeSamples; i++)
            {
                float w = i / (float)fadeSamples;
                samples[i] *= w;
                samples[totalSamples - 1 - i] *= w;
            }

            return samples;
        }

        /// <summary>
        /// Sintetiza o som clássico de rádio da polícia ao ganhar estrela (1.28s):
        /// 1. Squelch VHF inicial ("KSHHT")
        /// 2. Beep duplo de alerta de central policial (Two-Tone Dispatch Chirp)
        /// 3. Rajada curta de formantes de voz de rádio ("All units, suspect 10-31...") filtrada em banda estreita
        /// 4. Roger Beep + Squelch final ("KSHHT")
        /// </summary>
        public static float[] GeneratePoliceRadioStarSamples(int sampleRate = DefaultSampleRate)
        {
            const float durationSeconds = 1.28f;
            int totalSamples = Mathf.RoundToInt(sampleRate * durationSeconds);
            float[] samples = new float[totalSamples];

            System.Random rng = new System.Random(19991022); // Seed determinística (ano de lançamento do GTA 2!)
            float bpLow = 0f;
            float bpHigh = 0f;

            // Sílabas sintéticas da mensagem de despacho no rádio ("ALL U-NITS TEN THIR-TY ONE")
            (float start, float end, float pitch, float f1, float f2)[] syllables =
            {
                (0.32f, 0.43f, 138f, 720f, 1240f),
                (0.45f, 0.56f, 152f, 540f, 1680f),
                (0.58f, 0.69f, 144f, 660f, 1420f),
                (0.73f, 0.85f, 162f, 810f, 1850f),
                (0.87f, 1.02f, 132f, 610f, 1310f)
            };

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)sampleRate;
                float whiteNoise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float signal = 0f;

                // 1. Squelch de abertura do PTT (0.00s a 0.075s)
                if (t < 0.075f)
                {
                    float env = Mathf.Sin((t / 0.075f) * Mathf.PI);
                    float click = t < 0.008f ? Mathf.Sin(t * 2800f * Mathf.PI * 2f) * 0.8f : 0f;
                    signal += (whiteNoise * 0.72f * env) + click;
                }
                // 2. Beep duplo de despacho policial estilo Motorola/GTA (0.08s a 0.29s)
                else if (t >= 0.08f && t < 0.175f)
                {
                    float toneEnv = Mathf.Sin(((t - 0.08f) / 0.095f) * Mathf.PI);
                    float tone1 = Mathf.Sin(t * 1780f * Mathf.PI * 2f) * 0.62f +
                                  Mathf.Sin(t * 3560f * Mathf.PI * 2f) * 0.18f;
                    signal += tone1 * toneEnv + whiteNoise * 0.08f;
                }
                else if (t >= 0.185f && t < 0.295f)
                {
                    float toneEnv = Mathf.Sin(((t - 0.185f) / 0.11f) * Mathf.PI);
                    float tone2 = Mathf.Sin(t * 2340f * Mathf.PI * 2f) * 0.68f +
                                  Mathf.Sin(t * 4680f * Mathf.PI * 2f) * 0.20f;
                    signal += tone2 * toneEnv + whiteNoise * 0.08f;
                }
                // 3. Voz de despacho sintetizada por formantes + chiado de portadora VHF (0.31s a 1.04s)
                else if (t >= 0.31f && t < 1.04f)
                {
                    float voiceSample = 0f;
                    for (int s = 0; s < syllables.Length; s++)
                    {
                        var syl = syllables[s];
                        if (t >= syl.start && t <= syl.end)
                        {
                            float localT = (t - syl.start) / (syl.end - syl.start);
                            float sylEnv = Mathf.Sin(localT * Mathf.PI);

                            // Pulso glotal rico em harmônicos modulado pelos formantes F1 e F2
                            float f0 = syl.pitch * (1f - 0.08f * localT);
                            float glottal = Mathf.Sin(t * f0 * Mathf.PI * 2f) +
                                            0.5f * Mathf.Sin(t * f0 * 2f * Mathf.PI * 2f);

                            float formant1 = Mathf.Sin(t * syl.f1 * Mathf.PI * 2f) * (0.55f + 0.45f * glottal);
                            float formant2 = Mathf.Sin(t * syl.f2 * Mathf.PI * 2f) * (0.40f + 0.60f * glottal);

                            voiceSample = (formant1 * 0.58f + formant2 * 0.42f + whiteNoise * 0.14f) * sylEnv;
                            break;
                        }
                    }

                    // Compressão/distorção típica de alto-falante de walkie-talkie policial
                    float compressedVoice = (float)Math.Tanh(voiceSample * 2.4f) * 0.62f;
                    signal += compressedVoice + whiteNoise * 0.09f;
                }
                // 4. Roger Beep curto + Squelch final ("KSHHT") (1.05s a 1.28s)
                else if (t >= 1.05f && t < 1.12f)
                {
                    float pipEnv = Mathf.Sin(((t - 1.05f) / 0.07f) * Mathf.PI);
                    float rogerPip = Mathf.Sin(t * 2080f * Mathf.PI * 2f) * 0.55f;
                    signal += rogerPip * pipEnv + whiteNoise * 0.12f;
                }
                else if (t >= 1.12f)
                {
                    float tailProgress = (t - 1.12f) / (durationSeconds - 1.12f);
                    float squelchEnv = Mathf.Sin(Mathf.Clamp01(tailProgress) * Mathf.PI) * (1f - tailProgress * 0.35f);
                    signal += whiteNoise * 0.78f * squelchEnv;
                }

                // Filtro passa-faixa estilo rádio VHF (aprox. 450 Hz - 3100 Hz)
                bpLow += 0.38f * (signal - bpLow);
                bpHigh += 0.065f * (bpLow - bpHigh);
                float bandPassed = bpLow - bpHigh;

                // Saturação final de rádio comunicador
                float radioOut = (float)Math.Tanh(bandPassed * 1.65f);
                samples[i] = Mathf.Clamp(radioOut * 0.82f, -0.95f, 0.95f);
            }

            return samples;
        }

        /// <summary>
        /// Cria um AudioClip da Unity a partir de um buffer de samples float[-1..1].
        /// </summary>
        public static AudioClip CreateClipFromSamples(string clipName, float[] samples, int sampleRate = DefaultSampleRate)
        {
            AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, sampleRate, stream: false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// Grava um buffer de samples float[-1..1] como arquivo .wav PCM 16-bit mono em disco.
        /// </summary>
        public static void WriteWavFile(string fullFilePath, float[] samples, int sampleRate = DefaultSampleRate)
        {
            string dir = Path.GetDirectoryName(fullFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            int sampleCount = samples.Length;
            int byteRate = sampleRate * 2; // Mono 16-bit (2 bytes por sample)
            int dataChunkSize = sampleCount * 2;

            using FileStream fs = new FileStream(fullFilePath, FileMode.Create, FileAccess.Write);
            using BinaryWriter bw = new BinaryWriter(fs, Encoding.ASCII);

            // RIFF Header
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + dataChunkSize);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));

            // fmt Subchunk
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);                // Subchunk1Size (16 para PCM)
            bw.Write((short)1);          // AudioFormat (1 = PCM)
            bw.Write((short)1);          // NumChannels (1 = Mono)
            bw.Write(sampleRate);        // SampleRate
            bw.Write(byteRate);          // ByteRate
            bw.Write((short)2);          // BlockAlign
            bw.Write((short)16);         // BitsPerSample

            // data Subchunk
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(dataChunkSize);

            for (int i = 0; i < sampleCount; i++)
            {
                float clamped = Mathf.Clamp(samples[i], -1f, 1f);
                short pcmValue = (short)Mathf.RoundToInt(clamped * 32767f);
                bw.Write(pcmValue);
            }
        }
    }
}

