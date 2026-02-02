# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
