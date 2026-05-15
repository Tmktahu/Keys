using Unity.Entities;
using ProjectM.Network;
using Unity.Collections;
using Unity.Transforms;
using ProjectM;
using Il2CppInterop.Runtime;

namespace Keys.Services;

public static class EntityService
{
  static EntityManager EntityManager => Core.EntityManager;

  public static bool TryFindPlayer(string playerName, out Entity playerEntity, out Entity userEntity)
  {
    if (string.IsNullOrEmpty(playerName))
    {
      playerEntity = Entity.Null;
      userEntity = Entity.Null;
      return false;
    }

    playerEntity = Entity.Null;
    userEntity = Entity.Null;

    var userEntities = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<User>())
                       .ToEntityArray(Allocator.Temp);

    foreach (var entity in userEntities)
    {
      var userData = EntityManager.GetComponentData<User>(entity);
      if (userData.CharacterName.ToString().Equals(playerName, StringComparison.OrdinalIgnoreCase))
      {
        userEntity = entity;
        playerEntity = userData.LocalCharacter._Entity;
        return true;
      }
    }

    return false;
  }

  public static List<Entity> GetNearbyUserEntities(Entity playerEntity, float radius = 10f)
  {
    List<Entity> nearbyPlayers = new List<Entity>();
    if (!EntityManager.HasComponent<Translation>(playerEntity))
      return nearbyPlayers;

    var playerPos = EntityManager.GetComponentData<Translation>(playerEntity).Value;

    var playerCharactersQuery = QueryService.PlayerCharactersQuery;
    NativeArray<Entity> entities = playerCharactersQuery.ToEntityArray(Allocator.Temp);

    foreach (var entity in entities)
    {
      var playerCharacter = EntityManager.GetComponentData<PlayerCharacter>(entity);
      Entity userEntity = playerCharacter.UserEntity;
      if (EntityManager.HasComponent<Translation>(userEntity))
      {
        var userPos = EntityManager.GetComponentData<Translation>(userEntity).Value;
        float distance =
            (userPos.x - playerPos.x) * (userPos.x - playerPos.x) +
            (userPos.y - playerPos.y) * (userPos.y - playerPos.y) +
            (userPos.z - playerPos.z) * (userPos.z - playerPos.z);

        if (distance <= radius * radius)
        {
          nearbyPlayers.Add(userEntity);
        }
      }
    }

    return nearbyPlayers;
  }

  public static NativeArray<Entity> GetEntitiesByComponentType<T1>(bool includeAll = false, bool includeDisabled = false, bool includeSpawn = false, bool includePrefab = false, bool includeDestroyed = false)
  {
    EntityQueryOptions options = EntityQueryOptions.Default;
    if (includeAll) options |= EntityQueryOptions.IncludeAll;
    if (includeDisabled) options |= EntityQueryOptions.IncludeDisabled;
    if (includeSpawn) options |= EntityQueryOptions.IncludeSpawnTag;
    if (includePrefab) options |= EntityQueryOptions.IncludePrefab;
    if (includeDestroyed) options |= EntityQueryOptions.IncludeDestroyTag;

    var entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp)
      .AddAll(new(Il2CppType.Of<T1>(), ComponentType.AccessMode.ReadWrite))
      .WithOptions(options);

    var query = Core.EntityManager.CreateEntityQuery(ref entityQueryBuilder);

    var entities = query.ToEntityArray(Allocator.Temp);
    return entities;
  }
}
