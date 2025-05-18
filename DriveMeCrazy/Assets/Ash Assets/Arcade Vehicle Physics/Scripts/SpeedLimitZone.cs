// SpeedLimitZone.cs  ��replace the whole file with this
using UnityEngine;

namespace ArcadeVP
{
    [RequireComponent(typeof(Collider))]
    public class SpeedLimitZone : MonoBehaviour
    {
        [Tooltip("m/s  (60?km/h  ?�16.7)")]
        public float speedLimit = 15f;
        public Sprite signSprite;

        public string targetTag = "Driver";

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(targetTag)) return;

            var car = other.attachedRigidbody ?
                      other.attachedRigidbody.GetComponent<ArcadeVehicleController>() :
                      other.GetComponent<ArcadeVehicleController>();

            if (car) car.EnterSpeedLimit(speedLimit, signSprite, false);
        }
    }
}
