using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState {
    MainMenu,
    CarSelection,
    Countdown,
    Racing,
    RaceFinished,
    GameOver
}

public class GameManager : MonoBehaviour {

    public static GameManager Instance { get; private set; }

    [Header("Game Flow State")]
    public GameState currentState = GameState.MainMenu;
    public bool startInMenu = true;
    public string gameOverReason = "Xe đã rơi khỏi đường đua!";

    [Header("Race Configuration")]
    public int totalLaps = 1;
    public int currentLap = 0;
    public bool raceStarted = false;
    public bool raceFinished = false;
    public float raceTimer = 0f;
    public float finalTime = 0f;

    [Header("Countdown Settings")]
    public float countdownDuration = 3f;
    public float countdownTimer = 3f;
    public bool isCountingDown = false;
    private string countdownDisplay = "3";
    private float goDisplayTimer = 0f; // Shows "GO!" for 1.2 seconds after countdown ends

    [Header("Cars in Race")]
    public CarStateMachine playerCar;
    public List<CarStateMachine> allCars = new List<CarStateMachine>();

    [Header("Checkpoints")]
    public List<Checkpoint> checkpoints = new List<Checkpoint>();
    public HashSet<int> passedCheckpoints = new HashSet<int>();
    private int nextRequiredCheckpointIndex = 0;
    private int totalCheckpointsPassed = 0;

    [Header("Start Lights Integration")]
    public GameObject startLightsObject;
    private MeshRenderer[] ledRenderers = new MeshRenderer[3];
    private Material ledRedMaterial;
    private Material ledGreenMaterial;
    private Material ledOffMaterial;

    [Header("Audio")]
    public AudioClip countdownSoundClip;
    public AudioClip countdownBeepClip;
    public AudioClip countdownGoClip;
    private AudioSource audioSource;
    private int lastBeepCount = -1;

    [Header("UI Styling")]
    private GUIStyle headerStyle;
    private GUIStyle bannerStyle;
    private GUIStyle subBannerStyle;
    private GUIStyle infoStyle;
    private GUIStyle countdownStyle;
    private GUIStyle goalTipStyle;
    private GUIStyle gameOverTitleStyle;
    private GUIStyle gameOverReasonStyle;
    private bool stylesInitialized = false;

