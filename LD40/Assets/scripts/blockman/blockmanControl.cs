using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityStandardAssets.CrossPlatformInput;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class blockmanControl : MonoBehaviour {

    [SerializeField] float m_GroundCheckDistance = 1.5f;
    [SerializeField] float m_MovingTurnSpeed = 360;
    [SerializeField] float m_StationaryTurnSpeed = 180;
    [SerializeField] float m_JumpPower = 12f;
    [Range(1f, 4f)] [SerializeField] float m_GravityMultiplier = 1f;
    [SerializeField] float m_MoveSpeedMultiplier = 0.5f;
    [SerializeField] Vector3 currentVelocity;
    [SerializeField] Vector3 currentRotatedVelocity;
    public ParticleSystem ps;
    public Canvas menu;
    public Canvas intro;

    const float k_Half = 0.5f;
    // body
    float m_CapsuleHeight;
    Vector3 m_CapsuleCenter;
    CapsuleCollider m_Capsule;
    Rigidbody m_Rigidbody;
    // movement and jumping
    [SerializeField]  bool m_IsGrounded;
    float m_OrigGroundCheckDistance;
    float m_TurnAmount;
    float m_ForwardAmount;
    Vector3 m_GroundNormal;
    private bool m_Jump;                      // the world-relative desired move direction, calculated from the camForward and user input.

    // camera
    private Transform m_Cam;                  // A reference to the main camera in the scenes transform
    private Vector3 m_CamForward;             // The current forward direction of the camera
    public Vector3 m_Move;

    public void QuitGame()
    {
        Application.Quit();
    }

    public void replay()
    {
        var li = ps.lights;
        li.intensityMultiplier = 0.5f;
        mothScript.MoveSpeed = 1;
        SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex);
    }
       
    public void closeIntro()
    {
        intro.enabled = false;
    }

    void Start()
    {
        m_Rigidbody = GetComponent<Rigidbody>();
        m_Capsule = GetComponent<CapsuleCollider>();
        m_CapsuleHeight = m_Capsule.height;
        m_CapsuleCenter = m_Capsule.center;
        m_Rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        m_OrigGroundCheckDistance = m_GroundCheckDistance;
        // get the transform of the main camera
        if (Camera.main != null)
        {
            m_Cam = Camera.main.transform;
        }
        else
        {
            Debug.LogWarning( "Warning: no main camera found. Third person character needs a Camera tagged \"MainCamera\", for camera-relative controls.", gameObject);
        }
        menu.enabled = false;
    }

    private void Update()
    {
        if (!m_Jump)
        {
            m_Jump = CrossPlatformInputManager.GetButtonDown("Jump");
        }
    }


    // Fixed update is called in sync with physics
    private void FixedUpdate()
    {
        if (!intro.enabled)
        {
            // read inputs
            float h = CrossPlatformInputManager.GetAxis("Horizontal");
            float v = CrossPlatformInputManager.GetAxis("Vertical");
            bool crouch = Input.GetKey(KeyCode.C);

            // calculate move direction to pass to character
            if (m_Cam != null)
            {
                // calculate camera relative direction to move:
                m_CamForward = Vector3.Scale(m_Cam.forward, new Vector3(1, 0, 1)).normalized;
                m_Move = v * m_CamForward + h * m_Cam.right;
            }
            else
            {
                // we use world-relative directions in the case of no main camera
                m_Move = v * Vector3.forward + h * Vector3.right;
            }
#if !MOBILE_INPUT
            // walk speed multiplier
            if (Input.GetKey(KeyCode.LeftShift)) m_Move *= 0.5f;
#endif

            // pass all parameters to the character control script
            Move(m_Move, crouch, m_Jump);
            m_Jump = false;
        }
    }

    public void Move(Vector3 move, bool crouch, bool jump)
    {

        // convert the world relative moveInput vector into a local-relative
        // turn amount and forward amount required to head in the desired
        // direction.
        if (move.magnitude > 1f) move.Normalize();
        move = transform.InverseTransformDirection(move);
        CheckGroundStatus();
        move = Vector3.ProjectOnPlane(move, m_GroundNormal);
        m_TurnAmount = Mathf.Atan2(move.x, move.z);
        m_ForwardAmount = move.z;

        ApplyExtraTurnRotation();

        // control and velocity handling is different when grounded and airborne:
        if (m_IsGrounded)
        {
            HandleGroundedMovement(crouch, jump);
        }
        else
        {
            HandleAirborneMovement();
        }
        // no crouching for blockman!
        //ScaleCapsuleForCrouching(crouch);
        //PreventStandingInLowHeadroom();
        UpdateAnimator(move);
        currentVelocity = m_Rigidbody.velocity;

    }

    void UpdateAnimator(Vector3 move)
    {
        //if (m_IsGrounded && Time.deltaTime > 0)
        if (Time.deltaTime > 0)
        {
            move = m_Rigidbody.transform.rotation * move;
            currentRotatedVelocity = move;
            Vector3 v = (move * m_MoveSpeedMultiplier) / Time.deltaTime;

            // we preserve the existing y part of the current velocity.
            v.y = m_Rigidbody.velocity.y;
            m_Rigidbody.velocity = v;
        }
    }


    void CheckGroundStatus()
    {
        RaycastHit hitInfo;
        #if UNITY_EDITOR
        // helper to visualise the ground check ray in the scene view
        Debug.DrawLine(transform.position + (Vector3.up * 0.1f), transform.position + (Vector3.up * 0.1f) + (Vector3.down * m_GroundCheckDistance));
        #endif
        // 0.1f is a small offset to start the ray from inside the character
        // it is also good to note that the transform position in the sample assets is at the base of the character
        if (Physics.Raycast(transform.position + (Vector3.up * 0.1f), Vector3.down, out hitInfo, m_GroundCheckDistance))
        {
            m_GroundNormal = hitInfo.normal;
            m_IsGrounded = true;
        }
        else
        {
            m_IsGrounded = false;
            m_GroundNormal = Vector3.up;
        }
    }

    void ApplyExtraTurnRotation()
    {
        float turnSpeed = Mathf.Lerp(m_StationaryTurnSpeed, m_MovingTurnSpeed, m_ForwardAmount);
        transform.Rotate(0, m_TurnAmount * turnSpeed * Time.deltaTime, 0);
    }

    void HandleGroundedMovement(bool crouch, bool jump)
    {
        // check whether conditions are right to allow a jump:
        if (jump && !crouch)
        {
            // jump!
            m_Rigidbody.velocity = new Vector3(m_Rigidbody.velocity.x, m_JumpPower, m_Rigidbody.velocity.z);
            m_IsGrounded = false;
            m_GroundCheckDistance = 1.5f;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("powerup"))
        {

            var li = ps.lights;
            li.intensityMultiplier += 0.5f;
            //other.gameObject.SetActive(false);
            Destroy(other.gameObject);
            mothScript.MoveSpeed += 3;
        }
        if (other.gameObject.CompareTag("moth"))
        {
            var li = ps.lights;
            li.intensityMultiplier -= 0.3f;
            var pos = other.gameObject.transform.position;
            var rot = other.gameObject.transform.rotation;
            pos.x += Random.Range(15f, 25f);
            pos.y += Random.Range(15f, 25f);
            pos.z += Random.Range(15f, 25f);
            other.gameObject.transform.SetPositionAndRotation(pos, rot); 
        }
        if (GameObject.FindGameObjectsWithTag("powerup").Length <= 0)
        {
            menu.enabled = true;
        }
    }

    void HandleAirborneMovement()
    {
        // apply extra gravity from multiplier:
        Vector3 extraGravityForce = (Physics.gravity * m_GravityMultiplier) - Physics.gravity;
        m_Rigidbody.AddForce(extraGravityForce);
        m_GroundCheckDistance = m_Rigidbody.velocity.y < 0 ? m_OrigGroundCheckDistance : 1.5f;
    }

}
