using FluentAssertions;
using L2Dn.GameServer.Model.Actor;

namespace L2Dn.GameServer.Model.Tests;

public class WorldTests
{
    private static int _nextObjectId = 1_900_000_000;

    [Fact]
    public void AddObject_is_idempotent_for_the_same_instance()
    {
        World world = World.getInstance();
        TestWorldObject worldObject = new(GetNextObjectId());

        try
        {
            world.addObject(worldObject).Should().BeTrue();
            world.addObject(worldObject).Should().BeTrue();
            world.findObject(worldObject.ObjectId).Should().BeSameAs(worldObject);
        }
        finally
        {
            world.removeObject(worldObject);
        }
    }

    [Fact]
    public void AddObject_rejects_a_different_instance_with_the_same_id()
    {
        World world = World.getInstance();
        int objectId = GetNextObjectId();
        TestWorldObject registeredObject = new(objectId);
        TestWorldObject conflictingObject = new(objectId);

        try
        {
            world.addObject(registeredObject).Should().BeTrue();
            world.addObject(conflictingObject).Should().BeFalse();
            world.findObject(objectId).Should().BeSameAs(registeredObject);
        }
        finally
        {
            world.removeObject(registeredObject);
        }
    }

    [Fact]
    public void RemoveObject_does_not_remove_a_different_instance_with_the_same_id()
    {
        World world = World.getInstance();
        int objectId = GetNextObjectId();
        TestWorldObject registeredObject = new(objectId);
        TestWorldObject staleObject = new(objectId);

        try
        {
            world.addObject(registeredObject).Should().BeTrue();
            world.removeObject(staleObject).Should().BeFalse();
            world.findObject(objectId).Should().BeSameAs(registeredObject);
            world.removeObject(registeredObject).Should().BeTrue();
            world.findObject(objectId).Should().BeNull();
        }
        finally
        {
            world.removeObject(registeredObject);
        }
    }

    private static int GetNextObjectId() => Interlocked.Increment(ref _nextObjectId);

    private sealed class TestWorldObject(int objectId): WorldObject(objectId)
    {
        public override int getId() => 0;

        public override bool isAutoAttackable(Creature attacker) => false;

        public override void sendInfo(Player player)
        {
        }
    }
}
