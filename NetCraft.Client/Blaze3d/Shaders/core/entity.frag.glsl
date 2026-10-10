#version 450

//entity fragment shader 实体模型片段着色
//set 1 SAMPLER0_SAMPLER1 atlas 纹理图集 + lightmap 光照贴图
//三重调制 atlas 纹理 * tint 染色 * lightmap 光等级与 terrain 一致
//ALPHA_CUTOUT define 时 discard alpha<0.5 用于镂空实体模型

layout(set = 1, binding = 0) uniform sampler2D atlasSampler;
layout(set = 1, binding = 1) uniform sampler2D lightmapSampler;

layout(location = 0) in vec2 fragUv;
layout(location = 1) in vec2 fragLightCoord;
layout(location = 2) in vec4 fragTint;

layout(location = 0) out vec4 outColor;

void main() {
    vec4 texColor = texture(atlasSampler, fragUv);
    vec3 lightColor = texture(lightmapSampler, fragLightCoord).rgb;
    vec3 finalColor = texColor.rgb * fragTint.rgb * lightColor;
    outColor = vec4(finalColor, texColor.a * fragTint.a);
#ifdef ALPHA_CUTOUT
    if (outColor.a < 0.5) discard;
#endif
}
