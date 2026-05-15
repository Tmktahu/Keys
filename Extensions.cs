using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using System.Runtime.InteropServices;

namespace Keys;

public static class VExtensions
{
  static EntityManager EntityManager => Core.EntityManager;
  const string PREFIX = "Entity(";
  const int LENGTH = 7;

  public static bool TryGetComponent<T>(this Entity entity, out T componentData) where T : struct
  {
    componentData = default;

    if (entity.Has<T>())
    {
      componentData = entity.Read<T>();
      return true;
    }

    return false;
  }

  public static bool Has<T>(this Entity entity) where T : struct
  {
    return EntityManager.HasComponent(entity, new(Il2CppType.Of<T>()));
  }
  public static T Read<T>(this Entity entity) where T : struct
  {
    return EntityManager.GetComponentData<T>(entity);
  }
  public static ulong GetSteamId(this Entity entity)
  {
    if (entity.TryGetComponent(out PlayerCharacter playerCharacter))
    {
      return playerCharacter.UserEntity.GetUser().PlatformId;
    }
    else if (entity.TryGetComponent(out User user))
    {
      return user.PlatformId;
    }

    return 0;
  }
  public static User GetUser(this Entity entity)
  {
    if (entity.TryGetComponent(out User user)) return user;
    else if (entity.TryGetComponent(out PlayerCharacter playerCharacter) && playerCharacter.UserEntity.TryGetComponent(out user)) return user;

    return User.Empty;
  }
  public static bool IndexWithinCapacity(this Entity entity)
  {
    string entityStr = entity.ToString();
    ReadOnlySpan<char> span = entityStr.AsSpan();

    if (!span.StartsWith(PREFIX)) return false;
    span = span[LENGTH..];

    int colon = span.IndexOf(':');
    if (colon <= 0) return false;

    ReadOnlySpan<char> tail = span[(colon + 1)..];

    int closeRel = tail.IndexOf(')');
    if (closeRel <= 0) return false;

    if (!int.TryParse(span[..colon], out int index)) return false;
    if (!int.TryParse(tail[..closeRel], out _)) return false;

    int capacity = EntityManager.EntityCapacity;
    bool isValid = (uint)index < (uint)capacity;

    return isValid;
  }
  public static PrefabGUID GetPrefabGuid(this Entity entity)
  {
    if (entity.TryGetComponent(out PrefabGUID prefabGuid)) return prefabGuid;

    return PrefabGUID.Empty;
  }
  public unsafe static void Write<T>(this Entity entity, T componentData) where T : struct
  {
    var ct = new ComponentType(Il2CppType.Of<T>());

    byte[] byteArray = StructureToByteArray(componentData);

    int size = Marshal.SizeOf<T>();

    fixed (byte* p = byteArray)
    {
      Core.EntityManager.SetComponentDataRaw(entity, ct.TypeIndex, p, size);
    }
  }
  public static byte[] StructureToByteArray<T>(T structure) where T : struct
  {
    int size = Marshal.SizeOf(structure);
    byte[] byteArray = new byte[size];
    IntPtr ptr = Marshal.AllocHGlobal(size);

    Marshal.StructureToPtr(structure, ptr, true);
    Marshal.Copy(ptr, byteArray, 0, size);
    Marshal.FreeHGlobal(ptr);

    return byteArray;
  }
}
