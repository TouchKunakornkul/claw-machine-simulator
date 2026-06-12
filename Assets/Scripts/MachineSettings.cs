using UnityEngine;

namespace ClawMachine
{
    /// <summary>
    /// การตั้งค่าตู้แบบ "อิงตาม object" — จำลองแผงปรับของ SEGA UFO Catcher จริง
    /// (อ้างอิง SegaUFOCatcher_Manual: Section 11 POWER knob, 13-3 spring, 13-4 shovel, 13-5 arm angle)
    ///
    /// หลักการ: ตัวเครื่อง/ขาเหมือนกันทุกตู้ ความต่างอยู่ที่ config นี้ทั้งหมด
    /// สร้างได้หลาย asset (ตู้ง่าย/ตู้โหด) แล้ว assign ให้ ClawGripSystem ผ่าน Inspector
    /// </summary>
    [CreateAssetMenu(fileName = "MachineSettings", menuName = "Claw Machine/Machine Settings")]
    public class MachineSettings : ScriptableObject
    {
        // ===== 11-1: POWER knob (00 อ่อนสุด – 99 แรงสุด) =====
        [Header("POWER (ปุ่มปรับละเอียด 00–99)")]
        [Range(0, 99)] public int power = 50;

        // ===== 13-3: ตำแหน่งสปริง 3 ระดับ (shift bracket) =====
        public enum SpringStage
        {
            NearSpring, // ใกล้สปริง = อ่อน (ยาก)
            Middle,     // กลาง
            FarSpring   // ไกลสปริง = แรง (ง่าย)
        }
        [Header("ตำแหน่งสปริง (ปรับหยาบ 3 ระดับ)")]
        public SpringStage springStage = SpringStage.Middle;

        // ===== 13-3: ขนาดขา S/M/L (เปลี่ยนขนาด = แรงหนีบเปลี่ยนแม้สปริงเท่าเดิม) =====
        public enum ArmSize { S, M, L }
        [Header("ขนาดขา (S เบา / L หนักแรงกว่า)")]
        public ArmSize armSize = ArmSize.M;

        // ===== 13-4: ปลายขา (shovel) W30/W40/W60 — เลือกตามชนิดของรางวัล =====
        public enum ShovelType { W30, W40, W60 }
        [Header("ปลายขา shovel (กว้างขึ้น = รับของกว้างขึ้น)")]
        public ShovelType shovel = ShovelType.W40;

        // ===== 13-4: สกรูปรับระยะห่าง shovel ตอนหุบ (overlap adjustment screw) =====
        // สเปคจริงจาก exploded view (26): ระยะปลายตอนหุบ = 1±1 mm (เกือบแตะ ห้ามทับ)
        [Header("ระยะห่างปลาย shovel ตอนหุบ (cm) — สเปคโรงงาน 0.1")]
        [Range(0f, 3f)] public float shovelGapCm = 0.1f;

        // ===== 13-5: มุมกางขา (เลื่อน sensor bracket ด้าน A = กว้าง / B = แคบ) =====
        // กางเต็มมาตรฐานเครื่องจริง: "ปลายเล็บกางถึงตำแหน่งข้อศอกตอนหุบ"
        // (cranegame-kotsu.com) = ~80° จากแนวดิ่งในเรขาคณิตขานี้ / ตู้บางตู้ตั้งแคบกว่า
        [Header("มุมกางขา (องศา)")]
        [Range(40f, 88f)] public float openArmAngle = 80f;

        // ===== ทิศการกางขาเทียบคาน (ไม่มีใน manual — กำหนดโดยการติดตั้ง/รุ่นเครื่อง) =====
        [Header("ทิศการกางขา (หมุนรอบแกนตั้ง) — 0 = กางขนานคาน")]
        [Range(0f, 90f)] public float clawYaw = 0f;

