using UnityEngine;

namespace ClawMachine
{
    /// <summary>
    /// จัดการผิวคานคู่กลาง (pink tube / ปลอกใส / ไม่มีปลอก) แบบปรับสดได้
    /// อ้างอิง research (grabbit/90N): ปลอกยิ่งหนึบ ยิ่งจับยาก ร้านชดเชยด้วย POWER สูง
    ///
    /// ทำไมต้องมี: PhysicsMaterial ที่ builder สร้างเก็บค่า friction ฝังไว้ใน asset
    /// การหมุนแผง Tab จะไม่อัปเดตจนกว่าจะ rebuild — component นี้เขียนค่าใหม่ลง
    /// material ของ collider คานสดๆ เมื่อ ApplyFromSettings ถูกเรียก
    /// </summary>
    public class BarSurface : MonoBehaviour
    {
        [SerializeField] private MachineSettings settings;
        [Tooltip("collider คานคู่กลางที่สวมปลอก (เปลี่ยน friction ตามตัวเลือก)")]
        [SerializeField] private Collider[] coveredBars;
        [Tooltip("MeshRenderer คานคู่กลาง — เปลี่ยนสีตามปลอก")]
        [SerializeField] private Renderer[] coveredRenderers;

        private static readonly Color BareColor = new Color(0.75f, 0.75f, 0.78f);
        private static readonly Color ClearColor = new Color(0.80f, 0.90f, 0.95f);
        private static readonly Color PinkColor = new Color(0.95f, 0.45f, 0.65f);

        private void Start() => ApplyFromSettings();

        public void ApplyFromSettings()
        {
            if (settings == null) return;

            foreach (var col in coveredBars)
            {
                if (col == null) continue;
                var mat = col.sharedMaterial;
                if (mat == null) continue;
                mat.staticFriction = settings.BarStaticFriction;
                mat.dynamicFriction = settings.BarDynamicFriction;
            }

            Color c = settings.barCover == MachineSettings.BarCover.Bare ? BareColor
                    : settings.barCover == MachineSettings.BarCover.ClearTube ? ClearColor
                    : PinkColor;
            foreach (var r in coveredRenderers)
                if (r != null) r.sharedMaterial.color = c;
        }
    }
}
