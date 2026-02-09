using UnityEngine;

public class bilboard : MonoBehaviour
{
    public Transform cam;


    // Update is called once per frame
    void LateUpdate()
    {
        transform.LookAt(transform.position + cam.forward);
    }
}
