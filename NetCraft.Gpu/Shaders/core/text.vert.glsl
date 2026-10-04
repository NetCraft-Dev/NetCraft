#version 450

layout(set = 0, binding = 0) uniform Globals {
    mat4 ScreenSize;
};

layout(set = 1, binding = 0) uniform Matrices {
    mat4 Projection;
};

layout(location = 0) in vec3 Position;
layout(location = 1) in vec2 UV0;
layout(location = 2) in vec4 Color;
layout(location = 0) out vec4 fragColor;
layout(location = 1) out vec2 fragUv;

void main() {
    gl_Position = Projection * vec4(Position.xy, 0.0, 1.0);
    fragColor = Color;
    fragUv = UV0;
}
