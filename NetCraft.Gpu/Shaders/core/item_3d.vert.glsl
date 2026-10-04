#version 450

layout(set = 0, binding = 0) uniform MvpUbo {
    mat4 Model;
    mat4 View;
    mat4 Proj;
} mvp;

layout(set = 1, binding = 0) uniform LightingUbo {
    vec4 Light0;
    vec4 Light1;
} lighting;

layout(location = 0) in vec3 inPosition;
layout(location = 1) in float inColor;
layout(location = 2) in vec2 inUv;
layout(location = 3) in float inLight;
layout(location = 4) in vec3 inNormal;

layout(location = 0) out float fragDiffuse;
layout(location = 1) out vec2 fragUv;
layout(location = 2) out vec2 fragLightCoord;
layout(location = 3) out vec4 fragTint;

void main() {
    gl_Position = mvp.Proj * mvp.View * mvp.Model * vec4(inPosition, 1.0);
    vec3 n = normalize(inNormal);
    float d0 = max(dot(n, lighting.Light0.xyz), 0.0);
    float d1 = max(dot(n, lighting.Light1.xyz), 0.0);
    fragDiffuse = clamp(d0 + d1, 0.0, 1.0);
    fragUv = inUv;
    //解包 packed light 坐标 (block<<4)|(sky<<20) 转 [0,1] 采样 lightmap
    int lightInt = int(inLight);
    int blockLight = (lightInt >> 4) & 0xF;
    int skyLight = (lightInt >> 20) & 0xF;
    fragLightCoord = (vec2(float(blockLight), float(skyLight)) + 0.5) / 16.0;
    //解包 packed ARGB color int 到 vec4 RGBA 对标原版 vertex color
    int colorInt = int(inColor);
    float r = float((colorInt >> 16) & 0xFF) / 255.0;
    float g = float((colorInt >> 8) & 0xFF) / 255.0;
    float b = float(colorInt & 0xFF) / 255.0;
    float a = float((colorInt >> 24) & 0xFF) / 255.0;
    fragTint = vec4(r, g, b, a);
}
