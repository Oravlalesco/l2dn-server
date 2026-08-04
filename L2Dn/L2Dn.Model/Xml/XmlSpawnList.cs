using System.Xml.Serialization;

namespace L2Dn.Model.Xml;

[XmlRoot("list")]
public class XmlSpawnList
{
    [XmlElement("spawn")]
    public List<XmlSpawn> Spawns { get; set; } = [];

    [XmlAttribute("dailyMissionAreas")]
    public string DailyMissionAreas { get; set; } = string.Empty;

    [XmlAttribute("excludedDailyMissionAreas")]
    public string ExcludedDailyMissionAreas { get; set; } = string.Empty;
}
