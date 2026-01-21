# BotW-Style Grass System

A high-performance, GPU-based grass rendering system for **Unity URP**, inspired by *The Legend of Zelda: Breath of the Wild*.

---

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
| Paint | Shift + Left Click |
| Erase | Shift + Ctrl + Left Click |
| Adjust Brush Size | Shift + Scroll Wheel |
| Rotate View | Right Click + Drag |

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
| Toggle Overlay | **F1** | **Minus (-)** |
| Switch Mode | **F2** | **Plus (+)** |

**Inspector Properties:**

| Property | Description |
|----------|-------------|
| **Display Mode** | Full (all stats) or Minimal (FPS only) |
| **Toggle Key** | Keyboard key to show/hide overlay |
| **Switch Mode Key** | Keyboard key to change display mode |
| **Gamepad Toggle Button** | Controller button to show/hide overlay |
| **Gamepad Switch Mode Button** | Controller button to change display mode |
| **Anchor** | Screen position of the overlay |
| **Font Size** | Text size (12 - 32) |
| **Background Alpha** | Panel transparency (0 - 1) |
| **Log Lifecycle Events** | Enable console logging for debug |

**Displayed Metrics (Full Mode):**
- Current FPS (color-coded: green ≥30, yellow ≥24, red <24)
- Frame time in milliseconds
- Min/Max FPS recorded
- Total grass count
- Visible grass count (after culling)
- Culling percentage

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
| **Move Speed** | Base movement speed |
| **Sprint Multiplier** | Speed multiplier when sprinting |
| **Jump Height** | Jump height in units |
| **Gravity** | Gravity force |
| **Mouse Sensitivity** | Mouse look sensitivity |
| **Gamepad Look Sensitivity** | Right stick look sensitivity |
| **Invert Gamepad Y** | Invert Y axis on gamepad |
| **Stick Deadzone** | Deadzone for analog sticks (0.1 - 0.9) |

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

---

## Package Structure

```
GrassSystem/
├── Runtime/          # Core runtime scripts
│   ├── GrassRenderer.cs
│   ├── GrassInteractor.cs
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
