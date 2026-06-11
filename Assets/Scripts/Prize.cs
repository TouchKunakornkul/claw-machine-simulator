using UnityEngine;

namespace ClawMachine
{
    /// <summary>
    /// Marker component สำหรับของรางวัล (figure) — ติดบน prefab ที่มี Rigidbody + Collider
    /// แนะนำตั้ง layer = "Prize", Collision Detection = Continuous, Convex Mesh Collider
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Prize : MonoBehaviour
    {
        [Header("ข้อมูลของรางวัล")]
        public string prizeId = "figure_default";
        public string displayName = "Figure";

        public Rigidbody Rigidbody { get; private set; }

        private Vector3 spawnPosition;
        private Quaternion spawnRotation;

        private void Awake()
        {
            Rigidbody = GetComponent<Rigidbody>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        /// <summary>วางกลับตำแหน่งตั้งต้น (ใช้ตอน respawn หลังคีบได้)</summary>
        public void ResetToSpawn()
        {
            Rigidbody.linearVelocity = Vector3.zero;
            Rigidbody.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        }
    }
}
