import Foundation

// Thinking Orbs 0.3.2 searching-20, MIT, copyright 2026 Jakub Antalik.
// Same numeric construction as the licensed Windows port; no webview needed.
public struct OrbDot: Sendable { public let x, y, z, radius, white, alpha: Double }
public enum ThinkingOrb {
    public static func frame(seconds: Double) -> [OrbDot] {
        let t = seconds * 2.665, yaw = t * 0.5, tilt = 0.4 + 0.06 * sin(t * 0.35)
        let st = sin(tilt), ct = cos(tilt), sy = sin(yaw), cy = cos(yaw)
        let scan = t * (0.5 + 1.2 * 4.335), rs = pow(20.0 / 300, 0.6)
        var dots: [OrbDot] = []
        for latitude in 0...6 {
            let lat = -Double.pi / 2 + Double(latitude) / 6 * Double.pi
            let cosLat = cos(lat), sinLat = sin(lat), count = max(1, Int(floor(abs(cosLat) * 14 + 0.5)))
            for longitude in 0..<count {
                let lon = Double(longitude) / Double(count) * 2 * Double.pi
                let x = cosLat * cos(lon), z = cosLat * sin(lon)
                let x1 = x * cy + z * sy, z1 = -x * sy + z * cy
                let y1 = sinLat * ct - z1 * st, depthZ = sinLat * st + z1 * ct, depth = (depthZ + 1) / 2
                let angle = lon + t * 0.5 - scan, delta = atan2(sin(angle), cos(angle))
                let boost = exp(-delta * delta / 0.18) * max(0, depthZ)
                dots.append(OrbDot(x: 10 + x1 * 8.2, y: 10 - y1 * 8.2, z: depthZ,
                    radius: max(0.3, (1.05 + 2.975 * depth + boost) * rs),
                    white: 0.62 - 0.54 * depth, alpha: 0.45 + 0.55 * min(1, boost)))
            }
        }
        return dots.sorted { $0.z < $1.z }
    }
}
