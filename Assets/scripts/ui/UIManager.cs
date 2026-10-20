using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour {

    public static UIManager Instance { get; private set; }

    [System.Serializable]
    public class CarDisplayData {
        public CarStateMachine carMachine;
        public string displayName;
        public string carClass;
        public float topSpeed; // km/h
        public float acceleration; // 0 - 10
        public float handling; // 0 - 10
        public string drivetrain; // AWD, RWD...
        public Color themeColor = Color.white;
    }

    [Header("Cars Available")]
    public List<CarDisplayData> carList = new List<CarDisplayData>();
    public int currentCarIndex = 0;

    [Header("Camera Transitions")]
    private Transform mainCameraTransform;
    private CamraController originalCamController;
    private bool isPreviewingCar = false;
    private Vector3 targetCamPos;
    private Quaternion targetCamRot;
    private float camLerpSpeed = 4f;

    [Header("GUI Styles")]
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle headerStyle;
    private GUIStyle carNameStyle;
    private GUIStyle specLabelStyle;
    private GUIStyle buttonPrimaryStyle;
    private GUIStyle buttonSecondaryStyle;
    private GUIStyle navButtonStyle;
    private GUIStyle descStyle;
    private Texture2D panelBackgroundTex;
    private Texture2D cardBackgroundTex;
    private Texture2D barBackgroundTex;
    private Texture2D barFillTex;
    private bool stylesInitialized = false;

    void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start() {
        if (Camera.main != null) {
            mainCameraTransform = Camera.main.transform;
            originalCamController = Camera.main.GetComponent<CamraController>();
        }

        FindAndSetupCars();
    }

    public void FindAndSetupCars() {
        carList.Clear();
        var foundCars = FindObjectsByType<CarStateMachine>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var car in foundCars) {
            CarDisplayData data = new CarDisplayData();
            data.carMachine = car;

            string lowerName = car.gameObject.name.ToLower();
            if (lowerName.Contains("ferrari") || lowerName.Contains("testarosa")) {
                data.displayName = "FERRARI TESTAROSSA";
                data.carClass = "SUPERCAR CLASSIC";
                data.topSpeed = 330f;
                data.acceleration = 9.0f;
                data.handling = 8.9f;
                data.drivetrain = "RWD (Rear Wheel Drive)";
                data.themeColor = new Color(1f, 0.2f, 0.2f);
            } else if (lowerName.Contains("r 35") || lowerName.Contains("r35") || lowerName.Contains("gtr")) {
                data.displayName = "NISSAN GT-R R35 NISMO";
                data.carClass = "SUPERCAR TRACK BEAST";
                data.topSpeed = 315f;
                data.acceleration = 9.8f;
                data.handling = 9.5f;
                data.drivetrain = "AWD (All Wheel Drive)";
                data.themeColor = new Color(0.2f, 0.7f, 1f);
            } else if (lowerName.Contains("c63") || lowerName.Contains("c 63")) {
                data.displayName = "MERCEDES-AMG C63";
                data.carClass = "V8 MUSCLE SEDAN";
                data.topSpeed = 295f;
                data.acceleration = 8.6f;
                data.handling = 8.7f;
                data.drivetrain = "RWD (Rear Wheel Drive)";
                data.themeColor = new Color(1f, 0.8f, 0.1f);
            } else {
                data.displayName = car.gameObject.name.ToUpper();
                data.carClass = "RACING VEHICLE";
                data.topSpeed = 300f;
                data.acceleration = 8.5f;
                data.handling = 8.5f;
                data.drivetrain = "AWD";
                data.themeColor = Color.white;
            }

            carList.Add(data);
        }

        // Sort so Ferrari is first or R35 is first
        carList.Sort((a, b) => string.Compare(a.displayName, b.displayName, System.StringComparison.Ordinal));
        currentCarIndex = 0;
    }

    void Update() {
        if (GameManager.Instance == null) return;

        // In Menu or Car Selection: smoothly orbit or frame the camera
        if (GameManager.Instance.currentState == GameState.MainMenu) {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (originalCamController != null) originalCamController.enabled = false;

            // Overview camera overlooking the starting grid
            Vector3 menuCamPos = new Vector3(8.5f, 7.8f, 14.5f);
            Quaternion menuCamRot = Quaternion.Euler(14f, -42f, 0f);

            if (mainCameraTransform != null) {
                mainCameraTransform.position = Vector3.Lerp(mainCameraTransform.position, menuCamPos, Time.deltaTime * 2.5f);
                mainCameraTransform.rotation = Quaternion.Slerp(mainCameraTransform.rotation, menuCamRot, Time.deltaTime * 2.5f);
            }
        } else if (GameManager.Instance.currentState == GameState.CarSelection) {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (originalCamController != null) originalCamController.enabled = false;

            // Keyboard navigation shortcuts
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) {
                PreviousCar();
            }
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) {
                NextCar();
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) {
                StartRaceWithCurrentCar();
            }
            if (Input.GetKeyDown(KeyCode.Escape)) {
                GameManager.Instance.ShowMainMenu();
            }

            // Focus on currently selected car
            if (currentCarIndex >= 0 && currentCarIndex < carList.Count && carList[currentCarIndex].carMachine != null) {
                Transform carT = carList[currentCarIndex].carMachine.transform;
                // 3/4 front view
                Vector3 targetPos = carT.position + carT.forward * 4.6f + carT.right * 3.4f + Vector3.up * 1.6f;
                Vector3 lookTarget = carT.position + Vector3.up * 0.75f;
                Quaternion targetRot = Quaternion.LookRotation((lookTarget - targetPos).normalized);

                if (mainCameraTransform != null) {
                    mainCameraTransform.position = Vector3.Lerp(mainCameraTransform.position, targetPos, Time.deltaTime * camLerpSpeed);
                    mainCameraTransform.rotation = Quaternion.Slerp(mainCameraTransform.rotation, targetRot, Time.deltaTime * camLerpSpeed);
                }
            }
        } else {
            // Racing or Countdown
            if (originalCamController != null && !originalCamController.enabled) {
                originalCamController.enabled = true;
            }
        }
    }

    private Texture2D MakeSolidTexture(Color col) {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, col);
        tex.Apply();
        return tex;
    }

    private void InitStyles() {
        if (stylesInitialized) return;

        panelBackgroundTex = MakeSolidTexture(new Color(0.05f, 0.07f, 0.12f, 0.88f));
        cardBackgroundTex = MakeSolidTexture(new Color(0.12f, 0.16f, 0.24f, 0.90f));
        barBackgroundTex = MakeSolidTexture(new Color(0.2f, 0.25f, 0.35f, 0.95f));
        barFillTex = MakeSolidTexture(new Color(0.1f, 0.9f, 0.4f, 1f));

        titleStyle = new GUIStyle(GUI.skin.label) {
            fontSize = 42,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = new Color(1f, 0.85f, 0.2f); // Gold / Yellow

        subtitleStyle = new GUIStyle(GUI.skin.label) {
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        subtitleStyle.normal.textColor = new Color(0.85f, 0.9f, 1f);

        headerStyle = new GUIStyle(GUI.skin.label) {
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        headerStyle.normal.textColor = Color.white;

        carNameStyle = new GUIStyle(GUI.skin.label) {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        carNameStyle.normal.textColor = Color.white;

        specLabelStyle = new GUIStyle(GUI.skin.label) {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        specLabelStyle.normal.textColor = new Color(0.8f, 0.85f, 0.95f);

        descStyle = new GUIStyle(GUI.skin.label) {
            fontSize = 14,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleLeft
        };
        descStyle.normal.textColor = new Color(0.7f, 0.75f, 0.85f);

        buttonPrimaryStyle = new GUIStyle(GUI.skin.button) {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        buttonPrimaryStyle.normal.textColor = Color.white;

        buttonSecondaryStyle = new GUIStyle(GUI.skin.button) {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        buttonSecondaryStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);

        navButtonStyle = new GUIStyle(GUI.skin.button) {
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        navButtonStyle.normal.textColor = Color.white;

        stylesInitialized = true;
    }

    void OnGUI() {
        if (GameManager.Instance == null) return;
        InitStyles();

        switch (GameManager.Instance.currentState) {
            case GameState.MainMenu:
                DrawMainMenu();
                break;
            case GameState.CarSelection:
                DrawCarSelection();
                break;
            case GameState.RaceFinished:
                DrawRaceFinishedMenuButtons();
                break;
        }
    }

    private void DrawMainMenu() {
        float menuW = 540f;
        float menuH = 380f;
        float menuX = (Screen.width - menuW) * 0.5f;
        float menuY = (Screen.height - menuH) * 0.45f;

        // Dark backdrop box
        GUI.DrawTexture(new Rect(menuX, menuY, menuW, menuH), panelBackgroundTex);

        // Header Title
        GUI.Label(new Rect(menuX, menuY + 25, menuW, 50), "🏁 F1 RACING ROAD", titleStyle);
        GUI.Label(new Rect(menuX, menuY + 75, menuW, 30), "ARCADE GRAND PRIX SIMULATOR", subtitleStyle);

        // Decorative line
        GUI.DrawTexture(new Rect(menuX + 50, menuY + 115, menuW - 100, 2), barFillTex);

        // Buttons
        float btnW = 340f;
        float btnH = 55f;
        float btnX = menuX + (menuW - btnW) * 0.5f;

        // Button 1: Start Race (Go to Car Selection)
        GUI.backgroundColor = new Color(0.1f, 0.85f, 0.35f); // Vivid Green
        if (GUI.Button(new Rect(btnX, menuY + 150, btnW, btnH), "CHỌN XE VÀO ĐUA ➔", buttonPrimaryStyle)) {
            GameManager.Instance.ShowCarSelection();
        }

        // Button 2: Quick Race with Default Car
        GUI.backgroundColor = new Color(0.15f, 0.6f, 0.95f); // Electric Blue
        if (GUI.Button(new Rect(btnX, menuY + 225, btnW, 46), "⚡ ĐUA NGAY (XE MẶC ĐỊNH)", buttonSecondaryStyle)) {
            if (carList.Count > 0) {
                GameManager.Instance.StartRaceWithSelectedCar(carList[0].carMachine);
            }
        }

        // Button 3: Quit Game
        GUI.backgroundColor = new Color(0.35f, 0.4f, 0.48f);
        if (GUI.Button(new Rect(btnX, menuY + 290, btnW, 42), "THOÁT GAME", buttonSecondaryStyle)) {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        GUI.backgroundColor = Color.white;
    }

    private void DrawCarSelection() {
        if (carList.Count == 0) {
            FindAndSetupCars();
        }

        float screenW = Screen.width;
        float screenH = Screen.height;

        // 1. Top Bar
        float topH = 80f;
        GUI.DrawTexture(new Rect(0, 0, screenW, topH), panelBackgroundTex);
        GUI.Label(new Rect(0, 15, screenW, 35), "CHỌN CHIẾC XE CỦA BẠN", titleStyle);
        GUI.Label(new Rect(0, 52, screenW, 25), "Chỉ có chiếc xe bạn chọn sẽ xuất phát trên đường đua!", subtitleStyle);

        if (carList.Count == 0) return;
        CarDisplayData car = carList[currentCarIndex];

        // 2. Navigation Arrows (Left / Right) on screen edges
        float navSize = 65f;
        float navY = screenH * 0.48f;

        GUI.backgroundColor = new Color(0.2f, 0.3f, 0.45f, 0.9f);
        if (GUI.Button(new Rect(40, navY, navSize, navSize), "◀", navButtonStyle)) {
            PreviousCar();
        }
        if (GUI.Button(new Rect(screenW - 40 - navSize, navY, navSize, navSize), "▶", navButtonStyle)) {
            NextCar();
        }

        // 3. Bottom Stats & Selection Card
        float cardW = 680f;
        float cardH = 260f;
        float cardX = (screenW - cardW) * 0.5f;
        float cardY = screenH - cardH - 25f;

        GUI.DrawTexture(new Rect(cardX, cardY, cardW, cardH), cardBackgroundTex);

        // Header inside card: Car Name & Car Index
        carNameStyle.normal.textColor = car.themeColor;
        GUI.Label(new Rect(cardX + 30, cardY + 15, 450, 32), car.displayName, carNameStyle);
        GUI.Label(new Rect(cardX + cardW - 180, cardY + 18, 150, 25), $"{currentCarIndex + 1} / {carList.Count}", subtitleStyle);

        GUI.Label(new Rect(cardX + 30, cardY + 45, 300, 20), $"PHÂN HẠNG: {car.carClass}", descStyle);
        GUI.Label(new Rect(cardX + 30, cardY + 65, 300, 20), $"DẪN ĐỘNG: {car.drivetrain}", descStyle);

        // Stat Bars:
        DrawStatBar(cardX + 30, cardY + 95, 380, "TỐC ĐỘ TỐI ĐA", $"{car.topSpeed} km/h", car.topSpeed / 360f, new Color(0.2f, 0.8f, 1f));
        DrawStatBar(cardX + 30, cardY + 135, 380, "GIA TỐC", $"{car.acceleration:0.0} / 10", car.acceleration / 10f, new Color(1f, 0.4f, 0.3f));
        DrawStatBar(cardX + 30, cardY + 175, 380, "ĐỘ BÁM CUA", $"{car.handling:0.0} / 10", car.handling / 10f, new Color(0.3f, 0.95f, 0.4f));

        // Right side Action Buttons
        float actW = 200f;
        float actX = cardX + cardW - actW - 30;

        // Big Select & Race Button
        GUI.backgroundColor = new Color(0.1f, 0.85f, 0.35f);
        if (GUI.Button(new Rect(actX, cardY + 95, actW, 70), "🏁 VÀO ĐUA!", buttonPrimaryStyle)) {
            StartRaceWithCurrentCar();
        }

        // Back to Menu Button
        GUI.backgroundColor = new Color(0.4f, 0.45f, 0.55f);
        if (GUI.Button(new Rect(actX, cardY + 180, actW, 45), "↩ QUAY LẠI", buttonSecondaryStyle)) {
            GameManager.Instance.ShowMainMenu();
        }

        // Hotkey guidance hint
        GUI.Label(new Rect(cardX + 30, cardY + 222, 420, 25), "💡 Phím tắt: [A]/[D] hoặc [◀]/[▶] chuyển xe  •  [ENTER] Vào đua  •  [ESC] Quay lại", descStyle);

        GUI.backgroundColor = Color.white;
    }

    private void DrawStatBar(float x, float y, float width, string title, string valText, float fillRatio, Color fillColor) {
        GUI.Label(new Rect(x, y, 160, 20), title, specLabelStyle);
        GUI.Label(new Rect(x + width - 90, y, 90, 20), valText, specLabelStyle);

        float barH = 10f;
        float barY = y + 22;
        GUI.DrawTexture(new Rect(x, barY, width, barH), barBackgroundTex);

        Color prev = GUI.color;
        GUI.color = fillColor;
        GUI.DrawTexture(new Rect(x, barY, width * Mathf.Clamp01(fillRatio), barH), barFillTex);
        GUI.color = prev;
    }

    private void PreviousCar() {
        if (carList.Count == 0) return;
        currentCarIndex--;
        if (currentCarIndex < 0) currentCarIndex = carList.Count - 1;
    }

    private void NextCar() {
        if (carList.Count == 0) return;
        currentCarIndex++;
        if (currentCarIndex >= carList.Count) currentCarIndex = 0;
    }

    public void StartRaceWithCurrentCar() {
        if (currentCarIndex >= 0 && currentCarIndex < carList.Count) {
            CarStateMachine selected = carList[currentCarIndex].carMachine;
            GameManager.Instance.StartRaceWithSelectedCar(selected);
        }
    }

    private void DrawRaceFinishedMenuButtons() {
        float btnW = 220f;
        float btnH = 40f;
        float startX = (Screen.width - (btnW * 2 + 20)) * 0.5f;
        float startY = (Screen.height * 0.35f) + 195f;

        GUI.backgroundColor = new Color(0.15f, 0.7f, 0.3f);
        if (GUI.Button(new Rect(startX, startY, btnW, btnH), "🔄 ĐUA LẠI", buttonSecondaryStyle)) {
            GameManager.Instance.RestartRace();
        }

        GUI.backgroundColor = new Color(0.2f, 0.5f, 0.9f);
        if (GUI.Button(new Rect(startX + btnW + 20, startY, btnW, btnH), "🚗 ĐỔI XE / MENU", buttonSecondaryStyle)) {
            // Reload scene to restore all cars for menu selection
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        GUI.backgroundColor = Color.white;
    }
}
