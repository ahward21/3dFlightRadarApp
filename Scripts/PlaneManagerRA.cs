// ARPlaneManager.cs
using UnityEngine;
using System.Collections.Generic;
using TMPro; // If you want to display text labels
using UnityEngine.XR.ARFoundation;

public class PlaneManagerRA : MonoBehaviour
{
    [Header("Real-World AR Plane Settings")]
    public GameObject realWorldPlanePrefab; // Assign a 3D model for full-scale AR planes
    private Dictionary<string, GameObject> activeRealWorldPlanes = new Dictionary<string, GameObject>();
    private Dictionary<string, TextMeshPro> realWorldPlaneLabels = new Dictionary<string, TextMeshPro>();

    [Header("Real-World AR Scaling (Legacy)")]
    // These scales are highly dependent on your AR setup and how you want to map real-world units.
    public float altitudeScale = 0.1f;    // 1 foot altitude = 0.1 Unity units on Y axis
    public float longitudeScale = 100f;   // Scale factor for longitude to Unity X
    public float latitudeScale = 100f;    // Scale factor for latitude to Unity Z

    // These reference points are now effectively overridden by UserLocationProvider for relative AR placement
    // public float referenceLongitude = 0f; 
    // public float referenceLatitude = 0f;  

    [Header("3D Radar Integration")]
    // This will be set by the RadarPlacementController when the user places the radar
    private GameObject placedRadarDisplayObject; 
    private RadarDisplay radarDisplayScript;

    private ARPlaneManager arPlaneManager;
    private List<ARPlane> arPlanes = new List<ARPlane>();
    private GameObject radarInstance; // This will be set by the ARPlacementManager when the user places the radar
    private bool isRadarActive = false;

    public Material planeMaterial; // Assign your transparent material in the Inspector

    void OnEnable()
    {
        arPlaneManager = GetComponent<ARPlaneManager>(); // Ensure ARPlaneManager is fetched
        if (arPlaneManager == null)
        {
            Debug.LogError("ARPlaneManager not found on this GameObject!");
        }
        ARPlacementManager.OnRadarPlaced += HandleRadarPlaced;
        ARPlacementManager.OnRadarRemoved += HandleRadarRemoved;
        UpdatePlaneVisuals(); // Initial visual state update
        WebSocketConection.OnPlaneDataReceived += HandlePlaneData;
    }

    void OnDisable()
    {
        ARPlacementManager.OnRadarPlaced -= HandleRadarPlaced;
        ARPlacementManager.OnRadarRemoved -= HandleRadarRemoved;
        WebSocketConection.OnPlaneDataReceived -= HandlePlaneData;
    }

    void Update()
    {
        UpdatePlaneVisuals();
    }

    void UpdatePlaneVisuals()
    {
        bool showPlanes = !isRadarActive; // Show planes if radar is NOT active
        if (arPlaneManager != null)
        {
            foreach (ARPlane plane in arPlaneManager.trackables)
            {
                if (plane != null && plane.gameObject != null)
                {
                    Renderer planeRenderer = plane.GetComponent<Renderer>();
                    if (planeRenderer != null) planeRenderer.enabled = showPlanes;
                    // Also consider child renderers if your plane prefab has them
                }
            }
            // Enable/disable the ARPlaneManager component itself to control detection
            // arPlaneManager.enabled = showPlanes; // This might be too broad if you still want detection but not visuals
            Debug.Log($"AR Plane visuals set to: {showPlanes}");
        }
    }

    private void HandleRadarPlaced(GameObject placedRadarInstance)
    {
        placedRadarDisplayObject = placedRadarInstance;
        if (placedRadarDisplayObject != null)
        {
            isRadarActive = true;
            radarDisplayScript = placedRadarDisplayObject.GetComponent<RadarDisplay>();
            if (radarDisplayScript == null)
            {
                Debug.LogError("RadarDisplay script not found on the placed radar object!");
            }
            else
            {
                Debug.Log("3D Radar Display has been placed. PlaneManagerRA will now send data to it.");
                // Optionally, if you have existing planes, you might want to update them on the newly placed radar
                // foreach (var planeEntry in activeRealWorldPlanes)
                // {
                //    // This requires a way to get PlaneData from GameObject or re-fetch, which might be complex
                // }
            }
        }
    }

