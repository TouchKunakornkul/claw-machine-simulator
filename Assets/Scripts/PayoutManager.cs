using UnityEngine;

namespace ClawMachine
{
    /// <summary>
    /// ระบบ payout cycle ตามตู้ kakuritsu-ki ญี่ปุ่น
    /// ทุกครั้งที่ N (payoutEveryN) จะเป็นรอบ "จ่าย" -> ขาใช้แรง C3 เต็ม
    /// รอบอื่นแรง C3 อ่อน -> ของหลุดตอนยก
    /// ดู docs/research-claw-mechanics.md หัวข้อ 4 (Gocha Settings)
    /// </summary>
    public class PayoutManager : MonoBehaviour
    {
        [Header("การตั้งค่าตู้ (ถ้า assign จะใช้ payoutEveryN จาก asset แทน)")]
        [SerializeField] private MachineSettings settings;

        [Header("อัตราจ่าย (fallback เมื่อไม่มี settings)")]
        [Tooltip("จ่ายรางวัล (แรงเต็ม) ทุกครั้งที่ N — เครื่องจริง 10–18")]
        [Min(1)]
        [SerializeField] private int payoutEveryN = 12;

        [Header("Debug")]
        [Tooltip("บังคับให้รอบถัดไปเป็นรอบจ่าย (สำหรับเทสต์)")]
        [SerializeField] private bool forceNextPayout = false;

        public int PlayCount { get; private set; }
        public int PayoutEveryN => settings != null ? settings.payoutEveryN : payoutEveryN;
        /// <summary>เหลืออีกกี่ครั้งถึงรอบจ่าย (1 = ครั้งถัดไปจ่าย)</summary>
        public int PlaysUntilPayout
        {
            get
            {
                if (forceNextPayout) return 1;
                int into = PlayCount % PayoutEveryN;
                return PayoutEveryN - into;
            }
        }

        /// <summary>
        /// นับการเล่น 1 ครั้ง แล้วบอกว่าเป็นรอบจ่ายหรือไม่ — เรียกตอนขาหุบ (C1)
        /// </summary>
        public bool RegisterPlayAndEvaluate()
        {
            PlayCount++;

            if (forceNextPayout)
            {
                forceNextPayout = false;
                return true;
            }

            return PlayCount % PayoutEveryN == 0;
        }

        /// <summary>เปิด/ปิดบังคับจ่ายรอบหน้า (ผูกกับปุ่ม debug ได้)</summary>
        public void ForceNextPayout() => forceNextPayout = true;
    }
}
