#version 450

//terrain vertex shader 世界渲染区块 mesh 顶点变换
//set 0 MATRICES_PROJECTION 含 ViewProj mat4 CPU 端 bake section offset 进顶点位置 shader 端 Model=Identity
//face shading 已 bake 进顶点 color 不需要 Lighting UBO 方向光照

layout(set = 0, binding = 0) uniform MatricesUbo {
    mat4 ViewProj;
} matrices;

layout(location = 0) in vec3 inPosition;
layout(location = 1) in float inColor;
layout(location = 2) in vec2 inUv;
layout(location = 3) in float inLight;
layout(location = 4) in vec3 inNormal;

layout(location = 0) out vec2 fragUv;
layout(location = 1) out vec2 fragLightCoord;
layout(location = 2) out vec4 fragTint;

void main() {
    gl_Position = matrices.ViewProj * vec4(inPosition, 1.0);
    fragUv = inUv;
    //解包 packed light 坐标 (block<<4)|(sky<<20) 转 [0,1] 采样 lightmap
    int lightInt = int(inLight);
    int blockLight = (lightInt >> 4) & 0xF;
    int skyLight = (lightInt >> 20) & 0xF;
    fragLightCoord = (vec2(float(blockLight), float(skyLight)) + 0.5) / 16.0;
    //解包 packed ARGB color int 到 vec4 RGBA face shading 已 bake 进 RGB
    int colorInt = int(inColor);
    float r = float((colorInt >> 16) & 0xFF) / 255.0;
    float g = float((colorInt >> 8) & 0xFF) / 255.0;
    float b = float(colorInt & 0xFF) / 255.0;
    float a = float((colorInt >> 24) & 0xFF) / 255.0;
    fragTint = vec4(r, g, b, a);
}
