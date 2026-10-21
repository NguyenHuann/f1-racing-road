using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(CarStateMachine))]
public class EngineController : MonoBehaviour {
    private CarStateMachine stateMachine;

    [Header("Gear Settings")]
    [Range(300, 2000)] public float idleRPM = 1000f;
    [Range(300, 4000)] public float minRPM = 3000f;
    [Range(600, 13000)] public float maxRPM = 1000f;
    [Range(0, 20)] public float engineSmoothDamp = 9f;
    [Range(1, 7)] public float finalDrive = 3.6f;
    [Range(0.20f, 10f)] public float[] gears;
    public AnimationCurve enginePower;

    [Header("Reverse Gear")]
    [Range(1f, 8f)] public float reverseGearRatio = 3.5f;
    public bool isReversing = false;

    [HideInInspector] public int gearNum = 1;
    [HideInInspector] public float engineRPM;
    private float totalPower;
    private float torquePerWheel;

    [Header("shifter settings")]
    [Tooltip("this will remove rpm from rpm instantly to make the shifting a little more realistic and nice to look at !")]
    [Range(100, 3000)] public float upShiftRpmBounce = 550;
    [Range(50, 2000)] public float maxRpmBounceBackRPMs = 200;

    [Header("Braking")]
    [Range(800, 200000)] public float brakePower = 1000f;
    [Range(100, 200000)] public float handBrakePower = 2000f;
    private float handBrakeTorque;
    private List<float> absWheelRPMs = new();  // absolute to only store positive values
    public float throttleInput, brakeInput, velocity;

    [Header("shifter")]
    [Tooltip("me average wheel slip'drift' value requiered to allow for upshifting")]
    [Range(0.01f, 0.5f)] public float minSlipTpUpshift = .2f;
    float wheelsAvgRPM = 0;
    [Tooltip("this takes the max rpm * this '11000 * 0.01 > 800' and compares if rpm over this value to provide no power to the wheels . Rev limiter ")]
    float maxRpmProtectVal = 0.04f;     // value to not reach max rpm , used to prevent engine reving over the max rpm


    //tmp
    float engineLoad = 0;

    float calcRPM = 0;


    void Awake() {
        if (stateMachine == null) stateMachine = GetComponent<CarStateMachine>();
    }

    void Start() {
        if (stateMachine == null) stateMachine = GetComponent<CarStateMachine>();
        //  initialize variables
        if (stateMachine != null && stateMachine.wheelColliders != null) {
            if (absWheelRPMs.Count != stateMachine.wheelColliders.Length) 
                absWheelRPMs = new List<float>(new float[stateMachine.wheelColliders.Length]);
        }
    }

    void FixedUpdate() {
        automaticShifter();
    }

    private void Update() {
        HandleInputs();
        CalculateEnginePower();
        MoveVehicle();

        if (Input.GetKeyDown(KeyCode.F)) UpShift();
        if (Input.GetKeyDown(KeyCode.Q)) DownShift();
    }

    public void HandleInputs() {
        if (stateMachine == null) stateMachine = GetComponent<CarStateMachine>();
        if (stateMachine == null) return;

        float rawY = stateMachine.moveInput.y;

        if (GameManager.Instance != null && (!GameManager.Instance.raceStarted || GameManager.Instance.raceFinished)) {
            throttleInput = 0f;
            brakeInput = 1f;
            return;
        }

        // Measure speed along car forward heading
        if (stateMachine.rigidbody != null) {
            velocity = Vector3.Dot(stateMachine.rigidbody.linearVelocity, transform.forward);
        }

        if (!isReversing) {
            // FORWARD MODE
            if (rawY > 0) {
                throttleInput = rawY;
                brakeInput = 0f;
            } else if (rawY < 0) {
                // If moving forward faster than threshold, apply brakes
                if (velocity > 0.5f) {
                    throttleInput = 0f;
                    brakeInput = Mathf.Abs(rawY);
                } else {
                    // Car stopped or nearly stopped -> switch to reverse!
                    isReversing = true;
                    gearNum = -1;
                    throttleInput = Mathf.Abs(rawY);
                    brakeInput = 0f;
                }
            } else {
                throttleInput = 0f;
                brakeInput = 0f;
            }
        } else {
            // REVERSE MODE
            if (rawY < 0) {
                throttleInput = Mathf.Abs(rawY);
                brakeInput = 0f;
            } else if (rawY > 0) {
                // If moving backward faster than threshold, apply brakes
                if (velocity < -0.5f) {
                    throttleInput = 0f;
                    brakeInput = rawY;
                } else {
                    // Car stopped reversing -> switch back to forward!
                    isReversing = false;
                    gearNum = 1;
                    throttleInput = rawY;
                    brakeInput = 0f;
                }
            } else {
                throttleInput = 0f;
                brakeInput = 0f;
                if (Mathf.Abs(velocity) < 0.2f) {
                    isReversing = false;
                    gearNum = 1;
                }
            }
        }
    }

