#version 450

layout(set = 2, binding = 0) uniform sampler2D Sampler0;
layout(location = 0) in vec4 fragColor;
layout(location = 1) in vec2 fragUv;
layout(location = 0) out vec4 outColor;

void main() {
    vec4 tex = texture(Sampler0, fragUv);
    float alpha;
#ifdef IS_GRAYSCALE
    //灰度字体取 rgb 亮度作为覆盖度
    alpha = dot(tex.rgb, vec3(0.299, 0.587, 0.114));
#else
    //普通字体图集 R8 单通道 r 是覆盖度
    alpha = tex.r;
#endif
    outColor = vec4(fragColor.rgb, fragColor.a * alpha);
}
