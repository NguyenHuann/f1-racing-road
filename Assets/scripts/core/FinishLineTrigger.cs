using UnityEngine;

public class FinishLineTrigger : MonoBehaviour {
    private void OnTriggerEnter(Collider other) {
        if (GameManager.Instance != null) {
            GameManager.Instance.OnFinishLineCrossed(other);
        }
    }
}

