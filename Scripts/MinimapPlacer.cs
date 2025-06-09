using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System; // Added for Action
using UnityEngine.InputSystem; // Added for the new Input System

public class RadarDisplay : MonoBehaviour
{
    public static event Action<PlaneData, string, float> OnPlaneSelectedForInfoRaw; // Event now includes raw message string AND distance in KM

    [Header("Radar Visual Elements")]
    public GameObject radarPlanePrefab; // Small plane icon prefab
    public GameObject centerMarkerPrefab; // "You Are Here" marker prefab
    public Material radarRingMaterial; // Material for range rings
    public Material radarGridMaterial; // Material for grid lines
    public Material altitudeRingMaterial; // Material for altitude rings

    [Header("Radar Scale & Range")]
    public float radarDisplayRadiusMeters = 0.25f; // Physical radius of radar display (e.g., 50cm diameter)
    public float radarRealWorldRangeKm = 100f;      // Real-world range in kilometers (increased to 100km)
    public float radarAltitudeScale = 0.00007f;     // Adjusted scale for better vertical distribution
    public float radarBaseHeight = 0.01f;          // Height of radar base above surface
    public float radarMaxHeightMeters = 1.2f;      // Max height of 3D radar display, increased to fit more rings
    public Vector3 radarPlaneIconScale = new Vector3(0.02f, 0.02f, 0.02f); // Made larger for visibility

    [Header("3D Visual Settings")]
    public int numberOfRangeRings = 4; // How many range rings to draw
    public int numberOfAltitudeRings = 6; // Draw 6 rings, e.g., at 10k, 20k, 30k, 40k, 50k, 60k ft
    public float altitudeRingSpacing = 10000f; // 10,000 feet between altitude rings
    public Color rangeRingColor = Color.green;
    public Color gridColor = Color.cyan;
    public Color altitudeRingColor = Color.yellow;
    public bool showCompassLabels = true;
    public bool show3DAltitudeMarkers = true;

    [Header("Test Mode Settings")]
    public bool useTestLocation = true; // Enable to use hardcoded location for testing
    public float testLatitude = 52.2387f;  // University of Twente, Enschede
    public float testLongitude = 6.8564f;  // University of Twente, Enschede  
    public float testAltitude = 50f;       // University altitude in meters

    private Dictionary<string, GameObject> planeObjectsOnRadar = new Dictionary<string, GameObject>();
    private Dictionary<string, float> planeLastUpdateTime = new Dictionary<string, float>(); // Track last update time
    private Dictionary<string, Vector3> planeLastPosition = new Dictionary<string, Vector3>(); // Track last position for smoothing
    private Dictionary<string, Vector3> planeTargetPosition = new Dictionary<string, Vector3>(); // Target position for smooth movement
    private Dictionary<string, Vector3> planeVelocity = new Dictionary<string, Vector3>(); // Interpolated velocity for movement
    private Dictionary<string, PlaneData> planeLastData = new Dictionary<string, PlaneData>(); // Store last known data
    private Dictionary<string, string> planeRawMessages = new Dictionary<string, string>(); // To store raw JSON messages
    private Dictionary<string, float> planeRealWorldDistances = new Dictionary<string, float>(); // Store real-world distance in meters
    private GameObject centerMarker;
    private List<GameObject> rangeRings = new List<GameObject>();
    private List<GameObject> altitudeRings = new List<GameObject>(); // 3D altitude rings
    private List<GameObject> altitudeMarkers = new List<GameObject>(); // Height markers
    private GameObject radarBase;
    private GameObject radarTower; // Central vertical tower
    
    public float staleTimeThreshold = 120f; // Remove planes not updated for 2 minutes (increased from 30s)
    public float updateRateLimit = 0.1f; // Only update plane positions every 0.1 seconds (reduced for more responsiveness)
    public float minimumMovementThreshold = 0.001f; // Only update if plane moved more than 0.1cm (reduced for more sensitivity)
    public float positionSmoothingSpeed = 3f; // How fast to smooth position changes (faster for more responsive movement)
    public float interpolationSpeed = 0.1f; // How fast planes move between updates (simulated movement)

    [Header("UI Control")]
    public bool metricLabelsInitiallyVisible = true; // Visibility of N, S, E, W, km rings, etc.
    public bool planeLabelsInitiallyVisible = false; // Visibility of callsign, altitude, etc. on planes
    private bool currentPlaneLabelsVisibility; // The current state for plane labels, can be toggled
    public bool IsPlaneLabelsCurrentlyVisible => currentPlaneLabelsVisibility; // Public getter for UI

    // --- Selection Highlight State ---
    private GameObject currentlySelectedPlaneIcon = null;
    private Color originalEmissionColor;
    private bool wasEmissionEnabled;

