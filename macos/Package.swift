// swift-tools-version: 6.2
import PackageDescription

let package = Package(
    name: "MurmurMac",
    platforms: [.macOS(.v26)],
    products: [.executable(name: "Murmur", targets: ["MurmurMac"])],
    targets: [
        .target(name: "MurmurCore"),
        .executableTarget(name: "MurmurMac", dependencies: ["MurmurCore"]),
        .testTarget(name: "MurmurCoreTests", dependencies: ["MurmurCore"], resources: [.process("Fixtures")])
    ]
)
