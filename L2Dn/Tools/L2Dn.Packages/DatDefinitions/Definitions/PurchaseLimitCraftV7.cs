using L2Dn.Packages.DatDefinitions.Annotations;
using L2Dn.Packages.DatDefinitions.Definitions.Enums;

namespace L2Dn.Packages.DatDefinitions.Definitions;

[ChronicleRange(Chronicles.Shinemaker, Chronicles.Shinemaker)]
public sealed class PurchaseLimitCraftV7
{
    [ArrayLengthType(ArrayLengthType.Int32)]
    public PurchaseLimitCraftRecord[] Records { get; set; } = Array.Empty<PurchaseLimitCraftRecord>();

    public sealed class PurchaseLimitCraftRecord
    {
        public byte ShopIndex { get; set; }
        public ushort ProductId { get; set; }
        public byte Category { get; set; }
        public uint CategorySub { get; set; }
        public LCoinShopProductMarkType MarkType { get; set; }
        public byte MaxBuyCount { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public uint ProductItem { get; set; }
        public uint ProductEnchant { get; set; }

        [ArrayLengthType(ArrayLengthType.Byte)]
        public PurchaseLimitCraftBuyItem[] BuyItems { get; set; } = Array.Empty<PurchaseLimitCraftBuyItem>();

        public short LevelMin { get; set; }
        public short LevelMax { get; set; }
        public byte LimitType { get; set; }
        public LCoinResetType ResetType { get; set; }
        public uint LimitServerBuyCountMax { get; set; }

        [ArrayLengthType(ArrayLengthType.Byte)]
        public uint[] RequirementBuySkills { get; set; } = Array.Empty<uint>();

        [ArrayLengthType(ArrayLengthType.Byte)]
        public PurchaseLimitCraftKeepOptionFee[] KeepOptionFees { get; set; } =
            Array.Empty<PurchaseLimitCraftKeepOptionFee>();

        public byte KeepOption { get; set; }
        public byte AutomaticType { get; set; }
    }

    public sealed class PurchaseLimitCraftBuyItem
    {
        public uint ItemClassId { get; set; }
        public uint Count { get; set; }
        public float Probability { get; set; }
        public uint Enchant { get; set; }
        public uint ProductRank { get; set; }
        public byte IsLimitServer { get; set; }
    }

    public sealed class PurchaseLimitCraftKeepOptionFee
    {
        public uint ItemClassId { get; set; }
        public uint Count { get; set; }
        public float Probability { get; set; }
        public uint Enchant { get; set; }
        public uint ProductRank { get; set; }
        public byte IsLimitServer { get; set; }
    }
}
