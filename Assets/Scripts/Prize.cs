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

        private void Awake()
        {
            Rigidbody = GetComponent<Rigidbody>();
        }
    }
}
