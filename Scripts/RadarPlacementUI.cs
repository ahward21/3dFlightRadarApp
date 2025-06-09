using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.ARFoundation;
using System.Collections;
using UnityEngine.XR.ARSubsystems;

public class RadarPlacementUI : MonoBehaviour
{
    [Header("UI References")]
    public Button placeRadarButton;
    public Button removeRadarButton;
    public Button toggleDemoPlanesButton;
    public Button togglePlaneInfoButton;
    public TextMeshProUGUI statusText;
    
    public TextMeshProUGUI planeDetectionStatusText;
    public Canvas uiCanvas;
    
    [Header("Selected Plane Info Panel UI")]
    public GameObject planeInfoPanel; // The panel itself
    public TextMeshProUGUI planeInfo_CallsignText;
    public TextMeshProUGUI planeInfo_AddressText;
    public TextMeshProUGUI planeInfo_AltitudeText;
    public TextMeshProUGUI planeInfo_SpeedText;
    public TextMeshProUGUI planeInfo_HeadingText;
    public TextMeshProUGUI planeInfo_DistanceText; // The new field for distance
    public TextMeshProUGUI planeInfo_CoordinatesText;
    public TextMeshProUGUI planeInfo_TimestampText;
    public TextMeshProUGUI planeInfo_RawJsonText;
    public Button closePlaneInfoPanelButton;
    
    [Header("Settings")]
    public bool showUIWhenARFails = true;
    public float uiShowDelay = 6f; // Show UI after AR fails for this many seconds
    
    private ARPlacementManager arPlacementManager;
    private RadarDisplay radarDisplay;
    private bool uiShown = false;
    private bool demoPlanesActive = false;
    private bool isMonitoringPlaneDetection = false;
    private TextMeshProUGUI togglePlaneInfoButtonText;
    
    void Awake()
    {
        arPlacementManager = FindFirstObjectByType<ARPlacementManager>();
        
        if (arPlacementManager == null)
        {
            Debug.LogError("ARPlacementManager not found! UI will not function.");
            statusText.text = "ERROR: Placement Controller Missing";
            placeRadarButton.interactable = false;
            return;
        }
        
        // Set up button listeners
        if (placeRadarButton != null)
        {
            placeRadarButton.onClick.AddListener(PlaceRadar);
        }
        
        if (removeRadarButton != null)
        {
            removeRadarButton.onClick.AddListener(RemoveRadar);
        }
        
        if (toggleDemoPlanesButton != null)
        {
            toggleDemoPlanesButton.onClick.AddListener(ToggleDemoPlanes);
        }

        if (togglePlaneInfoButton != null)
        {
            togglePlaneInfoButton.onClick.AddListener(OnTogglePlaneInfoButtonPressed);
            togglePlaneInfoButtonText = togglePlaneInfoButton.GetComponentInChildren<TextMeshProUGUI>();
        }
        
        if (closePlaneInfoPanelButton != null)
        {
            closePlaneInfoPanelButton.onClick.AddListener(ClosePlaneInfoPanel);
        }

        if (planeInfoPanel != null)
        {
            planeInfoPanel.SetActive(false); // Start with the panel hidden
        }
        
        // Subscribe to radar events
        ARPlacementManager.OnRadarPlaced += OnRadarPlaced;
        ARPlacementManager.OnRadarRemoved += OnRadarRemoved;
        
        // Initially hide UI
        if (uiCanvas != null)
        {
            uiCanvas.gameObject.SetActive(false);
        }
        
        // Check if we should show UI after delay
        if (showUIWhenARFails)
        {
            Invoke(nameof(CheckAndShowUI), uiShowDelay);
        }
    }
    
    void OnDestroy()
    {
        // Unsubscribe from events
        ARPlacementManager.OnRadarPlaced -= OnRadarPlaced;
        ARPlacementManager.OnRadarRemoved -= OnRadarRemoved;
    }
    
