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
    float pointMultiplier = 1f;

    public static event Action<Passenger /*old*/, Passenger /*new*/> OnDriverChanged;

    /* -------- private -------- */
    readonly List<Passenger> _passengers = new();
    CarSeatManager _seats;
    public Passenger _lastSabotage;

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

    public void AwardDriver() => CurrentDriver?.IncrementPoints((int)(pointIncrement * pointMultiplier));
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

    public void SwapDriver()
    {
        int curNum = CurrentDriver.PlayerNumber;
        int nextNum = (curNum + 1) % _passengers.Count;

        if (nextNum < 0 || nextNum >= _passengers.Count) return;
        var newDriver = GetPassengerByNumber(nextNum);
        if (newDriver == CurrentDriver) return;
        int nextDriverIdx = _passengers.IndexOf(newDriver);
        int oldDriverIdx = _passengers.IndexOf(CurrentDriver);
        _passengers[oldDriverIdx] = newDriver;
        _passengers[nextDriverIdx] = CurrentDriver;

        ApplySeatPlacements();
        SetDriver(newDriver);
    }

    public void SwapDriverSabotage()
    {
        var newDriver = _lastSabotage;
        if (newDriver == CurrentDriver) return;
        int nextDriverIdx = _passengers.IndexOf(newDriver);
        int oldDriverIdx = _passengers.IndexOf(CurrentDriver);
        _passengers[oldDriverIdx] = newDriver;
        _passengers[nextDriverIdx] = CurrentDriver;

        ApplySeatPlacements();
        SetDriver(newDriver);
    }

    public int GetSeatIndex(Passenger p) => _passengers.IndexOf(p);

    public void SetPointMultiplier(float multiplier) => pointMultiplier = multiplier;
    public float PointMultiplier => pointMultiplier;

    public void AwardAllPassengers(int amount)
    {
        foreach (Passenger passenger in _passengers)
        {
            passenger.IncrementPoints(amount);
        }
    }

    public Passenger GetPassengerByNumber(int number)
    {
        foreach (Passenger passenger in _passengers)
        {
            if (passenger.PlayerNumber == number)
                return passenger;
        }

        return new Passenger();
    }

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