    private void CalculateEnginePower() {
        WheelRPM();
        wheelsAvgRPM = Mathf.Lerp(wheelsAvgRPM, absWheelRPMs.Average(), Time.deltaTime * 3.5f); // damped average , so we dont get wiggly engine RPM ps. adjust the 3.5f if needed to smooth it even more !

        float currentGearRatio = isReversing ? reverseGearRatio : (gearNum >= 0 && gearNum < gears.Length ? gears[gearNum] : (gears.Length > 1 ? gears[1] : 3.5f));

        if (isReversing) {
            calcRPM = math.clamp(wheelsAvgRPM < 5f ? (idleRPM + throttleInput * (minRPM - idleRPM + 1500f)) : (wheelsAvgRPM * finalDrive * reverseGearRatio), idleRPM, maxRPM + 100);
        } else if (gearNum < 1) {
            calcRPM = math.clamp(throttleInput * maxRPM, idleRPM, maxRPM + 100);
        } else {
            calcRPM = math.clamp(wheelsAvgRPM * finalDrive * currentGearRatio, idleRPM, maxRPM + 100);
        }

        if (engineRPM >= maxRPM) engineRPM -= maxRpmBounceBackRPMs;
        engineRPM = Mathf.Lerp(engineRPM, calcRPM, Time.deltaTime * engineSmoothDamp);

        // Power calculation
        bool canApplyPower = engineRPM < maxRPM - (maxRPM * maxRpmProtectVal);
        if (isReversing) {
            totalPower = finalDrive * (canApplyPower ? enginePower.Evaluate(engineRPM) * throttleInput : 0);
        } else {
            totalPower = finalDrive * (gearNum > 0 && canApplyPower ? enginePower.Evaluate(engineRPM) * throttleInput : 0);
        }

        // calculate the load , clamp so it dont go below 0 
        // used to the engine audio script 
        engineLoad = math.lerp(engineLoad, math.clamp(0, throttleInput - (engineRPM / maxRPM), 1), Time.deltaTime * 2);
    }

    private void WheelRPM() {
        for (int i = 0; i < stateMachine.wheelColliders.Length; i++) {
            absWheelRPMs[i] = Mathf.Abs(stateMachine.wheelColliders[i].rpm);
        }
    }

    public float brakeTPM;

