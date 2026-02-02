# BotW-Style Grass System

A high-performance, GPU-based grass rendering system for **Unity URP**, inspired by *The Legend of Zelda: Breath of the Wild*.

---

## Media
- [Click here to watch the Technical Showcase](https://youtu.be/YUdifIoGrvY)

## Features

### Rendering
- **GPU Instancing** - Render millions of grass blades efficiently using DrawMeshInstancedIndirect
- **Dual Rendering Modes** - Procedural triangular blades or custom imported meshes
- **GPU Frustum Culling** - Compute shader-based visibility culling
- **Distance-based LOD** - Automatic fade-out at configurable distances
- **Wind Simulation** - Realistic wind animation with customizable speed, strength, and frequency

### Interaction
- **Player Interaction** - Grass reacts to player movement in real-time
- **Real-time Deformation** - Grass bends away from interactors with configurable radius and strength
- **Grass Decals** - Project textures onto grass regions with rotation and alpha blending

### Editor Tools
- **Grass Painter** - Paint, erase, and modify grass directly in Scene view
- **ScriptableObject Settings** - Create and swap grass presets easily
- **Performance Overlay** - Built-in FPS and instance count monitoring

### Optimization
- **Console-Ready** - Optimized for Nintendo Switch (30 FPS target)
- **Compute Shader Culling** - Efficient GPU-based visibility culling
- **Memory Optimized** - Proper buffer management and cleanup

---

## Requirements

| Requirement | Version |
|-------------|---------|
| Unity | 2022.3 LTS or newer |
| Render Pipeline | URP 14.0+ |
| Input System | New Input System |

---

## Installation

### Via Git URL (Recommended)
1. Open **Window > Package Manager**
2. Click **+** > **Add package from git URL**
3. Enter: `https://github.com/devotobrendo/GrassSystem.git`

### Via Local Folder
1. Copy the `GrassSystem` folder to your project's `Packages/` directory
2. Unity will automatically detect and import the package

---

## Quick Start

### 1. Create Grass Settings
Right-click in Project > **Create > Grass System > Grass Settings**

### 2. Setup Grass Renderer
1. Create an empty GameObject
2. Add the `GrassRenderer` component
3. Assign your Grass Settings asset
4. Assign the culling shader (Shaders/GrassCulling.compute)
5. Assign a grass material (create one using Shaders/GrassLit or GrassUnlit)

### 3. Paint Grass
1. Open **Window > Grass System > Grass Painter**
2. Select your terrain/surface
3. Hold **Shift + Left Click** to paint grass

### 4. Add Player Interaction (Optional)
Add the `GrassInteractor` component to your player character.

---

## Painter Controls

| Action | Control |
|--------|---------|
| Paint | Left Click (drag to paint stroke) |
| Erase | Shift + Left Click |
| Adjust Brush Size | Ctrl + Scroll Wheel |
| Rotate View | Right Click + Drag |

### Fluid Brush System (v1.4.0+)

The Grass Painter now uses a **deferred processing system** for better performance:

1. **During Stroke**: Positions are collected (shown as ghost circles)
2. **On Mouse Release**: Positions are processed in batches with progress indicator
3. **Result**: Fluid brush experience even with 100k+ grass instances

---

## Multiple Renderers Support (v1.3.0+)

You can now have **multiple GrassRenderer components** in a single scene to render different grass types simultaneously.

### Setup
1. Create separate GameObjects for each grass type
2. Add `GrassRenderer` to each with different `SO_GrassSettings`
3. Use the **Renderer Dropdown** in Grass Painter to switch targets

### Features
- **Isolated Buffers**: Each renderer maintains its own grass data
- **Safe Switching**: Painter saves current renderer's data before switching
- **Simultaneous Rendering**: All renderers are visible at the same time
- **Per-Renderer Settings**: Different colors, sizes, densities per renderer

### Best Practices
- Use separate settings assets for each grass type
- Limit total instance count across all renderers for performance
- Consider using different LOD distances for less important grass types

---

## Component Reference

### GrassRenderer

The core rendering component that manages all grass instances.

**Setup:**
1. Add to any GameObject in your scene
2. Assign a `SO_GrassSettings` asset
3. Paint grass using the Grass Painter tool

**Inspector Properties:**

| Property | Description |
|----------|-------------|
| **Grass Settings** | Reference to the SO_GrassSettings asset |
| **Grass Data List** | Runtime list of all grass instances (read-only) |

**Public API:**

```csharp
// Get the grass renderer
GrassRenderer renderer = GetComponent<GrassRenderer>();

// Get current grass count
int total = renderer.GrassDataList.Count;

// Get visible grass count (after culling)
int visible = renderer.VisibleGrassCount;

// Clear all grass
renderer.ClearGrass();

// Rebuild buffers after modifying grass data
renderer.RebuildBuffers();
```

---

### GrassInteractor

Add this component to any object that should bend the grass (player, NPCs, vehicles, etc.).

**Setup:**
1. Add to your player or any moving object
2. Adjust radius and strength as needed

**Inspector Properties:**

| Property | Description |
|----------|-------------|
| **Radius** | Size of the bending area (0.1 - 5.0) |
| **Strength** | How much the grass bends (0.0 - 2.0) |
| **Height Offset** | Vertical offset from transform position |

**Usage Example:**

```csharp
// The component automatically registers itself
// Just add it to your player GameObject

public class Player : MonoBehaviour
{
    // GrassInteractor on same object will automatically affect nearby grass
    private GrassInteractor interactor;
    
    void Start()
    {
        interactor = GetComponent<GrassInteractor>();
    }
    
    // Optionally adjust at runtime
    void OnSprint()
    {
        interactor.radius = 2f;  // Larger area when running
        interactor.strength = 1.5f;
    }
}
```

---

### GrassDecal

Projects a texture onto grass, allowing you to "paint" patterns, logos, or effects onto specific regions.

**Setup:**
1. Add to any GameObject in your scene
2. Assign a decal texture (use textures with alpha for transparency)
3. Position using the Transform component
4. Adjust size and blend as needed

**Inspector Properties:**

| Property | Description |
|----------|-------------|
| **Decal Texture** | The texture to project onto the grass |
| **Size** | Projection area size in world units (X, Z) |
| **Blend** | Decal visibility (0 = invisible, 1 = fully visible) |
| **Target Renderer** | The GrassRenderer to apply to (auto-detected if null) |

**Features:**
- Full rotation support via Transform component
- Scene view gizmos for easy positioning
- Alpha-blended projection
- Works with both GrassLit and GrassUnlit shaders

**Usage Example:**

```csharp
// Create a decal programmatically
GameObject decalObj = new GameObject("GrassDecal");
GrassDecal decal = decalObj.AddComponent<GrassDecal>();

// Configure the decal
decal.decalTexture = myTexture;
decal.size = new Vector2(10f, 10f);
decal.blend = 0.8f;

// Position and rotate via Transform
decal.transform.position = new Vector3(50f, 0f, 50f);
decal.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
```

---

### GrassPerformanceOverlay

Displays real-time performance metrics for debugging and optimization. **Works on all platforms including Nintendo Switch.**

**Setup:**
1. Add to any GameObject in your scene
2. Press F1 (keyboard) or Minus button (Switch) to toggle the overlay

**Display Modes:**

| Mode | Description | Recommended For |
|------|-------------|-----------------|
| **Full** | Complete stats: FPS, frame time, grass counts, culling % | PC Development |
| **Minimal** | FPS only (lightweight) | Switch / Console |

**Controls:**

| Action | Keyboard | Gamepad (Switch) |
|--------|----------|------------------|
| Toggle Overlay | **F1** | **Select / Minus (-)** |
| Switch Mode | **F2** | **Start / Plus (+)** |

**Inspector Properties:**

| Property | Description |
|----------|-------------|
| **Display Mode** | Full (all stats) or Minimal (FPS only) |
| **Toggle Key** | Keyboard key to show/hide overlay (default: F1) |
| **Switch Mode Key** | Keyboard key to change display mode (default: F2) |
| **Gamepad Toggle Button** | Controller button to show/hide overlay (Select, Start, LeftShoulder, RightShoulder, LeftStick, RightStick) |
| **Gamepad Switch Mode Button** | Controller button to change display mode |
| **Anchor** | Screen position of the overlay |
| **Font Size** | Text size (12 - 32) |
| **Background Alpha** | Panel transparency (0 - 1) |
| **Log Lifecycle Events** | Enable console logging for debug (also accessible via `LogLifecycleEventsEnabled` static property) |

**Displayed Metrics (Full Mode):**
- Current FPS (color-coded: green ≥30, yellow ≥24, red <24)
- Frame time in milliseconds
- Min/Max FPS recorded
- Total grass count
- Visible grass count (after culling)
- Culling percentage

**Public API:**

```csharp
// Reset min/max FPS statistics
GrassPerformanceOverlay overlay = GetComponent<GrassPerformanceOverlay>();
overlay.ResetStats();

// Check if lifecycle logging is enabled (static)
bool loggingEnabled = GrassPerformanceOverlay.LogLifecycleEventsEnabled;
```

---

### SimplePlayerController

Simple first-person controller for testing the Grass System. **Supports keyboard/mouse and gamepad (including Nintendo Switch).**

**Setup:**
1. Add to a GameObject with a `CharacterController` component
2. Assign a camera to the `Camera Transform` field
3. Optionally add a `GrassInteractor` to the same object

**Controls:**

| Action | Keyboard | Gamepad (Switch) |
|--------|----------|------------------|
| Move | **WASD** | **Left Stick** |
| Look | **Mouse** | **Right Stick** |
| Jump | **Space** | **A (South)** |
| Sprint | **Left Shift** | **L Trigger/Shoulder** |
| Pause/Menu | **Escape** | **Start (+)** |

**Inspector Properties:**

| Property | Description |
|----------|-------------|
| **Move Speed** | Base movement speed (default: 5) |
| **Sprint Multiplier** | Speed multiplier when sprinting (default: 2x) |
| **Jump Height** | Jump height in units (default: 1.5) |
| **Gravity** | Gravity force (default: -20) |
| **Mouse Sensitivity** | Mouse look sensitivity (default: 100) |
| **Gamepad Look Sensitivity** | Right stick look sensitivity (default: 150) |
| **Camera Transform** | Reference to the camera for look rotation |
| **Invert Gamepad Y** | Invert Y axis on gamepad right stick |
| **Stick Deadzone** | Deadzone for analog sticks (0.1 - 0.9) |

**Public API:**

```csharp
// Check if player is using gamepad
SimplePlayerController player = GetComponent<SimplePlayerController>();
bool usingGamepad = player.IsUsingGamepad;
```

---

### SO_GrassSettings

ScriptableObject that holds all configuration for grass appearance and behavior.

**Create New:**
Right-click in Project > **Create > Grass System > Grass Settings**

**Grass Mode Options:**

| Mode | Description |
|------|-------------|
| **Default** | Procedural triangular blades (Zelda-style) |
| **CustomMesh** | Use imported FBX meshes |

**Default Mode Properties:**

| Property | Description |
|----------|-------------|
| **Min/Max Width** | Blade width variation range |
| **Min/Max Height** | Blade height variation range |

**Custom Mesh Mode Properties:**

| Property | Description |
|----------|-------------|
| **Custom Meshes** | List of meshes to randomly spawn |
| **Min/Max Size** | Uniform scale variation range |
| **Use Only Albedo Color** | Ignore tints, use texture colors only |
| **Mesh Rotation Offset** | Base rotation adjustment for meshes |
| **Max Tilt Angle** | Random tilt for natural variation |
| **Tilt Variation** | How much tilt varies between instances |

**Common Properties:**

| Category | Properties |
|----------|------------|
| **Wind** | Speed, Strength, Frequency |
| **LOD** | Min Fade Distance, Max Draw Distance |
| **Lighting** | Top Tint, Bottom Tint, Translucency |
| **Interaction** | Interactor Strength, Max Interactors |
| **Rendering** | Cast Shadows, Receive Shadows |

**Advanced Limits:**

Enable `Show Advanced Limits` in the Inspector to customize slider maximum values beyond default ranges.

| Category | Limit Property | Default | Description |
|----------|----------------|---------|-------------|
| **Size** | Max Size Limit | 3.0 | Maximum uniform scale for custom meshes |
| **Size** | Max Blade Width Limit | 0.3 | Maximum blade width (Default mode) |
| **Size** | Max Blade Height Limit | 1.5 | Maximum blade height (Default mode) |
| **Wind** | Max Wind Speed Limit | 5.0 | Maximum wind speed |
| **Wind** | Max Wind Strength Limit | 1.0 | Maximum wind strength |
| **LOD** | Max Draw Distance Limit | 200 | Maximum draw distance |
| **LOD** | Max Fade Distance Limit | 150 | Maximum fade start distance |
| **Tilt** | Max Tilt Angle Limit | 45° | Maximum random tilt angle |
| **Interaction** | Max Interactor Strength Limit | 2.0 | Maximum interactor strength |
| **Interaction** | Max Interactors Limit | 16 | Maximum number of interactors |
| **Pattern** | Max Pattern Scale Limit | 10.0 | Maximum checkered pattern scale |

---

### SO_GrassToolSettings

ScriptableObject that holds Grass Painter tool configuration. Created automatically when opening the Grass Painter window.

**Brush Properties:**

| Property | Description |
|----------|-------------|
| **Brush Size** | Size of the painting brush (0.1 - 50) |
| **Density** | Grass density per brush stroke (0.1 - 10) |
| **Normal Limit** | Maximum surface angle for painting (0 - 1) |

**Size Override:**

| Property | Description |
|----------|-------------|
| **Use Custom Size** | Enable to override settings with tool-specific values |
| **Min/Max Blade Width** | Custom blade width range (Default mode) |
| **Min/Max Blade Height** | Custom blade height range (Default mode) |
| **Min/Max Blade Size** | Custom uniform scale range (Custom Mesh mode) |

**Cluster Spawning:**

| Property | Description |
|----------|-------------|
| **Use Cluster Spawning** | Spawn multiple blades in natural clusters |
| **Min/Max Blades Per Cluster** | Number of blades per cluster (1 - 10) |
| **Cluster Radius** | Radius of each cluster (0.01 - 0.5) |

**Advanced Limits (Tool):**

Enable `Show Advanced Limits` in the Grass Painter window to customize tool slider ranges.

| Category | Limit Property | Default | Description |
|----------|----------------|---------|-------------|
| **Brush** | Max Brush Size Limit | 50 | Maximum brush size |
| **Brush** | Max Density Limit | 10 | Maximum brush density |
| **Cluster** | Max Blades Per Cluster Limit | 10 | Maximum blades per cluster |
| **Cluster** | Max Cluster Radius Limit | 0.5 | Maximum cluster radius |
| **Blade** | Max Blade Width Limit | 0.5 | Maximum blade width |
| **Blade** | Max Blade Height Limit | 2.0 | Maximum blade height |
| **Blade** | Max Blade Size Limit | 3.0 | Maximum uniform scale |
| **Height** | Max Height Brush Limit | 2.0 | Maximum height brush value |

---

## Package Structure

```
GrassSystem/
├── Runtime/          # Core runtime scripts
│   ├── GrassRenderer.cs
│   ├── GrassInteractor.cs
│   ├── GrassDecal.cs
│   ├── GrassData.cs
│   ├── GrassPerformanceOverlay.cs
│   └── SO_GrassSettings.cs
├── Editor/           # Editor tools and inspectors
│   ├── GrassPainterWindow.cs
│   ├── SO_GrassSettingsEditor.cs
│   └── GrassMeshGenerator.cs
├── Shaders/          # Grass shaders
│   ├── GrassLit.shader
│   ├── GrassUnlit.shader
│   ├── GrassCulling.compute
│   └── GrassCommon.hlsl
├── Textures/         # Default grass textures
└── Presets/          # Example settings and materials
```

---

## License

MIT License - See [LICENSE.md](LICENSE.md) for details.

---

## Credits

Developed by Brendo Otavio Carvalho de Matos as a technical showcase demonstrating:
- GPU compute shader programming
- Unity URP shader development
- Editor tool development
- Performance optimization for consoles
