using UnityEngine;

public class CamProxyStabilizer : MonoBehaviour
{
    [Header("Targets")]
    public Transform car;            // a kocsi transformja
    public Transform lookAtProxy;    // CamLookAtProxy

    [Header("Follow proxy")]
    public Vector3 followWorldOffset = Vector3.zero;
    [Tooltip("Csak a Y-t simítja (általában ez kell).")]
    public bool smoothYOnly = true;
    [Tooltip("0.06–0.18 tipikus. Kisebb = feszesebb.")]
    public float followSmoothTime = 0.10f;

    [Header("LookAt proxy")]
    [Tooltip("A kocsi local space-ében elõre/fejmagasságra nézzen.")]
    public Vector3 lookAtLocalOffset = new Vector3(0f, 1.0f, 4.0f);
    [Tooltip("0.08–0.25 tipikus. Kicsit lazább lehet, mint a follow.")]
    public float lookAtSmoothTime = 0.16f;

    [Tooltip("Ha true: LookAt Y fixen a kocsi Y+offset (nem ugrál a talajnormáloktól).")]
    public bool lockLookAtY = true;
    public float lookAtYFromCarPlus = 1.0f;

    float _followYVel;
    Vector3 _lookVel;

    void LateUpdate()
    {
        if (!car) return;

        // Pause alatt fagyjon a kamera-proxy is (ne simítson tovább)
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // --------- FOLLOW PROXY (position) ----------
        Vector3 desiredFollow = car.position + followWorldOffset;

        Vector3 newFollow;
        if (smoothYOnly)
        {
            float y = Mathf.SmoothDamp(transform.position.y, desiredFollow.y, ref _followYVel, followSmoothTime, Mathf.Infinity, dt);
            newFollow = new Vector3(desiredFollow.x, y, desiredFollow.z);
        }
        else
        {
            // Ha valaha kell full smoothing:
            newFollow = Vector3.Lerp(transform.position, desiredFollow, 1f - Mathf.Exp(-12f * dt));
        }

        transform.position = newFollow;

        // --------- LOOKAT PROXY (stabil aim) ----------
        if (lookAtProxy)
        {
            Vector3 desiredLook = car.TransformPoint(lookAtLocalOffset);

            if (lockLookAtY)
                desiredLook.y = car.position.y + lookAtYFromCarPlus;

            // lookAt simítás (kicsit “puhább”)
            lookAtProxy.position = Vector3.SmoothDamp(lookAtProxy.position, desiredLook, ref _lookVel, lookAtSmoothTime, Mathf.Infinity, dt);
        }
    }
}