    private void MoveVehicle() {
        // Calculate torque based on drive mode
        int drivenWheels = stateMachine.CarStats.driveMode == driveMode.allWheelDrive ? 4 : 2;
        torquePerWheel = engineRPM >= maxRPM - (engineRPM * maxRpmProtectVal) ? 0 : (totalPower / drivenWheels) + (isReversing ? 0 : stateMachine.boostNm);

        // Apply reverse torque direction if reversing
        float appliedMotorTorque = isReversing ? -torquePerWheel : torquePerWheel;

        // Apply handbrake (only to rear wheels)
        handBrakeTorque = Input.GetKey(KeyCode.Space) ? handBrakePower : 0f;

        // Apply torque based on drive mode
        switch (stateMachine.CarStats.driveMode) {
            case driveMode.frontWheelDrive:
                stateMachine.wheelColliders[0].motorTorque = appliedMotorTorque;
                stateMachine.wheelColliders[1].motorTorque = appliedMotorTorque;
                stateMachine.wheelColliders[2].motorTorque = 0f;
                stateMachine.wheelColliders[3].motorTorque = 0f;
                break;
            case driveMode.rearWheelDrive:
                // no power or brake at front wheels 
                stateMachine.wheelColliders[0].motorTorque = 0f;
                stateMachine.wheelColliders[1].motorTorque = 0f;
                // power as long as engine is not over the max 
                stateMachine.wheelColliders[2].motorTorque = appliedMotorTorque;
                stateMachine.wheelColliders[3].motorTorque = appliedMotorTorque;
                break;
            case driveMode.allWheelDrive:
                for (int i = 0; i < stateMachine.wheelColliders.Length; i++) {
                    stateMachine.wheelColliders[i].motorTorque = appliedMotorTorque;
                }
                break;
        }

        brakeTPM = (engineRPM >= maxRPM && !isReversing) ? brakePower : brakeInput * brakePower;
        // Apply brakes to all wheels
        for (int i = 0; i < stateMachine.wheelColliders.Length; i++) {
            stateMachine.wheelColliders[i].brakeTorque = brakeTPM;
        }

        // Apply handbrake to rear wheels
        stateMachine.wheelColliders[2].brakeTorque = Mathf.Max(brakeTPM, handBrakeTorque);
        stateMachine.wheelColliders[3].brakeTorque = Mathf.Max(brakeTPM, handBrakeTorque);
    }

    #region shifter

    void automaticShifter() {
        if (isReversing) return;

        if (gearNum > 0) {
            // moving >
            if (stateMachine.overallSlip < minSlipTpUpshift && engineRPM > maxRPM - (maxRPM * maxRpmProtectVal) && gearNum < gears.Length - 1) UpShift();
            if (engineRPM < minRPM + 2000 && gearNum > (throttleInput == 0 ? 0 : 1)) DownShift();
        } else if (throttleInput > 0) {
            gearNum = 1;
        }
    }

    bool upShiftTimeout = false;
    void DownShift() {
        if (gearNum > 1) {
            gearNum--;
            engineRPM += upShiftRpmBounce;
        } else if (gearNum == 1) {
            gearNum = 0;
        } else if (gearNum == 0) {
            gearNum = -1;
            isReversing = true;
        }
    }

    void UpShift() {
        if (upShiftTimeout) return;
        upShiftTimeout = true;

        if (isReversing || gearNum < 0) {
            isReversing = false;
            gearNum = 1;
        } else {
            gearNum = Mathf.Min(gearNum + 1, gears.Length - 1);
            if (gearNum > 1) engineRPM -= upShiftRpmBounce;
        }

        ResetShiftTimeout();
    }

    async void ResetShiftTimeout() {
        await Task.Delay(500);
        upShiftTimeout = false;
    }

    #endregion

    #region GUI
    [Header("GUI")]
    public bool showDebugGUI = false;
    public float GuiXPos = 0;
    public float GuiYPos = 0;
    public float GuiYSpace = 20;
    public GUIStyle customStyle = new();
    public float GuiCellWidth = 200;
    public float GuiCellHeight = 20;

    void OnGUI() {
        if (!showDebugGUI || !CompareTag("Player")) return;
        float pos = GuiYPos;
        GUI.HorizontalSlider(new Rect(GuiXPos, pos, GuiCellWidth, GuiCellHeight), engineRPM, idleRPM, maxRPM);
        pos += GuiYSpace;
        string gearStr = isReversing ? "R" : (gearNum == 0 ? "N" : $"{gearNum} ({(gearNum < gears.Length ? gears[gearNum].ToString("F2") : "1.00")})");
        GUI.Label(new Rect(GuiXPos, pos, GuiCellWidth, GuiCellHeight), $"Gear: {gearStr}", customStyle);
        pos += GuiYSpace;
        GUI.Label(new Rect(GuiXPos, pos, GuiCellWidth, GuiCellHeight), $"RPM: {engineRPM:F0}", customStyle);
        pos += GuiYSpace;
        GUI.Label(new Rect(GuiXPos, pos, GuiCellWidth, GuiCellHeight), $"Total horsepower: {totalPower:F2}", customStyle);
        pos += GuiYSpace;
        GUI.Label(new Rect(GuiXPos, pos, GuiCellWidth, GuiCellHeight), $"wheels rpm: {wheelsAvgRPM:F2}", customStyle);
        pos += GuiYSpace;
    }
    #endregion
}