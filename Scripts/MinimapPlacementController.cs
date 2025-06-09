using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using System.Collections;

public class RadarPlacementController : MonoBehaviour
{
    [Header("Radar Placement Settings")]
    public GameObject radarDisplayPrefab; // Assign your RadarDisplay prefab (which has RadarDisplay script on it)
    public float fallbackDistance = 2f; // Distance in front of camera for fallback placement
    public float arPlaneWaitTime = 5f; // Time to wait for AR planes before enabling fallback
    
    [Header("Placement Options")]
    public bool allowFallbackPlacement = true; // Enable tap-anywhere when AR fails
    public bool allowCameraRelativePlacement = true; // Enable placement relative to camera

    private GameObject placedRadarDisplayObject;
    private ARRaycastManager arRaycastManager;
    private ARAnchorManager arAnchorManager; // Added for proper anchoring
    private static List<ARRaycastHit> hits = new List<ARRaycastHit>();
    private ARPlaneManager arPlaneManager;
    private Camera arCamera;
    private ARAnchor currentAnchor; // Track the current anchor
    
    private bool arPlaneDetectionWorking = false;
    private bool fallbackEnabled = false;
    private float startTime;

    public static System.Action<GameObject> OnRadarPlaced;
    public static System.Action OnRadarRemoved;

    public bool IsRadarPlaced => placedRadarDisplayObject != null && placedRadarDisplayObject.activeInHierarchy;

    void Awake()
    {
        arRaycastManager = FindFirstObjectByType<ARRaycastManager>();
        arPlaneManager = FindFirstObjectByType<ARPlaneManager>();
        arCamera = FindFirstObjectByType<Camera>();
        arAnchorManager = FindFirstObjectByType<ARAnchorManager>();
        
        if (arCamera == null)
        {
            Debug.LogError("Camera not found in the scene.");
        }
        
        if (arAnchorManager == null)
        {
            Debug.LogWarning("ARAnchorManager not found. Radar may not track properly in AR.");
        }
        
        startTime = Time.time;
        
        // Start monitoring AR plane detection
        if (arPlaneManager != null)
        {
            StartCoroutine(MonitorARPlaneDetection());
        }
        else
        {
            Debug.LogWarning("ARPlaneManager not found. Will use fallback placement methods only.");
            EnableFallbackPlacement();
        }
    }

    IEnumerator MonitorARPlaneDetection()
    {
        float checkInterval = 1f;
        
        while (Time.time - startTime < arPlaneWaitTime)
        {
            // Check if we have any valid trackable planes
            if (arPlaneManager.trackables.count > 0)
            {
                foreach (ARPlane plane in arPlaneManager.trackables)
                {
                    if (plane.trackingState == TrackingState.Tracking)
                    {
                        Debug.Log("AR plane detection is working! Using AR placement.");
                        arPlaneDetectionWorking = true;
                        yield break;
                    }
                }
            }
            
            yield return new WaitForSeconds(checkInterval);
        }
        
        // If we get here, AR plane detection isn't working
        Debug.LogWarning($"AR plane detection not working after {arPlaneWaitTime} seconds. Enabling fallback placement methods.");
        EnableFallbackPlacement();
    }

    void EnableFallbackPlacement()
    {
        fallbackEnabled = true;
        Debug.Log("Fallback placement enabled. You can now tap anywhere to place the radar display.");
    }

    void Update()
    {
        if (Input.touchCount == 0 || radarDisplayPrefab == null) return;

        Touch touch = Input.GetTouch(0);

        // Prevent placing if touching UI
        if (EventSystem.current.IsPointerOverGameObject(touch.fingerId))
        {
            return;
        }

        if (touch.phase == TouchPhase.Began)
        {
            Debug.Log("Touch detected! Attempting to place radar...");
            
            bool placementSuccessful = false;
            
            // Method 1: Try AR plane placement if available
            if (arPlaneDetectionWorking && arRaycastManager != null && TryARPlanePlacement(touch.position))
            {
                placementSuccessful = true;
                Debug.Log("Radar placed using AR plane detection.");
            }
            // Method 2: Fallback to tap-anywhere placement
            else if (fallbackEnabled && allowFallbackPlacement && TryFallbackPlacement(touch.position))
            {
                placementSuccessful = true;
                Debug.Log("Radar placed using fallback tap-anywhere method.");
            }
            
            if (!placementSuccessful)
            {
                Debug.LogWarning("Failed to place radar using all available methods.");
            }
        }
    }

