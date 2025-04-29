using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ArcadeVP
{
    [RequireComponent(typeof(TrailRenderer))] // Ensure TrailRenderer is present
    public class SkidMarks : MonoBehaviour
    {
        private TrailRenderer skidMark;
        private ParticleSystem smoke; // Optional smoke particle system
        public ArcadeVehicleController carController; // Assign this in the Inspector

        private float fadeOutSpeed;
        private Color initialSkidColor; // Store the initial color for fading

        void Awake()
        {
            // --- Ensure components are assigned ---
            skidMark = GetComponent<TrailRenderer>();
            smoke = GetComponent<ParticleSystem>(); // Might be null if no particle system

            if (carController == null)
            {
                Debug.LogError("SkidMarks script needs Car Controller assigned in the Inspector!", gameObject);
                this.enabled = false; // Disable script if controller is missing
                return;
            }

            // --- Initialize Skid Mark ---
            skidMark.emitting = false; // Start not emitting

            // Set initial width from the controller
            skidMark.startWidth = carController.skidWidth;
            skidMark.endWidth = carController.skidWidth; // Ensure end width matches too

            // Store initial color if the material exists
            if (skidMark.material != null)
            {
                initialSkidColor = skidMark.material.color;
            }
            else
            {
                initialSkidColor = Color.black; // Default if no material
                Debug.LogWarning("SkidMark TrailRenderer is missing a material!", gameObject);
            }
            // Ensure initial transparency is full for the stored color
            initialSkidColor.a = 1.0f;
            skidMark.material.color = initialSkidColor; // Apply it initially


            // Move the trail slightly above the wheel position to prevent Z-fighting
            transform.localPosition = transform.localPosition + Vector3.up * 0.02f;
        }

        void OnEnable()
        {
            if (skidMark != null) skidMark.enabled = true;
        }

        void OnDisable()
        {
            if (skidMark != null) skidMark.enabled = false;
        }


        void Update() // Use Update for responsiveness to state changes
        {
            if (carController == null || skidMark == null) return; // Safety check

            // --- Determine if skids should be active ---
            // Skids active if grounded AND changing lanes
            bool shouldEmit = carController.grounded() && carController.IsChangingLane;

            // --- Control Emission ---
            if (shouldEmit)
            {
                if (!skidMark.emitting) // If we weren't emitting before
                {
                    skidMark.emitting = true;
                    // Reset color instantly to full opacity when starting to emit
                    if (skidMark.material != null) skidMark.material.color = initialSkidColor;
                    fadeOutSpeed = 0f; // Reset fade timer
                }

                // Play smoke particles if they exist and emitting is enabled
                if (smoke != null && !smoke.isPlaying)
                {
                    smoke.Play();
                }
            }
            else // Not grounded or not changing lane
            {
                if (skidMark.emitting) // If we *were* emitting
                {
                    skidMark.emitting = false;
                    // Start fading out immediately
                    fadeOutSpeed = 0f;
                }

                // Stop smoke particles if they exist
                if (smoke != null && smoke.isPlaying)
                {
                    smoke.Stop();
                }
            }


            // --- Handle Fading ---
            // If the trail is NOT currently emitting, fade it out
            if (!skidMark.emitting && skidMark.positionCount > 0) // Check positionCount to see if trail has points
            {
                fadeOutSpeed += Time.deltaTime * 2f; // Adjust fade speed (higher value = faster fade)
                if (skidMark.material != null)
                {
                    // Lerp alpha from initial full alpha down to zero
                    Color fadedColor = initialSkidColor;
                    fadedColor.a = Mathf.Lerp(initialSkidColor.a, 0f, fadeOutSpeed);
                    skidMark.material.color = fadedColor;
                }

                // Clear the trail once it's fully faded
                if (fadeOutSpeed >= 1.0f)
                {
                    skidMark.Clear();
                    fadeOutSpeed = 0f; // Reset for next time
                }
            }
        }
    }
}