// PS1 surface for lineside signs: identical to ps1_surface but unaffected by fog,
// like reflective paint catching the headlamp. Use sparingly, only for things the driver must read.
shader_type spatial;
render_mode vertex_lighting, specular_disabled, diffuse_lambert, cull_back, fog_disabled;

#include "res://shaders/ps1_common.gdshaderinc"