    bool TryARPlanePlacement(Vector2 touchPosition)
    {
        if (arRaycastManager.Raycast(touchPosition, hits, TrackableType.PlaneWithinPolygon))
        {
            Debug.Log($"AR Raycast hit detected! Found {hits.Count} hits.");
            
            ARRaycastHit hit = hits[0];
            ARPlane hitPlane = arPlaneManager.GetPlane(hit.trackableId);

            // Only place on horizontal planes
            if (hitPlane != null && (hitPlane.alignment == PlaneAlignment.HorizontalUp || hitPlane.alignment == PlaneAlignment.HorizontalDown))
            {
                Debug.Log("Valid horizontal plane found! Placing radar with anchor...");
                
                // Create an anchor for proper world tracking
                if (arAnchorManager != null)
                {
                    // Remove existing anchor if any
                    if (currentAnchor != null)
                    {
                        Destroy(currentAnchor.gameObject);
                        currentAnchor = null;
                    }
                    
                    // Create new anchor GameObject at hit position
                    GameObject anchorGO = new GameObject("RadarAnchor");
                    anchorGO.transform.SetPositionAndRotation(hit.pose.position, hit.pose.rotation);
                    currentAnchor = anchorGO.AddComponent<ARAnchor>();
                    
                    if (currentAnchor != null)
                    {
                        Debug.Log("✅ AR Anchor created successfully!");
                        PlaceRadarWithAnchor(hit.pose.position, hit.pose.rotation, currentAnchor);
                    }
                    else
                    {
                        Debug.LogWarning("Failed to create AR anchor, placing without anchor");
                        PlaceRadar(hit.pose.position, hit.pose.rotation);
                    }
                }
                else
                {
                    Debug.LogWarning("No ARAnchorManager, placing without anchor");
                    PlaceRadar(hit.pose.position, hit.pose.rotation);
                }
                
                // Optional: Disable plane detection after placing for cleaner view
                SetPlanesActive(false);
                return true;
            }
            else
            {
                Debug.LogWarning("Hit plane is not horizontal or is null.");
            }
        }
        else
        {
            Debug.LogWarning("AR Raycast did not hit any planes.");
        }
        return false;
    }

    bool TryFallbackPlacement(Vector2 touchPosition)
    {
        if (arCamera == null) return false;
        
        Debug.Log("🔄 Attempting fallback placement with anchor...");
        
        // First try: Use AR raycast against feature points (no plane required)
        if (arRaycastManager != null && arRaycastManager.Raycast(touchPosition, hits, TrackableType.FeaturePoint))
        {
            Debug.Log("📍 Hit feature point! Creating anchor...");
            ARRaycastHit hit = hits[0];
            
            if (arAnchorManager != null)
            {
                // Remove existing anchor if any
                if (currentAnchor != null)
                {
                    Destroy(currentAnchor.gameObject);
                    currentAnchor = null;
                }
                
                // Create anchor GameObject at feature point
                GameObject anchorGO = new GameObject("RadarFeatureAnchor");
                anchorGO.transform.SetPositionAndRotation(hit.pose.position, hit.pose.rotation);
                currentAnchor = anchorGO.AddComponent<ARAnchor>();
                
                if (currentAnchor != null)
                {
                    Debug.Log("✅ Fallback AR Anchor created at feature point!");
                    PlaceRadarWithAnchor(hit.pose.position, hit.pose.rotation, currentAnchor);
                    return true;
                }
            }
        }
        
        // Second try: Use estimated distance with attempt to create anchor
        Debug.Log("🎯 No feature points hit, using estimated placement...");
        Ray ray = arCamera.ScreenPointToRay(touchPosition);
        Vector3 estimatedPosition = ray.origin + ray.direction * fallbackDistance;
        
        // Try to create an anchor at the estimated position
        if (arAnchorManager != null)
        {
            // Remove existing anchor if any
            if (currentAnchor != null)
            {
                Destroy(currentAnchor.gameObject);
                currentAnchor = null;
            }
            
            // Create pose for estimated position
            Vector3 forward = arCamera.transform.forward;
            forward.y = 0; // Keep horizontal
            Quaternion estimatedRotation = forward.magnitude > 0.1f ? Quaternion.LookRotation(forward) : Quaternion.identity;
            
            // Try to create anchor GameObject
            GameObject anchorGO = new GameObject("RadarEstimatedAnchor");
            anchorGO.transform.SetPositionAndRotation(estimatedPosition, estimatedRotation);
            currentAnchor = anchorGO.AddComponent<ARAnchor>();
            
            if (currentAnchor != null)
            {
                Debug.Log("✅ Fallback AR Anchor created at estimated position!");
                PlaceRadarWithAnchor(estimatedPosition, estimatedRotation, currentAnchor);
                return true;
            }
        }
        
        // Last resort: Place without anchor (old behavior)
        Debug.LogWarning("⚠️ Could not create anchor, placing radar without world tracking");
        Vector3 forward2 = arCamera.transform.forward;
        forward2.y = 0;
        Quaternion placementRotation = forward2.magnitude > 0.1f ? Quaternion.LookRotation(forward2) : Quaternion.identity;
        PlaceRadar(estimatedPosition, placementRotation);
        return true;
    }

