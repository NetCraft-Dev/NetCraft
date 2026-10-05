# NetCraft.Gpu

The rendering subsystem. It maps to the original client rendering stack — the `RenderSystem` device abstraction, the `net.minecraft.client.renderer` pipeline model and the `net.minecraft.client.gui` layer — implemented on Vulkan through Silk.NET, with an empty backend for headless environments.

## Features

- Abstracts the GPU: context, device, buffers, images, shaders, descriptor sets, samplers, command encoders and render passes, with a Vulkan backend and a no-op backend.
- Declares render pipelines fluently and composes them from reusable snippets, compiling embedded GLSL to SPIR-V with injected defines. Pipelines are described as data and compiled on demand by the device, with a cache keyed by the description so equivalent pipelines are not rebuilt; shader defines participate in both the compile cache key and the pipeline description.
- Stitches sprite sets into block, GUI-item and item atlases and exposes sprite and UV lookup.
- Builds the GUI render state: stratum and level ordering, element and glyph batching, snapshots handed to the render thread, and picture-in-picture passes for oversized items. Submission is two-phase — the tick side writes into the shared render state, which is snapshotted, and the render thread only reads the snapshot.
- Renders text: glyph providers for bitmap, TTF, unihex and space fonts, glyph stitching and baked glyphs.
- Provides GUI layout primitives (linear, grid, frame, equal spacing) and the window and control framework.
- Exposes world-rendering hooks and the vertex, buffer, pose, projection and lighting helpers shared by 3D, entity and item rendering. Gameplay layers implement the `IWorldRenderer` interface and feed in assets, so this module has no project reference to any other NetCraft project.
