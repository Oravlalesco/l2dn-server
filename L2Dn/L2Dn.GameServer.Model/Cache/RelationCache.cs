using L2Dn.GameServer.Enums;

namespace L2Dn.GameServer.Cache;

/**
 * @author Sahar
 */
public class RelationCache
{
	private readonly long _relation;
	private readonly bool _isAutoAttackable;
	private readonly int _reputation;
	private readonly PvpFlagStatus _pvpFlag;
	
	public RelationCache(long relation, bool isAutoAttackable, int reputation, PvpFlagStatus pvpFlag)
	{
		_relation = relation;
		_isAutoAttackable = isAutoAttackable;
		_reputation = reputation;
		_pvpFlag = pvpFlag;
	}
	
	public long getRelation()
	{
		return _relation;
	}
	
	public bool isAutoAttackable()
	{
		return _isAutoAttackable;
	}

	public bool Matches(long relation, bool isAutoAttackable, int reputation, PvpFlagStatus pvpFlag) =>
		_relation == relation && _isAutoAttackable == isAutoAttackable && _reputation == reputation &&
		_pvpFlag == pvpFlag;
}