        // ===== ระยะเยื้องขา (ดีไซน์ hub จริง: ขาขนานกันแต่ไม่ตรงกัน) =====
        // manual 13-4 ให้เช็คว่า shovel ไม่ overlap ตอนหุบ = ขาถูกออกแบบให้สวนผ่านกันได้
        // ค่าลบ = สลับฝั่งการเยื้อง (ซ้าย-ขวากลับกัน)
        [Header("ระยะเยื้องขาซ้าย-ขวา (cm) — ขาขนานกันแต่เยื้องกันเหมือนเครื่องจริง")]
        [Range(-4f, 4f)] public float armOffsetCm = 1.6f;

        // ===== โหมดจ่ายรางวัล =====
        [Header("โหมดจ่ายรางวัล")]
        [Tooltip("SEGA แท้ = แรงคงที่ทุกตา (สู้ด้วยการจัดวาง) / Kakuritsu = อ่อนปกติ แรงเมื่อถึงรอบจ่าย")]
        public bool segaMode = true;
        [Tooltip("เฉพาะ kakuritsu: จ่ายทุก N ตา")]
        [Min(1)] public int payoutEveryN = 12;
        [Tooltip("เฉพาะ kakuritsu: สัดส่วนแรงรอบปกติเทียบแรงเต็ม")]
        [Range(0.05f, 1f)] public float normalGripRatio = 0.15f;

        // ===== ขอบเขตแรงกดปลายขา (N ต่อขา 1 ข้าง) =====
        // ที่มา (คำนวณจากขอบเขตจริง ไม่ใช่ค่าวัดตรง):
        // - แรงเต็มต้องถือของ ~1kg ในราง V ~42° ได้: 9.8N×tan42° ≈ 8.8N รวม → max 12N/ขา
        // - แรงอ่อนสุดต้องถือกล่อง figure 300g ไม่ได้ (ต้องการ ~1.3N/ขา) → min 0.3N
        [Header("ขอบเขตแรงกดปลายขา (N/ขา) — แปลงจาก POWER 00-99")]
        public float minGripForce = 0.3f;
        public float maxGripForce = 12f;

        // ---------- ค่าคำนวณ ----------

        /// <summary>ตัวคูณจากตำแหน่งสปริง (13-3)</summary>
        public float SpringStageMultiplier =>
            springStage == SpringStage.NearSpring ? 0.6f :
            springStage == SpringStage.FarSpring ? 1.4f : 1f;

        /// <summary>ตัวคูณจากขนาดขา (13-3: ขาใหญ่หนีบแรงกว่าที่สปริงเท่ากัน)</summary>
        public float ArmSizeMultiplier =>
            armSize == ArmSize.S ? 0.75f :
            armSize == ArmSize.L ? 1.3f : 1f;

        /// <summary>
        /// สเกลความยาวขาจริง (13-1: เปลี่ยนขา S/M/L = เปลี่ยนชิ้นส่วนจริง)
        /// อิง spec ขา pivot->ปลาย 12-18cm: S=13.2 / M=16.5 / L=19.8 cm
        /// </summary>
        public float ArmSizeScale =>
            armSize == ArmSize.S ? 0.8f :
            armSize == ArmSize.L ? 1.2f : 1f;

        /// <summary>ความกว้างปลายขาเป็นเมตร (W30=3cm, W40=4cm, W60=6cm)</summary>
        public float ShovelWidthMeters =>
            shovel == ShovelType.W30 ? 0.03f :
            shovel == ShovelType.W60 ? 0.06f : 0.04f;

        /// <summary>แรงหนีบเต็ม (C1) จาก POWER + สปริง + ขนาดขา</summary>
        public float FullGripForce
        {
            get
            {
                float byPower = Mathf.Lerp(minGripForce, maxGripForce, power / 99f);
                return byPower * SpringStageMultiplier * ArmSizeMultiplier;
            }
        }

        /// <summary>แรงตอนยก: SEGA = เท่าแรงเต็ม / kakuritsu = ลดตามอัตรา (ถ้าไม่ใช่รอบจ่าย)</summary>
        public float LiftGripForce(bool isPayoutRound)
        {
            if (segaMode || isPayoutRound) return FullGripForce;
            return FullGripForce * normalGripRatio;
        }
    }
}
