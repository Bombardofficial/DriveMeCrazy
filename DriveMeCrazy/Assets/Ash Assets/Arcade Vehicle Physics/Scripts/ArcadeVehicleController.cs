using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ArcadeVP
{
    public class ArcadeVehicleController : MonoBehaviour
    {
        public enum groundCheck { rayCast, sphereCaste };
        public enum MovementMode { Velocity, AngularVelocity };
        public MovementMode movementMode;
        public groundCheck GroundCheck;
        public LayerMask drivableSurface;

        public float MaxSpeed, accelaration, turn, gravity = 7f, downforce = 5f;
        [Tooltip("if true : can turn vehicle in air")]
        public bool AirControl = false; // Air control might feel weird with lanes, consider disabling

        [Header("Reverse Settings")]
        [Tooltip("Max backwards speed when reversing")]
        public float reverseMaxSpeed = 5f;

        [Header("Lane Settings")]
        public float leftLaneX = -81.64664f;  // World X for left lane center
        public float middleLaneX = -76.64664f; // World X for middle lane center
        public float rightLaneX = -86.64664f; // World X for right lane center
        public float laneCooldown = 1f;
        [Tooltip("How fast the car moves sideways between lanes")]
        public float laneChangeSpeed = 15f; // Increased speed for snappier movement
        [Tooltip("How close the car needs to be to the lane center to snap")]
        public float laneSnapThreshold = 0.05f; // Reduced threshold for a cleaner snap

        public Rigidbody rb, carBody; // Ensure 'rb' is the main Rigidbody doing the movement

        [HideInInspector]
        public RaycastHit hit;
        public AnimationCurve frictionCurve;
        // public AnimationCurve turnCurve; // Likely not needed for lane turning
        public PhysicMaterial frictionMaterial;

        [Header("Visuals")]
        public Transform BodyMesh; // The visual part of the car body that tilts/rolls
        public Transform[] FrontWheels = new Transform[2];
        public Transform[] RearWheels = new Transform[2];
        [Tooltip("Approximate radius of your car wheels for calculating spin speed")]
        public float wheelRadius = 0.3f; // *** ADDED: Set this in Inspector ***
        [Tooltip("How much the wheels turn visually during lane change")]
        public float visualWheelTurnAngle = 35f; // Increased angle
        [Range(0, 20)]
        [Tooltip("How much the car body rolls visually during lane change")]
        public float BodyTilt = 8f; // Increased default, adjust in Inspector

        [Header("Audio settings")]
        public AudioSource engineSound;
        [Range(0, 1)]
        public float minPitch;
        [Range(1, 3)]
        public float MaxPitch;
        public AudioSource SkidSound;

        [Header("Skid Mark Settings")]
        [Tooltip("Width of the skid mark trails")]
        public float skidWidth = 0.4f; // Restore this variable

        // --- Internal & State Vars ---
        [HideInInspector] public Vector3 carVelocity;
        private float radius; // Radius of the main physics collider (rb)
        private Vector3 origin;
        private float[] lanes = new float[3];
        private int currentLaneIndex = 1; // 0=Left, 1=Middle, 2=Right (based on sorted X values)
        private float targetXPosition;
        private bool isChangingLane = false;
        private float lastLaneChangeTime = -Mathf.Infinity;
        private float previousSteeringInput = 0f;
        private float visualSteeringInput = 0f; // -1, 0, or 1 for visual lean/turn

        // --- Input Vars ---
        private float steeringInput;
        private float accelerationInput;
        private float driftInput;
        private float slowInput;

        // This public property allows other scripts to read the private 'isChangingLane' state
        public bool IsChangingLane => isChangingLane;

        [Tooltip("How much the car body pitches visually (degrees) during accel/brake")]

        [Range(0, 15)]

        public float bodyPitchAngle = 5f; // Default pitch angle

        [Tooltip("How quickly the body pitch adjusts")]

        public float pitchLerpSpeed = 8f; // Speed for pitch lerp

        [Tooltip("How quickly the body roll adjusts")]

        public float rollLerpSpeed = 15f; // Speed for roll lerp 

        [Tooltip("Minimum forward speed required to initiate a lane change (as fraction of MaxSpeed)")]
        [Range(0f, 0.5f)]
        public float minSpeedFractionForLaneChange = 0.1f; // Default to 10%
        void Start()
        {
            // Ensure rb (the Rigidbody being moved) has a SphereCollider
            var sphereCollider = rb.GetComponent<SphereCollider>();
            if (sphereCollider == null)
            {
                Debug.LogError("Rigidbody 'rb' is missing a SphereCollider!", gameObject);
                radius = 0.5f; // Default radius if missing
            }
            else
            {
                radius = sphereCollider.radius;
            }

            if (movementMode == MovementMode.AngularVelocity)
            {
                Physics.defaultMaxAngularSpeed = 100;
            }

            // --- Lane setup ---
            // ** CRITICAL FIX **: Sort the lanes based on their actual X coordinates
            List<float> sortedLanes = new List<float> { leftLaneX, middleLaneX, rightLaneX };
            sortedLanes.Sort(); // Sorts numerically (ascending)

            if (sortedLanes.Count != 3)
            {
                Debug.LogError("Something went wrong sorting lanes. Check X values.", gameObject);
                // Fallback to original values if sort fails unexpectedly
                lanes[0] = rightLaneX; // Smallest X
                lanes[1] = leftLaneX;
                lanes[2] = middleLaneX; // Largest X
            }
            else
            {
                lanes[0] = sortedLanes[0]; // Leftmost X
                lanes[1] = sortedLanes[1]; // Middle X
                lanes[2] = sortedLanes[2]; // Rightmost X
                Debug.Log($"Lanes Initialized: Left={lanes[0]}, Middle={lanes[1]}, Right={lanes[2]}");
            }


            // Make sure the initial index corresponds to the intended middle lane X value
            // Find which index holds the value closest to the 'middleLaneX' parameter
            float smallestDiff = Mathf.Infinity;
            int initialLaneIndex = 1; // Default
            for (int i = 0; i < 3; i++)
            {
                float diff = Mathf.Abs(lanes[i] - middleLaneX);
                if (diff < smallestDiff)
                {
                    smallestDiff = diff;
                    initialLaneIndex = i;
                }
            }
            currentLaneIndex = initialLaneIndex; // Start in the lane closest to middleLaneX
            Debug.Log($"Starting in Lane Index: {currentLaneIndex} (X={lanes[currentLaneIndex]})");


            lastLaneChangeTime = -laneCooldown; // Allow immediate first change

            // Set initial position precisely
            targetXPosition = lanes[currentLaneIndex];
            var p = rb.position;
            p.x = targetXPosition;
            rb.position = p;
            rb.velocity = new Vector3(0, rb.velocity.y, rb.velocity.z); // Kill any initial sideways velocity

            isChangingLane = false;
            visualSteeringInput = 0f;

        }

        void Update()
        {
            HandleLaneChangeInput();
            UpdateEngineSound();
        }

        void HandleLaneChangeInput()
        {
            // Check 1: Already changing or in cooldown?
            // If so, just update previous input and exit.
            if (isChangingLane || Time.time < lastLaneChangeTime + laneCooldown)
            {
                previousSteeringInput = steeringInput;
                return;
            }

            // Check 2: Is speed sufficient for a lane change?
            // Calculate the minimum speed required (using fraction of MaxSpeed)
            // Use a small absolute fallback value if MaxSpeed is zero to avoid dividing by zero.
            float minSpeedThreshold = MaxSpeed > 0.1f ? MaxSpeed * minSpeedFractionForLaneChange : 0.1f;

            // Get the car's current forward/backward speed (local Z velocity)
            // Make sure carVelocity is correctly calculated in FixedUpdate using rb.velocity
            float currentForwardSpeed = Mathf.Abs(carVelocity.z);

            // If speed is below threshold, block the change
            if (currentForwardSpeed < minSpeedThreshold)
            {
                // Optional: Log why the change is blocked
                // Debug.Log($"Lane change blocked: Speed {currentForwardSpeed} < threshold {minSpeedThreshold}");

                previousSteeringInput = steeringInput; // Still update previous input
                return; // Exit because speed is too low
            }

            // --- Only proceed if Checks 1 & 2 passed ---

            // Check 3: Detect steering taps
            bool steerRightPressed = steeringInput > 0.5f && previousSteeringInput <= 0.5f;
            bool steerLeftPressed = steeringInput < -0.5f && previousSteeringInput >= -0.5f;

            int targetLaneIndex = currentLaneIndex;
            float changeDirection = 0;
            bool changeTriggered = false; // Flag to track if input detected

            // Check bounds using the 0, 1, 2 indices
            if (steerRightPressed && currentLaneIndex < 2)
            {
                targetLaneIndex++;
                changeDirection = 1f;
                changeTriggered = true;
            }
            else if (steerLeftPressed && currentLaneIndex > 0)
            {
                targetLaneIndex--;
                changeDirection = -1f;
                changeTriggered = true;
            }

            // If a lane change was triggered by input this frame
            if (changeTriggered)
            {
                currentLaneIndex = targetLaneIndex;
                targetXPosition = lanes[currentLaneIndex];
                lastLaneChangeTime = Time.time;
                isChangingLane = true;
                visualSteeringInput = changeDirection;
                Debug.Log($"Changing to Lane Index: {currentLaneIndex} (Target X: {targetXPosition})");
                if (SkidSound != null && grounded() && !SkidSound.isPlaying)
                {
                    SkidSound.Play(); // Start the skid sound
                }
            }

            // Always update previous input at the very end
            previousSteeringInput = steeringInput;
        }

        void UpdateEngineSound()
        {
            float forwardSpeed = transform.InverseTransformDirection(rb.velocity).z;
            if (engineSound != null)
            {
                engineSound.pitch = Mathf.Lerp(minPitch, MaxPitch, Mathf.Abs(forwardSpeed) / (MaxSpeed > 0 ? MaxSpeed : 1f));
            }
        }

        public void ProvideInputs(float _steer, float _accel, float _driftBrake, float _slowBrake)
        {
            steeringInput = _steer;
            accelerationInput = _accel;
            driftInput = _driftBrake;
            slowInput = _slowBrake;
        }

        void FixedUpdate()
        {
            if (carBody == null) carBody = rb; // Fallback if carBody not assigned
            if (rb != null) // Check rb is not null
            {
                carVelocity = carBody.transform.InverseTransformDirection(rb.velocity);
            }
            else
            {
                carVelocity = Vector3.zero; // Avoid errors if rb is missing
                Debug.LogError("Rigidbody 'rb' is not assigned or missing!");
            }
            ApplyFriction(); // Extracted friction logic

            bool isGrounded = grounded();

            // Apply Accel/Brake/Downforce/Alignment if grounded
            if (isGrounded)
            {
                HandleGroundedMovement();
                AlignToGround();
            }
            else // Handle Air Movement
            {
                HandleAirMovement();
            }

            // Handle Lane Changing Movement (positional)
            HandleLanePositioning(isGrounded);

            // Update Visuals (Wheels, Body Tilt)
            Visuals();
        }

        void ApplyFriction()
        {
            // Sideways friction (less important now, but keep for stability)
            if (Mathf.Abs(carVelocity.x) > 0.1f)
            {
                frictionMaterial.dynamicFriction = frictionCurve.Evaluate(Mathf.Abs(carVelocity.x / 10));
            }
            else
            {
                frictionMaterial.dynamicFriction = frictionCurve.Evaluate(0);
            }
            // You might want longitudinal friction too depending on feel
        }

        void HandleGroundedMovement()
        {
            // --- Acceleration / Deceleration ---
            if (Mathf.Abs(accelerationInput) > 0.1f)
            {
                float currentSpeed = carVelocity.z; // Local forward speed
                float forceMultiplier = (accelerationInput > 0) ? accelaration : accelaration * 1.5f; // Stronger reverse/brake?

                // Apply force if not exceeding speed limits
                if ((accelerationInput > 0 && currentSpeed < MaxSpeed) || (accelerationInput < 0 && currentSpeed > -reverseMaxSpeed))
                {
                    // Use carBody's forward direction
                    rb.AddForce(carBody.transform.forward * accelerationInput * forceMultiplier * 50f * rb.mass * Time.fixedDeltaTime, ForceMode.Force); // Adjusted multiplier
                }
            }

            // --- Gentle Braking (using 'S' or Down Arrow) ---
            if (slowInput > 0.1f)
            {
                // Apply braking force opposite to velocity, stronger than friction alone
                Vector3 brakeForce = -rb.velocity.normalized * slowInput * accelaration * 30f * rb.mass * Time.fixedDeltaTime; // Scaled multiplier
                rb.AddForce(brakeForce, ForceMode.Force);
            }

            // --- Downforce ---
            rb.AddForce(-transform.up * downforce * rb.mass);
        }

        void AlignToGround()
        {
            // --- Ground Normal Alignment ---
            if (hit.collider != null) // Ensure hit is valid from grounded() check
            {
                // Rotate carBody to match ground normal smoothly
                Quaternion targetRotation = Quaternion.FromToRotation(carBody.transform.up, hit.normal) * carBody.transform.rotation;
                carBody.MoveRotation(Quaternion.Slerp(carBody.rotation, targetRotation, 10f * Time.fixedDeltaTime)); // Faster alignment
            }
        }

        void HandleAirMovement()
        {
            // --- Air Control (Steering) --- Disabled by default
            if (AirControl)
            {
                // Minimal air steer influence if enabled
                // carBody.AddTorque(Vector3.up * steeringInput * turn * 10f * Time.fixedDeltaTime);
            }

            // --- Gravity ---
            rb.AddForce(Vector3.down * gravity * rb.mass);

            // --- Air Rotation Alignment (level out) ---
            Quaternion targetRotation = Quaternion.FromToRotation(carBody.transform.up, Vector3.up) * carBody.transform.rotation;
            carBody.MoveRotation(Quaternion.Slerp(carBody.rotation, targetRotation, 5f * Time.fixedDeltaTime)); // Slower alignment in air
        }


        void HandleLanePositioning(bool isGrounded) // Pass grounded state
        {
            // --- Smooth Lane Change Movement ---
            if (isChangingLane)
            {
                Vector3 currentPosition = rb.position;
                float newX = Mathf.MoveTowards(currentPosition.x, targetXPosition, laneChangeSpeed * Time.fixedDeltaTime);
                Vector3 nextPosition = new Vector3(newX, currentPosition.y, currentPosition.z);
                rb.MovePosition(nextPosition);

                // Check if target reached
                if (Mathf.Abs(rb.position.x - targetXPosition) < laneSnapThreshold)
                {
                    // Snap exactly to the lane center and stop changing
                    currentPosition.x = targetXPosition;
                    rb.position = currentPosition;
                    isChangingLane = false;
                    visualSteeringInput = 0f; // Reset visual steer

                    // Kill sideways velocity AFTER snapping
                    rb.velocity = new Vector3(0, rb.velocity.y, rb.velocity.z);

                    if (SkidSound != null && SkidSound.isPlaying)
                    {
                        SkidSound.Stop();
                    }

                    // Optional: Snap rotation back to straight forward quickly
                    // rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(transform.forward, transform.up), 0.8f));
                }
                else if (!isGrounded && SkidSound != null && SkidSound.isPlaying)
                {
                    SkidSound.Stop();
                }
            }
            // --- Stay in Lane (Correction Force) ---
            // Only apply if grounded and NOT currently changing lanes
            else if (isGrounded)
            {
                Vector3 currentPosition = rb.position;
                // Check if we've drifted off the center of the current lane
                if (Mathf.Abs(currentPosition.x - lanes[currentLaneIndex]) > 0.01f)
                {
                    // Gently move back towards the center
                    float correctionX = Mathf.MoveTowards(currentPosition.x, lanes[currentLaneIndex], (laneChangeSpeed * 0.5f) * Time.fixedDeltaTime); // Slower correction
                    rb.MovePosition(new Vector3(correctionX, currentPosition.y, currentPosition.z));
                }
            }
        }

        public void Visuals()
        {
            // Determine the steering input for visual purposes (-1, 0, or 1)
            float currentVisualSteer = isChangingLane ? visualSteeringInput : 0f;

            // --- Wheel Turning ---
            /*foreach (Transform FW in FrontWheels)

            {
                if (FW == null) continue; // Skip if wheel transform is missing
                // Lerp wheel Y rotation based on visual steer
                Quaternion targetWheelRotation = Quaternion.Euler(FW.localRotation.eulerAngles.x, visualWheelTurnAngle * currentVisualSteer, FW.localRotation.eulerAngles.z);
                FW.localRotation = Quaternion.Slerp(FW.localRotation, targetWheelRotation, 20f * Time.deltaTime); // ** INCREASED Slerp speed **
            }*/

            // --- Wheel Spinning (Based on forward velocity) ---
            // Ensure wheelRadius is positive to avoid division by zero or weirdness
            float circumference = (wheelRadius > 0.01f) ? (2f * Mathf.PI * wheelRadius) : 1.0f;
            float distancePerSec = carVelocity.z; // Use local Z velocity
            float rotationPerSec = (distancePerSec / circumference) * 360f;

            foreach (Transform FW in FrontWheels)
            {
                if (FW == null) continue;
                FW.Rotate(Vector3.right, rotationPerSec * Time.deltaTime, Space.Self); // Rotate around local right axis
            }
            foreach (Transform RW in RearWheels)
            {
                if (RW == null) continue;
                RW.Rotate(Vector3.right, rotationPerSec * Time.deltaTime, Space.Self); // Rotate around local right axis
            }

            // --- Body Tilt ---
            if (BodyMesh != null)

            {

                // --- Calculate Target Pitch (Accel/Brake) ---

                float targetPitch = 0f;

                // Use the isGrounded parameter passed from FixedUpdate

                if (grounded())

                {

                    if (accelerationInput > 0.1f)

                    { // Accelerating forward

                        targetPitch = Mathf.Lerp(0, -bodyPitchAngle, accelerationInput); // Tilt back

                    }

                    else if (slowInput > 0.1f || accelerationInput < -0.1f)

                    { // Braking or Reversing

                        float brakeEffect = Mathf.Clamp01(slowInput + Mathf.Abs(accelerationInput));

                        targetPitch = Mathf.Lerp(0, bodyPitchAngle, brakeEffect); // Tilt forward

                    }

                } // Else targetPitch remains 0 (level out in air)


                // --- Calculate Target Roll (Lane Change) ---

                float targetRoll = 0f;

                if (isChangingLane)

                { // Only roll when changing lanes

                    targetRoll = BodyTilt * currentVisualSteer * -1f;

                }


                // --- Apply Pitch & Roll Smoothly ---

                // Get current angles (handle 0-360 wrapping)

                Vector3 currentEuler = BodyMesh.localRotation.eulerAngles;

                float currentPitch = currentEuler.x > 180 ? currentEuler.x - 360 : currentEuler.x;

                float currentRoll = currentEuler.z > 180 ? currentEuler.z - 360 : currentEuler.z;


                // Interpolate angles smoothly using LerpAngle

                float smoothedPitch = Mathf.LerpAngle(currentPitch, targetPitch, pitchLerpSpeed * Time.deltaTime);

                float smoothedRoll = Mathf.LerpAngle(currentRoll, targetRoll, rollLerpSpeed * Time.deltaTime);


                // Apply the rotation, keeping the current local Y rotation

                BodyMesh.localRotation = Quaternion.Euler(smoothedPitch, currentEuler.y, smoothedRoll);

            }
        }

        public bool grounded()
        {
            // Raycast origin slightly above the bottom of the Rigidbody collider
            origin = rb.position + transform.up * (radius * 0.1f);
            var direction = -transform.up;
            // Check distance slightly greater than radius
            var maxdistance = radius + 0.15f;

            // Use SphereCast for better detection on uneven surfaces
            if (Physics.SphereCast(origin, radius * 0.9f, direction, out hit, maxdistance, drivableSurface))
            {
                Debug.DrawRay(origin, direction * (hit.distance), Color.green);
                return true;
            }
            else
            {
                Debug.DrawRay(origin, direction * maxdistance, Color.red);
                return false;
            }
        }
    }
}