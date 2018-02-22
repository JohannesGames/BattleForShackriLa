using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JB_GrapplingHookHOOK : MonoBehaviour
{
    public Rigidbody rb;
    public JB_GrapplingHookMA sender;
    public LayerMask layersToHit;
    [HideInInspector]
    public Vector3 direction;
    private bool destinationFound;

    void FixedUpdate()
    {
        if (!destinationFound)
        {
            CheckForObjectAhead();
        }
    }

    void CheckForObjectAhead()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, direction, out hit, 1, layersToHit))  // if it's about to hit terrain or a player
        {
            rb.velocity = Vector3.zero;
            transform.position = hit.point + hit.normal / 10;
            destinationFound = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == 8)    // if it hits terrain
        {
            destinationFound = true;
            rb.velocity = Vector3.zero;
            transform.parent = other.transform;
            sender.HookImpactTerrain();
        }
    }
}
