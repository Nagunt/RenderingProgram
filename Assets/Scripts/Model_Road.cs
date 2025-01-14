using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Model_Road : MonoBehaviour
{
    public GameObject line;

    // Start is called before the first frame update
    void Start()
    {
        BuildLine();
    }

    void BuildLine()
    {
        for(int i = 1; i <= 250; ++i) {
            GameObject newObject = Instantiate(line, transform);
            newObject.transform.localPosition = new Vector3(line.transform.position.x, line.transform.position.y, 10 * i);
            GameObject newObject2 = Instantiate(line, transform);
            newObject2.transform.localPosition = new Vector3(line.transform.position.x, line.transform.position.y, -10 * i);
        }
    }
}
