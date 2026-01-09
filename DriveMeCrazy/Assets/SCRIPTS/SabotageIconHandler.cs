using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class SabotageIconHandler : MonoBehaviour
{
    public GameObject _iconPrefab;

    public void ShowActionIcon(Transform seatAnchor, int icon = 0)
    {
        GameObject obj = Instantiate(_iconPrefab, seatAnchor);
        obj.transform.localPosition = Vector3.zero;
        
        //var sr = obj.GetComponent<SpriteRenderer>();
        //sr.sprite = icon;

        Destroy(obj, 2.0f); // auto-remove
    }
}
