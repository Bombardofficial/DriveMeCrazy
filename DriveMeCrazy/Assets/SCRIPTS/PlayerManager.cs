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

    [Header("Player Colors (by PlayerNumber)")]
    [Tooltip("Index 0 => Player 1, Index 1 => Player 2, etc.")]
    [SerializeField]
    private Color[] playerColors = new Color[4]
    {
        new Color(0.2f, 1f, 0.2f),   // P1 green-ish
        new Color(1f, 0.9f, 0.2f),   // P2 yellow-ish
        new Color(0.2f, 0.8f, 1f),   // P3 cyan-ish
        new Color(1f, 0.2f, 0.8f)    // P4 magenta-ish
    };

    [Tooltip("Optional: force a shader property if your material doesn't use _BaseColor/_Color.")]
    [SerializeField] private string forcedColorProperty = "";
    public string ForcedColorProperty => forcedColorProperty;



    [Tooltip("Points awarded when you call AwardDriver().")]
    public int pointIncrement = 100;
    float pointMultiplier = 1f;

    
    [Tooltip("Drag the GameObject with the PopupTextManager script here.")]
    [SerializeField] private PopupTextManager popupTextManager;

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
        ApplyColorForPassenger(p);
        /* decide where in the list he goes -------------------- */
        seatIdx = (seatIdx < 0) ? _passengers.Count :          // append
                  Mathf.Clamp(seatIdx, 0, _passengers.Count);  // insert

        _passengers.Insert(seatIdx, p);
        ApplySeatPlacements();

        if (!CurrentDriver) SetDriver(p);
    }

    public void AwardDriver() { 
        popupTextManager.Show($"{(int)(pointIncrement * pointMultiplier)}", popupTextManager.pointsColor);
        CurrentDriver?.IncrementPoints((int)(pointIncrement * pointMultiplier));
    }
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
        if (newDriver == null)
            if (_passengers.Count <= 1)
                newDriver = CurrentDriver;
            else
                newDriver = _passengers[UnityEngine.Random.Range(1, _passengers.Count - 1)];
        
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

    public void ShowDamageDone(float damagePercent)
    {
        popupTextManager.Show($"{damagePercent}%", popupTextManager.damageColor);
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

    public Color GetColorForPlayerNumber(int playerNumber)
    {
        int idx = playerNumber - 1;
        if (playerColors == null || idx < 0 || idx >= playerColors.Length)
            return Color.white;

        return playerColors[idx];
    }

    void ApplyColorForPassenger(Passenger p)
    {
        if (!p) return;

        Color c = GetColorForPlayerNumber(p.PlayerNumber);
        string prop = string.IsNullOrEmpty(forcedColorProperty) ? null : forcedColorProperty;

        p.ApplyPlayerColor(c, prop);
    }

    [ContextMenu("Reapply Player Colors")]
    public void ReapplyPlayerColors()
    {
        foreach (var p in _passengers)
            ApplyColorForPassenger(p);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (playerColors == null) playerColors = new Color[4];
        if (playerColors.Length != 4) System.Array.Resize(ref playerColors, 4);
    }
#endif

}
