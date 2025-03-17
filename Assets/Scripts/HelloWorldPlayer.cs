using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.UI; // Add this if you want to display the death counter in the UI

namespace HelloWorld
{
    public class HelloWorldPlayer : NetworkBehaviour
    {
        public NetworkVariable<Vector3> Position = new NetworkVariable<Vector3>();
        public NetworkVariable<Quaternion> Rotation = new NetworkVariable<Quaternion>();
        public NetworkVariable<int> Health = new NetworkVariable<int>(100);
        public NetworkVariable<int> DeathCount = new NetworkVariable<int>(0);

        public float moveSpeed = 5f;
        public float lookSpeed = 2f;

        public GameObject leftFist;
        public GameObject rightFist;
        public GameObject uppercutFist;

        private Camera playerCamera;
        private Rigidbody rb;
        private Animator animator;
        private NetworkAnimator networkAnimator;
        public float sensX;
        public float sensY;
        public Transform orientation;
        float xRotation;
        float yRotation;

        // Define spawn points and rotations for host and client
        private Vector3 hostSpawnPoint = new Vector3(0f, 0f, 0f);
        private Quaternion hostSpawnRotation = Quaternion.Euler(0f, 0f, 0f);
        private Vector3 clientSpawnPoint = new Vector3(0f, 0f, 20.41f);
        private Quaternion clientSpawnRotation = Quaternion.Euler(0f, 180f, 0f);

        private AudioSource audioSource;

        // Add a UI Text element to display the death counter (optional)
        public Text deathCounterText;

        private void Start()
        {
            playerCamera = GetComponentInChildren<Camera>();
            rb = GetComponent<Rigidbody>();
            animator = GetComponent<Animator>();
            networkAnimator = GetComponent<NetworkAnimator>();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (IsOwner)
            {
                playerCamera.enabled = true;
            }
            else
            {
                playerCamera.enabled = false;
            }

            // Constrain the Rigidbody's rotation to prevent the capsule from falling over
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            audioSource = GetComponent<AudioSource>();

            // Initialize the death counter text (optional)
            if (deathCounterText != null)
            {
                UpdateDeathCounterText();
            }
        }

        private void Update()
        {
            if (IsOwner)
            {
                HandleLook();
                HandleAttack();

                // Check for "R" key press to rotate 180 degrees on the Y-axis
                if (Input.GetKeyDown(KeyCode.R))
                {
                    Rotate180Degrees();
                }
            }
            else
            {
                // Update position and rotation for remote players
                transform.position = Position.Value;
                transform.rotation = Rotation.Value;
            }
        }

        private void FixedUpdate()
        {
            if (IsOwner)
            {
                HandleMovement();
            }
        }

        public override void OnNetworkSpawn()
        {
            Position.OnValueChanged += OnPositionChanged;
            Rotation.OnValueChanged += OnRotationChanged;
            Health.OnValueChanged += OnHealthChanged;

            // Set spawn position and rotation based on whether the player is the host or a client
            if (IsHost)
            {
                transform.position = hostSpawnPoint;
                transform.rotation = hostSpawnRotation;
                Debug.Log("Host spawned at position: " + hostSpawnPoint + " with rotation: " + hostSpawnRotation.eulerAngles);
            }
            else
            {
                transform.position = clientSpawnPoint;
                transform.rotation = clientSpawnRotation;
                Debug.Log("Client spawned at position: " + clientSpawnPoint + " with rotation: " + clientSpawnRotation.eulerAngles);
            }

            Position.Value = transform.position;
            Rotation.Value = transform.rotation;
        }

        public override void OnNetworkDespawn()
        {
            Position.OnValueChanged -= OnPositionChanged;
            Rotation.OnValueChanged -= OnRotationChanged;
            Health.OnValueChanged -= OnHealthChanged;
        }

        public void OnPositionChanged(Vector3 previous, Vector3 current)
        {
            if (!IsOwner)
            {
                transform.position = Position.Value;
            }
        }

        public void OnRotationChanged(Quaternion previous, Quaternion current)
        {
            if (!IsOwner)
            {
                transform.rotation = Rotation.Value;
            }
        }

        public void OnHealthChanged(int previous, int current)
        {
            if (current <= 0)
            {
                Die();
            }
        }

        private void HandleMovement()
        {
            float moveX = Input.GetAxis("Horizontal") * moveSpeed;
            float moveZ = Input.GetAxis("Vertical") * moveSpeed;

            Vector3 move = transform.right * moveX + transform.forward * moveZ;
            rb.linearVelocity = new Vector3(move.x, rb.linearVelocity.y, move.z);

            // Update position locally and send to server
            Position.Value = transform.position;
            SubmitPositionRequestServerRpc(transform.position);
        }

