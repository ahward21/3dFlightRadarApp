using System;

[System.Serializable] // Makes it visible in Inspector and usable by JsonUtility (though we prefer Newtonsoft)
public class PlaneData
{
    public string address;
    public float? altitude; // Use nullable types for fields that might be missing
    public float? latitude;
    public float? longitude;
    public float? speed;
    public float? heading;
    public string callsign;
    public string timestamp;
    public float? rssi;
    public string receiver;

    // Helper method to get a display string (optional)
    public override string ToString()
    {
        return $"Addr: {address}, Alt: {altitude?.ToString() ?? "N/A"}, Lat: {latitude?.ToString() ?? "N/A"}, Lon: {longitude?.ToString() ?? "N/A"}, Call: {callsign ?? "N/A"}";
    }
}