    private void HandleRadarRemoved()
    {
        Debug.Log("3D Radar Display has been removed. PlaneManagerRA will stop sending data to it.");
        isRadarActive = false;
        placedRadarDisplayObject = null;
        radarDisplayScript = null;
        UpdatePlaneVisuals(); // Show AR planes again when radar is removed
        // Note: Plane icons on the radar are children of RadarDisplay and will be destroyed with it.
    }

    void HandlePlaneData(PlaneData data)
    {
        if (data == null || string.IsNullOrEmpty(data.address))
        {
            Debug.LogWarning("Received invalid PlaneData (null or no address).");
            return;
        }

        Debug.Log($"PlaneManagerRA received data for {data.address}: {data.ToString()}");

        // Check if we should be in test mode (check prefab or placed radar)
        bool isRadarTestMode = false;
        if (radarDisplayScript != null && radarDisplayScript.useTestLocation)
        {
            isRadarTestMode = true;
        }
        else if (radarDisplayScript == null)
        {
            // Check the RadarPlacementController to see if it would use test mode
            RadarPlacementController controller = FindFirstObjectByType<RadarPlacementController>();
            if (controller != null && controller.radarDisplayPrefab != null)
            {
                RadarDisplay prefabRadarDisplay = controller.radarDisplayPrefab.GetComponent<RadarDisplay>();
                if (prefabRadarDisplay != null && prefabRadarDisplay.useTestLocation)
                {
                    isRadarTestMode = true;
                }
            }
        }

        Debug.Log($"Test mode: {isRadarTestMode}, Radar placed: {placedRadarDisplayObject != null}, Radar active: {(placedRadarDisplayObject != null && placedRadarDisplayObject.activeInHierarchy)}");
        
        // Ensure user location is available for real-world AR calculations if needed
        bool userLocationAvailable = UserLocationProvider.Instance != null && UserLocationProvider.Instance.IsLocationServiceRunning;
        
        if (!userLocationAvailable && !isRadarTestMode)
        {
             Debug.LogWarning($"User location not available yet. Skipping real-world AR update for plane: {data.address}. Radar might still update if ready.");
        }

        // --- Real-World AR Plane Object Handling (Skip if using radar test mode) ---
        if (!isRadarTestMode && realWorldPlanePrefab != null && userLocationAvailable)
        {
            Debug.Log($"Processing real-world AR plane for {data.address}");
            UpdateRealWorldARPlane(data);
        }
        else if (!isRadarTestMode && realWorldPlanePrefab != null)
        {
            Debug.LogWarning($"Plane {data.address}: Cannot update real-world AR plane - user location not available.");
        }

        // --- 3D Radar Update (Always attempt if radar is placed) --- 
        if (radarDisplayScript != null && placedRadarDisplayObject.activeInHierarchy)
        {
            Debug.Log($"Sending plane {data.address} to radar display for processing");
            radarDisplayScript.UpdateOrCreatePlaneOnRadar(data); // Pass the full PlaneData
        }
        else if (isRadarTestMode)
        {
            Debug.Log($"Received plane data for {data.address} in test mode - waiting for radar to be placed.");
        }
        else
        {
            Debug.LogWarning($"Cannot send plane {data.address} to radar - radar not placed or not active");
        }
    }

