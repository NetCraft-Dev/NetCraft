#version 450

//BLUR 高斯模糊单 pass 一个方向 BlurDir 控制水平或垂直
//set 0 IN_SAMPLER 输入纹理 set 1 BlurConfig uniform 不依赖 GLOBALS
layout(set = 0, binding = 0) uniform sampler2D InSampler;
layout(set = 1, binding = 0) uniform BlurConfig {
    vec4 Data;
};

layout(location = 0) in vec2 fragUv;
layout(location = 0) out vec4 outColor;

void main() {
    vec2 texSize = textureSize(InSampler, 0);
    vec2 texel = 1.0 / texSize;
    //Data.xy BlurDir 方向 Data.z Radius 控制采样步长
    vec2 step = texel * Data.xy * Data.z;
    //5-tap 高斯权重对称 0.06136 0.24477 0.38774 0.24477 0.06136 和=1
    vec4 sum = vec4(0.0);
    sum += texture(InSampler, fragUv + step * -2.0) * 0.06136;
    sum += texture(InSampler, fragUv + step * -1.0) * 0.24477;
    sum += texture(InSampler, fragUv) * 0.38774;
    sum += texture(InSampler, fragUv + step * 1.0) * 0.24477;
    sum += texture(InSampler, fragUv + step * 2.0) * 0.06136;
    outColor = sum;
}
