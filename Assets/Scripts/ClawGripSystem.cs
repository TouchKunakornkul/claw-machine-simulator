using UnityEngine;

namespace ClawMachine
{
    /// <summary>
    /// ระดับแรงคีบตาม 4 phase ของ UFO Catcher จริง (C1–C4)
    /// </summary>
    public enum GripPhase
    {
        RestClosed,  // ขาหุบพักตอนเล็ง (เครื่องจริงขาหุบตลอด กางเฉพาะก่อนดิ่ง)
        Open,        // ขากางก่อนดิ่ง/ตอนปล่อยของ
        C1,          // หุบแรงเต็มตอนล่างสุด
        C3Normal,    // ยกขึ้น รอบปกติ — สปริงอ่อน ของดันขากางหลุดเอง
        C3Payout,    // ยกขึ้น รอบจ่าย — สปริงแข็ง ประคองของไว้ได้
        C4Transport  // เลื่อนกลับท่อ — แรงกลาง
    }

    /// <summary>
    /// เรขาคณิตขาตัว L ตาม manual จริง (ACCESSORIES: ARM S UCS-3430 / ARM L UCS-3432)
    /// ขา = ท่อนดิ่งจาก pivot ลงมา + หักศอก ~90° เป็นท่อนนอนยื่นเข้าใน + shovel ยึดปลาย
    /// ขนาดเป็นขา M — builder และ grip ต้องใช้ชุดเดียวกันเสมอ
    /// </summary>
    public static class ArmGeometry
    {
        public const float ShoulderLen = 0.115f;   // ท่อนดิ่ง: pivot -> ข้อศอก
        public const float FootLen = 0.05f;        // ท่อนนอน: ข้อศอก -> จุดยึด shovel
        public const float PlateLen = 0.03f;       // ความยาวแผ่น shovel (ปรับตามสัดส่วนภาพจริง)
        public const float PlateCenterBeyondFoot = 0.008f; // กึ่งกลางแผ่นเลยปลายท่อนนอน
        public const float PivotOut = 0.025f;      // pivot สองข้างแยกจากแกนกลางหัวข้างละ (ห่างกัน 5cm)

        /// <summary>ระยะปลาย shovel ยื่นเข้าหากึ่งกลาง เมื่อ hinge มุม 0 (ขาดิ่ง เท้านอน)</summary>
        public const float TipIn = FootLen + PlateCenterBeyondFoot + PlateLen / 2f;
        /// <summary>ระยะปลาย shovel ต่ำกว่า pivot เมื่อ hinge มุม 0</summary>
        public const float TipDown = ShoulderLen + 0.006f;

        /// <summary>
        /// มุมหุบ (องศากางออกจากแนวดิ่ง) ที่ทำให้ปลาย shovel สองข้างห่างกัน = gap
        /// (13-4 overlap adjustment screw — gap 0 คือปลายพบกันพอดีใต้ของ)
        /// </summary>
        public static float ClosedAngleDeg(float armScale, float gapMeters)
        {
            float tipIn = TipIn * armScale;
            float tipDown = TipDown * armScale;
            float r = Mathf.Sqrt(tipIn * tipIn + tipDown * tipDown);
            float delta = Mathf.Atan2(tipDown, tipIn) * Mathf.Rad2Deg;
            float c = Mathf.Clamp((PivotOut + gapMeters / 2f) / r, -0.99f, 0.99f);
            float phi = Mathf.Acos(c) * Mathf.Rad2Deg - delta;
            return Mathf.Clamp(phi, 2f, 60f);
        }

        /// <summary>รัศมีปลายขาวัดจากแกนกลางหัว ตอนกาง — ใช้คำนวณ 13-2 sensor bracket</summary>
        public static float OpenTipReach(float openDeg, float armScale)
        {
            float rad = openDeg * Mathf.Deg2Rad;
            float outward = (TipDown * Mathf.Sin(rad) - TipIn * Mathf.Cos(rad)) * armScale;
            return PivotOut + Mathf.Max(0f, outward);
        }
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

        [Header("มุมขา (องศา hinge, มิเรอร์สองข้าง) — spec จริง: กาง ~45-50, หุบปลายเกือบแตะ")]
        [SerializeField] private float openAngle = 50f;
        [SerializeField] private float closedAngle = 15f;

        [Header("ความแข็งสปริง hinge (หน่วย N·m/องศา — ค่าเล็กมากก็แข็งแล้ว)")]
        [Tooltip("ตอนกาง — อ่อนพอให้เบนหลบของได้ตอนดิ่ง")]
        [SerializeField] private float springOpen = 0.5f;
        [Tooltip("ตอนหุบแรงเต็ม (C1/payout)")]
        [SerializeField] private float springStrong = 1.8f;
        [Tooltip("ตอนยกรอบปกติ (kakuritsu C3) — อ่อนจนน้ำหนักของดันขากางได้")]
        [SerializeField] private float springWeak = 0.25f;
        [SerializeField] private float springDamper = 0.01f;
        [Tooltip("ตัวแปลงแรงหนีบ (N) จาก MachineSettings -> ความแข็งสปริง (N·m/องศา)\n" +
                 "ต้องเล็กมาก: spring ของ Unity คิดแรงตามองศาที่เบี่ยง — ค่าใหญ่จะแข็งจน" +
                 "น้ำหนักของง้างขาไม่ออก (POWER ต่ำแล้วของต้องหลุดจริง)")]
        [SerializeField] private float springPerNewton = 0.0008f;

