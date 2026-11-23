// PS1-style surface shader. Used by every 3D mesh in the game.
//
//  - Vertex snapping: clip-space positions snap to the low-res pixel grid, giving the wobble.
//  - Affine texture mapping: UVs interpolate in screen space (no perspective correction),
//    giving the warping. Blend with affine_strength; keep it low on track and rails.
//  - Vertex lighting, no specular, nearest-filtered textures.
// The code lives in ps1_common.gdshaderinc, shared with ps1_sign.gdshader.
shader_type spatial;
render_mode vertex_lighting, specular_disabled, diffuse_lambert, cull_back;

#include "res://shaders/ps1_common.gdshaderinc"
