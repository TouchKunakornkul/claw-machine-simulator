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
        C3Normal,    // ยกขึ้น รอบปกติ — แรงอ่อน ของหลุด
        C3Payout,    // ยกขึ้น รอบจ่าย — แรงเต็ม คีบติด
        C4Transport  // เลื่อนกลับท่อ — แรงกลาง
    }

    /// <summary>
    /// ควบคุมขา 2 ข้างแบบ physics จริง (HingeJoint + spring) ตามกลไก UFO Catcher
    ///
    /// - ขาแต่ละข้าง = Rigidbody ห้อยจากหัวคีบด้วย HingeJoint (แกน Z) — แกว่งเบนได้
    /// - ตอนกาง: spring ดันขาออกไปมุม openAngle แต่ "เบนหลบ" ได้เมื่อโดนของ (ดิ่งลงผ่านขอบของได้)
    /// - ตอนหุบ: spring ขับขาเข้า closedAngle (แรง springClose) เพื่อหนีบ
    /// - การยึดของ: FixedJoint (breakForce = แรงคีบ phase) — รอบปกติแรง < น้ำหนัก joint ขาด ของหลุด
    /// ดู docs/research-claw-mechanics.md หัวข้อ 8 (HingeJoint approach)
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ClawGripSystem : MonoBehaviour
    {
        [Header("การตั้งค่าตู้ (อิงตาม object — สร้างจากเมนู Claw Machine/Machine Settings)")]
        [Tooltip("ถ้า assign ไว้ จะ override มุมกาง/แรงคีบทั้งหมดจาก asset นี้")]
        [SerializeField] private MachineSettings settings;

        [Header("ขา 2 ข้าง (มี HingeJoint)")]
        [Tooltip("ขาซ้าย — มุม hinge เป็นบวก (มิเรอร์กับขวา)")]
        [SerializeField] private Transform leftArm;
        [Tooltip("ขาขวา — มุม hinge เป็นลบ")]
        [SerializeField] private Transform rightArm;

        [Header("มุมขา (องศา hinge, ใช้กับทั้งสองข้างแบบมิเรอร์)")]
        [SerializeField] private float openAngle = 35f;
        [SerializeField] private float closedAngle = 8f;

        [Header("ความแข็ง spring ของ hinge")]
        [Tooltip("ตอนกาง — อ่อนพอให้เบนหลบของได้")]
        [SerializeField] private float springOpen = 5f;
        [Tooltip("ตอนหุบ — แข็งพอหนีบ (แต่ยึดจริงด้วย FixedJoint)")]
        [SerializeField] private float springClose = 25f;
        [SerializeField] private float springDamper = 1.5f;

        [Header("แรงคีบ = breakForce ของ FixedJoint (newtons)")]
        [SerializeField] private float forceC1 = 60f;
        [Tooltip("รอบปกติ — ต่ำกว่าน้ำหนักของ เพื่อให้หลุดตอนยก")]
        [SerializeField] private float forceC3Normal = 2f;
        [SerializeField] private float forceC3Payout = 60f;
        [SerializeField] private float forceC4 = 35f;

        [Header("Grab detection")]
        [Tooltip("จุดกึ่งกลางระหว่างปลายขา ใช้หา prize ที่จะยึด")]
        [SerializeField] private Transform grabPoint;
        [SerializeField] private float grabRadius = 0.09f;
        [SerializeField] private LayerMask prizeLayer = ~0;

        public GripPhase Phase { get; private set; } = GripPhase.Open;
        public bool IsHolding => heldPrize != null && heldJoint != null;

        private Rigidbody anchor;
        private HingeJoint leftHinge;
        private HingeJoint rightHinge;
        private Prize heldPrize;
        private FixedJoint heldJoint;

        private void Awake()
        {
            anchor = GetComponent<Rigidbody>();
            anchor.isKinematic = true;
            anchor.useGravity = false;

            if (leftArm != null) leftHinge = leftArm.GetComponent<HingeJoint>();
            if (rightArm != null) rightHinge = rightArm.GetComponent<HingeJoint>();

            // ตู้จริง: ค่าทั้งหมดมาจากแผงปรับ (MachineSettings) ไม่ฝังในโค้ด
            if (settings != null)
            {
                openAngle = settings.openArmAngle;
                forceC1 = settings.FullGripForce;
                forceC3Normal = settings.LiftGripForce(isPayoutRound: false);
                forceC3Payout = settings.LiftGripForce(isPayoutRound: true);
                forceC4 = forceC1 * 0.7f;
                // แรงสปริงหุบ สัมพันธ์กับแรงหนีบ (สปริงแรง = ขาหนีบแน่น)
                springClose = Mathf.Max(8f, forceC1 * 0.5f);
            }

            // กันขาซ้าย-ขวาชน/เกี่ยวกันเองตอนหุบ (manual 13-4: "confirm shovels do not overlap")
            if (leftArm != null && rightArm != null)
            {
                foreach (var lc in leftArm.GetComponentsInChildren<Collider>())
                    foreach (var rc in rightArm.GetComponentsInChildren<Collider>())
                        Physics.IgnoreCollision(lc, rc, true);
            }

            SetHinges(openAngle, springOpen);
        }

        private void Update()
        {
            // FixedJoint ขาดเอง (Unity ทำลาย object) -> ของหลุด
            if (heldPrize != null && heldJoint == null)
                heldPrize = null;
        }

        /// <summary>มี prize อยู่ในระยะคีบใต้หัวคีบหรือยัง</summary>
        public bool PrizeInRange()
        {
            if (grabPoint == null) return false;
            return Physics.CheckSphere(
                grabPoint.position, grabRadius, prizeLayer, QueryTriggerInteraction.Ignore);
        }

        // ---------- API ที่ ClawController เรียก ----------

        public void OpenArms()
        {
            Phase = GripPhase.Open;
            SetHinges(openAngle, springOpen);
            ReleasePrize();
        }

        public void CloseArms(GripPhase phase)
        {
            Phase = phase;
            SetHinges(closedAngle, springClose);
            TryGrabPrize();
        }

        public void SetGripPhase(GripPhase phase)
        {
            Phase = phase;
            if (heldJoint != null)
            {
                float f = CurrentHoldForce();
                heldJoint.breakForce = f;
                heldJoint.breakTorque = f;
            }
        }

        // ---------- ภายใน ----------

        // ตั้ง spring ของ hinge ทั้งสองข้างแบบมิเรอร์ (ซ้ายบวก / ขวาลบ — ทิศเดียวกับ visual เดิม)
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

        private float CurrentHoldForce()
        {
            switch (Phase)
            {
                case GripPhase.C1:          return forceC1;
                case GripPhase.C3Normal:    return forceC3Normal;
                case GripPhase.C3Payout:    return forceC3Payout;
                case GripPhase.C4Transport: return forceC4;
                default:                    return 0f;
            }
        }

        private void TryGrabPrize()
        {
            if (heldJoint != null || grabPoint == null) return;

            Collider[] hits = Physics.OverlapSphere(
                grabPoint.position, grabRadius, prizeLayer, QueryTriggerInteraction.Ignore);

            Prize closest = null;
            float best = float.MaxValue;
            foreach (var col in hits)
            {
                var prize = col.GetComponentInParent<Prize>();
                if (prize == null) continue;
                float d = (prize.transform.position - grabPoint.position).sqrMagnitude;
                if (d < best) { best = d; closest = prize; }
            }

            if (closest != null)
            {
                heldPrize = closest;
                heldJoint = gameObject.AddComponent<FixedJoint>();
                heldJoint.connectedBody = closest.Rigidbody;
                heldJoint.enableCollision = false;
                float f = CurrentHoldForce();
                heldJoint.breakForce = f;
                heldJoint.breakTorque = f;
            }
        }

        private void ReleasePrize()
        {
            if (heldJoint != null) Destroy(heldJoint);
            heldJoint = null;
            heldPrize = null;
        }

        private void OnJointBreak(float breakForce)
        {
            heldJoint = null;
            heldPrize = null;
        }

        private void OnDrawGizmosSelected()
        {
            if (grabPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(grabPoint.position, grabRadius);
            }
        }
    }
}
