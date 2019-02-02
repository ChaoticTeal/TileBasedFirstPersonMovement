using System;
using System.Collections;
using UnityEngine;
using UnityStandardAssets.CrossPlatformInput;
using UnityStandardAssets.Utility;
using Random = UnityEngine.Random;

[RequireComponent(typeof (CharacterController))]
[RequireComponent(typeof (AudioSource))]
public class GridFirstPersonController : MonoBehaviour
{
    #region FPCVars
    [SerializeField] private float m_WalkSpeed;
    [SerializeField] private float m_StickToGroundForce;
    [SerializeField] private float m_GravityMultiplier;
    [SerializeField] private UnityStandardAssets.Characters.FirstPerson.MouseLook m_MouseLook;
    [SerializeField] private bool m_UseHeadBob;
    [SerializeField] private CurveControlledBob m_HeadBob = new CurveControlledBob();
    [SerializeField] private float m_StepInterval;
    [SerializeField] private AudioClip[] m_FootstepSounds;    // an array of footstep sounds that will be randomly selected from.

    private Camera m_Camera;
    private Vector2 m_Input;
    private Vector3 m_MoveDir = Vector3.zero;
    private CharacterController m_CharacterController;
    private CollisionFlags m_CollisionFlags;
    private Vector3 m_OriginalCameraPosition;
    private float m_StepCycle;
    private float m_NextStep;
    private AudioSource m_AudioSource;
    #endregion

    [SerializeField]
    int gridSpacing = 6;
    [SerializeField]
    float movementMargin = 0.1f;

    bool isWalking;
    bool collidedWhileWalking;

    // Use this for initialization
    private void Start()
    {
        m_CharacterController = GetComponent<CharacterController>();
        m_Camera = Camera.main;
        m_OriginalCameraPosition = m_Camera.transform.localPosition;
        m_HeadBob.Setup(m_Camera, m_StepInterval);
        m_StepCycle = 0f;
        m_NextStep = m_StepCycle/2f;
        m_AudioSource = GetComponent<AudioSource>();
		m_MouseLook.Init(transform , m_Camera.transform);
    }


    // Update is called once per frame
    private void Update()
    {
        RotateView();
    }


    private void FixedUpdate()
    {
        float speed;
        if (!isWalking)
        {
            m_MoveDir = Vector3.zero;
            GetInput(out speed);
            // If there is input, start the movement coroutine
            if (m_Input.magnitude > 0)
                StartCoroutine(TileMove(speed));
        }
        
        m_MouseLook.UpdateCursorLock();
    }

    /// <summary>
    /// Gradually moves the player by a set amount
    /// </summary>
    /// <param name="speed">The speed at which to move the player</param>
    /// <returns></returns>
    private IEnumerator TileMove(float speed)
    {
        // Don't allow the coroutine to interrupt itself
        isWalking = true;
        // Variable to prevent repeated wall collision
        bool alreadyCollided = false;

        // Set movement direction relative to the camera
        Vector3 desiredMove = transform.forward * m_Input.y + transform.right * m_Input.x;
        // If move either along the x or z axis, whichever is closest to the input given
        if (Mathf.Abs(desiredMove.z) >= Mathf.Abs(desiredMove.x))
            desiredMove.x = 0;
        else
            desiredMove.z = 0;
        // Normalize to move the entire distance
        desiredMove.Normalize();

        // Set the move direction based on speed and the above calculations
        m_MoveDir.x = desiredMove.x * speed;
        m_MoveDir.z = desiredMove.z * speed;

        // Determine the attempted target position
        Vector3 targetPosition = transform.position + (desiredMove * gridSpacing);
        // Uncomment these lines to remain locked to whole number positions
        //targetPosition = RoundVector3toInt(targetPosition);
        //Vector3 startingPosition = RoundVector3toInt(transform.position);

        // Comment out this line to remain locked to whole number positions
        Vector3 startingPosition = transform.position;

        // Don't allow collision to interrupt movement before it starts
        collidedWhileWalking = false;
        // Attempt to move to the target position
        while(Mathf.Abs((targetPosition - transform.position).magnitude) > movementMargin)
        {
            // If the player walks into a wall, move back to their original position
            if(collidedWhileWalking && !alreadyCollided)
            {
                m_MoveDir = -m_MoveDir;
                targetPosition = startingPosition;
                collidedWhileWalking = false;
                alreadyCollided = true;
            }

            // The rest is from the standard FirstPersonController
            if (m_CharacterController.isGrounded)
            {
                m_MoveDir.y = -m_StickToGroundForce;
            }
            else
            {
                m_MoveDir += Physics.gravity * m_GravityMultiplier * Time.fixedDeltaTime;
            }

            m_CollisionFlags = m_CharacterController.Move(m_MoveDir * Time.fixedDeltaTime);

            ProgressStepCycle(speed);
            UpdateCameraPosition(speed);
            yield return null;
        }
        // Snap to the target position at the end
        transform.SetPositionAndRotation(targetPosition, transform.rotation);
        // Allow the coroutine to run again after it finishes
        isWalking = false;
    }