    void Start()
    {
        // Ensure radar range is set correctly (override prefab settings)
        Debug.Log("RadarDisplay Start() - Initial range: " + radarRealWorldRangeKm + "km");
        Debug.Log("RadarDisplay Start() - Initial radius: " + radarDisplayRadiusMeters + "m");
        Debug.Log("RadarDisplay Start() - Initial altitude scale: " + radarAltitudeScale);
        
        radarRealWorldRangeKm = 100f;  // Increased to 100km
        radarDisplayRadiusMeters = 0.25f;  // Force correct radar size!
        radarAltitudeScale = 0.00007f;  // Force altitude scale for consistency
        radarMaxHeightMeters = 1.2f;      // Ensure there is enough vertical space for altitude rings
        
        Debug.Log("RadarDisplay Start() - After override: " + radarRealWorldRangeKm + "km");
        Debug.Log("RadarDisplay radius: " + radarDisplayRadiusMeters + "m");
        Debug.Log("RadarDisplay altitude scale: " + radarAltitudeScale);
        
        currentPlaneLabelsVisibility = planeLabelsInitiallyVisible; // Initialize visibility state for plane-specific labels

        CreateRadarDisplay();
    }

    void CreateRadarDisplay()
    {
        // Create radar base
        CreateRadarBase();
        
        // Create center "You Are Here" marker
        CreateCenterMarker();
        
        // Create central tower for 3D effect
        CreateRadarTower();
        
        // Create range rings (horizontal)
        CreateRangeRings();
        
        // Create altitude rings (vertical/3D)
        if (show3DAltitudeMarkers)
            CreateAltitudeRings();
        
        // Create compass directions (optional)
        if (showCompassLabels)
            CreateCompassLabels();
    }

    void CreateRadarBase()
    {
        // Create a thin cylinder as the radar base
        radarBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        radarBase.transform.SetParent(this.transform);
        radarBase.transform.localPosition = new Vector3(0, radarBaseHeight * 0.5f, 0);
        radarBase.transform.localScale = new Vector3(radarDisplayRadiusMeters * 2f, radarBaseHeight, radarDisplayRadiusMeters * 2f);
        radarBase.name = "RadarBase";
        
        // Set material
        if (radarGridMaterial != null)
        {
            radarBase.GetComponent<Renderer>().material = radarGridMaterial;
        }
        else
        {
            // Default dark material
            radarBase.GetComponent<Renderer>().material.color = new Color(0.1f, 0.1f, 0.2f, 0.8f);
        }
    }

    void CreateCenterMarker()
    {
        if (centerMarkerPrefab != null)
        {
            centerMarker = Instantiate(centerMarkerPrefab, this.transform);
        }
        else
        {
            // Create a simple marker
            centerMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            centerMarker.transform.SetParent(this.transform);
            centerMarker.transform.localScale = Vector3.one * 0.02f;
            centerMarker.GetComponent<Renderer>().material.color = Color.red;
        }
        
        centerMarker.transform.localPosition = new Vector3(0, radarBaseHeight + 0.01f, 0);
        centerMarker.name = "YouAreHere";
        
        // Add text label
        CreateTextLabel(centerMarker, "YOU", Vector3.up * 0.03f);
    }

    void CreateRangeRings()
    {
        for (int i = 1; i <= numberOfRangeRings; i++)
        {
            float ringRadius = (radarDisplayRadiusMeters / numberOfRangeRings) * i;
            GameObject ring = CreateRangeRing(ringRadius);
            rangeRings.Add(ring);
            
            // Add distance label - now shows 12.5km, 25km, 37.5km, 50km for 50km total range
            float realWorldDistance = (radarRealWorldRangeKm / numberOfRangeRings) * i;
            CreateDistanceLabel(ring, realWorldDistance.ToString("F1") + "km", ringRadius);
        }
    }

    GameObject CreateRangeRing(float radius)
    {
        GameObject ring = new GameObject("RangeRing_" + radius.ToString("F2") + "m");
        ring.transform.SetParent(this.transform);
        ring.transform.localPosition = new Vector3(0, radarBaseHeight + 0.001f, 0);
        
        LineRenderer lineRenderer = ring.AddComponent<LineRenderer>();
        lineRenderer.material = radarRingMaterial != null ? radarRingMaterial : new Material(Shader.Find("Sprites/Default"));
        lineRenderer.material.color = rangeRingColor;
        lineRenderer.startWidth = 0.003f;
        lineRenderer.endWidth = 0.003f;
        lineRenderer.useWorldSpace = false;
        
        // Create circle points
        int segments = 64;
        lineRenderer.positionCount = segments + 1;
        
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            lineRenderer.SetPosition(i, new Vector3(x, 0, z));
        }
        
