
# AR Aircraft Tracker

A Unity-based augmented reality application that displays real-time aircraft positions from ADS-B data streams in a 3D radar visualization overlaid on the real world.
App was initialy created for a Uni project (M8 int-tech) however, due to the success of the app I will further optimize, improve and eventually release the app as a 3d flight tracker.

## 🌟 Features

- **Real-Time Aircraft Tracking**: Live ADS-B data via WebSocket connection (used with Utwente reciever) 
- **3D AR Radar Display**: Interactive radar showing aircraft positions in 3D space
- **Geographic Positioning**: GPS-based user location with coordinate transformation
- **Multi-Platform AR**: Unity AR Foundation supporting ARCore/ARKit
- **Signal Quality Indicators**: RSSI data capture and processing
- **Altitude Visualization**: 3D altitude rings showing aircraft at different flight levels
- **Interactive Aircraft Info**: Tap aircraft for detailed information panels
- **Test Mode**: Hardcoded coordinates for development without GPS
- **Demo Aircraft**: Built-in test aircraft for visualization testing

## 🚀 Quick Start

<img src="https://github.com/user-attachments/assets/d7550b2c-d6af-4cdc-96a5-b14e4950c07e" width="200" height="350" alt="ARappStartExample">
<img src="https://github.com/user-attachments/assets/46658fd1-021b-406b-9a5a-d89187cd532e" width="200" height="350" alt="ARappSpecific">

- Unity 2022.3+ with AR Foundation package
- Android device with ARCore support OR iOS device with ARKit support
- ADS-B data source (e.g., dump1090, RTL-SDR setup)
- Development: Android SDK, Xcode (for iOS)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/ar-aircraft-tracker.git
   cd ar-aircraft-tracker
   ```

2. **Open in Unity**
   - Launch Unity Hub
   - Open the project folder
   - Ensure AR Foundation packages are installed

3. **Configure ADS-B Source**
   - Update WebSocket URL in `WebSocketConection.cs`
   - Default: `ws://192.168.0.230:9000`

4. **Build and Deploy**
   - Switch platform to Android/iOS
   - Configure build settings
   - Deploy to AR-capable device

## 📱 Usage

### Quick Setup (Radar-Only Mode)
1. Launch the app on your AR device
2. Grant camera and location permissions
3. Tap "Place Radar" to position the 3D radar display
4. Enable "Demo Planes" to see test aircraft
5. Tap aircraft icons for detailed information

### Full AR Mode
1. Move device to scan for horizontal surfaces
2. Tap detected surface to place radar with world anchoring
3. Connect to live ADS-B data stream
4. View real-time aircraft positions in AR space

### Test Mode
- Uses University of Twente coordinates (52.2387°N, 6.8564°E)
- Works without GPS or live data
- Perfect for development and demonstrations

## 🛠️ Technical Stack

### Core Technologies
- **Unity 2022.3+**: Game engine and development platform
- **AR Foundation**: Cross-platform AR framework
- **ARCore/ARKit**: Platform-specific AR backends
- **NativeWebSocket**: Real-time data communication
- **Newtonsoft.Json**: JSON parsing and serialization

### Data Sources
- **ADS-B**: Automatic Dependent Surveillance-Broadcast
- **GPS**: Device location services
- **Compass/IMU**: Device orientation and pose tracking

### Key Components
- `WebSocketConection.cs`: Real-time ADS-B data acquisition
- `PlaneManagerRA.cs`: Aircraft state management and AR positioning
- `RadarDisplay.cs`: 3D radar visualization and coordinate transformation
- `UserLocationProvider.cs`: GPS and device positioning
- `ARPlacementManager.cs`: AR surface detection and radar placement

## 📡 Data Processing

### ADS-B Fields Processed
- **address**: ICAO aircraft identifier
- **latitude/longitude**: Geographic coordinates
- **altitude**: Flight level in feet
- **speed**: Ground speed in knots
- **heading**: Magnetic heading in degrees
- **callsign**: Flight identifier
- **rssi**: Signal strength indicator
- **receiver**: Ground station identifier

### Coordinate Transformation
1. Calculate geographic deltas from user position
2. Convert to real-world meters using Earth radius
3. Scale to radar display dimensions (100km → 0.25m)
4. Apply altitude scaling for 3D visualization
5. Smooth position updates for fluid movement

## 🔧 Configuration

### WebSocket Settings
```csharp
public string serverURL = "ws://192.168.0.230:9000";
```

### Radar Display Settings
```csharp
public float radarRealWorldRangeKm = 100f;      // 100km range
public float radarDisplayRadiusMeters = 0.25f;  // 25cm physical radius
public float radarAltitudeScale = 0.00007f;     // Vertical scaling
```

### Test Coordinates
```csharp
public float testLatitude = 52.2387f;   // University of Twente
public float testLongitude = 6.8564f;   // Enschede, Netherlands
public float testAltitude = 50f;        // 50 meters elevation
```

## 🎯 Key Features Explained

### AR Placement System
- **Primary**: AR plane detection on horizontal surfaces
- **Fallback**: Tap-anywhere placement after 5 seconds
- **Manual**: Button-based camera-relative positioning
- **Anchoring**: World-space anchors for stability

### Aircraft Visualization
- **3D Positioning**: Real-world coordinates mapped to AR space
- **Altitude Rings**: Visual indicators at 10k, 20k, 30k, 40k, 50k, 60k feet
- **Range Rings**: Distance markers showing 25km, 50km, 75km, 100km
- **Info Panels**: Detailed aircraft data on tap interaction

### Network Considerations
- **Real-time Processing**: Zero-latency message handling
- **Rate Limiting**: 0.1-second minimum update intervals
- **Stale Data**: Automatic removal after 120 seconds
- **Signal Quality**: RSSI capture for future reliability indicators

## 🚧 Development Notes

### Known Limitations
- Single ADS-B receiver support (multi-receiver planned)
- RSSI visualization not yet implemented
- Line-of-sight calculations not included
- Limited to 1090 MHz ADS-B signals

### Future Enhancements
- Multi-receiver data fusion
- RSSI-based visualization (signal strength colors/sizes)
- Terrain-aware coverage prediction
- Historical flight path visualization
- Aircraft type-specific icons

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Setup
- Use Unity 2022.3 LTS for compatibility
- Follow Unity C# coding conventions
- Test on both Android and iOS devices
- Include demo mode for feature testing

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- **Utwente**: ADS-B decoding script/software
- **Unity AR Foundation**: Cross-platform AR framework
- **FlightAware**: ADS-B data specifications
- **OpenSky Network**: Flight tracking research



🟢 **Active Development** - Regularly updated and maintained

**Current Version**: 0.2a
**Unity Version**: 2022.3 LTS
**AR Foundation**: 5.0+
**Platform Support**: Android (ARCore), iOS (ARKit)

---

*Built with ❤️ for M8 Int-tech and AR communities*
