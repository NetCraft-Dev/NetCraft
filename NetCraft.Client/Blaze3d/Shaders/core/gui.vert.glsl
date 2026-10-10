#version 450

//Globals 全局 uniform 块 set 0
layout(set = 0, binding = 0) uniform Globals {
    mat4 ScreenSize;
};

//Matrices 投影矩阵 uniform 块 set 1
layout(set = 1, binding = 0) uniform Matrices {
    mat4 Projection;
};

layout(location = 0) in vec3 Position;
layout(location = 1) in vec4 Color;
layout(location = 0) out vec4 fragColor;

void main() {
    gl_Position = Projection * vec4(Position.xy, 0.0, 1.0);
    fragColor = Color;
}
