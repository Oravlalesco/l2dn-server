using L2Dn.Packages.DatDefinitions.Annotations;
using L2Dn.Packages.DatDefinitions.Definitions.Shared;

namespace L2Dn.Packages.DatDefinitions.Definitions;

[ChronicleRange(Chronicles.Shinemaker, Chronicles.Latest)]
public sealed class PurchaseLimitCraftCategory
{
    [ArrayLengthType(ArrayLengthType.CompactInt)]
    public PurchaseLimitCraftCategoryRecord[] Records { get; set; } = [];

    public sealed class PurchaseLimitCraftCategoryRecord
    {
        public byte CategoryId { get; set; }
        public uint StringId { get; set; }
        public RgbaColor StringColor { get; set; } = new();

        [ArrayLengthType(ArrayLengthType.CompactInt)]
        public uint[] RibbonTextures { get; set; } = [];

        public byte Optional { get; set; }
    }
}
