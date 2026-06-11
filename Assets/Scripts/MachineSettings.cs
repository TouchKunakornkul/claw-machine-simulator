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
        [Header("ระยะห่างปลาย shovel ตอนหุบ (cm) — manual: ต้องไม่ overlap")]
        [Range(0f, 3f)] public float shovelGapCm = 0.5f;

        // ===== 13-5: มุมกางขา (เลื่อน sensor bracket ด้าน A = กว้าง / B = แคบ) =====
        [Header("มุมกางขา (องศา)")]
        [Range(20f, 60f)] public float openArmAngle = 50f;

        // ===== โหมดจ่ายรางวัล =====
        [Header("โหมดจ่ายรางวัล")]
        [Tooltip("SEGA แท้ = แรงคงที่ทุกตา (สู้ด้วยการจัดวาง) / Kakuritsu = อ่อนปกติ แรงเมื่อถึงรอบจ่าย")]
        public bool segaMode = true;
        [Tooltip("เฉพาะ kakuritsu: จ่ายทุก N ตา")]
        [Min(1)] public int payoutEveryN = 12;
        [Tooltip("เฉพาะ kakuritsu: สัดส่วนแรงรอบปกติเทียบแรงเต็ม")]
        [Range(0.05f, 1f)] public float normalGripRatio = 0.15f;

        // ===== ขอบเขตแรง (จูนระดับโปรเจกต์ ไม่ใช่ระดับตู้) =====
        [Header("ขอบเขตแรง (newtons) — แปลงจาก POWER")]
        public float minGripForce = 1f;
        public float maxGripForce = 60f;

        // ---------- ค่าคำนวณ ----------

        /// <summary>ตัวคูณจากตำแหน่งสปริง (13-3)</summary>
        public float SpringStageMultiplier =>
            springStage == SpringStage.NearSpring ? 0.6f :
            springStage == SpringStage.FarSpring ? 1.4f : 1f;

        /// <summary>ตัวคูณจากขนาดขา (13-3: ขาใหญ่หนีบแรงกว่าที่สปริงเท่ากัน)</summary>
        public float ArmSizeMultiplier =>
            armSize == ArmSize.S ? 0.75f :
            armSize == ArmSize.L ? 1.3f : 1f;

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
