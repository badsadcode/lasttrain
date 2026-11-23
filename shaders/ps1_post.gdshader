// Full-screen post-process over the upscaled low-res image:
// reduce colour depth with a 4x4 ordered (Bayer) dither, aligned to the low-res pixel grid.
shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, filter_nearest;
// Size of one low-res pixel in screen pixels (the SubViewportContainer stretch_shrink).
uniform float pixel_size = 4.0;
// Levels per channel. PS1 framebuffers were 15-bit: 32 levels.
uniform float color_levels = 32.0;
uniform float dither_strength = 1.0;
uniform vec3 tint = vec3(1.0);

const float BAYER[16] = float[16](
	0.0, 8.0, 2.0, 10.0,
	12.0, 4.0, 14.0, 6.0,
	3.0, 11.0, 1.0, 9.0,
	15.0, 7.0, 13.0, 5.0);

void fragment() {
	vec2 px = floor(FRAGCOORD.xy / pixel_size);
	// Sample the centre of the low-res pixel so the dither pattern stays on the low-res grid.
	vec2 uv = (px + 0.5) * pixel_size * SCREEN_PIXEL_SIZE;
	vec3 c = texture(screen_tex, uv).rgb * tint;

	int ix = int(mod(px.x, 4.0));
	int iy = int(mod(px.y, 4.0));
	float threshold = (BAYER[iy * 4 + ix] + 0.5) / 16.0 - 0.5;
	c += threshold * dither_strength / color_levels;
	c = floor(c * color_levels + 0.5) / color_levels;

	COLOR = vec4(c, 1.0);
}
