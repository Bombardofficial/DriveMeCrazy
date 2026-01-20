using UnityEngine;

public class HatEquipper : MonoBehaviour
{
    public Transform hatSocket;
    public GameObject hatPrefab;

    private GameObject currentHat;

    void Start()
    {
        Debug.Log($"[HatEquipper] Start on {gameObject.name}. Socket={hatSocket?.name}, Prefab={hatPrefab?.name}");
        EquipHat();
    }

    public void EquipHat()
    {
        if (hatPrefab == null) 
        { 
            Debug.LogWarning("[HatEquipper] hatPrefab is NULL"); 
            return; 
        }

        if (hatSocket == null) 
        { 
            Debug.LogWarning("[HatEquipper] hatSocket is NULL"); 
            return; 
        }

        //if (hatPrefab == null || hatSocket == null)
        //    return;

        if (currentHat != null)
            Destroy(currentHat);

        currentHat = Instantiate(hatPrefab, hatSocket);

        var rs = currentHat.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rs)
        {
            r.SetPropertyBlock(null); // clears tint from MaterialPropertyBlock
        }

        currentHat.name = "EquippedHat_RUNTIME";
        Debug.Log("[HatEquipper] Hat instantiated: " + currentHat.name);

        currentHat.transform.localPosition = Vector3.zero;
        currentHat.transform.localRotation = Quaternion.identity;
        currentHat.transform.localScale = Vector3.one;
    }
}