        return ring;
    }

    void CreateCompassLabels()
    {
        string[] directions = { "N", "E", "S", "W" };
        Vector3[] positions = {
            new Vector3(0, 0, radarDisplayRadiusMeters + 0.03f),  // North
            new Vector3(radarDisplayRadiusMeters + 0.03f, 0, 0),  // East
            new Vector3(0, 0, -(radarDisplayRadiusMeters + 0.03f)), // South
            new Vector3(-(radarDisplayRadiusMeters + 0.03f), 0, 0)  // West
        };
        
        for (int i = 0; i < directions.Length; i++)
        {
            GameObject compassLabel = new GameObject("Compass_" + directions[i]);
            compassLabel.transform.SetParent(this.transform);
            compassLabel.transform.localPosition = positions[i] + Vector3.up * (radarBaseHeight + 0.02f);
            CreateTextLabel(compassLabel, directions[i], Vector3.zero);
        }
    }

    void CreateTextLabel(GameObject parent, string text, Vector3 offset)
    {
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(parent.transform);
        labelObj.transform.localPosition = offset;
        
        TextMeshPro tmp = labelObj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 25f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.transform.localScale = Vector3.one * 0.01f;
        
        // Ensure text is readable
        tmp.fontStyle = FontStyles.Bold;
        tmp.sortingOrder = 10; // Make sure text appears on top
        
        labelObj.SetActive(metricLabelsInitiallyVisible); // Compass and "YOU" labels are metrics

        Debug.Log("Created text label: '" + text + "' for " + parent.name);
    }

    void CreateDistanceLabel(GameObject ring, string text, float radius)
    {
        GameObject labelObj = new GameObject("DistanceLabel");
        labelObj.transform.SetParent(ring.transform);
        labelObj.transform.localPosition = new Vector3(radius * 0.7f, 0.005f, radius * 0.7f);
        
        TextMeshPro tmp = labelObj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 25f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = rangeRingColor;
        tmp.transform.localScale = Vector3.one * 0.008f;
        labelObj.SetActive(metricLabelsInitiallyVisible); // Distance labels are metrics
    }

    void CreateRadarTower()
    {
        // Create a thin vertical tower in the center
        radarTower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        radarTower.transform.SetParent(this.transform);
        radarTower.transform.localPosition = new Vector3(0, radarBaseHeight + radarMaxHeightMeters * 0.5f, 0);
        radarTower.transform.localScale = new Vector3(0.005f, radarMaxHeightMeters * 0.5f, 0.005f); // Very thin tower
        radarTower.name = "RadarTower";
        
        // Set material - make it glow
        Renderer towerRenderer = radarTower.GetComponent<Renderer>();
        if (altitudeRingMaterial != null)
        {
            towerRenderer.material = altitudeRingMaterial;
        }
        else
        {
            towerRenderer.material.color = altitudeRingColor;
            towerRenderer.material.SetColor("_EmissionColor", altitudeRingColor * 0.3f);
            towerRenderer.material.EnableKeyword("_EMISSION");
        }
    }

    void CreateAltitudeRings()
    {
        // Determine user altitude to make rings relative to the user, just like planes
        float userAltMeters;
        if (useTestLocation)
        {
            userAltMeters = testAltitude;
        }
        else if (UserLocationProvider.Instance != null && UserLocationProvider.Instance.IsLocationServiceRunning)
        {
            userAltMeters = UserLocationProvider.Instance.CurrentAltitude;
        }
        else
        {
            userAltMeters = 0; // Fallback, rings might not align with planes until location is ready
            Debug.LogWarning("User location not available during ring creation. Rings may be misaligned until location services start.");
        }

        Debug.Log("Attempting to create altitude rings relative to user altitude " + userAltMeters + "m. Number: " + numberOfAltitudeRings + ", Spacing: " + altitudeRingSpacing + "ft");
        for (int i = 1; i <= numberOfAltitudeRings; i++)
        {
            float altitudeFeet = altitudeRingSpacing * i; // e.g., 10k, 20k, 30k...
            float altitudeMeters = altitudeFeet * 0.3048f; // Convert feet to meters
            float heightRelativeToUser = altitudeMeters - userAltMeters;
            float ringHeight = radarBaseHeight + (heightRelativeToUser * radarAltitudeScale); // Align with plane altitude calculation
            
            // Don't create rings that are outside the visible radar volume
            if (ringHeight > radarMaxHeightMeters || ringHeight < radarBaseHeight) 
            {
                Debug.LogWarning("Skipping altitude ring for " + altitudeFeet + "ft as it falls outside radar display range (calculated height: " + ringHeight + "m).");
                continue; // Use continue to check all possible rings
            }
            
            GameObject altRing = CreateAltitudeRing(ringHeight, altitudeFeet);
            altitudeRings.Add(altRing);
            
            // Add altitude label
            CreateAltitudeLabel(altRing, altitudeFeet.ToString("F0") + "k ft", ringHeight);
        }
    }

    GameObject CreateAltitudeRing(float height, float altitudeFeet)
    {
        GameObject ring = new GameObject("AltitudeRing_" + altitudeFeet.ToString("F0") + "ft");
        ring.transform.SetParent(this.transform);
        ring.transform.localPosition = new Vector3(0, height, 0);
        
        LineRenderer lineRenderer = ring.AddComponent<LineRenderer>();
        lineRenderer.material = altitudeRingMaterial != null ? altitudeRingMaterial : new Material(Shader.Find("Sprites/Default"));
        lineRenderer.material.color = altitudeRingColor;
        lineRenderer.startWidth = 0.01f; // Even thicker for visibility
        lineRenderer.endWidth = 0.01f;   // Even thicker for visibility
        lineRenderer.useWorldSpace = false;
        
        // Create circle points - smaller radius than base for 3D effect
        int segments = 32;
        float ringRadius = radarDisplayRadiusMeters * 0.8f; // 80% of base radius
        lineRenderer.positionCount = segments + 1;
        
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * 2f * Mathf.PI / segments;
            float x = Mathf.Cos(angle) * ringRadius;
            float z = Mathf.Sin(angle) * ringRadius;
            lineRenderer.SetPosition(i, new Vector3(x, 0, z));
        }
        
        return ring;
    }

    void CreateAltitudeLabel(GameObject ring, string text, float height)
    {
        GameObject labelObj = new GameObject("AltitudeLabel");
        labelObj.transform.SetParent(ring.transform);
        labelObj.transform.localPosition = new Vector3(radarDisplayRadiusMeters * 0.9f, 0.01f, 0);
        
        TextMeshPro tmp = labelObj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = 25f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = altitudeRingColor;
        tmp.transform.localScale = Vector3.one * 0.006f;
        tmp.fontStyle = FontStyles.Bold;
        labelObj.SetActive(metricLabelsInitiallyVisible); // Altitude labels are metrics
    }

    void OnEnable() {
        WebSocketConection.OnPlaneDataReceivedWithRaw += HandlePlaneDataWithRaw;
        // Ensure other event subscriptions if any are here too e.g. for UserLocationProvider
    }

    void OnDisable() {
        WebSocketConection.OnPlaneDataReceivedWithRaw -= HandlePlaneDataWithRaw;
        // Ensure other event unsubscriptions if any are here too
    }

    void HandlePlaneDataWithRaw(PlaneData data, string rawJson) {
        if (data != null && !string.IsNullOrEmpty(data.address)) {
            planeRawMessages[data.address] = rawJson;
            // The existing UpdateOrCreatePlaneOnRadar will be called by the other event subscription 
            // or you might consolidate logic if this is the primary way data comes in.
            // For now, just storing the raw message linked to planeData by its address.
        }
    }

    // Call this from PlaneManagerRA
    public void UpdateOrCreatePlaneOnRadar(PlaneData planeData)
    {
        Debug.Log("RadarDisplay processing plane " + planeData.address + ": " + planeData.ToString());
        
        // Declare text variables once at the top
        string callsign = "";
        string speedInfo = "";
        string textContent = "";
        
        // CHECK IF RADAR DISPLAY EXISTS IN HIERARCHY
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogError("❌ Radar display is NOT ACTIVE in hierarchy! Cannot update plane.");
            return;
        }

        // ALWAYS UPDATE TIMESTAMP FIRST - this prevents stale removal
        planeLastUpdateTime[planeData.address] = Time.time;
        Debug.Log("⏰ Updated timestamp for " + planeData.address + " at " + Time.time.ToString("F1"));

        // Check if UserLocationProvider is ready (skip if using test location)
        if (!useTestLocation && (UserLocationProvider.Instance == null || !UserLocationProvider.Instance.IsLocationServiceRunning))
        {
            Debug.LogWarning("UserLocationProvider not ready. Skipping radar plane update for: " + (planeData?.address ?? "Unknown"));
            return;
        }

        // Validate plane data - but be more lenient
        if (planeData == null || string.IsNullOrEmpty(planeData.address))
        {
            Debug.LogWarning("❌ Invalid plane data - missing address: " + (planeData?.address ?? "NULL"));
            return;
        }

        // Check for missing coordinates but don't immediately fail
        if (!planeData.latitude.HasValue || !planeData.longitude.HasValue || !planeData.altitude.HasValue)
        {
            Debug.LogWarning("⚠️ Incomplete plane data for " + planeData.address + ": " +
                           "Lat: " + (planeData?.latitude?.ToString() ?? "NULL") + ", " +
                           "Lon: " + (planeData?.longitude?.ToString() ?? "NULL") + ", " +
                           "Alt: " + (planeData?.altitude?.ToString() ?? "NULL"));
            
            // If plane already exists, keep it but don't update position
            if (planeObjectsOnRadar.TryGetValue(planeData.address, out GameObject existingObj))
            {
                Debug.Log("📍 Keeping existing plane " + planeData.address + " despite incomplete data");
                existingObj.SetActive(true); // Make sure it stays visible
                return;
            }
            else
            {
                Debug.LogWarning("❌ Cannot create new plane " + planeData.address + " with incomplete data");
                return;
            }
        }

        // Get user location (test or real)
        float userLat, userLon, userAlt;
        if (useTestLocation)
        {
            userLat = testLatitude;
            userLon = testLongitude;
            userAlt = testAltitude;
        }
        else
        {
            userLat = UserLocationProvider.Instance.CurrentLatitude;
            userLon = UserLocationProvider.Instance.CurrentLongitude;
            userAlt = UserLocationProvider.Instance.CurrentAltitude;
        }

        float planeLat = planeData.latitude.Value;
        float planeLon = planeData.longitude.Value;
        float planeAltFeet = planeData.altitude.Value;
        float planeAltMeters = planeAltFeet * 0.3048f;

        Debug.Log("📍 Position calculation for " + planeData.address + ": Plane(" + planeLat.ToString("F6") + ", " + planeLon.ToString("F6") + ", " + planeAltFeet.ToString("F0") + "ft) vs User(" + userLat.ToString("F6") + ", " + userLon.ToString("F6") + ", " + userAlt.ToString("F0") + "m)");

        // Calculate real-world offset in meters
        float deltaLat = planeLat - userLat;
        float deltaLon = planeLon - userLon;
        
        float metersPerDegreeLat = 111133f;
        float metersPerDegreeLon = 111320f * Mathf.Cos(userLat * Mathf.Deg2Rad);

        float zOffset_realWorld = deltaLat * metersPerDegreeLat;
        float xOffset_realWorld = deltaLon * metersPerDegreeLon;
        
        float realWorldDistance = Mathf.Sqrt(xOffset_realWorld * xOffset_realWorld + zOffset_realWorld * zOffset_realWorld);
        planeRealWorldDistances[planeData.address] = realWorldDistance; // Store the distance
        Debug.Log("🌍 Real-world distance for " + planeData.address + ": " + realWorldDistance.ToString("F0") + "m (" + (realWorldDistance/1000).ToString("F1") + "km), X=" + xOffset_realWorld.ToString("F0") + "m, Z=" + zOffset_realWorld.ToString("F0") + "m");
        
        // Scale to radar display
        float radarScaleFactor = radarDisplayRadiusMeters / (radarRealWorldRangeKm * 1000f);
        
        float radarX = xOffset_realWorld * radarScaleFactor;
        float radarZ = zOffset_realWorld * radarScaleFactor;
        
        Debug.Log("📡 Radar position for " + planeData.address + ": X=" + radarX.ToString("F4") + ", Z=" + radarZ.ToString("F4") + ", Scale factor=" + radarScaleFactor.ToString("F8"));
        
        // TEMPORARILY DISABLE RANGE CHECKING TO SEE IF THAT'S THE ISSUE
        bool withinRange = true; // Always allow for now
        float distanceFromCenter = Mathf.Sqrt(radarX * radarX + radarZ * radarZ);
        Debug.Log("📏 Distance check for " + planeData.address + ": distance=" + distanceFromCenter.ToString("F4") + "m, limit=" + radarDisplayRadiusMeters.ToString("F4") + "m, within range: " + withinRange);
        
        // Calculate height on radar (altitude above user)
        float altitudeAboveUser = planeAltMeters - userAlt;
        float radarY = radarBaseHeight + (altitudeAboveUser * radarAltitudeScale); // Removed +0.02f offset to align planes perfectly with rings

        Debug.Log("🎯 Final position for " + planeData.address + ": (" + radarX.ToString("F3") + ", " + radarY.ToString("F3") + ", " + radarZ.ToString("F3") + "), Real distance: " + realWorldDistance.ToString("F0") + "m, Alt diff: " + altitudeAboveUser.ToString("F0") + "m");

        // Check if this plane should be updated based on rate limiting
        bool shouldUpdatePosition = true;
        if (planeLastUpdateTime.ContainsKey(planeData.address))
        {
            float timeSinceLastUpdate = Time.time - planeLastUpdateTime[planeData.address];
            if (timeSinceLastUpdate < updateRateLimit)
            {
                shouldUpdatePosition = false;
                Debug.Log("⏳ Rate limiting plane " + planeData.address + " - last update " + timeSinceLastUpdate.ToString("F1") + "s ago");
            }
        }

        // Create or update plane icon
        GameObject planeIcon;
        bool isNewPlane = false;
        
        if (!planeObjectsOnRadar.TryGetValue(planeData.address, out planeIcon))
        {
            Debug.Log("🆕 Creating NEW plane icon for " + planeData.address);
            
            isNewPlane = true;
            shouldUpdatePosition = true; // Always update new planes
            
            // TEMPORARILY REMOVED RANGE CHECK FOR NEW PLANES
            // Debug.Log($"✅ Creating new plane {planeData.address} (range check disabled)");
            
            if (radarPlanePrefab != null)
            {
                planeIcon = Instantiate(radarPlanePrefab, this.transform);
                // Ensure RadarPlaneIcon script is on the prefab instance
                RadarPlaneIcon iconScript = planeIcon.GetComponent<RadarPlaneIcon>();
                if (iconScript == null) {
                    iconScript = planeIcon.AddComponent<RadarPlaneIcon>();
                }
                iconScript.planeAddress = planeData.address;
                Debug.Log("✅ Created plane icon from prefab for " + planeData.address + " and set address for click detection.");
            }
            else
            {
                // Create default plane icon
                planeIcon = GameObject.CreatePrimitive(PrimitiveType.Cube);
                planeIcon.transform.SetParent(this.transform);
                
                // Add collider for raycasting if it's a primitive
                if (planeIcon.GetComponent<Collider>() == null) {
                    planeIcon.AddComponent<BoxCollider>();
                }

                RadarPlaneIcon iconScript = planeIcon.AddComponent<RadarPlaneIcon>();
                iconScript.planeAddress = planeData.address;
                
                // Make it VERY visible!
                Renderer renderer = planeIcon.GetComponent<Renderer>();
                renderer.material.color = Color.yellow;
                renderer.material.SetColor("_EmissionColor", Color.yellow * 0.3f);
                renderer.material.EnableKeyword("_EMISSION");
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                
                Debug.Log("✅ Created DEFAULT YELLOW CUBE icon for " + planeData.address);
            }
            
            // VERIFY THE GAME OBJECT WAS ACTUALLY CREATED
            if (planeIcon == null)
            {
                Debug.LogError("❌ FAILED to create plane icon for " + planeData.address + " - planeIcon is NULL!");
                return;
            }
            
            Debug.Log("🔍 After creation - Radar has " + transform.childCount + " children");
            
            planeObjectsOnRadar[planeData.address] = planeIcon;
            planeIcon.name = "RadarPlane_" + planeData.address + "_" + (planeData.callsign?.Trim() ?? "Unknown");
            
            // Don't create text label - it's already in the prefab
            // Just update the existing one - check for both TextMeshPro and TextMeshProUGUI
            TextMeshPro tmpPro = planeIcon.GetComponentInChildren<TextMeshPro>();
            TMPro.TextMeshProUGUI tmpUGUI = planeIcon.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            
            callsign = !string.IsNullOrEmpty(planeData.callsign) ? planeData.callsign.Trim() : "Unknown";
            speedInfo = planeData.speed.HasValue ? planeData.speed.Value.ToString("F0") + "kt" : "";
            textContent = callsign + "\n" + planeData.address + "\n" + planeAltFeet.ToString("F0") + "ft\n" + speedInfo;
            
            if (tmpPro != null)
            {
                tmpPro.text = textContent;
                Debug.Log("📝 Updated TextMeshPro label for " + planeData.address + ": '" + tmpPro.text + "'");
            }
            else if (tmpUGUI != null)
            {
                tmpUGUI.text = textContent;
                Debug.Log("📝 Updated TextMeshProUGUI label for " + planeData.address + ": '" + tmpUGUI.text + "'");
            }
            else
            {
                Debug.LogWarning("⚠️ No TextMeshPro or TextMeshProUGUI found in prefab for " + planeData.address);
                // List all components to debug
                Component[] allComponents = planeIcon.GetComponentsInChildren<Component>();
                Debug.Log("🔍 All components in prefab: " + string.Join(", ", System.Array.ConvertAll(allComponents, c => c.GetType().Name)));
            }
                
            Debug.Log("✅ Successfully created radar plane icon: " + planeIcon.name);
        }
        else
        {
            Debug.Log("🔄 Updating EXISTING plane icon for " + planeData.address);
            Debug.Log("🔍 Existing icon - Name: " + planeIcon.name + ", Active: " + planeIcon.activeInHierarchy);
        }

        // Ensure plane stays active and visible
        if (!planeIcon.activeInHierarchy)
        {
            planeIcon.SetActive(true);
            Debug.Log("🔄 Reactivated plane icon for " + planeData.address);
        }

        // Only update position if rate limiting allows it
        if (shouldUpdatePosition)
        {
            // Check if movement is significant enough
            Vector3 newPosition = new Vector3(radarX, radarY, radarZ);
            Vector3 oldPosition = planeIcon.transform.localPosition;
            
            float movementDistance = Vector3.Distance(oldPosition, newPosition);
            
            if (isNewPlane || movementDistance > minimumMovementThreshold)
            {
                // TEMPORARILY DISABLED RANGE CHECK FOR EXISTING PLANES
                // Debug.Log($"✅ Updating position for {planeData.address} (range check disabled)");
                
                // Calculate velocity for interpolation (only for existing planes)
                if (!isNewPlane && planeLastPosition.ContainsKey(planeData.address) && planeLastUpdateTime.ContainsKey(planeData.address))
                {
                    float deltaTime = Time.time - planeLastUpdateTime[planeData.address];
                    if (deltaTime > 0.1f) // Only calculate if enough time has passed
                    {
                        Vector3 positionDelta = newPosition - planeLastPosition[planeData.address];
                        Vector3 velocity = positionDelta / deltaTime;
                        planeVelocity[planeData.address] = velocity;
                        Debug.Log("🎯 Calculated velocity for " + planeData.address + ": " + velocity.magnitude.ToString("F4") + " units/sec");
                    }
                }
                
                // Set target position for smooth movement
                planeTargetPosition[planeData.address] = newPosition;
                
                // For new planes, set position immediately
                if (isNewPlane)
                {
                    planeIcon.transform.localPosition = newPosition;
                    Debug.Log("✅ NEW plane icon " + planeData.address + " placed at local position: " + newPosition);
                }
                else
                {
                    Debug.Log("🎯 Setting target position for " + planeData.address + ": " + newPosition + " (current: " + oldPosition + ")");
                }
                
                planeIcon.transform.localScale = radarPlaneIconScale;
                
                // Update last update time and position
                planeLastUpdateTime[planeData.address] = Time.time;
                planeLastPosition[planeData.address] = newPosition;
                
                Debug.Log("⏰ Updated position and timestamp for " + planeData.address + " at " + Time.time.ToString("F1"));
            }
            else
            {
                Debug.Log("📍 Plane " + planeData.address + " movement too small (" + movementDistance.ToString("F4") + "m < " + minimumMovementThreshold.ToString("F4") + "m) - not updating position");
                // Still update the timestamp to prevent stale removal
                planeLastUpdateTime[planeData.address] = Time.time;
            }
        }
        else
        {
            // Still update the timestamp to prevent stale removal
            planeLastUpdateTime[planeData.address] = Time.time;
            Debug.Log("⏸️ Plane " + planeData.address + " position update skipped due to rate limiting - keeping existing position");
        }

        Debug.Log("🔍 Plane icon world position: " + planeIcon.transform.position);
        Debug.Log("🔍 Radar world position: " + transform.position);
        Debug.Log("🔍 Distance from radar center: " + Vector3.Distance(planeIcon.transform.position, transform.position).ToString("F3") + "m");

        // Set orientation
        if (planeData.heading.HasValue)
        {
            planeIcon.transform.localRotation = Quaternion.Euler(0, planeData.heading.Value, 0);
            Debug.Log("🧭 Set heading for " + planeData.address + ": " + planeData.heading.Value.ToString("F1") + "°");
        }

        // Store the latest plane data
        planeLastData[planeData.address] = planeData;

        // Update label if it exists - FIX THE TEXT LABELS!
        TextMeshPro label = planeIcon.GetComponentInChildren<TextMeshPro>();
        TMPro.TextMeshProUGUI labelUGUI = planeIcon.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        
        callsign = !string.IsNullOrEmpty(planeData.callsign) ? planeData.callsign.Trim() : "Unknown";
        speedInfo = planeData.speed.HasValue ? planeData.speed.Value.ToString("F0") + "kt" : "";
        textContent = callsign + "\n" + planeData.address + "\n" + planeAltFeet.ToString("F0") + "ft\n" + speedInfo;
        
        if (label != null)
        {
            label.text = textContent;
            label.color = Color.white;
            label.fontSize = 25f; // Make text readable
            label.alignment = TextAlignmentOptions.Center;
            label.gameObject.SetActive(currentPlaneLabelsVisibility); // This is plane-specific info
            Debug.Log("📝 Updated TextMeshPro label: '" + textContent + "'");
        }
        else if (labelUGUI != null)
        {
            labelUGUI.text = textContent;
            labelUGUI.color = Color.white;
            labelUGUI.fontSize = 25f; // Different scale for UGUI
            labelUGUI.alignment = TMPro.TextAlignmentOptions.Center;
            labelUGUI.gameObject.SetActive(currentPlaneLabelsVisibility); // This is plane-specific info
            Debug.Log("📝 Updated TextMeshProUGUI label: '" + textContent + "'");
        }
        else
        {
            Debug.LogWarning("⚠️ No TextMeshPro or TextMeshProUGUI label found for plane " + planeData.address);
        }
        
        Debug.Log("🎯 PLANE ICON UPDATE COMPLETE for " + planeData.address + " - Active: " + planeIcon.activeInHierarchy + ", Position: " + planeIcon.transform.localPosition + ", Timestamp: " + planeLastUpdateTime[planeData.address].ToString("F1"));
        Debug.Log("📊 Plane " + planeData.address + " status: Real distance " + realWorldDistance.ToString("F0") + "m, Radar distance " + distanceFromCenter.ToString("F4") + "m, Within radar: " + withinRange);
    }

    // Public method to toggle visibility of ONLY plane info labels
    public void TogglePlaneInfoVisibility()
    {
        currentPlaneLabelsVisibility = !currentPlaneLabelsVisibility;
        Debug.Log("Toggling plane-specific info visibility to: " + currentPlaneLabelsVisibility);

        foreach (GameObject planeIcon in planeObjectsOnRadar.Values)
        {
            if (planeIcon != null)
            {
                TextMeshPro label = planeIcon.GetComponentInChildren<TextMeshPro>(true); // true to include inactive
                TMPro.TextMeshProUGUI labelUGUI = planeIcon.GetComponentInChildren<TMPro.TextMeshProUGUI>(true); // true to include inactive

                if (label != null)
                {
                    label.gameObject.SetActive(currentPlaneLabelsVisibility);
                }
                if (labelUGUI != null)
                {
                    labelUGUI.gameObject.SetActive(currentPlaneLabelsVisibility);
                }
            }
        }
    }

    // --- Highlighting Logic ---
    void ClearHighlight()
    {
        if (currentlySelectedPlaneIcon != null)
        {
            // Check if the renderer still exists before trying to modify it
            var renderer = currentlySelectedPlaneIcon.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                // Restore the original emission properties of the material instance
                if (wasEmissionEnabled)
                {
                    renderer.material.SetColor("_EmissionColor", originalEmissionColor);
                }
                else
                {
                    renderer.material.DisableKeyword("_EMISSION");
                }
            }
            currentlySelectedPlaneIcon = null;
        }
    }

    void HighlightPlane(GameObject planeIcon)
    {
        // First, clear any existing highlight
        ClearHighlight();

        currentlySelectedPlaneIcon = planeIcon;
        var renderer = planeIcon.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            // Store the original properties from this material instance
            wasEmissionEnabled = renderer.material.IsKeywordEnabled("_EMISSION");
            if (wasEmissionEnabled)
            {
                originalEmissionColor = renderer.material.GetColor("_EmissionColor");
            }

            // Apply a bright cyan emission to highlight it
            renderer.material.EnableKeyword("_EMISSION");
            renderer.material.SetColor("_EmissionColor", Color.cyan * 2.0f); // Multiplier for intensity
        }
    }

    void Update()
    {
        // --- Plane Click Detection using the new Input System (handles mouse and touch) ---
        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            Ray ray = Camera.main.ScreenPointToRay(Pointer.current.position.ReadValue());
            RaycastHit hit;

            // Make sure to set a layer for your plane icons if they are hard to hit, 
            // and then use a layerMask in the Raycast.
            // For now, casting against all colliders.
            if (Physics.Raycast(ray, out hit, 100f)) // 100f is max distance, adjust as needed
            {
                RadarPlaneIcon clickedIcon = hit.collider.GetComponent<RadarPlaneIcon>();
                if (clickedIcon != null)
                {
                    // Highlight the clicked plane visually to give feedback
                    HighlightPlane(clickedIcon.gameObject);

                    if (planeLastData.TryGetValue(clickedIcon.planeAddress, out PlaneData selectedPlaneData) &&
                        planeRealWorldDistances.TryGetValue(clickedIcon.planeAddress, out float distanceMeters))
                    {
                        // Raw message is optional, especially for demo planes
                        planeRawMessages.TryGetValue(clickedIcon.planeAddress, out string rawMessage);
                        rawMessage = rawMessage ?? "N/A (demo plane or no raw data received)";

                        Debug.Log("Plane icon clicked: " + clickedIcon.planeAddress + ". Firing OnPlaneSelectedForInfoRaw event.");
                        OnPlaneSelectedForInfoRaw?.Invoke(selectedPlaneData, rawMessage, distanceMeters / 1000f); // Pass distance in KM
                    }
                    else
                    {
                        Debug.LogWarning("Clicked on plane icon " + clickedIcon.planeAddress + ", but no data or distance found.");
                    }
                }
            }
        }

        // Simplified interpolation - only move toward target positions, no extrapolation
        foreach (var kvp in planeObjectsOnRadar)
        {
            string planeId = kvp.Key;
            GameObject planeIcon = kvp.Value;
            
            if (planeIcon != null && planeIcon.activeInHierarchy && planeTargetPosition.ContainsKey(planeId))
            {
                Vector3 currentPos = planeIcon.transform.localPosition;
                Vector3 targetPos = planeTargetPosition[planeId];
                
                // Only move if there's a significant difference
                float distance = Vector3.Distance(currentPos, targetPos);
                if (distance > 0.001f)
                {
                    // Smooth movement toward target
                    Vector3 newPos = Vector3.Lerp(currentPos, targetPos, Time.deltaTime * positionSmoothingSpeed);
                    planeIcon.transform.localPosition = newPos;
                    
                    // Debug movement
                    if (distance > 0.01f) // Only log significant movements
                    {
                        Debug.Log("🔄 Smoothing " + planeId + ": " + currentPos.ToString("F3") + " → " + targetPos.ToString("F3") + " (distance: " + distance.ToString("F3") + ")");
                    }
                }
            }
        }
        
        // Debug radar state every few seconds
        if (Time.time % 10f < 0.1f) // Every 10 seconds (less frequent)
        {
            Debug.Log("🔄 Radar Update - Active planes: " + planeObjectsOnRadar.Count + ", Children: " + transform.childCount);
            foreach (var kvp in planeObjectsOnRadar)
            {
                if (kvp.Value != null && kvp.Value.activeInHierarchy)
                {
                    float timeSinceUpdate = Time.time - (planeLastUpdateTime.ContainsKey(kvp.Key) ? planeLastUpdateTime[kvp.Key] : 0);
                    Debug.Log("  ✅ " + kvp.Key + ": Active, last update " + timeSinceUpdate.ToString("F1") + "s ago, pos: " + kvp.Value.transform.localPosition);
                }
                else
                {
                    Debug.Log("  ❌ " + kvp.Key + ": Inactive or null");
                }
            }
        }
        
        // Remove planes not updated for staleTimeThreshold seconds
        List<string> planesToRemove = new List<string>();
        foreach (var kvp in planeLastUpdateTime)
        {
            float timeSinceUpdate = Time.time - kvp.Value;
            if (timeSinceUpdate > staleTimeThreshold)
            {
                Debug.LogWarning("🕒 Plane " + kvp.Key + " is stale - last update " + timeSinceUpdate.ToString("F1") + "s ago (threshold: " + staleTimeThreshold.ToString("F1") + "s)");
                planesToRemove.Add(kvp.Key);
            }
        }

        foreach (var plane in planesToRemove)
        {
            if (planeObjectsOnRadar.TryGetValue(plane, out GameObject obj))
            {
                // If the plane being removed is the currently highlighted one, clear the reference
                // so we don't try to access a destroyed object.
                if (obj == currentlySelectedPlaneIcon)
                {
                    currentlySelectedPlaneIcon = null;
                }

                Debug.Log("🗑️ Removing stale plane " + plane + " (not updated for " + staleTimeThreshold.ToString("F1") + "s)");
                obj.SetActive(false);
                Destroy(obj);
                planeObjectsOnRadar.Remove(plane);
                planeLastUpdateTime.Remove(plane);
                planeLastPosition.Remove(plane);
                planeTargetPosition.Remove(plane);
                planeVelocity.Remove(plane);
                planeLastData.Remove(plane);
                planeRealWorldDistances.Remove(plane); // Clean up distance data
            }
        }
    }
}