using Silk.NET.Vulkan;
using VMASharp;

namespace dat_sharp_engine.Rendering.Vulkan;

/// <summary>
/// A class describing an image allocated on the GPU
/// </summary>
/// <param name="image">The image on the GPU</param>
/// <param name="imageView">The ImageView representing the image</param>
/// <param name="allocation">The allocation of the image</param>
/// <param name="imageExtent">The extent of the image</param>
/// <param name="format">The format of the image</param>
public class AllocatedImage(
    Image image,
    ImageView imageView,
    Allocation allocation,
    Extent3D imageExtent,
    Format format) {
    public Image image = image;
    public ImageView imageView = imageView;
    public Allocation allocation = allocation;
    public Extent3D imageExtent = imageExtent;
    public Format format = format;
}