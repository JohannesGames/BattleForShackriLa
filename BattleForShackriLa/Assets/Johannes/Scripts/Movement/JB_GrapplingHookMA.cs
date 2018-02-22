using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JB_GrapplingHookMA : JB_MovementAbility
{
    // Firing
    [Header("Firing")]
    public float maxTimeFired = 3;  // how long before it must be retracted
    private float firingTimer;
    public float firingForce;
    private bool hasFired;
    private Vector3 hookDirection;
    public JB_GrapplingHookHOOK hookPrefab;
    private JB_GrapplingHookHOOK firedHook;

    // Impacting
    private bool hasMadeContact;
    public float travelSpeedPC;
    private float appliedTravelSpeed;
    public float cancellationForce = 10;

    void Start()
    {

    }


    void Update()
    {
        if (hasFired)   // once it has been fired
        {
            if (hasMadeContact) // once it has hit something (e.g. terrain or player)
            {

            }
            else
            {
                firingTimer += Time.deltaTime;
                if (firingTimer >= maxTimeFired)
                {
                    Destroy(hookPrefab);
                }
            }
        }
    }

    void FixedUpdate()
    {
        
    }

    public override void UseAbility(Vector3 direction)
    {
        if (firedHook) Destroy(firedHook.gameObject);
        firedHook = Instantiate(hookPrefab, pc.cam.transform.position + direction, Quaternion.identity);
        firedHook.sender = this;
        firedHook.rb.velocity = direction * firingForce;
        firedHook.direction = direction;
        cooldownTime = Time.time + abilityCooldown;
        hasMadeContact = false;
    }

    public void HookImpactTerrain()
    {
        pc.movedByAbility = true;
        hasMadeContact = true;
        StartCoroutine("MoveToHook");
    }

    IEnumerator MoveToHook()
    {
        appliedTravelSpeed = travelSpeedPC / 2;
        Vector3 heading = firedHook.transform.position - pc.transform.position;
        float distance = heading.magnitude;
        hookDirection = heading / distance;
        while (heading.sqrMagnitude > 2)
        {
            pc.cc.Move(hookDirection * appliedTravelSpeed * Time.deltaTime);
            heading = firedHook.transform.position - pc.transform.position;
            distance = heading.magnitude;
            hookDirection = heading / distance;
            if (appliedTravelSpeed < travelSpeedPC) appliedTravelSpeed += Time.deltaTime;
            yield return null;
        }
        if (firedHook) Destroy(firedHook.gameObject);
        pc.movementModifiers.Add(new MovementMod(hookDirection + Vector3.up * cancellationForce, .5f, true, false, true));
        pc.appliedGravity = 0;
        pc.movedByAbility = false;
    }

    public override void CancelAbility()
    {
        StopCoroutine("MoveToHook");
        if (firedHook) Destroy(firedHook.gameObject);
        pc.movementModifiers.Add(new MovementMod(hookDirection * 4 + Vector3.up * cancellationForce, 2, true, false, false));
        pc.appliedGravity = 0;
        pc.movedByAbility = false;
    }
}
