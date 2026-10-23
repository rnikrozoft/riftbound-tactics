shader_type canvas_item;
render_mode unshaded;

uniform sampler2D card_mask : filter_nearest;
uniform vec4 mask_rect = vec4(0.0, 0.0, 1.0, 1.0);
uniform float strength : hint_range(0.0, 1.0) = 0.38;

void fragment() {
    vec4 sheen = texture(TEXTURE, UV);
    float card_alpha = texture(card_mask, mask_rect.xy + UV * mask_rect.zw).a;
    COLOR = vec4(sheen.rgb, sheen.a * card_alpha * strength);
}
