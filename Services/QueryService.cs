using ProjectM;
using Unity.Entities;

namespace Keys.Services;

internal static class QueryService
{
    static EntityManager EntityManager => Core.EntityManager;

    static readonly EntityQuery _playerCharactersQuery;

    static QueryService()
    {
        _playerCharactersQuery = EntityManager.CreateEntityQuery(
            ComponentType.ReadOnly<PlayerCharacter>()
        );
    }

    public static EntityQuery PlayerCharactersQuery => _playerCharactersQuery;
}
