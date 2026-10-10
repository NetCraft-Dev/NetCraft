#version 450

layout(set = 2, binding = 0) uniform sampler2D lightmapSampler;
layout(set = 3, binding = 0) uniform sampler2D atlasSampler;

layout(location = 0) in float fragDiffuse;
layout(location = 1) in vec2 fragUv;
layout(location = 2) in vec2 fragLightCoord;
layout(location = 3) in vec4 fragTint;

layout(location = 0) out vec4 outColor;

void main() {
    //四重调制 atlas 纹理 * tint 染色 * diffuse 方向光照 * lightmap 光等级
    vec4 texColor = texture(atlasSampler, fragUv);
    vec3 lightColor = texture(lightmapSampler, fragLightCoord).rgb;
    vec3 finalColor = texColor.rgb * fragTint.rgb * fragDiffuse * lightColor;
    outColor = vec4(finalColor, texColor.a * fragTint.a);
}