    private void UpdateRealWorldARPlane(PlaneData data)
    {
        GameObject realWorldPlaneObject;
        TextMeshPro realWorldLabel = null;

        if (!activeRealWorldPlanes.TryGetValue(data.address, out realWorldPlaneObject))
        {
            // Real-world planes are parented to this PlaneManagerRA object or another AR anchor as needed
            realWorldPlaneObject = Instantiate(realWorldPlanePrefab, this.transform); 
            activeRealWorldPlanes[data.address] = realWorldPlaneObject;
            realWorldPlaneObject.name = $"ARPlane_{data.address}{(string.IsNullOrEmpty(data.callsign) ? "" : "_" + data.callsign.Trim())}";
            Debug.Log($"CREATED new real-world AR plane object: {realWorldPlaneObject.name}");

            realWorldLabel = realWorldPlaneObject.GetComponentInChildren<TextMeshPro>();
            if (realWorldLabel == null) 
            {
                GameObject labelObj = new GameObject("Label");
                labelObj.transform.SetParent(realWorldPlaneObject.transform);
                labelObj.transform.localPosition = new Vector3(0, 0.6f, 0); 
                realWorldLabel = labelObj.AddComponent<TextMeshPro>();
                realWorldLabel.fontSize = 2; 
                realWorldLabel.alignment = TextAlignmentOptions.Center;
            }
            if (realWorldLabel != null) realWorldPlaneLabels[data.address] = realWorldLabel;
        }
        else
        {
            if (realWorldLabel == null && realWorldPlaneLabels.ContainsKey(data.address))
            {
                realWorldLabel = realWorldPlaneLabels[data.address];
            }
        }

        // --- Update Real-World AR Plane Position & Rotation (If object exists and data is complete) ---
        if (realWorldPlaneObject != null && data.latitude.HasValue && data.longitude.HasValue && data.altitude.HasValue)
        {
            float rawLat = data.latitude.Value;
            float rawLon = data.longitude.Value;
            float rawAltFeet = data.altitude.Value;

            // Using your existing scaling for real-world AR for now.
            float x = (rawLon - UserLocationProvider.Instance.CurrentLongitude) * longitudeScale;
            float y = (rawAltFeet * 0.3048f /* To Meters */ - UserLocationProvider.Instance.CurrentAltitude) * altitudeScale;
            float z = (rawLat - UserLocationProvider.Instance.CurrentLatitude) * latitudeScale;

            realWorldPlaneObject.transform.localPosition = new Vector3(x, y, z);
            
            if (data.heading.HasValue)
            {
                realWorldPlaneObject.transform.localRotation = Quaternion.Euler(0, data.heading.Value, 0);
            }

            if (realWorldLabel != null)
            {
                realWorldLabel.text = $"{data.callsign?.Trim() ?? data.address}\nAlt: {rawAltFeet.ToString("F0")} ft";
            }
        }
        else if (realWorldPlaneObject != null)
        {
            // Handle missing data for an existing real-world plane
            Debug.LogWarning($"Plane {data.address}: Missing full location data for real-world AR update. Has Alt? {data.altitude.HasValue}, Lat? {data.latitude.HasValue}, Lon? {data.longitude.HasValue}");
        }
    }

    public void CheckAndUseTestLocationIfNeeded()
    {
        // Check the ARPlacementManager to see if it would use test mode
        // ARPlacementManager controller = FindFirstObjectByType<ARPlacementManager>(); // NEW
        // if (controller != null && controller.useTestLocation) // Assuming useTestLocation would be part of ARPlacementManager
        // {
        // SetUserLocationToTest();
        // }

        //This functionality was moved to RadarDisplay.cs, so this method may no longer be needed here
        //or it needs to get the RadarDisplay component from the placedRadarInstance if available.
        RadarDisplay radarDisplay = null;
        if(placedRadarDisplayObject != null) radarDisplay = placedRadarDisplayObject.GetComponent<RadarDisplay>();

        if (radarDisplay != null && radarDisplay.useTestLocation)
        {
            Debug.Log("PlaneManagerRA: Radar is using test location. This script doesn't directly set it anymore.");
        }
    }

    private void SetUserLocationToTest()
    {
        // Implementation of SetUserLocationToTest method
    }
}