using Unity.Mathematics;
using UnityEngine;
using System.Linq;

public class CamraController : MonoBehaviour {
    private CarStateMachine carStateMachine;
    private Transform targetTransform;

    private Vector2 offset = new Vector2(0, 0);

    [Range(0.1f, 5)] public float siddewaysLerpSpeed = 1;

    [Range(0.1f, 2)] public float maxDistanceToTarget = 4;

    private float distanceToTarget;
    private float tmp;
    private float result;

    void Start() {
        if (carStateMachine == null) {
            carStateMachine = GameObject.FindGameObjectsWithTag("Player").FirstOrDefault()?.GetComponent<CarStateMachine>();
        }
        if (carStateMachine != null) {
            SetTarget(carStateMachine, true);
        }
    }

    public void SetTarget(CarStateMachine car, bool snapImmediately = false) {
        carStateMachine = car;
        if (carStateMachine == null) return;

        targetTransform = carStateMachine.transform;
        if (carStateMachine.CarStats != null && carStateMachine.CarStats.camraPositions != null && carStateMachine.CarStats.camraPositions.Count > carStateMachine.cameraindexPos) {
            offset = carStateMachine.CarStats.camraPositions[carStateMachine.cameraindexPos];
        } else {
            offset = new Vector2(-4.5f, 1.8f);
        }

        if (snapImmediately && targetTransform != null) {
            transform.position = targetTransform.TransformPoint(new Vector3(0, offset.y, offset.x));
            if (carStateMachine.CarStats != null) {
                transform.LookAt(targetTransform.TransformPoint(new Vector3(0, carStateMachine.CarStats.lookAtPoint.y, carStateMachine.CarStats.lookAtPoint.x)));
            }
        }
    }

    void FixedUpdate() {
        if (carStateMachine == null || targetTransform == null) return;

        // If game is over, stop aggressive following and just keep looking at the car safely
        if (GameManager.Instance != null && GameManager.Instance.currentState == GameState.GameOver) {
            transform.LookAt(targetTransform.position);
            return;
        }

        Vector3 lookPoint = targetTransform.position;
        if (carStateMachine.CarStats != null) {
            lookPoint = targetTransform.TransformPoint(new Vector3(0, carStateMachine.CarStats.lookAtPoint.y, carStateMachine.CarStats.lookAtPoint.x));
            if (carStateMachine.CarStats.camraPositions != null && carStateMachine.CarStats.camraPositions.Count > carStateMachine.cameraindexPos) {
                offset = carStateMachine.CarStats.camraPositions[carStateMachine.cameraindexPos];
            }
        }

        transform.LookAt(lookPoint);

        // use this to do the sideways smoothing follow
        Vector3 targetPos = targetTransform.TransformPoint(new Vector3(0, offset.y, offset.x));
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * siddewaysLerpSpeed);

        // used to calculate dist between cam offset and target
        distanceToTarget = Vector3.Distance(transform.position, targetTransform.position);
        tmp = distanceToTarget - offset.magnitude;

        // Safe threshold with clamp to prevent exponential explosion and NaN crash
        float excess = Mathf.Clamp(Mathf.Abs(tmp) - maxDistanceToTarget, 0f, 10f);
        result = Mathf.Sign(tmp) * Mathf.Pow(excess, 2);
        result = Mathf.Clamp(result, -10f, 10f);

        // add this so the car doesn't go too far
        transform.localPosition += transform.forward * (result / 2f);
    }
}