    /// <summary>
    /// Rounds a given vector to integer values
    /// </summary>
    /// <param name="targetPosition">The vector to round</param>
    /// <returns>The rounded vector</returns>
    private Vector3 RoundVector3toInt(Vector3 targetPosition)
    {
        return new Vector3(Mathf.Round(targetPosition.x), Mathf.Round(targetPosition.y), Mathf.Round(targetPosition.z));
    }

    private void ProgressStepCycle(float speed)
    {
        if (m_CharacterController.velocity.sqrMagnitude > 0 && (m_Input.x != 0 || m_Input.y != 0))
        {
            m_StepCycle += (m_CharacterController.velocity.magnitude + speed)*
                            Time.fixedDeltaTime;
        }

        if (!(m_StepCycle > m_NextStep))
        {
            return;
        }

        m_NextStep = m_StepCycle + m_StepInterval;

        PlayFootStepAudio();
    }


    private void PlayFootStepAudio()
    {
        if (!m_CharacterController.isGrounded)
        {
            return;
        }
        // pick & play a random footstep sound from the array,
        // excluding sound at index 0
        int n = Random.Range(1, m_FootstepSounds.Length);
        m_AudioSource.clip = m_FootstepSounds[n];
        m_AudioSource.PlayOneShot(m_AudioSource.clip);
        // move picked sound to index 0 so it's not picked next time
        m_FootstepSounds[n] = m_FootstepSounds[0];
        m_FootstepSounds[0] = m_AudioSource.clip;
    }


    private void UpdateCameraPosition(float speed)
    {
        Vector3 newCameraPosition;
        if (!m_UseHeadBob)
        {
            return;
        }
        if (m_CharacterController.velocity.magnitude > 0 && m_CharacterController.isGrounded)
        {
            m_Camera.transform.localPosition =
                m_HeadBob.DoHeadBob(m_CharacterController.velocity.magnitude + speed);
            newCameraPosition = m_Camera.transform.localPosition;
        }
        else
        {
            newCameraPosition = m_Camera.transform.localPosition;
        }
        m_Camera.transform.localPosition = newCameraPosition;
    }


    private void GetInput(out float speed)
    {
        // Read input
        float horizontal = CrossPlatformInputManager.GetAxis("Horizontal");
        float vertical = CrossPlatformInputManager.GetAxis("Vertical");
        
        speed = m_WalkSpeed;
        if (Mathf.Abs(horizontal) >= Mathf.Abs(vertical))
            m_Input = new Vector2(horizontal, 0);
        else
            m_Input = new Vector2(0, vertical);

        // normalize input if it exceeds 1 in combined length:
        if (m_Input.sqrMagnitude > 1)
        {
            m_Input.Normalize();
        }
    }


    private void RotateView()
    {
        m_MouseLook.LookRotation (transform, m_Camera.transform);
    }


    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;
        //dont move the rigidbody if the character is on top of it
        if (m_CollisionFlags == CollisionFlags.Below)
        {
            return;
        }
        if ((m_CollisionFlags & CollisionFlags.Sides) != 0 && isWalking)
        {
            collidedWhileWalking = true;
        }
    }
}

