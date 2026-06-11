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
    /// จำลองหัวใจของตู้คีบ: แรงตอนยก (C3) ต่างกันระหว่างรอบปกติกับรอบ payout
    /// แรงยึดจำลองผ่านการดูว่า น้ำหนักของ × gravity เกิน gripHoldForce ปัจจุบันหรือไม่
    /// ถ้าเกิน -> ของลื่นหลุดจากขา (คลาย parent / ลดแรงเสียดทานเสมือน)
    /// ดู docs/research-claw-mechanics.md หัวข้อ C1–C4
    /// </summary>
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

        [Header("แรงยึด (newtons เสมือน) ต่อแต่ละ phase")]
        [Tooltip("C1: แรงหุบเต็ม — พอยกของหนักสุดที่ออกแบบ")]
        [SerializeField] private float forceC1 = 8f;
        [Tooltip("C3 รอบปกติ — 15% ของ C1 ทำให้ของ >~0.1kg หลุด")]
        [SerializeField] private float forceC3Normal = 1.2f;
        [Tooltip("C3 รอบ payout — เท่า C1")]
        [SerializeField] private float forceC3Payout = 8f;
        [Tooltip("C4 ตอนเลื่อน — แรงกลาง")]
        [SerializeField] private float forceC4 = 6f;

        [Header("Grab detection")]
        [Tooltip("จุดกึ่งกลางระหว่างปลายขา 2 ข้าง ใช้หา prize ที่อยู่ในวงหุบ")]
        [SerializeField] private Transform grabPoint;
        [SerializeField] private float grabRadius = 0.06f;
        [SerializeField] private LayerMask prizeLayer = ~0;

        public GripPhase Phase { get; private set; } = GripPhase.Open;
        public bool IsHolding => heldPrize != null;

        private float targetAngle;
        private Prize heldPrize;
        // joint เสมือน: ตำแหน่ง offset ของ prize เทียบ grabPoint ตอนเริ่มจับ
        private Vector3 heldLocalOffset;
        private Quaternion heldLocalRotation;

        private void Awake()
        {
            targetAngle = openAngle;
            ApplyArmAngles(openAngle);
        }

        private void Update()
        {
            // อนิเมตขาเข้าหา targetAngle
            float current = Mathf.MoveTowards(CurrentAngle(), targetAngle, armSpeed * Time.deltaTime);
            ApplyArmAngles(current);
        }

        private void FixedUpdate()
        {
            if (heldPrize == null) return;

            float hold = CurrentHoldForce();
            float weight = heldPrize.Rigidbody.mass * Mathf.Abs(Physics.gravity.y);

            if (weight > hold)
            {
                // แรงยึดไม่พอ -> ของลื่นหลุด (ปล่อยให้ physics เล่นต่อ)
                ReleasePrize();
            }
            else
            {
                // ยึดของไว้กับ grabPoint แบบ kinematic follow (จำลองการคีบติด)
                KeepPrizeAttached();
            }
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
            // ขายังหุบอยู่ระหว่าง C3/C4 — เปลี่ยนแค่ระดับแรงยึด
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
            if (heldPrize != null || grabPoint == null) return;

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
                heldPrize.Rigidbody.isKinematic = true;
                heldLocalOffset = grabPoint.InverseTransformPoint(heldPrize.transform.position);
                heldLocalRotation = Quaternion.Inverse(grabPoint.rotation) * heldPrize.transform.rotation;
            }
        }

        private void KeepPrizeAttached()
        {
            heldPrize.transform.position = grabPoint.TransformPoint(heldLocalOffset);
            heldPrize.transform.rotation = grabPoint.rotation * heldLocalRotation;
        }

        private void ReleasePrize()
        {
            if (heldPrize == null) return;
            heldPrize.Rigidbody.isKinematic = false;
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
