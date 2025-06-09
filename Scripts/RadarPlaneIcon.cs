using UnityEngine;

public class RadarPlaneIcon : MonoBehaviour
{
    public string planeAddress;
    // Optional: You could store the full PlaneData here if needed, 
    // or a direct reference to the RadarDisplay for callbacks.
    // For now, planeAddress is enough for RadarDisplay to look up the data.

    // This script primarily acts as a tag and data holder for raycast detection.
    // Ensure the GameObject this is attached to (your radar plane icon prefab)
    // has a Collider component (e.g., BoxCollider, SphereCollider).
} 