# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [4.0.3] - 2026-02-06

### Added
- **Migration UI** - New "Migrate to Asset" button when renderer has data but external asset is empty
  - Handles all 4 data states: asset only, renderer only, both, neither
  - Clear error messages and recovery options when both are empty
  - Automatic backup restore prompt when data is lost

## [4.0.2] - 2026-02-05

### Fixed
- **Scene/Prefab File Bloat** - Fixed critical issue where grass data was duplicated in scene files
  - `OnBeforeSerialize()` now clears embedded data when using external `GrassDataAsset`
  - Prefab size reduced from ~80MB to ~1KB when using external data
  - Scene files no longer bloat to 400MB+ with grass prefabs
  - Fixes "Failed to create Object Undo" errors when dragging prefabs

### Improved
- **Prefab Instantiation** - Fixed grass not appearing when dragging prefab into scene
  - `OnEnable()` now correctly loads data from external asset after instantiation
  - Added deferred loading fallback for edge cases
  - Better logging for debugging initialization flow

## [4.0.1] - 2026-02-05

### Fixed
- **Decal Texture Rotation** - Decal textures now rotate correctly with the gizmo arrow
  - Fixed UV calculation so texture "up" aligns with world forward (Z+)
  - Visual rotation matches green arrow indicator in Scene view
- **Ghost Decal Images** - Fixed residual decal images when changing blend modes or layers
  - Layer change detection now clears old layer data before applying to new slot
  - OnValidate properly handles Inspector changes to layer property
- **Prefab Persistence** - Improved grass data serialization for prefabs
  - Added `ISerializationCallbackReceiver` interface to GrassRenderer
  - Deserialization callback triggers proper reinitialization on prefab instantiation

## [1.4.0] - 2026-02-02

### Added
- **Fluid Brush System** - Dramatically improved brush responsiveness
  - Positions are queued during stroke instead of processed immediately
  - Batch processing (5 positions/frame) on mouse release prevents editor freezing
  - Ghost circle preview shows pending paint/erase areas during stroke
  - Pulsing progress indicator with "Processing X/Y..." text during deferred processing
  - Cursor pulses during active stroke for visual feedback

### Fixed
- **Memory Leak Prevention** - `GrassRenderer.Initialize()` now always calls `Cleanup()` first
  - Prevents accumulation of material instances and compute buffers
  - Fixes editor slowdown after extended use
  - Proper resource release on renderer reinitialization

### Removed
- **Ground Shader Feature** (temporarily) - Removed for stability while under development
  - Ground blend UI removed from inspector
  - `GroundTextureResolution` enum removed
  - Will be re-added in future release with improved baking algorithm

### Technical
- Deferred paint processing uses `EditorApplication.delayCall` for non-blocking batch execution
- Input blocked during deferred processing to prevent race conditions
- Buffer rebuild only called once at end of stroke instead of per-position



## [1.3.0] - 2026-02-01

### Added
- **Multiple GrassRenderers Support** - Paint and render different grass types simultaneously
  - Proper buffer isolation per renderer
  - Safe switching between renderers in Grass Painter tool
  - Per-frame buffer rebinding in compute shader
- **Density Modes** - Three density calculation methods:
  - `PerUnitRadius` - Legacy linear density
  - `InstancesPerM2` - Instances per square meter
  - `ClustersPerM2` - Clusters per square meter
- **Area Unit Control** - Custom area base for density (e.g., per 2m² instead of 1m²)
- **Partial Removal** - Removal strength slider (0-100%) for gradual grass removal
- **Renderer Dropdown** - Quick-switch dropdown in Grass Painter to select target renderer
- **Cluster Count Display** - Shows estimated cluster count next to total grass instances

### Fixed
- **TopTint/BottomTint Colors** - Now correctly apply as vertex color when UseOnlyAlbedoColor is OFF
- **First Click Paint** - Grass now paints immediately on mouse down
- **Paint Overlap** - Drag strokes no longer overlap using brush diameter as minimum distance
- **GPU Crash Prevention** - Validation before painting prevents crashes from invalid settings
- **Multi-Renderer Visibility** - All GrassRenderers now remain visible simultaneously

### Technical
- Compute buffers rebound per-frame to support multiple renderers sharing same compute shader
- OnRendererChanged method ensures proper buffer state when switching targets
- Settings validation before painting operations


## [1.2.0] - 2026-01-28

### Added
- **Grass Decal System** - New `GrassDecal` component for projecting textures onto grass
  - Supports texture projection with alpha blending
  - Full rotation support via Transform component
  - Configurable size and blend strength
  - Auto-detection of GrassRenderer in scene
  - Scene view gizmos for easy positioning
- **Decal Shader Support** - Added decal projection to `GrassUnlit` shader

## [1.1.0] - 2026-01-21

### Added
- **Dual Display Modes for Performance Overlay** - Full mode (all stats) and Minimal mode (FPS only, lightweight for consoles)
- **Gamepad Support for Performance Overlay** - Toggle with Minus button, switch modes with Plus button (Switch compatible)
- **Gamepad Support for SimplePlayerController** - Full controller support including left/right sticks, triggers, and face buttons
- **Configurable Gamepad Settings** - Sensitivity, deadzone, and invert Y axis options

### Changed
- Removed `targetFPS` from Performance Overlay - Now measures native FPS without comparison to target
- Removed `Application.targetFrameRate` setting from overlay component
- Performance Overlay now shows dynamic control hints based on input device

### Fixed
- Minimal FPS overlay display size increased to properly show the FPS value

## [1.0.0] - 2026-01-20

### Added
- GPU Instanced Rendering - High-performance grass rendering using DrawMeshInstancedIndirect
- Dual Rendering Modes - Support for procedural triangular blades and custom meshes
- Compute Shader Culling - GPU-based frustum culling for optimal performance
- Wind Simulation - Realistic wind animation with customizable strength and frequency
- Player Interaction - Grass bends away from player/interactors in real-time
- LOD System - Distance-based level-of-detail for performance scaling
- Grass Painter Tool - Intuitive editor window for painting grass in Scene view
- ScriptableObject Settings - Modular grass configuration via SO_GrassSettings
- Performance Overlay - Built-in FPS and instance monitoring
- Lit and Unlit Shaders - Two shader variants for different visual needs
- Custom Inspector - Enhanced settings editor with visual feedback
- Memory Management - Proper buffer cleanup and leak prevention

### Technical Highlights
- Optimized for Nintendo Switch (30 FPS target with 100k+ instances)
- Uses structured buffers for efficient GPU data transfer
- Supports both procedural geometry and mesh instancing
- Implements async GPU readback for performance monitoring
