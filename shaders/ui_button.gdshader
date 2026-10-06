shader_type canvas_item;
render_mode unshaded;
uniform vec4 fill_color : source_color = vec4(0.13,0.19,0.25,1.0);
uniform vec4 edge_color : source_color = vec4(0.3,0.4,0.48,1.0);
uniform float hover_amount = 0.0;
varying vec4 button_modulation;
void vertex() {
    button_modulation = COLOR;
}
void fragment() {
    vec4 skin = texture(TEXTURE,UV);
    float lightness = dot(skin.rgb,vec3(0.299,0.587,0.114));
    vec3 surface = mix(edge_color.rgb,fill_color.rgb,smoothstep(0.47,0.65,lightness));
    COLOR = vec4(mix(surface,surface + vec3(0.12),hover_amount),skin.a) * button_modulation;
}
