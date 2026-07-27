using L2Dn.GameServer.Data.Xml;
using L2Dn.GameServer.Enums;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Events;
using L2Dn.GameServer.Model.Events.Annotations;
using L2Dn.GameServer.Model.Events.Impl.Players;
using L2Dn.GameServer.Network.OutgoingPackets.ClassChange;
using L2Dn.Model.Enums;

namespace L2Dn.GameServer.Scripts.AI.Players;

public sealed class PlayerClassChange: AbstractScript
{
    private const int FirstClassMinLevel = 20;
    private const int SecondClassMinLevel = 40;
    private const int ThirdClassMinLevel = 76;

    [SubscribeEvent(SubscriptionType.GlobalPlayers)]
    public void OnPlayerLevelChanged(OnPlayerLevelChanged ev) => CheckRequirementAndNotify(ev.getPlayer());

    [SubscribeEvent(SubscriptionType.GlobalPlayers)]
    public void OnPlayerLogin(OnPlayerLogin ev) => CheckRequirementAndNotify(ev.getPlayer());

    private void CheckRequirementAndNotify(Player player)
    {
        if (player == null)
            return;

        CategoryData categories = CategoryData.getInstance();
        CharacterClass classId = player.getClassId();
        int level = player.getLevel();

        if ((level >= FirstClassMinLevel && categories.isInCategory(CategoryType.FIRST_CLASS_GROUP, classId)) ||
            (level >= SecondClassMinLevel && categories.isInCategory(CategoryType.SECOND_CLASS_GROUP, classId)) ||
            (level >= ThirdClassMinLevel && categories.isInCategory(CategoryType.THIRD_CLASS_GROUP, classId)))
        {
            player.sendPacket(ExClassChangeSetAlarmPacket.STATIC_PACKET);
        }
    }
}