        [Header("ตรวจสถานะ")]
        [Tooltip("ระยะรอบ grabPoint ที่ถือว่า 'มีของอยู่ในง่าม' (ใช้แสดงผล/หยุดดิ่ง)")]
        [SerializeField] private float holdCheckRadius = 0.05f;
        [Tooltip("ขาโดนดันจนมุมเบี่ยงจากเป้าเกินนี้ = มีแรงต้านจริง (องศา)")]
        [SerializeField] private float resistanceAngle = 12f;
        [SerializeField] private Transform grabPoint;
        [SerializeField] private LayerMask prizeLayer = ~0;

        public GripPhase Phase { get; private set; } = GripPhase.RestClosed;
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

            ApplyFromSettings();

            // กันขาซ้าย-ขวาชน/เกี่ยวกันเองตอนหุบ (manual 13-4: shovels must not overlap)
            if (leftArm != null && rightArm != null)
            {
                foreach (var lc in leftArm.GetComponentsInChildren<Collider>())
                    foreach (var rc in rightArm.GetComponentsInChildren<Collider>())
                        Physics.IgnoreCollision(lc, rc, true);
            }

            ReapplyPhase(); // เริ่มแบบเครื่องจริง: ขาหุบพัก
        }

        private void FixedUpdate()
        {
            // สถานะ "มีของในง่าม" — เช็คทาง physics ล้วน ไว้โชว์ HUD
            IsHolding = Phase != GripPhase.Open && Phase != GripPhase.RestClosed
                        && PrizeBetweenArms();
        }

        /// <summary>ขาโดนของดันจนเบี่ยงเกิน threshold = แรงต้านจริง (ใช้หยุดการดิ่ง)</summary>
        public bool ArmsResisted()
        {
            return HingeDeviation(leftHinge) > resistanceAngle
                || HingeDeviation(rightHinge) > resistanceAngle;
        }

        // ---------- Live tuning API (ใช้โดย TuningPanel) ----------

        public MachineSettings Settings => settings;
        public float OpenAngleDeg { get => openAngle; set { openAngle = value; ReapplyPhase(); } }
        public float ClosedAngleDeg { get => closedAngle; set { closedAngle = value; ReapplyPhase(); } }
        public float SpringOpenVal { get => springOpen; set { springOpen = value; ReapplyPhase(); } }
        public float SpringStrongVal { get => springStrong; set { springStrong = value; ReapplyPhase(); } }
        public float SpringWeakVal { get => springWeak; set { springWeak = value; ReapplyPhase(); } }
        public float DamperVal { get => springDamper; set { springDamper = value; ReapplyPhase(); } }
        public float ResistanceAngleDeg { get => resistanceAngle; set => resistanceAngle = value; }
        public float LeftHingeAngle => leftHinge != null ? leftHinge.angle : 0f;
        public float RightHingeAngle => rightHinge != null ? rightHinge.angle : 0f;

        /// <summary>คำนวณค่าทั้งหมดใหม่จาก MachineSettings (เรียกเมื่อหมุนแผงปรับ)</summary>
        public void ApplyFromSettings()
        {
            if (settings == null) return;

            // ทิศการกางขาเทียบคาน (ตู้จริงส่วนใหญ่ ~45°) — หมุนทั้งหัวคีบรอบแกนตั้ง
            transform.localRotation = Quaternion.Euler(0f, settings.clawYaw, 0f);

            // 13-1: เปลี่ยนขา S/M/L = เปลี่ยนความยาวขาจริง (สเกลรอบ pivot ซึ่งเป็นจุดหมุน)
            float armScale = settings.ArmSizeScale;
            if (leftArm != null) leftArm.localScale = Vector3.one * armScale;
            if (rightArm != null) rightArm.localScale = Vector3.one * armScale;

            // ขาขนานกันแต่ "เยื้องกัน" เหมือนเครื่องจริง — ขยับจุดแขวน hinge ตามแกน Z
            ApplyArmOffset(leftHinge, +1f);
            ApplyArmOffset(rightHinge, -1f);

            // 13-4: สกรูปรับระยะห่าง shovel -> มุมหุบของขาตัว L
            // (ขาหุบสุดยังกางออก ~25° เหมือนเครื่องจริง — เท้าสองข้างทำมุม V ช้อนใต้ของ)
            closedAngle = ArmGeometry.ClosedAngleDeg(armScale, settings.shovelGapCm * 0.01f);

            // 13-5: มุมกางขา (ต้องกางมากกว่ามุมหุบเสมอ)
            openAngle = Mathf.Max(settings.openArmAngle, closedAngle + 8f);

            // 11-1 + 13-3: POWER × ตำแหน่งสปริง × ขนาดขา -> ความแข็งสปริง
            // POWER ต่ำ = สปริงอ่อนจริงๆ: น้ำหนักกล่อง (~0.06 N·m ที่ปลายขา) ง้างขา
            // จนแผ่นเอียง ของไถลหลุดตอนยก / POWER สูงถึงจะต้านไหว
            springStrong = Mathf.Max(0.002f, settings.FullGripForce * springPerNewton);
            springWeak = settings.segaMode
                ? springStrong // SEGA แท้: แรงคงที่ทุกตา
                : Mathf.Max(0.001f, springStrong * settings.normalGripRatio);

            // 13-4: เปลี่ยนความกว้างแผ่น shovel (W30/W40/W60) สดๆ
            // หารด้วย armScale เพื่อให้ W30/40/60 เป็นขนาดสัมบูรณ์ ไม่โดนสเกลขาคูณซ้ำ
            // มุมปลาย: ตั้งให้แผ่นราบพอดีตอนหุบ + แอ่นรับ (scoop tilt) เพื่อให้ของนั่งบนแผ่นได้
            ApplyShovel(leftArm, armScale, inwardSign: -1f);
            ApplyShovel(rightArm, armScale, inwardSign: +1f);

            ReapplyPhase();
        }