    void PlaceRadar(Vector3 position, Quaternion rotation)
    {
        if (placedRadarDisplayObject == null)
        {
            placedRadarDisplayObject = Instantiate(radarDisplayPrefab, position, rotation);
            Debug.Log($"3D Radar Display placed at {position}");
            OnRadarPlaced?.Invoke(placedRadarDisplayObject); 
        }
        else
        {
            // Relocate existing radar
            placedRadarDisplayObject.transform.SetPositionAndRotation(position, rotation);
            Debug.Log($"3D Radar Display relocated to {position}");
            OnRadarPlaced?.Invoke(placedRadarDisplayObject); // Notify about relocation as well
        }
    }

    void PlaceRadarWithAnchor(Vector3 position, Quaternion rotation, ARAnchor anchor)
    {
        if (placedRadarDisplayObject == null)
        {
            placedRadarDisplayObject = Instantiate(radarDisplayPrefab, position, rotation);
            // Parent to anchor for world tracking
            placedRadarDisplayObject.transform.SetParent(anchor.transform);
            Debug.Log($"✅ 3D Radar Display placed at {position} with AR anchor for world tracking");
            OnRadarPlaced?.Invoke(placedRadarDisplayObject); 
        }
        else
        {
            // Relocate existing radar and reparent to new anchor
            placedRadarDisplayObject.transform.SetParent(null); // Unparent first
            placedRadarDisplayObject.transform.SetPositionAndRotation(position, rotation);
            placedRadarDisplayObject.transform.SetParent(anchor.transform); // Parent to new anchor
            Debug.Log($"✅ 3D Radar Display relocated to {position} with new AR anchor");
            OnRadarPlaced?.Invoke(placedRadarDisplayObject);
        }
    }

    // Public method to force camera-relative placement
    public void PlaceRadarAtCameraPosition()
    {
        if (arCamera == null || radarDisplayPrefab == null) return;
        
        Vector3 cameraPosition = arCamera.transform.position;
        Vector3 cameraForward = arCamera.transform.forward;
        
        // Place in front of camera at ground level
        Vector3 placementPosition = cameraPosition + cameraForward * fallbackDistance;
        placementPosition.y = cameraPosition.y - 1f; // Place below camera level
        
        Quaternion placementRotation = Quaternion.LookRotation(cameraForward);
        PlaceRadar(placementPosition, placementRotation);
        
        Debug.Log("Radar placed at camera-relative position (manual).");
    }

    public void RemoveRadar()
    {
        if (placedRadarDisplayObject != null)
        {
            Destroy(placedRadarDisplayObject);
            placedRadarDisplayObject = null;
            Debug.Log("3D Radar Display removed.");
            OnRadarRemoved?.Invoke();
            
            // Clean up anchor
            if (currentAnchor != null)
            {
                Destroy(currentAnchor.gameObject);
                currentAnchor = null;
                Debug.Log("✅ AR Anchor removed.");
            }
            
            // Re-enable plane detection
            SetPlanesActive(true);
        }
    }

    // Helper to show/hide ARPlanes. Called after placing radar for cleaner view.
    private void SetPlanesActive(bool isActive)
    {
        if (arPlaneManager == null) return;
        foreach (ARPlane plane in arPlaneManager.trackables)
        {
            plane.gameObject.SetActive(isActive);
        }
        arPlaneManager.enabled = isActive; // Disables new plane detection
    }
} 