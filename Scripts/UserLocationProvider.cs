// UserLocationProvider.cs
using UnityEngine;
using System.Collections;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class UserLocationProvider : MonoBehaviour
{
    public static UserLocationProvider Instance { get; private set; }

    public bool IsLocationServiceRunning { get; private set; } = false;
    public float CurrentLatitude { get; private set; }
    public float CurrentLongitude { get; private set; }
    public float CurrentAltitude { get; private set; } // Altitude from GPS (meters above WGS84 ellipsoid)
    public float HorizontalAccuracy { get; private set; }
    public float VerticalAccuracy { get; private set; }
    public double LastUpdateTime { get; private set; }

    [Tooltip("How often (in seconds) to poll for location updates after service is running.")]
    public float updateIntervalSeconds = 1f;
    [Tooltip("Desired accuracy in meters. Lower values are more accurate but may take longer or fail on some devices.")]
    public float desiredAccuracyInMeters = 10f;
    [Tooltip("Minimum distance (in meters) device must move before location is updated.")]
    public float updateDistanceInMeters = 10f;

    private bool isRequestingPermission = false;

    void Awake()
    {
        Debug.LogError("!!! UserLocationProvider AWAKE !!!");
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keep this alive across scenes
        }
        else
        {
            Debug.LogWarning("Another instance of UserLocationProvider found. Destroying this one.");
            Destroy(gameObject);
        }
    }

    void Start()
    {
        Debug.LogError("!!! UserLocationProvider START !!!");
        StartCoroutine(InitializeLocationService());
    }

    IEnumerator InitializeLocationService()
    {
        Debug.LogError("!!! UserLocationProvider InitializeLocationService STARTED !!!");

        #if UNITY_ANDROID
        // First check if we have permission
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Debug.LogError("Location permission not granted. Requesting permission...");
            isRequestingPermission = true;
            Permission.RequestUserPermission(Permission.FineLocation);
            
            // Wait for permission dialog to be answered
            while (isRequestingPermission)
            {
                if (Permission.HasUserAuthorizedPermission(Permission.FineLocation))
                {
                    Debug.LogError("Location permission granted!");
                    isRequestingPermission = false;
                }
                else if (Permission.HasUserAuthorizedPermission(Permission.CoarseLocation))
                {
                    Debug.LogError("Coarse location permission granted, but fine location is preferred.");
                    isRequestingPermission = false;
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
        #endif

        // Check if location is enabled on the device
        if (!Input.location.isEnabledByUser)
        {
            Debug.LogError("Location services are disabled on the device. Please enable them in device settings.");
            IsLocationServiceRunning = false;
            yield break;
        }

        Debug.LogError("Starting location services...");
        Input.location.Start(desiredAccuracyInMeters, updateDistanceInMeters);

        // Wait until service initializes
        int maxWaitSeconds = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWaitSeconds > 0)
        {
            yield return new WaitForSeconds(1);
            maxWaitSeconds--;
            Debug.LogError($"Location service initializing... {maxWaitSeconds}s remaining");
        }

        if (maxWaitSeconds < 1)
        {
            Debug.LogError("Location service initialization timed out!");
            IsLocationServiceRunning = false;
            yield break;
        }

        if (Input.location.status == LocationServiceStatus.Failed)
        {
            Debug.LogError("Unable to determine device location. Service failed!");
            IsLocationServiceRunning = false;
            yield break;
        }

        if (Input.location.status == LocationServiceStatus.Running)
        {
            Debug.LogError("Location service started successfully!");
            IsLocationServiceRunning = true;
            StartCoroutine(PollLocationDataCoroutine());
        }
        else
        {
            Debug.LogError($"Location service ended with unexpected status: {Input.location.status}");
            IsLocationServiceRunning = false;
        }
    }

    IEnumerator PollLocationDataCoroutine()
    {
        Debug.LogError("Starting to poll location data...");
        while (IsLocationServiceRunning)
        {
            if (Input.location.status == LocationServiceStatus.Running)
            {
                LocationInfo locationData = Input.location.lastData;
                CurrentLatitude = locationData.latitude;
                CurrentLongitude = locationData.longitude;
                CurrentAltitude = locationData.altitude;
                HorizontalAccuracy = locationData.horizontalAccuracy;
                VerticalAccuracy = locationData.verticalAccuracy;
                LastUpdateTime = locationData.timestamp;

                Debug.LogError($"Location Update - Lat: {CurrentLatitude:F6}, Lon: {CurrentLongitude:F6}, Alt: {CurrentAltitude:F1}m");
            }
            else
            {
                Debug.LogError($"Location service status changed to: {Input.location.status}");
                IsLocationServiceRunning = false;
            }
            yield return new WaitForSeconds(updateIntervalSeconds);
        }
        Debug.LogError("Stopped polling location data.");
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            if (IsLocationServiceRunning)
            {
                Input.location.Stop();
                Debug.LogError("Location service stopped due to application pause.");
                IsLocationServiceRunning = false;
            }
        }
        else
        {
            if (Input.location.isEnabledByUser && !IsLocationServiceRunning)
            {
                Debug.LogError("Application resumed. Restarting location services...");
                StartCoroutine(InitializeLocationService());
            }
        }
    }

    void OnDestroy()
    {
        if (Input.location.status == LocationServiceStatus.Running)
        {
            Input.location.Stop();
            Debug.LogError("Location service stopped on UserLocationProvider destroy.");
        }
    }
}