        private void HandleLook()
        {
            float mouseX = Input.GetAxis("Mouse X") * Time.deltaTime * sensX;
            float mouseY = Input.GetAxis("Mouse Y") * Time.deltaTime * sensY;

            yRotation += mouseX;
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);
            transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
            orientation.rotation = Quaternion.Euler(0, yRotation, 0);

            // Send rotation update to server
            SubmitRotationRequestServerRpc(transform.rotation);
        }

        private void HandleAttack()
        {
            if (Input.GetMouseButtonDown(0))
            {
                // Standard punch with left fist
                LaunchFist(leftFist, 1f, 10f);
                networkAnimator.SetTrigger("LeftPunchTrigger");
            }
            if (Input.GetMouseButtonDown(1))
            {
                // Strong punch with right fist
                LaunchFist(rightFist, 1f, 20f);
                networkAnimator.SetTrigger("RightPunchTrigger");
            }
            if (Input.GetKeyDown(KeyCode.Space))
            {
                // Uppercut with uppercut fist
                LaunchFist(uppercutFist, 1f, 100f, true);
                networkAnimator.SetTrigger("UppercutTrigger");
            }
        }

        private void LaunchFist(GameObject fist, float chargeTime, float damage, bool isUppercut = false)
        {
            var fistScript = fist.GetComponent<Fist>();
            if (fistScript != null)
            {
                fistScript.Launch(chargeTime, damage, isUppercut);
            }
        }

        private void Rotate180Degrees()
        {
            Quaternion currentRotation = transform.rotation;
            Quaternion newRotation = currentRotation * Quaternion.Euler(0, 180, 0);
            transform.rotation = newRotation;

            // Send rotation update to server
            SubmitRotationRequestServerRpc(transform.rotation);
        }

        private void Die()
        {
            // Play explosion animation
            networkAnimator.SetTrigger("DeathTrigger");

            // Update the death counter
            IncrementDeathCountServerRpc();

            // Implement respawn logic
            Invoke(nameof(Respawn), 3f); // Respawn after 3 seconds
        }

        private void Respawn()
        {
            Health.Value = 100;
            if (IsHost)
            {
                transform.position = hostSpawnPoint;
                transform.rotation = hostSpawnRotation;
                Debug.Log("Host respawned at position: " + hostSpawnPoint + " with rotation: " + hostSpawnRotation.eulerAngles);
            }
            else
            {
                transform.position = clientSpawnPoint;
                transform.rotation = clientSpawnRotation;
                Debug.Log("Client respawned at position: " + clientSpawnPoint + " with rotation: " + clientSpawnRotation.eulerAngles);
            }
            Position.Value = transform.position;
            Rotation.Value = transform.rotation;
        }

        [ServerRpc]
        public void SubmitPositionRequestServerRpc(Vector3 position, ServerRpcParams rpcParams = default)
        {
            Position.Value = position;
        }

        [ServerRpc]
        public void SubmitRotationRequestServerRpc(Quaternion rotation, ServerRpcParams rpcParams = default)
        {
            Debug.Log("SubmitRotationRequestServerRpc called with rotation: " + rotation.eulerAngles);
            Rotation.Value = rotation;
        }

        [ServerRpc]
        public void TakeDamageServerRpc(int damage)
        {
            Health.Value -= damage;
            PlayDamageSoundClientRpc();
        }

        [ServerRpc]
        private void IncrementDeathCountServerRpc()
        {
            DeathCount.Value++;
            UpdateDeathCounterClientRpc(DeathCount.Value);
        }

        [ClientRpc]
        private void UpdateDeathCounterClientRpc(int newDeathCount)
        {
            if (deathCounterText != null)
            {
                deathCounterText.text = "Deaths: " + newDeathCount;
            }
        }

        [ClientRpc]
        private void PlayDamageSoundClientRpc()
        {
            if (audioSource != null)
            {
                audioSource.Play();
            }
        }

        private void UpdateDeathCounterText()
        {
            if (deathCounterText != null)
            {
                deathCounterText.text = "Deaths: " + DeathCount.Value;
            }
        }

        public static Vector3 GetRandomPositionOnPlane()
        {
            return new Vector3(Random.Range(-3f, 3f), 1f, Random.Range(-3f, 3f));
        }
    }
}