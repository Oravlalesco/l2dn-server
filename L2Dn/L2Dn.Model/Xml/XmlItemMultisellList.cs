using System.Xml.Serialization;

namespace L2Dn.Model.Xml;

[XmlRoot("itemMultisells")]
public sealed class XmlItemMultisellList
{
    [XmlElement("route")]
    public List<XmlItemMultisellRoute> Routes { get; set; } = [];
}

public sealed class XmlItemMultisellRoute
{
    [XmlAttribute("itemId")]
    public int ItemId { get; set; }

    [XmlAttribute("listId")]
    public int ListId { get; set; }

    [XmlAttribute("enabled")]
    public bool Enabled { get; set; }
}
