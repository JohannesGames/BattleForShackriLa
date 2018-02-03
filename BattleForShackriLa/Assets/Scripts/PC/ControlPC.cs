using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class ControlPC : NetworkBehaviour
{
    // Animation
    private Animator anim;

    // Basic Movement
    private Vector2 moveDirection;
    private float speed;
    [Header("Basic Movement")]
    public float baseSpeed;
    public bool isGrounded;
    public float jumpHeight = 2;
    public float airMultiplier = .75f;
    private bool jumpPressed;

    // Rigidbody & Physics
    private Rigidbody rb;
    private bool wasStopped;
    public float maxVelocityChange = 10.0f;
    public float stoppingForce = 5;
    public float gravity = 1;
    private float appliedGravity;

    // Camera
    [Header("Camera")]
    public Transform cameraContianer;
    private Camera cam;
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
        rb = GetComponent<Rigidbody>();

        if (isLocalPlayer)
        {
            cameraContianer.GetChild(0).gameObject.SetActive(true);
            cam = cameraContianer.GetComponentInChildren<Camera>();
            Instantiate(baseHud);
            yRotation = transform.localEulerAngles.y;
            xRotation = cam.transform.localEulerAngles.x;
            wasStopped = true;
            fireTimer = 0;
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
            CmdCallSync(transform.position, transform.rotation, rb.velocity);

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

        MovePC();
    }


    void GetPlayerInput()
    {
        // Keyboard input
        moveDirection = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        speed = 0;
        if (Mathf.Abs(moveDirection.x) != 0 || Mathf.Abs(moveDirection.y) != 0)
        {
            if (Mathf.Abs(moveDirection.x) == 1 || Mathf.Abs(moveDirection.y) == 1)
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

        if (isGrounded && !jumpPressed && Input.GetButtonDown("Jump"))
        {
            jumpPressed = true;
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

    void MovePC()
    {
        CheckForGround();

        if (isGrounded)
        {
            if (Mathf.Abs(moveDirection.x) != 0 || Mathf.Abs(moveDirection.y) != 0) // if there's some input
            {
                Vector3 targetVelocity = new Vector3(moveDirection.x, 0, moveDirection.y);

                targetVelocity = transform.TransformDirection(targetVelocity);

                targetVelocity.Normalize();

                targetVelocity *= baseSpeed;

                if (pcAnimationState == AnimationStates.Running)
                {
                    targetVelocity *= 2;
                }

                Vector3 velocity = rb.velocity;
                Vector3 velocityChange = (targetVelocity - velocity);
                velocityChange.x = Mathf.Clamp(velocityChange.x, -maxVelocityChange, maxVelocityChange) * .8f;
                velocityChange.z = Mathf.Clamp(velocityChange.z, -maxVelocityChange, maxVelocityChange);
                velocityChange.y = 0;
                rb.AddForce(velocityChange, ForceMode.VelocityChange);
            }

            if (Mathf.Abs(moveDirection.x) < 1 && Mathf.Abs(moveDirection.y) < 1 && !wasStopped)
            {
                rb.AddForce(-rb.velocity * stoppingForce, ForceMode.VelocityChange);
            }

            if (jumpPressed)
            {
                jumpPressed = false;
                rb.velocity = new Vector3(rb.velocity.x, CalculateJumpVerticalSpeed(), rb.velocity.z);
            }
        }
        else
        {
            if (Mathf.Abs(moveDirection.x) != 0 || Mathf.Abs(moveDirection.y) != 0) // if there's some input
            {
                Vector3 targetVelocity = new Vector3(moveDirection.x, 0, moveDirection.y);

                targetVelocity = transform.TransformDirection(targetVelocity);

                targetVelocity.Normalize();

                targetVelocity *= baseSpeed * airMultiplier;

                if (pcAnimationState == AnimationStates.Running)
                {
                    targetVelocity *= 2;
                }

                Vector3 velocity = rb.velocity;
                Vector3 velocityChange = (targetVelocity - velocity);
                velocityChange.x = Mathf.Clamp(velocityChange.x, -maxVelocityChange, maxVelocityChange) * .8f;
                velocityChange.z = Mathf.Clamp(velocityChange.z, -maxVelocityChange, maxVelocityChange);
                velocityChange.y = 0;
                rb.AddForce(velocityChange * 10, ForceMode.Acceleration);
            }
        }

        // Gravity
        appliedGravity = gravity;
        float grav = -appliedGravity * rb.mass;
        rb.AddForce(new Vector3(0, grav, 0));
        //

        moveDirection = Vector2.zero;
        if (rb.velocity == Vector3.zero) wasStopped = true;
    }

    void CheckForGround()
    {
        int layermask = 1 << 8;
        RaycastHit hit;
        if (Physics.SphereCast(transform.position + Vector3.up, .6f, Vector3.down, out hit, .5f, layermask))
        {
            isGrounded = true;
        }
        else
        {
            isGrounded = false;
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
            CmdCallSync(transform.position, transform.rotation, rb.velocity);
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
            if (rb) rb.velocity = velocity;
        }
    }
}
