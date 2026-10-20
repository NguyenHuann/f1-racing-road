using UnityEngine;

public class CarFallRescue : MonoBehaviour {

    [Header("Fall Threshold")]
    [Tooltip("If the car's Y position drops below this value, trigger Game Over.")]
    public float fallThresholdY = -8.5f;

    [Tooltip("Check frequency in seconds")]
    public float checkInterval = 0.15f;
    private float nextCheckTime = 0f;

    private CarStateMachine carMachine;
    private Rigidbody rb;

    void Awake() {
        carMachine = GetComponent<CarStateMachine>();
        rb = GetComponent<Rigidbody>();
    }

    void Update() {
        if (!CompareTag("Player")) return;

        // Only trigger game over if race is actively in progress
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.currentState != GameState.Racing) return;

        if (Time.time >= nextCheckTime) {
            nextCheckTime = Time.time + checkInterval;

            // Check if car fell off the map track
            if (transform.position.y < fallThresholdY) {
                TriggerFallGameOver();
            }
        }
    }

    private void TriggerFallGameOver() {
        Debug.LogWarning($"[CarFallRescue] Player {gameObject.name} fell off map at Y={transform.position.y:F1}! Triggering GAME OVER.");

        // Stop the car's motion safely and freeze physics to avoid infinite fall calculations
        if (rb != null) {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // Lock wheels
        if (carMachine != null && carMachine.wheelColliders != null) {
            foreach (var wc in carMachine.wheelColliders) {
                if (wc != null) {
                    wc.motorTorque = 0f;
                    wc.brakeTorque = 10000f;
                }
            }
        }

        enabled = false;

        // Notify GameManager
        if (GameManager.Instance != null) {
            GameManager.Instance.TriggerGameOver("Xe đã rơi khỏi đường đua!");
        }
    }
}
