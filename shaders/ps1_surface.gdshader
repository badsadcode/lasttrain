// PS1-style surface shader. Used by every 3D mesh in the game.
//
//  - Vertex snapping: clip-space positions snap to the low-res pixel grid, giving the wobble.
//  - Affine texture mapping: UVs interpolate in screen space (no perspective correction),
//    giving the warping. Blend with affine_strength; keep it low on track and rails.
//  - Vertex lighting, no specular, nearest-filtered textures.
shader_type spatial;
render_mode vertex_lighting, specular_disabled, diffuse_lambert, cull_back;

// Set at runtime to the SubViewport size (see Ps1Pipeline.cs).
global uniform vec2 ps1_snap_resolution;

uniform vec4 albedo_color : source_color = vec4(1.0);
uniform sampler2D albedo_texture : source_color, filter_nearest, repeat_enable;
uniform bool use_texture = false;
uniform bool use_vertex_color = false;
uniform vec2 uv_scale = vec2(1.0);
// > 0: use world XZ as UVs (ground), so the texture doesn't slide as meshes are rebuilt.
uniform float world_uv_scale = 0.0;
uniform float snap_strength : hint_range(0.0, 1.0) = 1.0;
uniform float affine_strength : hint_range(0.0, 1.0) = 0.6;
uniform vec3 emission_color : source_color = vec3(0.0);
uniform float emission_energy = 0.0;

// (uv * w, w): perspective-correct interpolation of this, divided per fragment, yields affine UVs.
varying vec3 affine_uv;

void vertex() {
	vec2 uv = UV * uv_scale;
	if (world_uv_scale > 0.0) {
		vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
		uv = world.xz * world_uv_scale;
	}

	vec4 clip = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	if (snap_strength > 0.0 && clip.w > 0.0) {
		vec2 grid = ps1_snap_resolution * 0.5;
		vec2 ndc = clip.xy / clip.w;
		vec2 snapped = floor(ndc * grid + 0.5) / grid;
		clip.xy = mix(ndc, snapped, snap_strength) * clip.w;
	}
	POSITION = clip;

	affine_uv = vec3(uv * clip.w, clip.w);
	UV = uv;
}

void fragment() {
	vec2 affine = affine_uv.xy / affine_uv.z;
	vec2 uv = mix(UV, affine, affine_strength);
	vec4 col = albedo_color;
	if (use_texture) {
		col *= texture(albedo_texture, uv);
	}
	if (use_vertex_color) {
		col *= COLOR;
	}
	ALBEDO = col.rgb;
	EMISSION = emission_color * emission_energy;
}
