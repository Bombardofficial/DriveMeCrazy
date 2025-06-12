using UnityEngine;

/// Supplies seat transforms to PlayerManager.
/// If `seats` is left empty in the Inspector we fall back to the
///   direct children of this object (excluding the root itself).
public class CarSeatManager : MonoBehaviour
{
    [Tooltip("Leave empty to auto-grab the first-level children.")]
    public Transform[] seats;

    void Awake()
    {
        if (seats == null || seats.Length == 0)
        {
            var list = new System.Collections.Generic.List<Transform>();
            foreach (Transform t in transform)         // direct children only
                list.Add(t);
            seats = list.ToArray();
        }
    }

    public int SeatCount => seats?.Length ?? 0;

    public Transform GetSeat(int i)
    {
        if (SeatCount == 0)
        {
            Debug.LogWarning("[CarSeatManager] No seats defined – using root.");
            return transform;
        }
        i = Mathf.Clamp(i, 0, SeatCount - 1);
        return seats[i] ? seats[i] : transform;        // never return null
    }
}
