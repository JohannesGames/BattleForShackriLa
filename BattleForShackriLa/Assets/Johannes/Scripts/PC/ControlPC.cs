using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class MovementMod
{
    public MovementMod(Vector3 direction, float length, bool fade, bool groundClear, bool gravReset)
    {
        modDirection = currentVector = direction;
        modLength = length;
        modFadesOut = fade;
        resetGravityWhileActive = gravReset;
        removeWhenGrounded = groundClear;
    }

    public Vector3 modDirection;
    public Vector3 currentVector;
    public float modLength;
    public bool modFadesOut;
    public bool removeWhenGrounded;
    public bool resetGravityWhileActive;
    public float modTimer = 0;
}

public class ControlPC : NetworkBehaviour
{
    // Animation
    private Animator anim;
    public AnimationCurve exponentialCurveUp;

    // Basic Movement
    [HideInInspector]
    public CharacterController cc;
    private Vector3 moveDirection;
    private float speed;
    [Header("Basic Movement")]
    public float baseSpeed;
    public float airBaseSpeed;
    public bool isGrounded;
    private bool isFalling;
    [HideInInspector]
    public List<MovementMod> movementModifiers = new List<MovementMod>();

    // Jumping
    [Header("Jumping")]
    public float jumpTimeLength = 1;
    public float jumpHeight = 2;
    private bool isJumping;
    private float jumpTimer = 0;

    // Movement abilities
    [HideInInspector]
    public JB_MovementAbility currentMovementAbility;
    [HideInInspector]
    public bool movedByAbility;

    // Rigidbody & Physics
    private bool wasStopped;
    public float maxVelocityChange = 10.0f;
    public float stoppingForce = 5;
    public float timeToMaxGravity = 2;
    public float gravity = 1;
    [HideInInspector]
    public float appliedGravity;

    // Camera
    [Header("Camera")]
    public Transform cameraContianer;
    [HideInInspector]
    public Camera cam;
    public Transform head;
    public float yRotationSpeed = 45;
    public float xRotationSpeed = 45;
    private float yRotation;
    private float xRotation;

    // UI
    [Header("UI")]
    public BaseHUD baseHud;

    // Transform and State syncvars
    //[SyncVar]
    //private bool valueChanged;

    //[SyncVar]
    //private Vector3 location;

    //[SyncVar]
    //private Quaternion rotation;

    [SyncVar]
    private int animState;

    private enum AnimationStates
    {
        Idle,
        Walking,
        Running,
        Jumping,
    }
    private AnimationStates pcAnimationState;

    // Stats
    [SyncVar(hook = "OnHealthChange")]
    public int health = 100;

    private NetworkStartPosition[] spawnPoints;

    // Weapons
    [Header("Weapons")]
    public LayerMask weaponLayermask;
    public bool firedPrimary;
    public ParticleSystem onHitParticle;


    // Debug
    [Header("Debug")]
    public Material lineMat;

    public int weaponDamage = 10;
    public float fireRate = 5; // per second
    private float fireTimer;

