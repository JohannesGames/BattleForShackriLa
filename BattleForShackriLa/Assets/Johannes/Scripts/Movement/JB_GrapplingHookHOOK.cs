using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JB_GrapplingHookHOOK : MonoBehaviour
{
    public Rigidbody rb;
    public JB_GrapplingHookMA sender;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == 8)    // if it hits terrain
        {
            rb.velocity = Vector3.zero;
            transform.parent = other.transform;
            sender.HookImpactTerrain();
        }
    }
}