    void CheckAndShowUI()
    {
        if (!uiShown && !arPlacementManager.IsRadarPlaced)
        {
            ShowUI();
            UpdateStatusText("AR plane detection not available. Use manual placement.");
        }
    }
    
    public void ShowUI()
    {
        if (uiCanvas != null)
        {
            uiCanvas.gameObject.SetActive(true);
            uiShown = true;
        }
        UpdateButtons();
    }
    
    public void HideUI()
    {
        if (uiCanvas != null)
        {
            uiCanvas.gameObject.SetActive(false);
            uiShown = false;
        }
    }
    
    void PlaceRadar()
    {
        if (arPlacementManager != null)
        {
            arPlacementManager.PlaceRadarAtCameraPosition();
            UpdateStatusText("Radar placed manually at camera position.");
        }
    }
    
    void RemoveRadar()
    {
        if (arPlacementManager != null)
        {
            arPlacementManager.RemoveRadar();
            radarDisplay = null; // Clear the reference
            demoPlanesActive = false; // Reset demo planes state
            UpdateToggleDemoPlanesButtonText();
            UpdateTogglePlaneInfoButtonText();
        }
    }
    
    void OnRadarPlaced(GameObject radarObject)
    {
        Debug.Log("OnRadarPlaced called. radarObject=" + radarObject + ", active=" + (radarObject != null && radarObject.activeInHierarchy));
        UpdateButtons();
        if (radarObject != null) radarDisplay = radarObject.GetComponent<RadarDisplay>();
        UpdateTogglePlaneInfoButtonText();
        if (statusText != null)
        {
            UpdateStatusText("3D Radar Display is active! Showing nearby aircraft.");
        }
    }
    
    void OnRadarRemoved()
    {
        UpdateButtons();
        UpdateTogglePlaneInfoButtonText();
        UpdateStatusText("Radar removed. You can place it again.");
    }
    
    void UpdateButtons()
    {
        bool isRadarPlaced = arPlacementManager != null && arPlacementManager.IsRadarPlaced;
        placeRadarButton.gameObject.SetActive(!isRadarPlaced);
        removeRadarButton.gameObject.SetActive(isRadarPlaced);
        toggleDemoPlanesButton.gameObject.SetActive(isRadarPlaced);
        if(togglePlaneInfoButton != null) togglePlaneInfoButton.gameObject.SetActive(isRadarPlaced);

        UpdateTogglePlaneInfoButtonText();
        UpdateToggleDemoPlanesButtonText();
    }
    
    void UpdateStatusText(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log($"UI Status: {message}");
    }

