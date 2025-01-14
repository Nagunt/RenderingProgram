using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Model_Truck : MonoBehaviour
{
    public Transform[] wheels;

    // Update is called once per frame
    void Update()
    {
        foreach (Transform wheel in wheels) {
            wheel.Rotate(Vector3.right, 5f);
        }
    }
}
