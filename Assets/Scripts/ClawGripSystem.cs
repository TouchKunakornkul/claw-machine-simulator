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
    /// ควบคุมการหุบ/กางขา 2 ขา และ "แรงยึด" ของรางวัล
    ///
    /// โมเดลฟิสิกส์ (อ้างอิง implementation จริงของ czazuaga/Claw_Machine_Simulator + งานวิจัย):
    /// - ของรางวัลเป็น Rigidbody dynamic ตลอด (วางบนคานได้ ขาดันได้จริง)
    /// - ตอนหุบขา (C1) ถ้ามี prize อยู่ในวงคีบ จะผูก FixedJoint กับหัวคีบ (kinematic anchor)
    /// - breakForce ของ joint = แรงคีบ phase ปัจจุบัน
    ///   * รอบปกติ (C3Normal) แรงต่ำกว่าน้ำหนักของ -> joint ขาดตอนยก -> ของหลุด (สมจริง ไม่ teleport)
    ///   * รอบจ่าย (C3Payout) แรงสูง -> joint ไม่ขาด -> คีบติด
    /// ดู docs/research-claw-mechanics.md หัวข้อ C1–C4
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ClawGripSystem : MonoBehaviour
    {
        [Header("ขา 2 ข้าง (pivot)")]
        [Tooltip("จุดหมุนของขาซ้าย/ขวา — หมุนรอบแกน Z เพื่อหุบ/กาง")]
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;

        [Header("องศาขา (จากแนวตั้ง)")]
        [SerializeField] private float openAngle = 45f;
        [SerializeField] private float closedAngle = 5f;
        [Tooltip("ความเร็วหุบ/กาง องศา/วินาที")]
        [SerializeField] private float armSpeed = 180f;

        [Header("แรงคีบ = breakForce ของ joint (newtons)")]
        [Tooltip("C1: แรงหุบเต็มตอนล่างสุด — มากพอยึดของไว้")]
        [SerializeField] private float forceC1 = 50f;
        [Tooltip("C3 รอบปกติ — ต่ำกว่าน้ำหนักของ (มวล×g) เพื่อให้ joint ขาด ของหลุดตอนยก")]
        [SerializeField] private float forceC3Normal = 2f;
        [Tooltip("C3 รอบ payout — สูงพอคีบติด")]
        [SerializeField] private float forceC3Payout = 50f;
        [Tooltip("C4 ตอนเลื่อน — แรงกลาง")]
        [SerializeField] private float forceC4 = 30f;

        [Header("Grab detection")]
        [Tooltip("จุดกึ่งกลางระหว่างปลายขา 2 ข้าง ใช้หา prize ที่อยู่ในวงหุบ")]
        [SerializeField] private Transform grabPoint;
        [SerializeField] private float grabRadius = 0.07f;
        [SerializeField] private LayerMask prizeLayer = ~0;

        public GripPhase Phase { get; private set; } = GripPhase.Open;
        public bool IsHolding => heldPrize != null && heldJoint != null;

        private float targetAngle;
        private Rigidbody anchor;     // หัวคีบ kinematic ที่ joint ยึดไว้
        private Prize heldPrize;
        private FixedJoint heldJoint;

        private void Awake()
        {
            anchor = GetComponent<Rigidbody>();
            anchor.isKinematic = true;
            anchor.useGravity = false;
            anchor.interpolation = RigidbodyInterpolation.Interpolate;

            targetAngle = openAngle;
            ApplyArmAngles(openAngle);
        }

        private void Update()
        {
            // อนิเมตขาเข้าหา targetAngle
            float current = Mathf.MoveTowards(CurrentAngle(), targetAngle, armSpeed * Time.deltaTime);
            ApplyArmAngles(current);

            // joint ขาดเอง (Unity ทำลาย object) -> ถือว่าของหลุด
            if (heldPrize != null && heldJoint == null)
                heldPrize = null;
        }

        // ---------- API ที่ ClawController เรียก ----------

        public void OpenArms()
        {
            Phase = GripPhase.Open;
            targetAngle = openAngle;
            ReleasePrize();
        }

        public void CloseArms(GripPhase phase)
        {
            Phase = phase;
            targetAngle = closedAngle;
            TryGrabPrize();
        }

        public void SetGripPhase(GripPhase phase)
        {
            Phase = phase;
            // ขายังหุบอยู่ระหว่าง C3/C4 — ปรับแค่ breakForce ของ joint
            if (heldJoint != null)
            {
                float f = CurrentHoldForce();
                heldJoint.breakForce = f;
                heldJoint.breakTorque = f;
            }
        }

        // ---------- ภายใน ----------

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
                // ผูก prize ไว้กับหัวคีบที่ตำแหน่งปัจจุบัน (ไม่ snap) ด้วยแรงตาม phase
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

        // Unity เรียกเมื่อ joint บน GameObject นี้ขาด (แรงคีบไม่พอ)
        private void OnJointBreak(float breakForce)
        {
            heldJoint = null;
            heldPrize = null;
        }

        private float CurrentAngle()
        {
            if (leftArm == null) return targetAngle;
            // อ่านองศาปัจจุบันจากขาซ้าย (รอบแกน Z)
            return Mathf.Abs(leftArm.localEulerAngles.z > 180f
                ? leftArm.localEulerAngles.z - 360f
                : leftArm.localEulerAngles.z);
        }

        private void ApplyArmAngles(float angle)
        {
            if (leftArm != null)
                leftArm.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (rightArm != null)
                rightArm.localRotation = Quaternion.Euler(0f, 0f, -angle);
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
