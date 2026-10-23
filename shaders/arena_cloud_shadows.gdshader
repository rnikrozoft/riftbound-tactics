shader_type canvas_item;
render_mode unshaded;

uniform vec2 viewport_size = vec2(1280.0, 720.0);
uniform vec2 reference_origin = vec2(0.0);
uniform vec2 reference_size = vec2(1280.0, 720.0);
uniform vec2 screen_to_field_x = vec2(1.0, 0.0);
uniform vec2 screen_to_field_y = vec2(0.0, 1.0);
uniform vec2 screen_to_field_origin = vec2(0.0);
uniform float elapsed_seconds = 0.0;
uniform float opacity : hint_range(0.0, 1.0) = 0.34;

float random_value(float seed) {
    return fract(sin(seed * 127.1 + 311.7) * 43758.5453);
}

void fragment() {
    vec2 screen = SCREEN_UV * viewport_size;
    vec2 field = screen_to_field_x * screen.x + screen_to_field_y * screen.y + screen_to_field_origin;
    vec2 p = (field - reference_origin) / reference_size.y;
    vec2 direction = normalize(vec2(1.0, -0.68));
    vec2 across = vec2(-direction.y, direction.x);
    vec2 midpoint = vec2(reference_size.x / reference_size.y * 0.5, 0.5);
    float shadow = 0.0;
    float edge = 3.0 / reference_size.y;
    for (int i = 0; i < 14; i++) {
        float n = float(i);
        float seed = n * 17.0 + 5.0;
        float sign_direction = mod(n, 2.0) < 0.5 ? 1.0 : -1.0;
        float phase = fract(random_value(seed) + elapsed_seconds * mix(0.0035, 0.007, random_value(seed + 1.0)) * sign_direction);
        float lane = mix(-1.05, 1.05, (n + 0.5) / 14.0);
        vec2 center = midpoint + direction * ((phase - 0.5) * 4.4) + across * lane;
        float size = mix(0.12, 0.28, random_value(seed + 2.0));
        float angle = mix(-0.65, 0.65, random_value(seed + 3.0));
        vec2 delta = p - center;
        vec2 q = vec2(cos(angle) * delta.x - sin(angle) * delta.y, sin(angle) * delta.x + cos(angle) * delta.y);
        q /= vec2(mix(1.15, 1.9, random_value(seed + 4.0)), mix(0.65, 1.0, random_value(seed + 5.0)));
        float theta = atan(q.y, q.x);
        // A different asymmetric, scalloped silhouette for every cloud bank.
        float radius = size * (1.0
            + 0.16 * sin(theta * 5.0 + seed)
            + 0.10 * sin(theta * 9.0 + seed * 1.7)
            + 0.055 * sin(theta * 15.0 + seed * 0.8));
        float cloud = 1.0 - smoothstep(-edge, edge, length(q) - radius);
        float fade = smoothstep(0.0, 0.09, phase) * (1.0 - smoothstep(0.91, 1.0, phase));
        float density = mix(0.75, 1.0, random_value(seed + 6.0));
        shadow = max(shadow, cloud * fade * density);
    }
    COLOR = vec4(0.0, 0.0, 0.0, shadow * opacity);
}
