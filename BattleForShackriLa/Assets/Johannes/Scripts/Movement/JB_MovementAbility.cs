using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class JB_MovementAbility : MonoBehaviour
{
    [HideInInspector]
    public ControlPC pc;

    public float abilityCooldown;
    [HideInInspector]
    public float cooldownTime;

    public abstract void UseAbility(Vector3 direction);

    public abstract void CancelAbility();
}
