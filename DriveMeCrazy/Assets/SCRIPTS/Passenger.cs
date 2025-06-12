using UnityEngine;

/// A player avatar that can earn points.
/// *No* seat bookkeeping lives here – that’s the manager’s job.
public class Passenger : MonoBehaviour
{
    int _points;
    public int Points => _points;

    public void IncrementPoints(int amount) => _points += amount;

    void Start() => _points = 0;
}
