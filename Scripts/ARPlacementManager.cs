using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using System.Collections; // Required for IEnumerator

// NEW CLASS NAME: ARPlacementManager
public class ARPlacementManager : MonoBehaviour
{
    [Header("Radar Placement Settings")]
    public GameObject radarDisplayPrefab; 
    public float fallbackDistance = 2f; 
    public float manualPlacementHeightOffset = -1.0f; // How far below camera height to place manually (e.g., -1.0 for 1m below)
    public float arPlaneWaitTime = 5f; 

    [Header("Placement Options")]
    public bool allowFallbackPlacement = true; 
    public bool allowCameraRelativePlacement = true; 

    private GameObject placedRadarDisplayObject;
    private ARRaycastManager arRaycastManager;
    private static List<ARRaycastHit> hits = new List<ARRaycastHit>();
    private ARPlaneManager arPlaneManager;
    private Camera arCamera;
    private ARAnchor currentAnchor; 

    private bool arPlaneDetectionWorking = false;
    private bool fallbackEnabled = false;
    private float startTime;

    // Note: If other scripts were using these static actions with the old class name,
    // they will need to be updated to ARPlacementManager.OnRadarPlaced, etc.
    public static System.Action<GameObject> OnRadarPlaced;
    public static System.Action OnRadarRemoved;

    public bool IsRadarPlaced => placedRadarDisplayObject != null && placedRadarDisplayObject.activeInHierarchy;
    public GameObject PlacedRadarDisplayObject => placedRadarDisplayObject; 

    void Awake()
    {
        arRaycastManager = FindFirstObjectByType<ARRaycastManager>();
        arPlaneManager = FindFirstObjectByType<ARPlaneManager>();
        arCamera = FindFirstObjectByType<Camera>();

        if (arRaycastManager == null) Debug.LogError("ARRaycastManager not found in scene!");
        if (arPlaneManager == null) Debug.LogWarning("ARPlaneManager not found. Plane detection will be unavailable.");
        if (arCamera == null) Debug.LogError("AR Camera not found in scene!");
        
        startTime = Time.time;

        if (arPlaneManager != null)
        {
            StartCoroutine(MonitorARPlaneDetection());
        }
        else
        {
            Debug.LogWarning("ARPlaneManager is null. Enabling fallback placement immediately as plane detection is not possible.");
            EnableFallbackPlacement();
        }
    }

    IEnumerator MonitorARPlaneDetection()
    {
        float checkInterval = 1f;
        Debug.Log("Monitoring AR Plane Detection...");
        while (Time.time - startTime < arPlaneWaitTime)
        {
            if (arPlaneManager.trackables.count > 0)
            {
                bool foundTrackingPlane = false;
                foreach (ARPlane plane in arPlaneManager.trackables)
                {
                    if (plane.trackingState == TrackingState.Tracking && plane.alignment == PlaneAlignment.HorizontalUp)
                    {
                        foundTrackingPlane = true;
                        break;
                    }
                }
                if (foundTrackingPlane)
                {
                    Debug.Log("✅ AR horizontal plane detection is working! Using AR placement.");
                    arPlaneDetectionWorking = true;
                    // Optionally notify UI here: UIManager.Instance.UpdateStatus("AR Planes Detected. Tap to place.");
                    yield break; 
                }
            }
            // Optionally notify UI: UIManager.Instance.UpdateStatus("Scanning for surfaces...");
            yield return new WaitForSeconds(checkInterval);
        }
        Debug.LogWarning($"⚠️ AR plane detection did not find suitable planes after {arPlaneWaitTime}s. Enabling fallback.");
        EnableFallbackPlacement();
    }

    void EnableFallbackPlacement()
    {
        fallbackEnabled = true;
        Debug.Log("Fallback placement enabled. Tap anywhere to place radar (will attempt to anchor).");
        // Optionally notify UI: UIManager.Instance.UpdateStatus("Tap anywhere to place radar.");
    }

    void Update()
    {
        if (Input.touchCount == 0 || radarDisplayPrefab == null) return;

        Touch touch = Input.GetTouch(0);

        if (EventSystem.current.IsPointerOverGameObject(touch.fingerId))
        {
            return; 
        }

        if (touch.phase == TouchPhase.Began)
        {
            Debug.Log("Touch detected! Attempting to place radar...");
            bool placementSuccessful = false;

            if (arPlaneDetectionWorking && arRaycastManager != null && TryARPlanePlacement(touch.position))
            {
                placementSuccessful = true;
                Debug.Log("Radar placed via AR plane detection.");
            }
            else if (fallbackEnabled && allowFallbackPlacement && TryFallbackPlacement(touch.position))
            {
                placementSuccessful = true;
                Debug.Log("Radar placed via fallback method.");
            }
            
            if (!placementSuccessful) Debug.LogWarning("Failed to place radar.");
        }
    }

    bool TryARPlanePlacement(Vector2 touchPosition)
    {
        if (arRaycastManager.Raycast(touchPosition, hits, TrackableType.PlaneWithinPolygon))
        {
            ARRaycastHit hit = hits[0];
            ARPlane hitPlane = arPlaneManager.GetPlane(hit.trackableId);

            if (hitPlane != null && hitPlane.alignment == PlaneAlignment.HorizontalUp)
            {
                Debug.Log("Valid horizontal AR plane found. Placing radar with anchor.");
                CreateOrUpdateAnchorAndPlace(hit.pose);
                SetPlanesActive(false); 
                return true;
            }
            Debug.LogWarning("AR Raycast hit a plane, but it was not horizontal-up or was null.");
        }
        else Debug.Log("AR Raycast did not hit any AR planes.");
        return false;
    }

