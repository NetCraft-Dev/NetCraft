using System.Numerics;
using RenderPipeline = NetCraft.Gpu.Pipeline.RenderPipeline;

namespace NetCraft.Gpu;

//TiledBlitRenderState tiled texture blit render state, maps to vanilla TiledBlitRenderState
//Repeatedly tiles the texture by tileWidth/tileHeight to fill the (x0,y0)-(x1,y1) area
//At the edges, when less than one cell remains, the uv is cropped proportionally with lerp to avoid stretching
//Immutable record participating in GuiRenderState sorting and batching
public sealed record TiledBlitRenderState(
    RenderPipeline Pipeline,
    TextureSetup TextureSetup,
    Matrix3x2 Pose,
    int TileWidth, int TileHeight,
    int X0, int Y0, int X1, int Y1,
    float U0, float U1, float V0, float V1,
    int Color,
    ScreenRectangle ScissorArea,
    ScreenRectangle Bounds) : GuiElementRenderState
{
    //The constructor overload without bounds derives it automatically from the geometry+pose+scissor
    public TiledBlitRenderState(
        RenderPipeline pipeline,
        TextureSetup textureSetup,
        Matrix3x2 pose,
        int tileWidth, int tileHeight,
        int x0, int y0, int x1, int y1,
        float u0, float u1, float v0, float v1,
        int color,
        ScreenRectangle scissorArea)
        : this(pipeline, textureSetup, pose, tileWidth, tileHeight, x0, y0, x1, y1, u0, u1, v0, v1, color, scissorArea,
            GetBounds(x0, y0, x1, y1, pose, scissorArea))
    {
    }

    //BuildVertices double-loops over the tiles, writing a 4-vertex quad per tile
    public void BuildVertices(IVertexConsumer consumer)
    {
        int width = X1 - X0;
        int height = Y1 - Y0;
        for (int tileX = 0; tileX < width; tileX += TileWidth)
        {
            int remainingWidth = width - tileX;
            int curTileWidth;
            float curU1;
            if (TileWidth <= remainingWidth)
            {
                curTileWidth = TileWidth;
                curU1 = U1;
            }
            else
            {
                curTileWidth = remainingWidth;
                curU1 = Lerp((float)remainingWidth / TileWidth, U0, U1);
            }

            for (int tileY = 0; tileY < height; tileY += TileHeight)
            {
                int remainingHeight = height - tileY;
                int curTileHeight;
                float curV1;
                if (TileHeight <= remainingHeight)
                {
                    curTileHeight = TileHeight;
                    curV1 = V1;
                }
                else
                {
                    curTileHeight = remainingHeight;
                    curV1 = Lerp((float)remainingHeight / TileHeight, V0, V1);
                }

                int x0 = X0 + tileX;
                int x1 = X0 + tileX + curTileWidth;
                int y0 = Y0 + tileY;
                int y1 = Y0 + tileY + curTileHeight;
                consumer.AddVertexWith2DPose(Pose, x0, y0, U0, V0, Color);
                consumer.AddVertexWith2DPose(Pose, x0, y1, U0, curV1, Color);
                consumer.AddVertexWith2DPose(Pose, x1, y1, curU1, curV1, Color);
                consumer.AddVertexWith2DPose(Pose, x1, y0, curU1, V0, Color);
            }
        }
    }

    private static ScreenRectangle GetBounds(int x0, int y0, int x1, int y1, Matrix3x2 pose, ScreenRectangle scissorArea)
    {
        var raw = new ScreenRectangle(x0, y0, x1 - x0, y1 - y0).TransformMaxBounds(pose);
        var intersection = scissorArea.Intersect(raw);
        return intersection ?? raw;
    }

    private static float Lerp(float t, float a, float b) => a + (b - a) * t;
}
