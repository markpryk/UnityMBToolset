using UnityEngine;

namespace WarbandParticles
{
    /// <summary>
    /// Utility class to convert between Warband and Unity coordinate systems
    /// Warband: Z+ is up
    /// Unity: Y+ is up
    /// </summary>
    public static class CoordinateConverter
    {
        /// <summary>
        /// Convert a position from Warband to Unity coordinate system
        /// </summary>
        public static Vector3 WarbandToUnity(Vector3 warbandPosition)
        {
            // Warband (X, Y, Z) → Unity (X, Z, Y)
            return new Vector3(
                warbandPosition.x,
                warbandPosition.z, // Warband Z (up) → Unity Y (up)
                warbandPosition.y  // Warband Y → Unity Z
            );
        }

        /// <summary>
        /// Convert a position from Unity to Warband coordinate system
        /// </summary>
        public static Vector3 UnityToWarband(Vector3 unityPosition)
        {
            // Unity (X, Y, Z) → Warband (X, Z, Y)
            return new Vector3(
                unityPosition.x,
                unityPosition.z,  // Unity Z → Warband Y
                unityPosition.y   // Unity Y (up) → Warband Z (up)
            );
        }

        /// <summary>
        /// Convert a direction from Warband to Unity coordinate system
        /// </summary>
        public static Vector3 WarbandToUnityDirection(Vector3 warbandDirection)
        {
            // Same as position conversion
            return WarbandToUnity(warbandDirection);
        }

        /// <summary>
        /// Convert a direction from Unity to Warband coordinate system
        /// </summary>
        public static Vector3 UnityToWarbandDirection(Vector3 unityDirection)
        {
            // Same as position conversion
            return UnityToWarband(unityDirection);
        }

        /// <summary>
        /// Get the correct gravity vector in Unity coordinates
        /// </summary>
        public static Vector3 GetUnityGravityVector(float strength)
        {
            // In Warband, gravity is -Z
            // In Unity, gravity is -Y
            return new Vector3(0, -strength, 0);
        }

        /// <summary>
        /// Convert rotation from Warband to Unity
        /// </summary>
        public static Quaternion WarbandToUnityRotation(Quaternion warbandRotation)
        {
            // This is a simplified conversion - for complex rotations you might need Euler angles
            Vector3 eulerAngles = warbandRotation.eulerAngles;
            return Quaternion.Euler(eulerAngles.x, eulerAngles.z, eulerAngles.y);
        }

        /// <summary>
        /// Create a quaternion that looks from origin to target in Unity coordinates
        /// </summary>
        public static Quaternion LookAtUnity(Vector3 direction, Vector3 up)
        {
            // Handle the case where direction is zero
            if (direction.sqrMagnitude < 0.001f)
                return Quaternion.identity;

            return Quaternion.LookRotation(direction, up);
        }
    }
}