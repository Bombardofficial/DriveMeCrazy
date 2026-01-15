using UnityEngine;
using TMPro;
using System;

public class PopupText : MonoBehaviour
{
    [Header("Parameters")]
    [Tooltip("Duration of the Popup")]
    public float lifetime = 1f;

    [Tooltip("Movement length of the Popup")]
     public float riseDistance = 80f;

    [Tooltip("Rotation of the Popup")]
    public float maxRotation = 50f;

    [Tooltip("Side drift of the Popup")]
    public float sideDrift = 50f;

    private TextMeshProUGUI text;
    private float timer;
    private Vector3 startPos;
    private Vector3 driftDir;
    private float rotationSpeed;
    private Vector3 startScale;

    void Awake()
    {
        text = GetComponent<TextMeshProUGUI>();
        startPos = transform.localPosition;
    }

    public void Init(string message, Color color)
    {
        text.text = message;
        text.color = color;
        text.alpha = 1f;
        timer = lifetime;

        startScale = transform.localScale;

        startPos = transform.localPosition +
                   new Vector3(
                       UnityEngine.Random.Range(-50f, 50f),
                       UnityEngine.Random.Range(-15f, 15f),
                       0f
                   );
        driftDir = new Vector3(
            UnityEngine.Random.Range(-1f, 1f),
            1f,
            0f
        ).normalized;
        rotationSpeed = UnityEngine.Random.Range(maxRotation/4f, maxRotation) * Math.Sign(driftDir.x) * -1f;
    }

    void Update()
    {
        timer -= Time.deltaTime;
        float t = 1f - (timer / lifetime);

        // Curved upward motion
        Vector3 movement =
            Vector3.up * riseDistance * t +
            Vector3.right * driftDir.x * sideDrift * t * t;

        transform.localPosition = startPos + movement;

        // Rotation
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

        // Fade out
        transform.localScale = startScale * (1f -  (0.5f * t * t));
        text.alpha = 1f - t;

        if (timer <= 0f)
            Destroy(gameObject);
    }
}
