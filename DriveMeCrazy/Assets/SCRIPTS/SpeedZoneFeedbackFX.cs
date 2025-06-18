using UnityEngine;
using Cinemachine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;
namespace ArcadeVP
{
    /* Attach this to ANY GameObject (camera rig is cleanest) */
    public class SpeedZoneFeedbackFX : MonoBehaviour
    {
        [Header("Links")]
        public CinemachineVirtualCamera vcam;
        public SpeedZoneBalanceUI gauge;
        public Volume postVolume;                  // Global URP volume with a Vignette

        [Header("Tuning")]
        public float fovBoost = 3f;           // +FOV at perfect centre
        public float maxVignette = 0.45f;        // danger vignette at edges
        public float maxRumble = 0.8f;         // game-pad motor strength

        LensDistortion lens;
        ChromaticAberration chroma;

        [Header("Danger cue")]
        public float dangerCA = 0.25f;   // chromatic aberration while outside
        public float dangerLens = -0.10f;  // barrel distortion while outside

        [Header("Crash flash")]
        public float crashCA = 1.0f;
        public float crashLens = -0.45f;
        public float crashFade = .35f;  // seconds

        public void CrashPulse() => StartCoroutine(DoCrashPulse());
        /* ­­­–––––––– interneals –––––––– */
        Vignette vig;
        float baseFov;
        bool activeLastFrame;

        void Awake()
        {
            if (!vcam) vcam = GetComponentInChildren<CinemachineVirtualCamera>();
            if (!postVolume) postVolume = FindObjectOfType<Volume>();

            baseFov = vcam ? vcam.m_Lens.FieldOfView : 60f;

            if (postVolume && postVolume.profile.TryGet(out vig))
                vig.intensity.value = 0f;

            if (postVolume.profile.TryGet(out chroma)) chroma.intensity.value = 0;
            if (postVolume.profile.TryGet(out lens)) lens.intensity.value = 0;
        }

        void Update()
        {
            if (!PlayerJoinManager.IsRaceStarted)   // ? post-race? turn off
            {
                if (activeLastFrame) ResetAllFX();
                activeLastFrame = false;
                return;
            }

            bool gaugeActive = gauge && gauge.gameObject.activeSelf;

            if (!gaugeActive)
            {
                if (activeLastFrame) ResetAllFX();
                activeLastFrame = false;
                return;
            }
            activeLastFrame = true;

            float err = Mathf.Clamp01(gauge.NormalizedError); // 0 centre, 1 edge
            float focus = 1f - err;                             // 1 centre

            /* ---------- Camera FOV ---------- */
            if (vcam)
                vcam.m_Lens.FieldOfView = baseFov + fovBoost * focus;

            /* ---------- Vignette ---------- */
            if (vig)
            {
                vig.color.value = Color.red;
                vig.intensity.value = maxVignette * err * err; // softer rise
            }
            if (chroma && lens)
            {
                float cue = gauge.NormalizedError <= 1f ? 0 :     // inside -> none
                             Mathf.Clamp01(gauge.NormalizedError - 1f);  // >1 ..2
                chroma.intensity.value = cue * dangerCA;
                lens.intensity.value = cue * dangerLens;
            }
            /* ---------- Game-pad rumble ---------- */
            if (Gamepad.current != null)
            {
                float motor = maxRumble * err;
                Gamepad.current.SetMotorSpeeds(motor, motor);
            }
        }

        void OnDisable() => ResetAllFX();

        void ResetAllFX()
        {
            if (vcam) vcam.m_Lens.FieldOfView = baseFov;
            if (vig) vig.intensity.value = 0f;
            if (Gamepad.current != null) Gamepad.current.SetMotorSpeeds(0f, 0f);
        }

        IEnumerator DoCrashPulse()
        {
            if (chroma) chroma.intensity.value = crashCA;
            if (lens) lens.intensity.value = crashLens;

            float t = 0;
            while (t < crashFade)
            {
                t += Time.deltaTime;
                float k = 1f - t / crashFade;
                if (chroma) chroma.intensity.value = crashCA * k;
                if (lens) lens.intensity.value = crashLens * k;
                yield return null;
            }

            if (chroma) chroma.intensity.value = 0;
            if (lens) lens.intensity.value = 0;
        }
    }
}