    void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null) {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D audio for clear global UI sound

#if UNITY_EDITOR
        if (countdownSoundClip == null) {
            countdownSoundClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/countdown_sound.mp3");
        }
#endif
    }

    void Start() {
        FindRaceCars();
        FindOrCreateCheckpoints();
        SetupStartLights();

        // Check if quick restart was requested (from Try Again button)
        string restartCar = PlayerPrefs.GetString("QuickRestartCarName", "");
        if (!string.IsNullOrEmpty(restartCar)) {
            PlayerPrefs.DeleteKey("QuickRestartCarName");
            PlayerPrefs.Save();
            CarStateMachine target = null;
            foreach (var car in allCars) {
                if (car != null && (car.name.ToLower().Contains(restartCar.ToLower()) || restartCar.ToLower().Contains(car.name.ToLower()))) {
                    target = car;
                    break;
                }
            }
            if (target != null) {
                StartRaceWithSelectedCar(target);
                return;
            }
        }

        // Initialize Race State
        raceStarted = false;
        currentLap = 0;
        raceFinished = false;
        raceTimer = 0f;
        countdownTimer = countdownDuration;
        countdownDisplay = "3";
        lastBeepCount = -1;

        // Freeze all cars in place until countdown finishes
        SetCarsControllable(false);
        UpdateStartLightsVisuals(3);

        if (startInMenu) {
            currentState = GameState.MainMenu;
            isCountingDown = false;
        } else {
            StartCountdownSequence();
        }
    }

    public void ShowMainMenu() {
        currentState = GameState.MainMenu;
        isCountingDown = false;
        raceStarted = false;
        SetCarsControllable(false);
        if (audioSource != null) audioSource.Stop();
    }

    public void ShowCarSelection() {
        currentState = GameState.CarSelection;
        isCountingDown = false;
        raceStarted = false;
        SetCarsControllable(false);
        if (audioSource != null) audioSource.Stop();
    }

    public void StartRaceWithSelectedCar(CarStateMachine selectedCar) {
        if (selectedCar == null) {
            Debug.LogError("[GameManager] StartRaceWithSelectedCar called with null car!");
            return;
        }

        // 1. Mark selected car as Player
        selectedCar.tag = "Player";
        playerCar = selectedCar;

        // 2. Position selected car at center Pole Position
        selectedCar.transform.position = new Vector3(3.5f, 5.83f, 22.0f);
        selectedCar.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

        if (selectedCar.rigidbody != null) {
            selectedCar.rigidbody.linearVelocity = Vector3.zero;
            selectedCar.rigidbody.angularVelocity = Vector3.zero;
        }

        // 3. Remove all other cars from scene so ONLY the player's car remains on map!
        var otherCars = new List<CarStateMachine>();
        foreach (var car in allCars) {
            if (car != null && car != selectedCar) {
                otherCars.Add(car);
            }
        }

        foreach (var car in otherCars) {
            allCars.Remove(car);
            if (car != null && car.gameObject != null) {
                Destroy(car.gameObject);
            }
        }

        allCars.Clear();
        allCars.Add(selectedCar);

        // 4. Update camera target
        var cam = Camera.main;
        if (cam != null) {
            var camController = cam.GetComponent<CamraController>();
            if (camController != null) {
                camController.enabled = true;
                camController.SetTarget(selectedCar, true);
            }
        }

        // 5. Start Countdown
        StartCountdownSequence();
        Debug.Log($"[GameManager] Single-player race initialized with {selectedCar.name}!");
    }

    public void StartCountdownSequence() {
        currentState = GameState.Countdown;
        countdownTimer = countdownDuration;
        isCountingDown = true;
        countdownDisplay = "3";
        lastBeepCount = -1;
        raceStarted = false;
        raceFinished = false;
        raceTimer = 0f;
        currentLap = 0;
        passedCheckpoints.Clear();
        nextRequiredCheckpointIndex = 0;
        totalCheckpointsPassed = 0;

        SetCarsControllable(false);
        UpdateStartLightsVisuals(3);

        if (audioSource != null) {
            audioSource.Stop();
            if (countdownSoundClip != null) {
                audioSource.PlayOneShot(countdownSoundClip, 1f);
            }
        }
    }

    public void TriggerGameOver(string reason = "Xe đã rơi khỏi đường đua!") {
        if (currentState == GameState.GameOver || raceFinished) return;

        currentState = GameState.GameOver;
        raceStarted = false;
        gameOverReason = reason;

        // Freeze car controls and physics
        SetCarsControllable(false);

        // Stop countdown audio if playing
        if (audioSource != null) {
            audioSource.Stop();
        }

        // Stop car engine audio
        if (playerCar != null && playerCar.carAudio != null) {
            playerCar.carAudio.StopSoundInstant();
        }

        Debug.LogWarning($"[GameManager] GAME OVER: {reason}");
    }

    void Update() {
        // 1. Handle Countdown
        if (isCountingDown) {
            countdownTimer -= Time.deltaTime;

            int currentCount = Mathf.CeilToInt(countdownTimer);
            if (currentCount != lastBeepCount && currentCount >= 1 && currentCount <= 3) {
                lastBeepCount = currentCount;
                countdownDisplay = currentCount.ToString();
                if (countdownSoundClip == null) {
                    PlayBeep(false);
                }
                UpdateStartLightsVisuals(currentCount);
            }

            if (countdownTimer <= 0f) {
                // COUNTDOWN COMPLETED -> START RACE!
                isCountingDown = false;
                raceStarted = true;
                currentState = GameState.Racing;
                countdownDisplay = "GO!";
                goDisplayTimer = 1.2f;
                if (countdownSoundClip == null) {
                    PlayBeep(true);
                }
                UpdateStartLightsVisuals(0); // 0 = All Green GO!
                SetCarsControllable(true);
                Debug.Log("[GameManager] RACE STARTED! GO GO GO!");
            }
        } else if (goDisplayTimer > 0f) {
            goDisplayTimer -= Time.deltaTime;
        }

        // 2. Race in progress
        if (raceStarted && !raceFinished && currentState != GameState.GameOver) {
            raceTimer += Time.deltaTime;
        }

        // 3. Shortcuts on Finish or GameOver
        if ((raceFinished || currentState == GameState.GameOver)) {
            if (Input.GetKeyDown(KeyCode.R)) {
                TryAgain();
            }
            if (Input.GetKeyDown(KeyCode.Escape)) {
                ReturnToStartScreen();
            }
        }
    }

    public void FindRaceCars() {
        allCars.Clear();
        var found = FindObjectsByType<CarStateMachine>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var car in found) {
            allCars.Add(car);
            if (car.CompareTag("Player")) {
                playerCar = car;
            }
        }
    }

    public void FindOrCreateCheckpoints() {
        checkpoints.Clear();
        var foundCps = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
        Array.Sort(foundCps, (a, b) => a.checkpointIndex.CompareTo(b.checkpointIndex));
        checkpoints.AddRange(foundCps);
    }

    private void SetupStartLights() {
        var finishLine = GameObject.Find("FinishLine");
        if (finishLine != null) {
            var lightsTrans = finishLine.transform.Find("Cartoon_StartLights");
            if (lightsTrans != null) {
                startLightsObject = lightsTrans.gameObject;
                var body = lightsTrans.Find("startlights_body");
                if (body != null) {
                    for (int i = 0; i < 3 && i < body.childCount; i++) {
                        ledRenderers[i] = body.GetChild(i).GetComponent<MeshRenderer>();
                    }
                }
            }
        }

        // Create Materials for Red, Green, and Dim/Off
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");

        ledRedMaterial = new Material(urpLit);
        ledRedMaterial.name = "LED_Red_Runtime";
        ledRedMaterial.SetColor("_BaseColor", new Color(1f, 0.1f, 0.05f));
        ledRedMaterial.EnableKeyword("_EMISSION");
        ledRedMaterial.SetColor("_EmissionColor", new Color(1f, 0.1f, 0.05f) * 3.5f);

        ledGreenMaterial = new Material(urpLit);
        ledGreenMaterial.name = "LED_Green_Runtime";
        ledGreenMaterial.SetColor("_BaseColor", new Color(0.1f, 0.95f, 0.2f));
        ledGreenMaterial.EnableKeyword("_EMISSION");
        ledGreenMaterial.SetColor("_EmissionColor", new Color(0.1f, 1f, 0.2f) * 3.5f);

        ledOffMaterial = new Material(urpLit);
        ledOffMaterial.name = "LED_Off_Runtime";
        ledOffMaterial.SetColor("_BaseColor", new Color(0.15f, 0.15f, 0.15f));
        ledOffMaterial.DisableKeyword("_EMISSION");
        ledOffMaterial.SetColor("_EmissionColor", Color.black);
    }

    /// <summary>
    /// Update the 3 start lights:
    /// count 3: 3 Red
    /// count 2: 2 Red
    /// count 1: 1 Red
    /// count 0: All 3 Green (GO!)
    /// </summary>
    private void UpdateStartLightsVisuals(int count) {
        if (ledRenderers[0] == null || ledRedMaterial == null) return;

        if (count == 0) {
            // GO! All Green
            for (int i = 0; i < 3; i++) {
                if (ledRenderers[i] != null) ledRenderers[i].sharedMaterial = ledGreenMaterial;
            }
        } else {
            // Countdown: illuminate 'count' red lights
            for (int i = 0; i < 3; i++) {
                if (ledRenderers[i] != null) {
                    ledRenderers[i].sharedMaterial = (i < count) ? ledRedMaterial : ledOffMaterial;
                }
            }
        }
    }

    private void SetCarsControllable(bool canControl) {
        foreach (var car in allCars) {
            if (car == null) continue;

            if (!canControl) {
                // Freeze inputs
                car.moveInput = Vector2.zero;
                car.isSpacebarPressed = false;
                car.isShiftPressed = false;

                // Stop linear and angular movement
                if (car.rigidbody != null && !car.rigidbody.isKinematic) {
                    car.rigidbody.linearVelocity = Vector3.zero;
                    car.rigidbody.angularVelocity = Vector3.zero;
                }

                // Apply full brakes on wheels to ensure no sliding down slopes
                if (car.wheelColliders != null) {
                    foreach (var wc in car.wheelColliders) {
                        if (wc != null) {
                            wc.motorTorque = 0f;
                            wc.brakeTorque = 10000f;
                        }
                    }
                }

                // Set idle RPM on engine
                if (car.engineController != null) {
                    car.engineController.throttleInput = 0f;
                    car.engineController.brakeInput = 1f;
                    car.engineController.engineRPM = car.engineController.idleRPM;
                }
            } else {
                // Release brakes
                if (car.wheelColliders != null) {
                    foreach (var wc in car.wheelColliders) {
                        if (wc != null) {
                            wc.brakeTorque = 0f;
                        }
                    }
                }
                if (car.engineController != null) {
                    car.engineController.brakeInput = 0f;
                }
            }
        }
    }

    private void PlayBeep(bool isGo) {
        if (audioSource == null) return;

        if (isGo && countdownGoClip != null) {
            audioSource.PlayOneShot(countdownGoClip, 1f);
        } else if (!isGo && countdownBeepClip != null) {
            audioSource.PlayOneShot(countdownBeepClip, 0.8f);
        } else {
            // Generate procedural tone if no audio clip is assigned
            StartCoroutine(PlayProceduralBeep(isGo ? 880f : 440f, isGo ? 0.4f : 0.18f));
        }
    }

    private IEnumerator PlayProceduralBeep(float frequency, float duration) {
        int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++) {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Clamp01(1f - (t / duration)); // linear decay
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.4f;
        }

        AudioClip clip = AudioClip.Create("Beep", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);

        audioSource.PlayOneShot(clip, 0.8f);
        yield return new WaitForSeconds(duration + 0.1f);
        Destroy(clip);
    }

    /// <summary>
    /// Called when any car triggers an intermediate checkpoint
    /// </summary>
    public void OnCheckpointPassed(Checkpoint cp, Collider carCollider) {
        if (!IsPlayerCollider(carCollider)) return;

        int idx = cp.checkpointIndex;
        if (!passedCheckpoints.Contains(idx)) {
            passedCheckpoints.Add(idx);
            if (idx >= nextRequiredCheckpointIndex) {
                nextRequiredCheckpointIndex = idx + 1;
            }
            totalCheckpointsPassed++;
            Debug.Log($"[GameManager] Checkpoint {idx + 1}/{checkpoints.Count} passed! (Total cleared: {passedCheckpoints.Count})");
        }
    }

    /// <summary>
    /// Called when any car crosses the Finish Line
    /// </summary>
    public void OnFinishLineCrossed(Collider carCollider) {
        if (!IsPlayerCollider(carCollider)) return;
        if (raceFinished || !raceStarted) return;

        int requiredCpCount = checkpoints.Count;
        // Anti-cheat & valid lap validation:
        // Require at least 70% of checkpoints (e.g. at least 5 of 7) to be passed,
        // and at least one of the final checkpoints (idx 5 or 6) before the finish line.
        int minRequired = (requiredCpCount == 0) ? 0 : Mathf.Max(1, Mathf.CeilToInt(requiredCpCount * 0.7f));
        bool hasEnoughCheckpoints = (requiredCpCount == 0) || (passedCheckpoints.Count >= minRequired);
        bool clearedEndTrack = (requiredCpCount <= 2) || passedCheckpoints.Contains(requiredCpCount - 1) || passedCheckpoints.Contains(requiredCpCount - 2);

        if (hasEnoughCheckpoints && clearedEndTrack) {
            currentLap++;
            Debug.Log($"[GameManager] Lap {currentLap}/{totalLaps} completed in {FormatTime(raceTimer)}! ({passedCheckpoints.Count}/{requiredCpCount} checkpoints cleared)");

            if (currentLap >= totalLaps) {
                // RACE FINISHED!
                raceFinished = true;
                currentState = GameState.RaceFinished;
                finalTime = raceTimer;
                Debug.Log($"[GameManager] RACE FINISHED! Final Time: {FormatTime(finalTime)}");
            } else {
                passedCheckpoints.Clear();
                nextRequiredCheckpointIndex = 0;
            }
        } else {
            Debug.LogWarning($"[GameManager] Finish line crossed, but only {passedCheckpoints.Count}/{requiredCpCount} checkpoints cleared! (Anti-cheat prevented invalid lap)");
        }
    }

    private bool IsPlayerCollider(Collider col) {
        if (playerCar == null) return false;
        return col.transform.IsChildOf(playerCar.transform) || col.gameObject == playerCar.gameObject;
    }

    public void TryAgain() {
        Time.timeScale = 1f;
        if (playerCar != null) {
            PlayerPrefs.SetString("QuickRestartCarName", playerCar.gameObject.name);
            PlayerPrefs.Save();
        }
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ReturnToStartScreen() {
        Time.timeScale = 1f;
        PlayerPrefs.DeleteKey("QuickRestartCarName");
        PlayerPrefs.Save();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void RestartRace() {
        TryAgain();
    }

    private string FormatTime(float timeInSeconds) {
        TimeSpan t = TimeSpan.FromSeconds(timeInSeconds);
        return string.Format("{0:D2}:{1:D2}.{2:D2}", t.Minutes, t.Seconds, t.Milliseconds / 10);
    }

    private void InitStyles() {
        if (stylesInitialized) return;

        headerStyle = new GUIStyle(GUI.skin.label) {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperRight
        };
        headerStyle.normal.textColor = Color.white;

        infoStyle = new GUIStyle(GUI.skin.label) {
            fontSize = 15,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.UpperRight
        };
        infoStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);

        countdownStyle = new GUIStyle(GUI.skin.label) {
            fontSize = 90,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        countdownStyle.normal.textColor = Color.white;

        goalTipStyle = new GUIStyle(GUI.skin.label) {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        goalTipStyle.normal.textColor = new Color(1f, 0.9f, 0.3f);

        bannerStyle = new GUIStyle(GUI.skin.label) {
            fontSize = 32,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        bannerStyle.normal.textColor = new Color(1f, 0.85f, 0.1f); // Gold

        subBannerStyle = new GUIStyle(GUI.skin.label) {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        subBannerStyle.normal.textColor = Color.white;

        gameOverTitleStyle = new GUIStyle(GUI.skin.label) {
            fontSize = 42,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        gameOverTitleStyle.normal.textColor = new Color(1f, 0.2f, 0.15f); // Vivid Red

        gameOverReasonStyle = new GUIStyle(GUI.skin.label) {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        gameOverReasonStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f);

        stylesInitialized = true;
    }

    void OnGUI() {
        if (currentState == GameState.MainMenu || currentState == GameState.CarSelection) {
            return;
        }

        InitStyles();

        // 1. Top-right HUD: Lap, Time, Checkpoints
        float hudWidth = 260f;
        float hudHeight = 95f;
        float hudX = Screen.width - hudWidth - 20f;
        float hudY = 20f;

        GUI.Box(new Rect(hudX - 10, hudY - 5, hudWidth + 15, hudHeight), GUIContent.none);

        int displayLap = Mathf.Min(currentLap + (raceFinished ? 0 : 1), totalLaps);
        string lapText = $"LAP: {displayLap} / {totalLaps}";
        string timeText = $"TIME: {FormatTime(raceFinished ? finalTime : raceTimer)}";
        string cpText = $"CHECKPOINTS: {passedCheckpoints.Count} / {checkpoints.Count}";

        GUI.Label(new Rect(hudX, hudY, hudWidth, 26), lapText, headerStyle);
        GUI.Label(new Rect(hudX, hudY + 28, hudWidth, 24), timeText, infoStyle);
        GUI.Label(new Rect(hudX, hudY + 54, hudWidth, 22), cpText, infoStyle);

        // 2. Countdown Display (3 -> 2 -> 1 -> GO!)
        if (isCountingDown || goDisplayTimer > 0f) {
            float cdW = 300f;
            float cdH = 120f;
            float cdX = (Screen.width - cdW) * 0.5f;
            float cdY = (Screen.height - cdH) * 0.30f;

            if (isCountingDown) {
                countdownStyle.normal.textColor = new Color(1f, 0.3f, 0.2f); // Red
                GUI.Label(new Rect(cdX, cdY, cdW, cdH), countdownDisplay, countdownStyle);
            } else {
                countdownStyle.normal.textColor = new Color(0.2f, 1f, 0.3f); // Green GO!
                GUI.Label(new Rect(cdX, cdY, cdW, cdH), "GO!", countdownStyle);
            }
        }

        // 3. Center Finish Banner
        if (raceFinished) {
            float bannerW = 520f;
            float bannerH = 190f;
            float bannerX = (Screen.width - bannerW) * 0.5f;
            float bannerY = (Screen.height - bannerH) * 0.35f;

            GUI.Box(new Rect(bannerX, bannerY, bannerW, bannerH), GUIContent.none);

            GUI.Label(new Rect(bannerX, bannerY + 15, bannerW, 40), "HOÀN THÀNH CUỘC ĐUA!", bannerStyle);
            GUI.Label(new Rect(bannerX, bannerY + 60, bannerW, 30), $"THỜI GIAN HOÀN THÀNH: {FormatTime(finalTime)}", subBannerStyle);
            GUI.Label(new Rect(bannerX, bannerY + 100, bannerW, 25), "Nhấn phím [ R ] để chơi lại", infoStyle);

            float bBtnW = 180f;
            float bBtnH = 36f;
            float bSpacing = 20f;
            float bStartX = bannerX + (bannerW - (bBtnW * 2 + bSpacing)) * 0.5f;
            float bBtnY = bannerY + 135f;

            if (GUI.Button(new Rect(bStartX, bBtnY, bBtnW, bBtnH), "CHƠI LẠI (R)")) {
                TryAgain();
            }

            if (GUI.Button(new Rect(bStartX + bBtnW + bSpacing, bBtnY, bBtnW, bBtnH), "TRỞ VỀ BẮT ĐẦU")) {
                ReturnToStartScreen();
            }
        }

        // 4. Center Game Over Banner
        if (currentState == GameState.GameOver) {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            float goW = 540f;
            float goH = 205f;
            float goX = (Screen.width - goW) * 0.5f;
            float goY = (Screen.height - goH) * 0.35f;

            GUI.Box(new Rect(goX, goY, goW, goH), GUIContent.none);

            GUI.Label(new Rect(goX, goY + 15, goW, 45), "💥 GAME OVER", gameOverTitleStyle);
            GUI.Label(new Rect(goX, goY + 65, goW, 30), gameOverReason, gameOverReasonStyle);
            GUI.Label(new Rect(goX, goY + 102, goW, 25), "Nhấn phím [ R ] để thử lại  •  [ ESC ] Về màn hình bắt đầu", infoStyle);

            float bBtnW = 190f;
            float bBtnH = 42f;
            float bSpacing = 20f;
            float bStartX = goX + (goW - (bBtnW * 2 + bSpacing)) * 0.5f;
            float bBtnY = goY + 140f;

            GUI.backgroundColor = new Color(0.95f, 0.22f, 0.2f);
            if (GUI.Button(new Rect(bStartX, bBtnY, bBtnW, bBtnH), "THỬ LẠI (R)")) {
                TryAgain();
            }

            GUI.backgroundColor = new Color(0.2f, 0.6f, 0.95f);
            if (GUI.Button(new Rect(bStartX + bBtnW + bSpacing, bBtnY, bBtnW, bBtnH), "TRỞ VỀ BẮT ĐẦU")) {
                ReturnToStartScreen();
            }
            GUI.backgroundColor = Color.white;
        }
    }
}
