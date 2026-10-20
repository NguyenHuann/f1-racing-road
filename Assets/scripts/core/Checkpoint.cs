using UnityEngine;

public class Checkpoint : MonoBehaviour {
    [Tooltip("Zero-based index of checkpoint around the track loop")]
    public int checkpointIndex = 0;

    private void OnTriggerEnter(Collider other) {
        if (GameManager.Instance != null) {
            GameManager.Instance.OnCheckpointPassed(this, other);
        }
    }
}

