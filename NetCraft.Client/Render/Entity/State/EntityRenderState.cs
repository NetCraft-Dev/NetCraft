using System.Numerics;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render;
using NetCraft.Client.Render.Model;
using NetCraft.Client.Render.Texture;
using NetCraft.Client.Render.Texture.Atlas;
using NetCraft.Client.Render.Item;
using NetCraft.Client.Render.Entity;
using NetCraft.Client.Render.Entity.State;
using NetCraft.Client.Render.State.Gui;
using NetCraft.Client.Gui;
using NetCraft.Client.Gui.Render;
using NetCraft.Client.Gui.Render.State;
using NetCraft.Client.Gui.Render.Pip;
using NetCraft.Client.Gui.Navigation;
using NetCraft.Client.Gui.Layouts;
using NetCraft.Client.Gui.Font;
using NetCraft.Client.Gui.Font.Providers;
using NetCraft.Client.Gui.Font.Glyphs;
using NetCraft.Client.Model;
using NetCraft.Client.Model.Geom;
using NetCraft.Client.Resources.Metadata.Gui;

namespace NetCraft.Client.Render.Entity.State;

//EntityRenderState entity render state, maps to vanilla EntityRenderState
//Holds the position/orientation/animation/lighting data needed for entity rendering, extracted from Entity by EntityRenderer
//W9.2 extends the animation inputs AgeInTicks/WalkAnimation and LightCoords real lighting
public sealed class EntityRenderState
{
    //Position entity world position
    public Vector3 Position { get; set; }
    //YRot entity Y-axis rotation in radians, the heading
    public float YRot { get; set; }
    //XRot entity X-axis rotation in radians, the pitch
    public float XRot { get; set; }
    //YBodyRot body heading in radians, separate from YRot so the head can turn independently of the body
    public float YBodyRot { get; set; }
    //AgeInTicks entity's tick count driving idle animation, maps to vanilla ageInTicks
    public float AgeInTicks { get; set; }
    //BobOffset bobbing phase, maps to vanilla ItemEntity's bobOffs derived from the entity id for stability
    public float BobOffset { get; set; }
    //WalkAnimationPos walk animation phase (limbSwing), accumulated from movement distance, maps to vanilla walkAnimationPos
    public float WalkAnimationPos { get; set; }
    //WalkAnimationSpeed walk animation speed (limbSwingAmount) 0-1 movement intensity, maps to vanilla walkAnimationSpeed
    public float WalkAnimationSpeed { get; set; }
    //LightCoords packed int light coordinates, filled by the caller from the light level at the entity's position
    //Defaults to full bright for callers that have not wired lighting
    public int LightCoords { get; set; } = LightTexture.FullBrightCoords;
    //Name entity name, for debugging
    public string Name { get; set; } = string.Empty;
    //CustomData renderer-specific data interpreted by the concrete EntityRenderer
    //The GPU layer does not know domain types; a dropped item's ItemStack is passed through here to ItemEntityRenderer
    public object? CustomData { get; set; }
}