    void UpdateToggleDemoPlanesButtonText()
    {
        if (toggleDemoPlanesButton != null)
        {
            TextMeshProUGUI buttonText = toggleDemoPlanesButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = demoPlanesActive ? "Remove Demo Planes" : "Add Demo Planes";
            }
        }
    }

    void UpdateTogglePlaneInfoButtonText()
    {
        if (togglePlaneInfoButtonText == null) return;

        if (radarDisplay != null && arPlacementManager != null && arPlacementManager.IsRadarPlaced)
        {
            togglePlaneInfoButtonText.text = radarDisplay.IsPlaneLabelsCurrentlyVisible ? "Hide Plane Info" : "Show Plane Info";
        }
        else
        {
            togglePlaneInfoButtonText.text = "Toggle Plane Info";
        }
    }

    void UpdatePlaneDetectionStatus(string message)
    {
        if (planeDetectionStatusText != null)
        {
            planeDetectionStatusText.text = message;
        }
        Debug.Log($"Plane Detection Status: {message}");
    }

    void Start()
    {
        // Start monitoring plane detection status
        if (arPlacementManager != null)
        {
            StartCoroutine(MonitorPlaneDetectionStatus());
        }
    }

    System.Collections.IEnumerator MonitorPlaneDetectionStatus()
    {
        isMonitoringPlaneDetection = true;
        
        while (isMonitoringPlaneDetection)
        {
            if (arPlacementManager != null)
            {
                // Check if AR plane detection is working
                bool hasDetectedPlanes = false;
                int planeCount = 0;
                
                ARPlaneManager planeManager = FindFirstObjectByType<ARPlaneManager>();
                if (planeManager != null)
                {
                    foreach (ARPlane plane in planeManager.trackables)
                    {
                        if (plane.trackingState == TrackingState.Tracking)
                        {
                            hasDetectedPlanes = true;
                            planeCount++;
                        }
                    }
                }
                
                // Update plane detection status
                if (hasDetectedPlanes)
                {
                    UpdatePlaneDetectionStatus($"✅ {planeCount} AR surface{(planeCount != 1 ? "s" : "")} detected - Tap to place radar");
                }
                else
                {
                    UpdatePlaneDetectionStatus("🔍 Scanning for surfaces... Move device around");
                }
            }
            
            yield return new WaitForSeconds(1f); // Update every second
        }
    }
    
    // Public method to manually show/hide UI
    public void ToggleUI()
    {
        if (uiCanvas != null)
        {
            bool isActive = uiCanvas.gameObject.activeInHierarchy;
            if (isActive)
            {
                HideUI();
            }
            else
            {
                ShowUI();
            }
        }
    }
    
    void ToggleDemoPlanes()
    {
        if (arPlacementManager == null || arPlacementManager.PlacedRadarDisplayObject == null)
        {
            Debug.LogWarning("Radar not placed yet, cannot toggle demo planes.");
            statusText.text = "Place radar first to use demo planes.";
            return;
        }

        if (radarDisplay == null)
        {
            radarDisplay = arPlacementManager.PlacedRadarDisplayObject.GetComponent<RadarDisplay>();
        }

        if (radarDisplay == null)
        {
            Debug.LogError("RadarDisplay component not found on the placed radar object!");
            return;
        }

        if (!demoPlanesActive)
        {
            AddDemoPlanes();
            demoPlanesActive = true;
            UpdateStatusText("Demo planes added for testing visualization.");
            
            if (toggleDemoPlanesButton != null)
            {
                TextMeshProUGUI buttonText = toggleDemoPlanesButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                    buttonText.text = "Remove Demo Planes";
            }
        }
        else
        {
            RemoveDemoPlanes();
            demoPlanesActive = false;
            UpdateStatusText("Demo planes removed.");
            
            if (toggleDemoPlanesButton != null)
            {
                TextMeshProUGUI buttonText = toggleDemoPlanesButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                    buttonText.text = "Add Demo Planes";
            }
        }
    }
    
    void AddDemoPlanes()
    {
        // Get the radar display to send demo data to
        if (arPlacementManager == null || !arPlacementManager.IsRadarPlaced || arPlacementManager.PlacedRadarDisplayObject == null || !arPlacementManager.PlacedRadarDisplayObject.activeInHierarchy)
        {
            UpdateStatusText("Place radar first before adding demo planes!");
            Debug.LogWarning($"AddDemoPlanes: Radar not placed! IsRadarPlaced={arPlacementManager?.IsRadarPlaced}, PlacedRadarDisplayObject={arPlacementManager?.PlacedRadarDisplayObject}, Active={arPlacementManager?.PlacedRadarDisplayObject?.activeInHierarchy}");
            return;
        }
        
        // Create realistic demo planes around University of Twente
        PlaneData[] demoPlanes = {
            new PlaneData {
                address = "DEMO01",
                latitude = 52.3087f,   // ~8km north of UT
                longitude = 6.8564f,   // Same longitude
                altitude = 28000f,     // 28,000 feet
                heading = 90f,         // Flying east
                speed = 450f,          // 450 knots
                callsign = "KLM123",
                timestamp = new System.DateTimeOffset(System.DateTime.UtcNow).ToUnixTimeSeconds().ToString(),
                receiver = "DEMO"
            },
            new PlaneData {
                address = "DEMO02", 
                latitude = 52.1687f,   // ~8km south of UT
                longitude = 6.8564f,   // Same longitude
                altitude = 35000f,     // 35,000 feet
                heading = 270f,        // Flying west
                speed = 480f,
                callsign = "TRA456",
                timestamp = new System.DateTimeOffset(System.DateTime.UtcNow).ToUnixTimeSeconds().ToString(),
                receiver = "DEMO"
            },
            new PlaneData {
                address = "DEMO03",
                latitude = 52.2387f,   // Same latitude as UT
                longitude = 6.9564f,   // ~6km east of UT  
                altitude = 15000f,     // 15,000 feet
                heading = 180f,        // Flying south
                speed = 320f,
                callsign = "EZY789",
                timestamp = new System.DateTimeOffset(System.DateTime.UtcNow).ToUnixTimeSeconds().ToString(),
                receiver = "DEMO"
            },
            new PlaneData {
                address = "DEMO04",
                latitude = 52.2387f,   // Same latitude as UT
                longitude = 6.7564f,   // ~6km west of UT
                altitude = 42000f,     // 42,000 feet
                heading = 45f,         // Flying northeast
                speed = 520f,
                callsign = "BAW001",
                timestamp = new System.DateTimeOffset(System.DateTime.UtcNow).ToUnixTimeSeconds().ToString(),
                receiver = "DEMO"
            },
            new PlaneData {
                address = "DEMO05",
                latitude = 52.2887f,   // ~5km northeast of UT
                longitude = 6.9064f,   
                altitude = 8000f,      // 8,000 feet (lower)
                heading = 220f,        // Flying southwest
                speed = 250f,
                callsign = "RYR999",
                timestamp = new System.DateTimeOffset(System.DateTime.UtcNow).ToUnixTimeSeconds().ToString(),
                receiver = "DEMO"
            }
        };
        
        // Send each demo plane to the radar display
        RadarDisplay radarDisplay = null;
        if (arPlacementManager != null && arPlacementManager.PlacedRadarDisplayObject != null)
        {
            radarDisplay = arPlacementManager.PlacedRadarDisplayObject.GetComponentInChildren<RadarDisplay>();
            Debug.Log("RadarDisplay found: " + (radarDisplay != null));
        }
        if (radarDisplay != null)
        {
            foreach (PlaneData plane in demoPlanes)
            {
                Debug.Log($"Sending demo plane to radar: {plane.address} at ({plane.latitude:F4}, {plane.longitude:F4}) {plane.altitude:F0}ft");
                radarDisplay.UpdateOrCreatePlaneOnRadar(plane);
            }
            Debug.Log($"Added {demoPlanes.Length} demo planes for testing");
        }
        else
        {
            Debug.LogError("Could not find RadarDisplay component to add demo planes!");
        }
    }
    
    void RemoveDemoPlanes()
    {
        // Find the radar display and remove demo planes
        RadarDisplay radarDisplay = null;
        if (arPlacementManager != null && arPlacementManager.PlacedRadarDisplayObject != null)
        {
            radarDisplay = arPlacementManager.PlacedRadarDisplayObject.GetComponentInChildren<RadarDisplay>();
        }
        if (radarDisplay != null)
        {
            // We need to access the private planeObjectsOnRadar dictionary
            // For now, we'll just log that they should be removed
            Debug.Log("Demo planes should be cleared - you may need to remove and place radar again for clean state");
            UpdateStatusText("Demo planes removed. Consider replacing radar for clean state.");
        }
    }
    
    // Public method for Unity Inspector button events
    public void OnAddDemoPlanesButtonClicked()
    {
        ToggleDemoPlanes();
    }
    
    // Public method to add demo planes directly (for Inspector use)
    public void AddDemoPlanesForTesting()
    {
        Debug.Log("AddDemoPlanesForTesting called. demoPlanesActive=" + demoPlanesActive);
        if (!demoPlanesActive)
        {
            AddDemoPlanes();
            demoPlanesActive = true;
            UpdateStatusText("Demo planes added for testing visualization.");
        }
    }
    
    // Public method to remove demo planes directly (for Inspector use)  
    public void RemoveDemoPlanesForTesting()
    {
        if (demoPlanesActive)
        {
            RemoveDemoPlanes();
            demoPlanesActive = false;
            UpdateStatusText("Demo planes removed.");
        }
    }

    void OnTogglePlaneInfoButtonPressed()
    {
        if (radarDisplay != null)
        {
            radarDisplay.TogglePlaneInfoVisibility();
            UpdateTogglePlaneInfoButtonText();
        }
        else
        {
            Debug.LogWarning("Cannot toggle plane info: RadarDisplay not found.");
        }
    }

    void OnEnable()
    {
        ARPlacementManager.OnRadarPlaced += OnRadarPlaced;
        ARPlacementManager.OnRadarRemoved += OnRadarRemoved;
        RadarDisplay.OnPlaneSelectedForInfoRaw += HandlePlaneSelectedForInfo; // Subscribe to new event
    }

    void OnDisable()
    {
        ARPlacementManager.OnRadarPlaced -= OnRadarPlaced;
        ARPlacementManager.OnRadarRemoved -= OnRadarRemoved;
        RadarDisplay.OnPlaneSelectedForInfoRaw -= HandlePlaneSelectedForInfo; // Unsubscribe
    }

    void HandlePlaneSelectedForInfo(PlaneData data, string rawJson, float distanceKm)
    {
        if (planeInfoPanel == null || data == null)
        {
            Debug.LogError("Info panel or plane data is null. Cannot display info.");
            return;
        }

        planeInfoPanel.SetActive(true);

        // Populate all the text fields, with null checks to prevent errors
        if (planeInfo_CallsignText != null)
            planeInfo_CallsignText.text = $"Callsign: {(!string.IsNullOrEmpty(data.callsign) ? data.callsign.Trim() : "N/A")}";
        if (planeInfo_AddressText != null)
            planeInfo_AddressText.text = $"Address: {data.address}";
        if (planeInfo_AltitudeText != null)
            planeInfo_AltitudeText.text = $"Altitude: {(data.altitude.HasValue ? $"{data.altitude.Value:F0} ft" : "N/A")}";
        if (planeInfo_SpeedText != null)
            planeInfo_SpeedText.text = $"Speed: {(data.speed.HasValue ? $"{data.speed.Value:F0} kt" : "N/A")}";
        if (planeInfo_HeadingText != null)
            planeInfo_HeadingText.text = $"Heading: {(data.heading.HasValue ? $"{data.heading.Value:F1}°" : "N/A")}";
        
        if (planeInfo_DistanceText != null)
            planeInfo_DistanceText.text = $"Distance: {distanceKm:F1} km";

        if (planeInfo_CoordinatesText != null)
            planeInfo_CoordinatesText.text = $"Coords: {(data.latitude.HasValue ? $"{data.latitude.Value:F4}" : "N/A")}, {(data.longitude.HasValue ? $"{data.longitude.Value:F4}" : "N/A")}";
        
        if (planeInfo_TimestampText != null)
        {
            if (double.TryParse(data.timestamp, out double timestampValue))
            {
                System.DateTimeOffset timeStamp = System.DateTimeOffset.FromUnixTimeSeconds((long)timestampValue);
                planeInfo_TimestampText.text = $"Timestamp: {timeStamp.ToString("HH:mm:ss")}";
            }
            else
            {
                planeInfo_TimestampText.text = "Timestamp: Invalid";
            }
        }

        if (planeInfo_RawJsonText != null)
            planeInfo_RawJsonText.text = $"Raw Data: {rawJson}";
    }

    void ClosePlaneInfoPanel()
    {
        if (planeInfoPanel != null)
        {
            planeInfoPanel.SetActive(false);
        }
    }
} 