    void Start()
    {
        Application.runInBackground = true;
        anim = GetComponent<Animator>();
        cc = GetComponent<CharacterController>();
        currentMovementAbility = GetComponentInChildren<JB_MovementAbility>();
        if (currentMovementAbility) currentMovementAbility.pc = this;

        if (isLocalPlayer)
        {
            cameraContianer.GetChild(0).gameObject.SetActive(true);
            cam = cameraContianer.GetComponentInChildren<Camera>();
            Instantiate(baseHud);
            yRotation = transform.localEulerAngles.y;
            xRotation = cam.transform.localEulerAngles.x;
            wasStopped = true;
            fireTimer = 0;
            appliedGravity = gravity / 2;
            spawnPoints = FindObjectsOfType<NetworkStartPosition>();
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }


    void Update()
    {
        if (isLocalPlayer)
        {
            GetPlayerInput();
            AlternateMovePC();
            CmdCallSync(transform.position, transform.rotation, cc.velocity);

            if (Input.GetKeyDown(KeyCode.Escape))   //show cursor in editor
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            
        }
    }

    void FixedUpdate()
    {
        if (!isLocalPlayer)
        {
            return;
        }
        CheckForGround();
    }


    void GetPlayerInput()
    {
        // Keyboard input
        moveDirection = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
        moveDirection = transform.TransformDirection(moveDirection);
        speed = 0;
        if (Mathf.Abs(moveDirection.x) != 0 || Mathf.Abs(moveDirection.z) != 0)
        {
            if (Mathf.Abs(moveDirection.x) == 1 || Mathf.Abs(moveDirection.z) == 1)
            {
                wasStopped = false;
            }
            
            pcAnimationState = AnimationStates.Walking;
            speed = 1;

            if (Input.GetButton("Sprint"))  // if PC is sprinting
            {
                speed *= 2;
                pcAnimationState = AnimationStates.Running;
            }
        }
        anim.SetFloat("Speed", speed);

        // Movement Ability
        if (Time.time >= currentMovementAbility.cooldownTime && Input.GetMouseButtonDown(1))
        {
            currentMovementAbility.UseAbility(cam.transform.TransformDirection(Vector3.forward));
        }

        // Aerial
        if (movedByAbility && Input.GetButtonDown("Jump"))
        {
            currentMovementAbility.CancelAbility();
        }
        else if (isGrounded && !isJumping && Input.GetButtonDown("Jump"))
        {
            isJumping = true;
        }
        

        // Mouse input
        yRotation += Input.GetAxis("Mouse X") * yRotationSpeed * Time.deltaTime;
        xRotation -= Input.GetAxis("Mouse Y") * xRotationSpeed * Time.deltaTime;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        if (xRotation != cam.transform.eulerAngles.x || yRotation != transform.eulerAngles.y)
        {
            cam.transform.localEulerAngles = new Vector3(xRotation, 0, 0);
            transform.localEulerAngles = new Vector3(0, yRotation, 0);
        }

        WeaponFire();
    }

    void ResetGravity()
    {
        appliedGravity = 0;
    }

    void AlternateMovePC()
    {
        if (!movedByAbility)
        {
            if (isGrounded)
            {
                if (Mathf.Abs(moveDirection.x) != 0 || Mathf.Abs(moveDirection.z) != 0) // if there's some input
                {
                    moveDirection *= baseSpeed * speed;
                }
                else
                {
                    pcAnimationState = AnimationStates.Idle;
                }
            }
            else
            {
                if (Mathf.Abs(moveDirection.x) != 0 || Mathf.Abs(moveDirection.z) != 0) // if there's some input
                {
                    moveDirection *= airBaseSpeed * speed;
                }
            }

            ApplyJump();
            ApplyGravity();
            ApplyMovementModifiers();

            cc.Move(moveDirection * Time.deltaTime);
            moveDirection = Vector3.zero;
            if (cc.velocity == Vector3.zero) wasStopped = true;
        }

        ResetGravityFromModifier();
    }

    void ApplyJump()
    {
        if (isJumping)
        {
            jumpTimer += Time.deltaTime;
            moveDirection += Vector3.up * jumpHeight * (1 - (jumpTimer / jumpTimeLength));

            if (jumpTimer >= jumpTimeLength)
            {
                isJumping = false;
                appliedGravity = jumpTimer = 0;
            }
        }
    }

    void ApplyGravity()
    {
        if (!isGrounded)
        {
            if (!isFalling)
            {
                isFalling = true;
                movementModifiers.Add(new MovementMod(cc.velocity / 2, 1, true, true, false));
            }
        }
        if (!isJumping)
        {
            moveDirection += Vector3.down * appliedGravity;
            appliedGravity += gravity * Time.deltaTime;
        }

    }

    #region Movement Mods

    void ApplyMovementModifiers()   // applies movement modifiers (e.g. motion retained when walking over an edge, or from an explosion)
    {
        for (int i = movementModifiers.Count - 1; i > -1; i--)
        {
            movementModifiers[i].modTimer += Time.deltaTime;

            if (movementModifiers[i].modTimer >= movementModifiers[i].modLength)    // if the movement modifier has timed out
            {
                movementModifiers.RemoveAt(i);
            }
            else
            {
                if (movementModifiers[i].modFadesOut)   // if the mod force fades out over time reduce it's force
                {
                    movementModifiers[i].currentVector = movementModifiers[i].modDirection * (1 - movementModifiers[i].modTimer / movementModifiers[i].modLength);
                }

                moveDirection += movementModifiers[i].currentVector;
            }
        }
    }

    void ResetGravityFromModifier()
    {
        for (int i = movementModifiers.Count - 1; i > -1; i--)
        {
            if (movementModifiers[i].resetGravityWhileActive)
            {
                appliedGravity = 0;
                return;
            }
        }
    }

    void GroundClearMoveMods()
    {
        for (int i = movementModifiers.Count - 1; i > -1; i--)
        {
            if (movementModifiers[i].removeWhenGrounded)
            {
                movementModifiers.RemoveAt(i);
            }
        }
    }

#endregion

    #region RB Movement
    void MovePC()
    {
        if (CheckForGround())
        {
            if (Mathf.Abs(moveDirection.x) != 0 || Mathf.Abs(moveDirection.z) != 0) // if there's some input
            {
                Vector3 targetVelocity = new Vector3(moveDirection.x, 0, moveDirection.z);

                targetVelocity = transform.TransformDirection(targetVelocity);

                targetVelocity.Normalize();

                targetVelocity *= baseSpeed;

                if (pcAnimationState == AnimationStates.Running)
                {
                    targetVelocity *= 2;
                }

                Vector3 velocity = cc.velocity;
                Vector3 velocityChange = (targetVelocity - velocity);
                velocityChange.x = Mathf.Clamp(velocityChange.x, -maxVelocityChange, maxVelocityChange) * .8f;
                velocityChange.z = Mathf.Clamp(velocityChange.z, -maxVelocityChange, maxVelocityChange);
                velocityChange.y = 0;
                //rb.AddForce(velocityChange, ForceMode.VelocityChange);
            }

            //if (Mathf.Abs(moveDirection.x) < 1 && Mathf.Abs(moveDirection.z) < 1 && !wasStopped)
            //{
            //    rb.AddForce(-rb.velocity * stoppingForce, ForceMode.VelocityChange);
            //}

            //if (jumpPressed)
            //{
            //    jumpPressed = false;
            //    rb.velocity = new Vector3(rb.velocity.x, CalculateJumpVerticalSpeed(), rb.velocity.z);
            //}
        }
        else
        {
            if (Mathf.Abs(moveDirection.x) != 0 || Mathf.Abs(moveDirection.z) != 0) // if there's some input
            {
                Vector3 targetVelocity = new Vector3(moveDirection.x, 0, moveDirection.z);

                targetVelocity = transform.TransformDirection(targetVelocity);

                targetVelocity.Normalize();

                targetVelocity *= airBaseSpeed;

                //if (pcAnimationState == AnimationStates.Running)
                //{
                //    targetVelocity *= 2;
                //}

                //Vector3 velocity = rb.velocity;
                //Vector3 velocityChange = (targetVelocity - velocity);
                //velocityChange.x = Mathf.Clamp(velocityChange.x, -maxVelocityChange, maxVelocityChange) * .8f;
                //velocityChange.z = Mathf.Clamp(velocityChange.z, -maxVelocityChange, maxVelocityChange);
                //velocityChange.y = 0;
                //rb.AddForce(targetVelocity, ForceMode.Acceleration);
            }
        }

        //// Gravity
        //appliedGravity = gravity;
        //float grav = -appliedGravity * rb.mass;
        //rb.AddForce(new Vector3(0, grav, 0));
        ////

        //moveDirection = Vector3.zero;
        //if (rb.velocity == Vector3.zero) wasStopped = true;
    }
#endregion

    bool CheckForGround()
    {
        int layermask = 1 << 8;
        RaycastHit hit;
        if (Physics.SphereCast(transform.position + Vector3.up, (movedByAbility ? .4f : .6f), Vector3.down, out hit, .6f, layermask))
        {
            appliedGravity = gravity / 3;
            isFalling = false;
            GroundClearMoveMods();
            return isGrounded = true;
        }
        else
        {
            return isGrounded = false;
        }
    }

    float CalculateJumpVerticalSpeed()
    {
        // From the jump height and gravity we deduce the upwards speed 
        // for the character to reach at the apex.
        return Mathf.Sqrt(2 * jumpHeight * gravity);
    }

    void WeaponFire()
    {
        firedPrimary = false;
        fireTimer += Time.deltaTime;
        if (fireTimer >= 1 / fireRate && Input.GetButton("Fire1"))
        {
            fireTimer = 0;
            firedPrimary = true;
            
            Ray ray = cam.ScreenPointToRay(new Vector3(cam.pixelWidth / 2, cam.pixelHeight / 2, 0));
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 200, weaponLayermask))
            {
                if (hit.collider.gameObject.layer == 8) // if it's terrain
                {
                    print("terrain hit");
                }
                else    // if it's another PC
                {
                    print("hit player");

                    Instantiate(onHitParticle, hit.point, Quaternion.LookRotation(hit.normal));

                    ShootPC(hit.collider.gameObject, weaponDamage, hit.collider.gameObject.layer);
                   
                }
            }
        }

    }

    //[Command]
    public void ShootPC(GameObject hitPoint, int dmg, int layer)
    {
        if (hitPoint)
        {
            ControlPC _pc = hitPoint.GetComponent<HitboxLink>().pc;
            if (_pc)
            {
                print("PC hit");
                if (layer == 10)    // if it's a headshot
                {
                    _pc.TakeDamage(weaponDamage * 2);
                }
                else    // it's a body shot
                {
                    _pc.TakeDamage(weaponDamage);
                }
            }
            else
            {
                print("No PC");
            }
        }
    }

    void TakeDamage(int dmg)
    {
        health -= dmg;
        print("Took " + dmg + " damage");
    }

    void OnHealthChange(int newHealth)
    {
        health = newHealth;
        CheckHealth();
    }

    void CheckHealth()
    {
        if (health <= 0)
        {
            print("dead");
            CmdRespawn();
            CmdCallSync(transform.position, transform.rotation, cc.velocity);
        }
        baseHud.health.text = health.ToString();
    }

    [Command]
    void CmdRespawn()
    {
        Transform spawn = NetworkManager.singleton.GetStartPosition();
        GameObject newPlayer = (GameObject)Instantiate(NetworkManager.singleton.playerPrefab, spawn.position, spawn.rotation);
        NetworkServer.Destroy(this.gameObject);
        NetworkServer.ReplacePlayerForConnection(this.connectionToClient, newPlayer, this.playerControllerId);
    }

    Transform GetRespawnTransform()
    {
        return spawnPoints[Random.Range(0, spawnPoints.Length)].transform;
    }

    [Command]
    void CmdCallSync(Vector3 position, Quaternion rotation, Vector3 velocity)
    {
        RpcSyncClientPos(position, rotation, velocity);
    }

    [ClientRpc]
    void RpcSyncClientPos(Vector3 position, Quaternion rotation, Vector3 velocity)
    {
        if (!isLocalPlayer)
        {
            transform.position = position;
            transform.rotation = rotation;
            //if (cc) cc.velocity = velocity;
        }
    }
}
