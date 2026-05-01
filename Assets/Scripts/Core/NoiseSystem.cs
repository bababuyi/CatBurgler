using UnityEngine;

/// <summary>
/// Defines the type of noise emitted, allowing the dog to react differently.
/// Footstep = quiet walking, Sprint = loud running, Land = jump landing, ObjectKnocked = environmental.
/// </summary>
public enum NoiseType { Footstep, Sprint, Land, ObjectKnocked }

/// <summary>
/// Static event bus for the noise system.
/// Call NoiseSystem.Emit() from anywhere to broadcast a noise event.
/// DogAI subscribes to OnNoiseEmitted to detect sound.
///
/// Usage: NoiseSystem.Emit(transform.position, 8f, NoiseType.Sprint);
/// </summary>
public static class NoiseSystem
{
    /// <summary>Fired when any noise is emitted. Parameters: (worldPosition, radius, type)</summary>
    public static event System.Action<Vector3, float, NoiseType> OnNoiseEmitted;

    /// <summary>Emits a noise at a world position that any listener within radius can hear.</summary>
    public static void Emit(Vector3 position, float radius, NoiseType type)
    {
        OnNoiseEmitted?.Invoke(position, radius, type);
    }
}
