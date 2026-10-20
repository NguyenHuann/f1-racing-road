using System;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(CarStateMachine))]
public class Audio : MonoBehaviour {

    CarStateMachine stateMachine;

    [Header("low pitch")]
    public AudioClip lowAccelClip;
    [Range(.5f, 1.5f)] public float lowPitchMin = 1f;

    [Header("high pitch")]
    public AudioClip highAccelClip;
    [Range(4, 10)] public float lowPitchMax = 6f;
    [Range(.1f, .5f)] public float highPitchMultiplier = 0.25f;

    [Header("EQ / Filter Effects")]
    public AnimationCurve lowPassCurve = new AnimationCurve(new Keyframe(0, 10000), new Keyframe(1, 15000));    // Control "Air/Clatter" (Highs)
    public AnimationCurve distortionCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 0.3f)); // Control "Growl/Bite" (Mids)
    public AnimationCurve highPassCurve = new AnimationCurve(new Keyframe(0, 10), new Keyframe(1, 200));   // Control "Rumble" (Lows)
    
    [Range(0, 20)] public float eqLerpSpeed = 5f;
    private float loadLerpIndex;

    // Components
    private AudioSource m_LowAccel;
    private AudioSource m_HighAccel;
    private AudioLowPassFilter lowPassFilter;
    private AudioHighPassFilter highPassFilter;
    private AudioDistortionFilter midDistortion;
    
    private bool m_StartedSound;
    private bool m_ForceStopped;

    [Header("debug")]
    public float pitch;
    private float maxRolloffDistance = 700;

    void Awake() {
        stateMachine = GetComponent<CarStateMachine>();
        InitAudioComponents();
    }

    void Start() {
        if (stateMachine == null) stateMachine = GetComponent<CarStateMachine>();
        InitAudioComponents();
    }

    void OnDisable() {
        StopSound();
    }

    void OnDestroy() {
        StopSound();
    }

    public void InitAudioComponents() {
        // High Accel (Engine screams / highs + EQ filters)
        if (m_HighAccel == null) {
            Transform highT = transform.Find("EngineAudio_High");
            GameObject highGO;
            if (highT != null) {
                highGO = highT.gameObject;
            } else {
                highGO = new GameObject("EngineAudio_High");
                highGO.transform.SetParent(transform, false);
            }

            m_HighAccel = highGO.GetComponent<AudioSource>();
            if (m_HighAccel == null) m_HighAccel = highGO.AddComponent<AudioSource>();
            ConfigureAudioSource(m_HighAccel, highAccelClip);

            lowPassFilter = highGO.GetComponent<AudioLowPassFilter>();
            if (lowPassFilter == null) lowPassFilter = highGO.AddComponent<AudioLowPassFilter>();
            if (lowPassFilter != null) lowPassFilter.cutoffFrequency = 5000;

            highPassFilter = highGO.GetComponent<AudioHighPassFilter>();
            if (highPassFilter == null) highPassFilter = highGO.AddComponent<AudioHighPassFilter>();
            if (highPassFilter != null) highPassFilter.cutoffFrequency = 10;

            midDistortion = highGO.GetComponent<AudioDistortionFilter>();
            if (midDistortion == null) midDistortion = highGO.AddComponent<AudioDistortionFilter>();
            if (midDistortion != null) midDistortion.distortionLevel = 0;
        } else if (highAccelClip != null && m_HighAccel.clip != highAccelClip) {
            m_HighAccel.clip = highAccelClip;
        }

        // Low Accel (Engine rumble / bass)
        if (m_LowAccel == null) {
            Transform lowT = transform.Find("EngineAudio_Low");
            GameObject lowGO;
            if (lowT != null) {
                lowGO = lowT.gameObject;
            } else {
                lowGO = new GameObject("EngineAudio_Low");
                lowGO.transform.SetParent(transform, false);
            }

            m_LowAccel = lowGO.GetComponent<AudioSource>();
            if (m_LowAccel == null) m_LowAccel = lowGO.AddComponent<AudioSource>();
            ConfigureAudioSource(m_LowAccel, lowAccelClip);
        } else if (lowAccelClip != null && m_LowAccel.clip != lowAccelClip) {
            m_LowAccel.clip = lowAccelClip;
        }
    }

    private void ConfigureAudioSource(AudioSource source, AudioClip clip) {
        if (source == null) return;
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.loop = true;
        source.minDistance = 5f;
        source.maxDistance = maxRolloffDistance;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        if (clip != null) {
            source.clip = clip;
            if (clip.length > 0) {
                source.time = Random.Range(0f, clip.length);
            }
        }
    }

    private void FixedUpdate() {
        if (m_ForceStopped) return;
        if (GameManager.Instance != null && GameManager.Instance.currentState == GameState.GameOver) {
            if (m_StartedSound) StopSound();
            return;
        }

        if (Camera.main == null || stateMachine == null || stateMachine.engineController == null) return;
        if (lowAccelClip == null || highAccelClip == null) return;

        float camDist = (Camera.main.transform.position - transform.position).sqrMagnitude;

        if (m_StartedSound && camDist > maxRolloffDistance * maxRolloffDistance) {
            StopSound();
        }

        if (!m_StartedSound && camDist < maxRolloffDistance * maxRolloffDistance) {
            StartSound();
        }

        if (m_StartedSound) {
            CalculatePitch();
            CalculateEQ();
        }
    }

    void CalculateEQ() {
        if (lowPassFilter == null || midDistortion == null || highPassFilter == null) return;
        if (stateMachine.engineController == null || stateMachine.engineController.maxRPM <= 0) return;

        // 'Load' is throttle input (works for both forward and reverse)
        float targetLoad = Mathf.Clamp01(stateMachine.engineController.throttleInput);
        loadLerpIndex = Mathf.Lerp(loadLerpIndex, targetLoad, Time.deltaTime * eqLerpSpeed);

        // BAND 1: HIGHS (Low Pass Filter)
        if (lowPassCurve != null && lowPassFilter != null)
            lowPassFilter.cutoffFrequency = lowPassCurve.Evaluate(loadLerpIndex);

        // BAND 2: MIDS/GROWL (Distortion)
        if (distortionCurve != null && midDistortion != null)
            midDistortion.distortionLevel = distortionCurve.Evaluate(loadLerpIndex);

        // BAND 3: LOWS (High Pass Filter)
        if (highPassCurve != null && highPassFilter != null)
            highPassFilter.cutoffFrequency = highPassCurve.Evaluate(stateMachine.engineController.engineRPM / stateMachine.engineController.maxRPM);
    }

    void CalculatePitch() {
        if (m_LowAccel == null || m_HighAccel == null) return;
        if (stateMachine.engineController == null || stateMachine.engineController.maxRPM <= 0) return;

        float rpmNormalized = stateMachine.engineController.engineRPM / stateMachine.engineController.maxRPM;
        pitch = ULerp(lowPitchMin, lowPitchMax, rpmNormalized);
        pitch = Mathf.Min(lowPitchMax, pitch);

        m_LowAccel.pitch = pitch;
        m_HighAccel.pitch = pitch * highPitchMultiplier;

        float highFade = Mathf.InverseLerp(0.2f, 0.8f, rpmNormalized);
        float lowFade = 1 - highFade;

        highFade = 1 - ((1 - highFade) * (1 - highFade));
        lowFade = 1 - ((1 - lowFade) * (1 - lowFade));

        // Injecting a little extra volume boost when under load
        float loadVolumeBoost = Mathf.Lerp(0.7f, 1.0f, loadLerpIndex);
        m_LowAccel.volume = lowFade * loadVolumeBoost;
        m_HighAccel.volume = highFade * loadVolumeBoost;
    }

    private static float ULerp(float from, float to, float value) {
        return (1.0f - value) * from + value * to;
    }

    public void StartSound() {
        m_ForceStopped = false;
        InitAudioComponents();

        if (m_HighAccel != null && !m_HighAccel.isPlaying) {
            m_HighAccel.Play();
        }
        if (m_LowAccel != null && !m_LowAccel.isPlaying) {
            m_LowAccel.Play();
        }

        m_StartedSound = true;
    }

    public void StopSound() {
        if (m_LowAccel != null) {
            m_LowAccel.volume = 0f;
            m_LowAccel.Stop();
        }
        if (m_HighAccel != null) {
            m_HighAccel.volume = 0f;
            m_HighAccel.Stop();
        }
        m_StartedSound = false;
    }

    public void StopSoundInstant() {
        m_ForceStopped = true;
        StopSound();
    }
}