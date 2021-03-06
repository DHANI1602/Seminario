using Game.Level;

using UnityEngine;
using UnityEngine.AI;

namespace Game.Player
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class PlayerController : MonoBehaviour
    {
        public static float MouseSensibility = 1;

        [Header("Configuration")]
        [SerializeField, Min(1), Tooltip("Determines the walking speed of the player.")]
        private float walkingSpeed = 15;

        [SerializeField, Min(1), Tooltip("Determines the walking acceleration of the player.")]
        private float walkingAcceleration = 25;

        [SerializeField, Tooltip("Key used to run.")]
        private KeyCode runKey;

        [SerializeField, Min(1), Tooltip("Determines the running speed of the player.")]
        private float runningSpeed = 30;

        [SerializeField, Min(1), Tooltip("Determines the running acceleration of the player.")]
        private float runningAcceleration = 50;

        [SerializeField, Min(0), Tooltip("Determines the rotation speed of the player. If 0, rotation becomes instantaneous.")]
        private float rotationSpeed = 1;

        [SerializeField, Range(0, 180), Tooltip("Determines the angle of rotation that the head can perfom vertically.")]
        private float maximumVerticalAngle = 45;

        [SerializeField, Min(0), Tooltip("Smooth interpolation applied to rotation. If 0, smooth rotation is disabled.")]
        private float smoothRotation = 3;

        [SerializeField, Range(0, 1), Tooltip("Rotation speed multiplier when player is dead.")]
        private float rotationSpeedWhenDead = .5f;

        [SerializeField, Min(.01f), Tooltip("Determines the sensibility of the mouse.")]
        private float mouseSensibility = 1;

        [Header("Setup")]
        [SerializeField, Tooltip("Transform used to rotate head (and camera).")]
        private Transform head;

        [SerializeField, Tooltip("Transform used to detect floor.")]
        private Transform feet;

        [SerializeField, Range(.01f, 10), Tooltip("Radius from feet used to check floor.")]
        private float feetCheckRadius = .1f;

        [SerializeField, Tooltip("Determines layers that are walkable.")]
        private LayerMask walkableLayers;

        [SerializeField, Tooltip("The body animator that controls walking and run animation.")]
        private Animator bodyAnimator;

        private NavMeshAgent agent;
        private PlayerStamina stamina;

        private static new readonly Collider[] collider = new Collider[1];

        public static bool IsMoving { get; private set; }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            stamina = GetComponent<PlayerStamina>();
            if (stamina == null || !stamina.enabled)
                stamina = null;

            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = false;

            // Negation of Y-axis fixes camera rotating to opposite side on start.
            targetRotation = currentRotation = new Vector2(head.transform.localEulerAngles.x, -transform.rotation.eulerAngles.y);
            transform.rotation = Quaternion.AngleAxis(currentRotation.y, Vector3.up);
            head.transform.localRotation = Quaternion.AngleAxis(currentRotation.x, Vector3.left);

            agent.updateRotation = false;
            agent.updateUpAxis = false;
        }

        private void FixedUpdate()
        {
            if (!GameManager.IsGameRunning)
                return;

            bodyAnimator.SetFloat("PlayerSpeed", agent.velocity.magnitude);

            if (Physics.OverlapSphereNonAlloc(feet.position, feetCheckRadius, collider, walkableLayers) > 0)
            {
                Vector3 axis = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
                if (axis.x == 0 && axis.z == 0)
                {
                    IsMoving = false;
                    stamina?.Rest();
                }
                else
                {
                    IsMoving = true;
                    if (Input.GetKey(runKey) && (stamina?.TryRun() ?? true))
                        Move(runningSpeed, runningAcceleration, axis);
                    else
                    {
                        stamina?.Walk();
                        Move(walkingSpeed, walkingAcceleration, axis);
                    }
                }
            }
            else
            {
                IsMoving = false;
                stamina?.Rest();
            }

            Rotate();
        }

        private void Move(float speed, float acceleration, Vector3 axis)
        {
            Vector3 targetSpeed = axis * speed;
            targetSpeed = transform.TransformDirection(targetSpeed);

            agent.velocity = Vector3.MoveTowards(agent.velocity, targetSpeed, acceleration * Time.fixedDeltaTime);
        }

        private Vector2 currentRotation;
        private Vector2 targetRotation;
        private Vector2 lastFrameRotation;
        private void Rotate()
        {
            const float wrapAt = .05f;

            Vector3 rawMousePosition = GetRawMousePosition();
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Confined;
                lastFrameRotation = GetMousePosition(rawMousePosition);
            }

            Vector2 mousePosition = GetMousePosition(rawMousePosition);
            Vector2 difference = mousePosition - lastFrameRotation;
            lastFrameRotation = mousePosition;
            targetRotation += mouseSensibility * MouseSensibility * difference;
            targetRotation = new Vector2(Mathf.Clamp(targetRotation.x, -maximumVerticalAngle, maximumVerticalAngle), targetRotation.y);

            if (rawMousePosition.x < wrapAt || rawMousePosition.x > 1 - wrapAt || rawMousePosition.y < wrapAt || rawMousePosition.y > 1 - wrapAt)
                Cursor.lockState = CursorLockMode.Locked;

            float multiplier = PlayerBody.IsAlive ? 1 : rotationSpeedWhenDead;

            if (rotationSpeed > 0)
                currentRotation = Vector3.MoveTowards(currentRotation, targetRotation, rotationSpeed * multiplier);
            else
                currentRotation = targetRotation;

            Quaternion xQ = Quaternion.AngleAxis(currentRotation.x, Vector3.left);
            Quaternion yQ = Quaternion.AngleAxis(currentRotation.y, Vector3.up);

            if (smoothRotation > 0)
            {
                // Horizontal rotation is applied on the player body.
                float delta = Time.fixedDeltaTime * smoothRotation * multiplier;
                transform.rotation = Quaternion.Lerp(transform.rotation, yQ, delta);
                // Vertical rotation is only applied on the player head.
                head.transform.localRotation = Quaternion.Lerp(head.transform.localRotation, xQ, delta);
            }
            else
            {
                // Horizontal rotation is applied on the player body.
                transform.rotation = yQ;
                // Vertical rotation is only applied on the player head.
                head.transform.localRotation = xQ;
            }

            Vector3 GetRawMousePosition()
                => Camera.main.ScreenToViewportPoint(Input.mousePosition);

            Vector2 GetMousePosition(Vector3 rawMousePosition)
                => new Vector2((rawMousePosition.y - .5f) * maximumVerticalAngle, rawMousePosition.x * 360);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(feet.transform.position, feetCheckRadius);
        }
    }
}
