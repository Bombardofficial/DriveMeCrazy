using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CarSeatManager))]
public class PlayerManager : MonoBehaviour
{
    /* -------- singleton -------- */
    public static PlayerManager Instance { get; private set; }

    /* -------- public read-only -------- */
    public IReadOnlyList<Passenger> Passengers => _passengers;

    int _nextPlayerNumber = 1;
    public Passenger CurrentDriver { get; private set; }

    [Tooltip("Points awarded when you call AwardDriver().")]
    public int pointIncrement = 100;

    public static event Action<Passenger /*old*/, Passenger /*new*/> OnDriverChanged;

    /* -------- private -------- */
    readonly List<Passenger> _passengers = new();
    CarSeatManager _seats;

    /* ===== life-cycle ===== */
    void Awake()
    {
        if (Instance && Instance != this) { Destroy(this); return; }
        Instance = this;
        _seats = GetComponent<CarSeatManager>();
    }


    /* ===== public API ===== */
    public void RegisterPassenger(Passenger p, int seatIdx = -1)
    {
        if (!p || _passengers.Contains(p)) return;
        p.PlayerNumber = _nextPlayerNumber++;
        /* decide where in the list he goes -------------------- */
        seatIdx = (seatIdx < 0) ? _passengers.Count :          // append
                  Mathf.Clamp(seatIdx, 0, _passengers.Count);  // insert

        _passengers.Insert(seatIdx, p);
        ApplySeatPlacements();

        if (!CurrentDriver) SetDriver(p);
    }

    public void AwardDriver() => CurrentDriver?.IncrementPoints(pointIncrement);
    public void IncrementDriverPoints() => AwardDriver();      // alias

    public void SwapPassengers(int a, int b)
    {
        if (a == b) return;
        if (a < 0 || b < 0 || a >= _passengers.Count || b >= _passengers.Count) return;

        (_passengers[a], _passengers[b]) = (_passengers[b], _passengers[a]);
        ApplySeatPlacements();
    }

    public void SwapWithDriver(int idx)
    {
        if (idx < 0 || idx >= _passengers.Count) return;
        var newDriver = _passengers[idx];
        if (newDriver == CurrentDriver) return;

        int curIdx = _passengers.IndexOf(CurrentDriver);
        _passengers[curIdx] = newDriver;
        _passengers[idx] = CurrentDriver;

        ApplySeatPlacements();
        SetDriver(newDriver);
    }

    public int GetSeatIndex(Passenger p) => _passengers.IndexOf(p);

    /* ===== helpers ===== */
    void ApplySeatPlacements()
    {
        int seatCnt = _seats.SeatCount;
        for (int i = 0; i < _passengers.Count; ++i)
        {
            Transform seat = _seats.GetSeat(i % seatCnt);
            var t = _passengers[i].transform;
            t.SetPositionAndRotation(seat.position, seat.rotation);
            t.SetParent(seat, true);
        }
    }

    void SetDriver(Passenger newDriver)
    {
        var old = CurrentDriver;
        CurrentDriver = newDriver;
        OnDriverChanged?.Invoke(old, newDriver);
    }
}
