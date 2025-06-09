// WebSocketController.cs
using UnityEngine;
using System;
using NativeWebSocket; // Make sure this matches the namespace of your imported asset
using Newtonsoft.Json; // For robust JSON parsing

public class WebSocketConection : MonoBehaviour
{
    public string serverURL = "ws://192.168.0.230:9000";
    private WebSocket websocket;

    // Event to notify other parts of the application when new data arrives
    public static event Action<PlaneData> OnPlaneDataReceived;
    public static event Action<PlaneData, string> OnPlaneDataReceivedWithRaw; // New event with raw JSON

    async void Start()
    {
        websocket = new WebSocket(serverURL);

        websocket.OnOpen += () =>
        {
            Debug.Log("WebSocket Connection open!");
        };

        websocket.OnError += (e) =>
        {
            Debug.LogError("WebSocket Error! " + e);
        };

        websocket.OnClose += (e) =>
        {
            Debug.Log("WebSocket Connection closed: " + e);
        };

        websocket.OnMessage += (bytes) =>
        {
            var message = System.Text.Encoding.UTF8.GetString(bytes);
            // Debug.Log("Raw Message from server: " + message); // Optional: log raw message

            // Process the message (parse JSON and invoke event)
            ProcessMessage(message);
        };

        Debug.Log($"Attempting to connect WebSocket to: {serverURL}");
        // Important: NativeWebSocket's Connect() is asynchronous.
        // You need to `await` it or handle its completion.
        try
        {
            await websocket.Connect();
        }
        catch (Exception ex)
        {
            Debug.LogError($"WebSocket connection failed: {ex.Message}");
        }
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        // NativeWebSocket requires you to dispatch messages regularly in Update
        // if you are not in a WebGL build.
        if (websocket != null)
        {
            websocket.DispatchMessageQueue();
        }
#endif
    }

    private void ProcessMessage(string jsonString)
    {
        try
        {
            PlaneData data = JsonConvert.DeserializeObject<PlaneData>(jsonString);

            if (data != null && !string.IsNullOrEmpty(data.address))
            {
                Debug.Log($"WebSocket received plane data: {data.ToString()}"); // Enable detailed logging
                OnPlaneDataReceived?.Invoke(data); // Notify subscribers
                OnPlaneDataReceivedWithRaw?.Invoke(data, jsonString); // Notify subscribers with raw JSON
            }
            else
            {
                Debug.LogWarning("Received message could not be parsed into PlaneData or address is missing: " + jsonString);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to parse JSON message: {jsonString}\nError: {e.Message}\nStackTrace: {e.StackTrace}");
        }
    }

    private async void OnApplicationQuit()
    {
        if (websocket != null && (websocket.State == WebSocketState.Open || websocket.State == WebSocketState.Connecting))
        {
            Debug.Log("Closing WebSocket connection on application quit.");
            await websocket.Close();
        }
    }
}