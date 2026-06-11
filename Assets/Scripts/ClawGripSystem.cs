using UnityEngine;

namespace ClawMachine
{
    /// <summary>
    /// ระดับแรงคีบตาม 4 phase ของ UFO Catcher จริง (C1–C4)
    /// </summary>
    public enum GripPhase
    {
        Open,        // ขากาง ไม่จับ
        C1,          // หุบแรงเต็มตอนล่างสุด
        C3Normal,    // ยกขึ้น รอบปกติ — สปริงอ่อน ของดันขากางหลุดเอง
        C3Payout,    // ยกขึ้น รอบจ่าย — สปริงแข็ง ประคองของไว้ได้
        C4Transport  // เลื่อนกลับท่อ — แรงกลาง
    }

    /// <summary>
    /// ขา 2 ข้างแบบ physics ล้วน (HingeJoint + spring) — ไม่มีการ "ดูดติด" ใดๆ
    ///
    /// หลักการจริง (research + SEGA manual):
    /// - ปลายขาเป็น shovel แผ่นแบน หนีบไม่ได้ — ได้แค่ "ช้อน/รองใต้ของ"
    /// - ของถูกยกเพราะวางอยู่บนแผ่น shovel ด้วยแรงสัมผัส + สมดุลล้วนๆ
    /// - "ยกแล้วหลุด" ของจริง = สปริงอ่อน → น้ำหนักของดันขากางออกเอง → ของลื่นร่วง
    /// - แรงคีบ (POWER 00–99) = ความแข็งสปริง hinge ตอนหุบ ไม่ใช่แรงยึดวิเศษ
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ClawGripSystem : MonoBehaviour
    {
        [Header("การตั้งค่าตู้ (อิงตาม object — สร้างจากเมนู Claw Machine/Machine Settings)")]
        [Tooltip("ถ้า assign ไว้ จะ override มุมกาง/ความแข็งสปริงทั้งหมดจาก asset นี้")]
        [SerializeField] private MachineSettings settings;

        [Header("ขา 2 ข้าง (มี HingeJoint)")]
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;

        [Header("มุมขา (องศา hinge, มิเรอร์สองข้าง)")]
        [SerializeField] private float openAngle = 35f;
        [SerializeField] private float closedAngle = 8f;

        [Header("ความแข็งสปริง hinge")]
        [Tooltip("ตอนกาง — อ่อนพอให้เบนหลบของได้ตอนดิ่ง")]
        [SerializeField] private float springOpen = 5f;
        [Tooltip("ตอนหุบแรงเต็ม (C1/payout) — มาจาก settings ถ้า assign ไว้")]
        [SerializeField] private float springStrong = 35f;
        [Tooltip("ตอนยกรอบปกติ (kakuritsu C3) — อ่อนจนน้ำหนักของดันขากางได้")]
        [SerializeField] private float springWeak = 3f;
        [SerializeField] private float springDamper = 1.5f;

        [Header("ตรวจสถานะ")]
        [Tooltip("ระยะรอบ grabPoint ที่ถือว่า 'มีของอยู่ในง่าม' (ใช้แสดงผล/หยุดดิ่ง)")]
        [SerializeField] private float holdCheckRadius = 0.05f;
        [Tooltip("ขาโดนดันจนมุมเบี่ยงจากเป้าเกินนี้ = มีแรงต้านจริง (องศา)")]
        [SerializeField] private float resistanceAngle = 12f;
        [SerializeField] private Transform grabPoint;
        [SerializeField] private LayerMask prizeLayer = ~0;

        public GripPhase Phase { get; private set; } = GripPhase.Open;
        /// <summary>มีของอยู่ระหว่างง่ามไหม (ตรวจทาง physics ไม่มีการยึด)</summary>
        public bool IsHolding { get; private set; }

        private Rigidbody anchor;
        private HingeJoint leftHinge;
        private HingeJoint rightHinge;

        private void Awake()
        {
            anchor = GetComponent<Rigidbody>();
            anchor.isKinematic = true;
            anchor.useGravity = false;

            if (leftArm != null) leftHinge = leftArm.GetComponent<HingeJoint>();
            if (rightArm != null) rightHinge = rightArm.GetComponent<HingeJoint>();

            // ตู้จริง: ค่าทั้งหมดมาจากแผงปรับ (MachineSettings)
            if (settings != null)
            {
                openAngle = settings.openArmAngle;
                springStrong = Mathf.Max(8f, settings.FullGripForce);
                springWeak = Mathf.Max(0.5f, settings.LiftGripForce(isPayoutRound: false)
                                              * (settings.segaMode ? 1f : settings.normalGripRatio));
                // segaMode: แรงคงที่ทุกตา (springWeak = springStrong)
                if (settings.segaMode) springWeak = springStrong;
            }

            // กันขาซ้าย-ขวาชน/เกี่ยวกันเองตอนหุบ (manual 13-4: shovels must not overlap)
            if (leftArm != null && rightArm != null)
            {
                foreach (var lc in leftArm.GetComponentsInChildren<Collider>())
                    foreach (var rc in rightArm.GetComponentsInChildren<Collider>())
                        Physics.IgnoreCollision(lc, rc, true);
            }

            SetHinges(openAngle, springOpen);
        }

        private void FixedUpdate()
        {
            // สถานะ "มีของในง่าม" — เช็คทาง physics ล้วน ไว้โชว์ HUD
            IsHolding = Phase != GripPhase.Open && PrizeBetweenArms();
        }

        /// <summary>ขาโดนของดันจนเบี่ยงเกิน threshold = แรงต้านจริง (ใช้หยุดการดิ่ง)</summary>
        public bool ArmsResisted()
        {
            return HingeDeviation(leftHinge) > resistanceAngle
                || HingeDeviation(rightHinge) > resistanceAngle;
        }

        // ---------- API ที่ ClawController เรียก ----------

        public void OpenArms()
        {
            Phase = GripPhase.Open;
            SetHinges(openAngle, springOpen);
        }

        /// <summary>หุบขาด้วยสปริงแรงเต็ม — shovel ช้อนเข้าใต้ของตามฟิสิกส์จริง ไม่มีการยึด</summary>
        public void CloseArms(GripPhase phase)
        {
            Phase = phase;
            SetHinges(closedAngle, springStrong);
        }

        /// <summary>
        /// เปลี่ยนระดับแรง (C3/C4): ปรับแค่ความแข็งสปริงขณะขายังหุบ
        /// รอบอ่อน: น้ำหนักของจะดันขากางออกเอง → ของลื่นร่วง (ไม่มี joint ให้ขาด)
        /// </summary>
        public void SetGripPhase(GripPhase phase)
        {
            Phase = phase;
            float spring =
                phase == GripPhase.C3Normal ? springWeak :
                phase == GripPhase.C4Transport ? Mathf.Lerp(springWeak, springStrong, 0.7f) :
                springStrong;
            SetHinges(closedAngle, spring);
        }

        // ---------- ภายใน ----------

        private bool PrizeBetweenArms()
        {
            if (grabPoint == null) return false;
            return Physics.CheckSphere(
                grabPoint.position, holdCheckRadius, prizeLayer, QueryTriggerInteraction.Ignore);
        }

        private static float HingeDeviation(HingeJoint h)
        {
            if (h == null) return 0f;
            return Mathf.Abs(h.angle - h.spring.targetPosition);
        }

        private void SetHinges(float angleMagnitude, float springForce)
        {
            ApplyHinge(leftHinge, +angleMagnitude, springForce);
            ApplyHinge(rightHinge, -angleMagnitude, springForce);
        }

        private void ApplyHinge(HingeJoint hinge, float target, float springForce)
        {
            if (hinge == null) return;
            var s = hinge.spring;
            s.spring = springForce;
            s.damper = springDamper;
            s.targetPosition = target;
            hinge.spring = s;
            hinge.useSpring = true;
        }

        private void OnDrawGizmosSelected()
        {
            if (grabPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(grabPoint.position, holdCheckRadius);
            }
        }
    }
}
