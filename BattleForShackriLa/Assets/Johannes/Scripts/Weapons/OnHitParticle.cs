using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OnHitParticle : MonoBehaviour
{
    void Start()
    {
        Destroy(this.gameObject, .6f);
    }
}
