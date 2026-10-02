shader_type canvas_item;

uniform vec4 outline_color : source_color = vec4(1.0);
uniform float alpha_threshold : hint_range(0.0, 1.0) = 0.5;

void fragment() {
	vec4 tint = COLOR;
	vec4 sprite = texture(TEXTURE, UV);
	vec2 pixel = TEXTURE_PIXEL_SIZE;
	float neighbor_alpha = 0.0;
	for (int y = -1; y <= 1; y++) {
		for (int x = -1; x <= 1; x++) {
			neighbor_alpha = max(neighbor_alpha,
				texture(TEXTURE, UV + vec2(float(x), float(y)) * pixel).a);
		}
	}
	// Ignore translucent ground shadows when finding the character silhouette.
	if (sprite.a < alpha_threshold && neighbor_alpha >= alpha_threshold) {
		COLOR = outline_color;
	} else {
		COLOR = tint;
	}
}
