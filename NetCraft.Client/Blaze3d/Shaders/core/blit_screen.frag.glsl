#version 450

//BLIT 输入纹理 set 1 IN_SAMPLER
layout(set = 1, binding = 0) uniform sampler2D InSampler;
layout(location = 0) in vec2 fragUv;
layout(location = 0) out vec4 outColor;

void main() {
    outColor = texture(InSampler, fragUv);
}
