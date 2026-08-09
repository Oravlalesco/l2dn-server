using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Runtime;

internal interface ILegacyNpcEntityResolver
{
    WorldObject? Resolve(Attackable observer, EntityKey key);
}