        [Tooltip("มุมแอ่นรับของแผ่น shovel ตอนหุบ (องศา) — ขอบในเชิดขึ้นเล็กน้อยเหมือนถาด")]
        [SerializeField] private float shovelScoopTilt = 5f;

        // ขยับจุดแขวนขาออกข้างตามแกน Z — ขาสองข้างขนานกันแต่ไม่ตรงกัน (เหมือนเครื่องจริง
        // ที่ shovel สวนผ่านกันได้ตอนหุบ) ปรับสดได้: joint จะดึงขาไปตำแหน่งใหม่เอง
        private void ApplyArmOffset(HingeJoint hinge, float sideSign)
        {
            if (hinge == null || settings == null) return;
            hinge.autoConfigureConnectedAnchor = false;
            var ca = hinge.connectedAnchor;
            ca.z = sideSign * settings.armOffsetCm * 0.005f; // ระยะเยื้องรวม cm -> ครึ่งข้างเป็น m
            hinge.connectedAnchor = ca;
        }

        // inwardSign ต้องตรงกับตอนสร้างใน ClawSceneBuilder (ซ้าย=-1 / ขวา=+1)
        private void ApplyShovel(Transform arm, float armScale, float inwardSign)
        {
            if (arm == null || settings == null) return;
            foreach (Transform child in arm)
            {
                if (!child.name.Contains("_Shovel")) continue;

                var s = child.localScale;
                s.z = settings.ShovelWidthMeters / Mathf.Max(0.1f, armScale);
                child.localScale = s;

                // แผ่นราบ (แนวนอน) พอดีเมื่อขาหุบที่ closedAngle + แอ่นรับขอบในขึ้นอีกนิด
                child.localRotation = Quaternion.Euler(
                    0f, 0f, inwardSign * (closedAngle + shovelScoopTilt));
            }
        }

        /// <summary>apply มุม/สปริงของ phase ปัจจุบันใหม่ (หลังค่าโดนแก้สดๆ)</summary>
        public void ReapplyPhase()
        {
            switch (Phase)
            {
                case GripPhase.RestClosed: SetHinges(closedAngle, springOpen); break;
                case GripPhase.Open: SetHinges(openAngle, springOpen); break;
                case GripPhase.C3Normal: SetHinges(closedAngle, springWeak); break;
                case GripPhase.C4Transport:
                    SetHinges(closedAngle, Mathf.Lerp(springWeak, springStrong, 0.7f)); break;
                default: SetHinges(closedAngle, springStrong); break;
            }
        }

        // ---------- API ที่ ClawController เรียก ----------

        public void OpenArms()
        {
            Phase = GripPhase.Open;
            ReapplyPhase();
        }

        /// <summary>ขาหุบพักตอนเล็ง (เครื่องจริงขาหุบตลอด กางเฉพาะตอนจะดิ่ง)</summary>
        public void RestArms()
        {
            Phase = GripPhase.RestClosed;
            ReapplyPhase();
        }

        /// <summary>หุบขาด้วยสปริงแรงเต็ม — shovel ช้อนเข้าใต้ของตามฟิสิกส์จริง ไม่มีการยึด</summary>
        public void CloseArms(GripPhase phase)
        {
            Phase = phase;
            ReapplyPhase();
        }

        /// <summary>
        /// เปลี่ยนระดับแรง (C3/C4): ปรับแค่ความแข็งสปริงขณะขายังหุบ
        /// รอบอ่อน: น้ำหนักของจะดันขากางออกเอง → ของลื่นร่วง (ไม่มี joint ให้ขาด)
        /// </summary>
        public void SetGripPhase(GripPhase phase)
        {
            Phase = phase;
            ReapplyPhase();
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
