namespace UnityEngine.XR.Templates.MR
{
    // Copied from XRI Examples. Commit: c40b958

    /// <summary>
    /// Apply forward force to instantiated prefab
    /// </summary>
    public class LaunchProjectile : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The projectile that's created")]
        GameObject m_ProjectilePrefab = null;

        [SerializeField]
        [Tooltip("The point that the project is created")]
        Transform m_StartPoint = null;

        [SerializeField]
        [Tooltip("The speed at which the projectile is launched")]
        float m_LaunchSpeed = 1.0f;

        public void Fire()
        {
            GameObject newObject = Instantiate(m_ProjectilePrefab, m_StartPoint.position, m_StartPoint.rotation, null);

            if (newObject.TryGetComponent(out Rigidbody rigidBody))
            {
                Vector3 force = m_StartPoint.forward * m_LaunchSpeed;
                rigidBody.AddForce(force);
            }
        }
    }
}