    bool TryFallbackPlacement(Vector2 touchPosition)
    {
        if (arCamera == null) 
        { 
            Debug.LogError("AR Camera is null in TryFallbackPlacement."); 
            return false; 
        }
        Debug.Log("🔄 Attempting fallback placement (will try to anchor)...");

        if (arRaycastManager != null && arRaycastManager.Raycast(touchPosition, hits, TrackableType.FeaturePoint))
        {
            ARRaycastHit hit = hits[0];
            Debug.Log("📍 Fallback: Hit AR feature point. Creating anchor.");
            CreateOrUpdateAnchorAndPlace(hit.pose);
            return true;
        }

        Debug.Log("🎯 Fallback: No feature points hit. Using estimated placement from camera.");
        Ray ray = arCamera.ScreenPointToRay(touchPosition);
        Vector3 estimatedPosition = ray.origin + ray.direction * fallbackDistance;
        Vector3 cameraForwardHorizontal = Vector3.ProjectOnPlane(arCamera.transform.forward, Vector3.up);
        Quaternion estimatedRotation = cameraForwardHorizontal.sqrMagnitude > 0.001f ? Quaternion.LookRotation(cameraForwardHorizontal.normalized) : Quaternion.identity;
        Pose estimatedPose = new Pose(estimatedPosition, estimatedRotation);
        
        CreateOrUpdateAnchorAndPlace(estimatedPose);
        return true;
    }
    
    void CreateOrUpdateAnchorAndPlace(Pose pose)
    {
        if (currentAnchor != null)
        {
            Destroy(currentAnchor.gameObject); 
            currentAnchor = null;
        }

        GameObject anchorGO = new GameObject("RadarWorldAnchor");
        anchorGO.transform.SetPositionAndRotation(pose.position, pose.rotation);
        currentAnchor = anchorGO.AddComponent<ARAnchor>();

        if (currentAnchor != null)
        {
            Debug.Log($"✅ AR Anchor '{anchorGO.name}' created at {pose.position}.");
            PlaceRadarAtAnchor(currentAnchor);
        }
        else
        {
            Debug.LogWarning("⚠️ Failed to create ARAnchor component. Placing radar without world anchor.");
            PlaceRadarWithoutAnchor(pose.position, pose.rotation);
        }
    }

    void PlaceRadarAtAnchor(ARAnchor anchor)
    {
        if (placedRadarDisplayObject == null)
        {
            placedRadarDisplayObject = Instantiate(radarDisplayPrefab, anchor.transform.position, anchor.transform.rotation, anchor.transform);
        }
        else
        {
            placedRadarDisplayObject.transform.SetParent(anchor.transform); 
            placedRadarDisplayObject.transform.SetPositionAndRotation(anchor.transform.position, anchor.transform.rotation);
        }
        Debug.Log($"✅ Radar Display placed/moved to AR anchor '{anchor.name}'. It will track with the world.");
        OnRadarPlaced?.Invoke(placedRadarDisplayObject);
    }

    void PlaceRadarWithoutAnchor(Vector3 position, Quaternion rotation)
    {
        if (placedRadarDisplayObject == null)
        {
            placedRadarDisplayObject = Instantiate(radarDisplayPrefab, position, rotation);
        }
        else
        {
            placedRadarDisplayObject.transform.SetParent(null); 
            placedRadarDisplayObject.transform.SetPositionAndRotation(position, rotation);
        }
        Debug.LogWarning("Radar Display placed/moved WITHOUT an AR anchor (will NOT track world).");
        OnRadarPlaced?.Invoke(placedRadarDisplayObject);
    }

    public void PlaceRadarAtCameraPosition() 
    {
        if (arCamera == null || radarDisplayPrefab == null) 
        {
            Debug.LogError("AR Camera or Radar Prefab not set for manual placement.");
            return;
        }

        Vector3 cameraPosition = arCamera.transform.position;
        Vector3 cameraForwardProjected = Vector3.ProjectOnPlane(arCamera.transform.forward, Vector3.up).normalized;
        Vector3 placementPosition = cameraPosition + cameraForwardProjected * fallbackDistance;
        placementPosition.y = cameraPosition.y + manualPlacementHeightOffset; 
        
        Quaternion placementRotation = cameraForwardProjected.sqrMagnitude > 0.001f ? Quaternion.LookRotation(cameraForwardProjected) : Quaternion.identity;
        Pose manualPose = new Pose(placementPosition, placementRotation);

        Debug.Log("Manual placement triggered. Attempting to create anchor at camera-relative position.");
        CreateOrUpdateAnchorAndPlace(manualPose);
        SetPlanesActive(false); 
    }

    public void RemoveRadar()
    {
        if (placedRadarDisplayObject != null)
        {
            Destroy(placedRadarDisplayObject);
            placedRadarDisplayObject = null;
            Debug.Log("3D Radar Display object removed.");
            OnRadarRemoved?.Invoke();

            if (currentAnchor != null)
            {
                Destroy(currentAnchor.gameObject);
                currentAnchor = null;
                Debug.Log("✅ AR Anchor associated with radar removed.");
            }
            SetPlanesActive(true); 
        }
    }

    private void SetPlanesActive(bool isActive)
    {
        if (arPlaneManager == null) return;
        foreach (ARPlane plane in arPlaneManager.trackables)
        {
            plane.gameObject.SetActive(isActive);
        }
        arPlaneManager.enabled = isActive; 
        Debug.Log($"AR Planes visibility and detection set to: {isActive}");
    }
} 