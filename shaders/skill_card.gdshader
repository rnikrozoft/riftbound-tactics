shader_type canvas_item;
uniform sampler2D skill_icon : filter_nearest;
uniform sampler2D rip_mask : filter_nearest;
uniform bool ripping = false;
void fragment(){
 vec4 card=texture(TEXTURE,UV);
 vec2 icon_uv=(UV-vec2(.15,.25))/vec2(.7,.46);
 if(all(greaterThanEqual(icon_uv,vec2(0.0)))&&all(lessThanEqual(icon_uv,vec2(1.0)))){vec4 icon=texture(skill_icon,icon_uv);card.rgb=mix(card.rgb,icon.rgb,icon.a);card.a=max(card.a,icon.a);}
 if(ripping)card.a*=texture(rip_mask,UV).a;
 COLOR=card;
}
