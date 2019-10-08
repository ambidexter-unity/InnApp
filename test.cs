using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class test : MonoBehaviour
{
    void Start()
    {
        SaveProductToPlayerPrefs();
    }

    private void SaveProductToPlayerPrefs(params string[] keys)
    {
        Debug.Log(keys.Length);
    }
}