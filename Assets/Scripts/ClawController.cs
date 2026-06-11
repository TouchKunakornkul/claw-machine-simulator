using UnityEngine;

namespace ClawMachine
{
    /// <summary>
    /// State machine ควบคุมขาคีบ 3 จังหวะตามกลไก UFO Catcher ญี่ปุ่น
    /// Aiming(P1) -> Dropping(P2) -> Gripping(C1/C2) -> Lifting(C3) -> Returning(C4) -> Releasing -> Idle
    /// ดู docs/prd-mvp.md และ docs/research-claw-mechanics.md
    /// </summary>
    public class ClawController : MonoBehaviour
    {
        public enum ClawState
        {
            Idle,       // รอเริ่ม
            Aiming,     // Phase 1: ผู้เล่นเลื่อน X/Z
            Dropping,   // Phase 2: ดิ่งลง Y
            Gripping,   // C1 + C2: หุบขา + ค้าง
            Lifting,    // C3: ยกขึ้น (จุดที่ของหลุด)
            Returning,  // C4: เลื่อนกลับเหนือท่อ
            Releasing   // กางขา ปล่อยของ
        }

        [Header("References")]
        [Tooltip("Transform ของหัวคีบที่เลื่อน X/Z (gantry). ปกติคือ object นี้เอง")]
        [SerializeField] private Transform gantry;
        [Tooltip("Transform ของชุดขาที่ดิ่งขึ้น-ลงแกน Y")]
        [SerializeField] private Transform clawHead;
        [SerializeField] private ClawGripSystem gripSystem;
        [SerializeField] private PayoutManager payoutManager;

        [Header("ขอบเขตการเลื่อน (local X/Z ของ gantry)")]
        [SerializeField] private Vector2 xLimits = new Vector2(-0.22f, 0.22f);
        [SerializeField] private Vector2 zLimits = new Vector2(-0.22f, 0.22f);

        [Header("13-2 Sensor bracket (จำกัดพื้นที่เลื่อนตามขนาดขา)")]
        [Tooltip("ถ้า assign settings ขอบเขตจะคำนวณจากความยาวขา+มุมกาง (ขา L = พื้นที่แคบลง)")]
        [SerializeField] private MachineSettings machineSettings;
        [Tooltip("ครึ่งความกว้างพื้นที่เล่นในตู้ (ถึงผนังกระจก)")]
        [SerializeField] private float cabinetHalfExtent = 0.30f;
        [Tooltip("ระยะกันปลายขาเฉี่ยวกระจก")]
        [SerializeField] private float wallClearance = 0.01f;

        /// <summary>ขอบเขตเลื่อนปัจจุบัน (สำหรับแสดงบนแผงปรับ)</summary>
        public float CurrentTravelLimit => xLimits.y;

        /// <summary>
        /// 13-2: คำนวณขอบเขตเลื่อนใหม่จากขนาดขา — ปลายขาตอนกางต้องไม่ถึงกระจก
        /// (เหมือนร้านเลื่อน sensor bracket ตามผัง FIG 13-2a เมื่อเปลี่ยนขา S/M/L)
        /// </summary>
        public void ApplyFromSettings()
        {
            if (machineSettings == null) return;
            // รัศมีปลายขาตอนกาง = sin(มุมกาง) × ความยาวขา (ขา M = 0.165m)
            float tipReach = Mathf.Sin(machineSettings.openArmAngle * Mathf.Deg2Rad)
                             * 0.165f * machineSettings.ArmSizeScale;
            float limit = Mathf.Max(0.05f, cabinetHalfExtent - tipReach - wallClearance);
            xLimits = new Vector2(-limit, limit);
            zLimits = new Vector2(-limit, limit);
        }

        [Header("ตำแหน่งแกน Y (local ของ clawHead)")]
        [Tooltip("ระดับบนสุด (home) ที่ขาพักและปล่อยของ")]
        [SerializeField] private float yTop = 0f;
        [Tooltip("ระดับล่างสุดที่ขาดิ่งลงได้ (กันทะลุพื้นตู้)")]
        [SerializeField] private float yBottom = -0.35f;

        [Header("ตำแหน่ง home เหนือท่อรับของ (local X/Z)")]
        [SerializeField] private Vector2 chuteHome = new Vector2(0.22f, -0.22f);
        [Tooltip("true = ยกแล้วลากกลับไปปล่อยที่ chuteHome (ตู้แบบมีท่อมุม) / false = ปล่อยตรงจุดที่คีบ (ตู้ hashi-watashi คานขนาน)")]
        [SerializeField] private bool returnToChute = true;

        [Header("ความเร็ว (m/s) — อ้างอิงเครื่องจริง")]
        [SerializeField] private float horizontalSpeed = 0.07f;
        [SerializeField] private float descentSpeed = 0.20f;
        [SerializeField] private float ascentSpeed = 0.14f;

        [Header("จังหวะเวลา")]
        [Tooltip("C2: ค้างแรงหุบก่อนยก (วินาที)")]
        [SerializeField] private float holdDuration = 1.2f;
        [Tooltip("หน่วงก่อนกางขาปล่อยของที่ท่อ")]
        [SerializeField] private float releaseDelay = 0.4f;

        [Header("Input")]
        [SerializeField] private KeyCode dropKey = KeyCode.Space;

        [Header("Collision ตอนดิ่ง")]
        [Tooltip("จุดยิง ray ตรวจการชน — ควรตั้งเป็น GrabPoint (ปลายขา) ถ้าเว้นว่างใช้ clawHead")]
        [SerializeField] private Transform dropProbe;
        [Tooltip("ระยะ raycast ตรวจว่าปลายขาชนของแล้วหรือยัง")]
        [SerializeField] private float groundCheckDistance = 0.03f;
        [Tooltip("เลเยอร์ที่หยุดการดิ่ง (ตั้งเป็น Prize เท่านั้น เพื่อไม่ให้ ray ชนขาตัวเอง)")]
        [SerializeField] private LayerMask dropBlockingLayers = ~0;

        public ClawState State { get; private set; } = ClawState.Idle;
        public int CurrentPlayCount => payoutManager != null ? payoutManager.PlayCount : 0;

        // Live tuning API (ใช้โดย TuningPanel)
        public float DescentSpeedVal { get => descentSpeed; set => descentSpeed = value; }
        public float AscentSpeedVal { get => ascentSpeed; set => ascentSpeed = value; }
        public float HoldDurationSec { get => holdDuration; set => holdDuration = value; }
        public float GroundCheckDist { get => groundCheckDistance; set => groundCheckDistance = value; }

        private float stateTimer;
        private bool isPayoutRound;

        private void Awake()
        {
            if (gantry == null) gantry = transform;
            ResetToHome();
        }

        private void Start()
        {
            ApplyFromSettings(); // 13-2: ขอบเขตตามขนาดขาที่ติดตั้ง
            EnterAiming();
        }

        private void Update()
        {
            switch (State)
            {
                case ClawState.Aiming:    TickAiming();   break;
                case ClawState.Dropping:  TickDropping(); break;
                case ClawState.Gripping:  TickGripping(); break;
                case ClawState.Lifting:   TickLifting();  break;
                case ClawState.Returning: TickReturning(); break;
                case ClawState.Releasing: TickReleasing(); break;
            }
        }

        // ---------- Phase 1: Aiming ----------

        private void EnterAiming()
        {
            State = ClawState.Aiming;
            gripSystem.OpenArms();
        }

        private void TickAiming()
        {
            float h = Input.GetAxisRaw("Horizontal"); // ซ้าย/ขวา = X
            float v = Input.GetAxisRaw("Vertical");   // หน้า/หลัง = Z

            Vector3 p = gantry.localPosition;
            p.x = Mathf.Clamp(p.x + h * horizontalSpeed * Time.deltaTime, xLimits.x, xLimits.y);
            p.z = Mathf.Clamp(p.z + v * horizontalSpeed * Time.deltaTime, zLimits.x, zLimits.y);
            gantry.localPosition = p;

            if (Input.GetKeyDown(dropKey))
            {
                State = ClawState.Dropping;
            }
        }

        // ---------- Phase 2: Dropping ----------

        private void TickDropping()
        {
            // ดิ่งต่อจนกว่ามี "แรงต้านจริง": ขาโดนของดันจนเบี่ยงเกิน threshold
            // (เฉียดขอบ = ขาแกว่งนิดเดียวแล้วลื่นผ่าน — ดิ่งต่อ)
            if (gripSystem.ArmsResisted())
            {
                EnterGripping();
                return;
            }

            Vector3 p = clawHead.localPosition;
            p.y -= descentSpeed * Time.deltaTime;

            // ของอยู่ใต้กึ่งกลางหัวพอดี (ขาคร่อมไม่โดน) ก็ถือว่าถึงของ + กันทะลุพื้น
            bool hitBottom = p.y <= yBottom;
            Vector3 probeOrigin = dropProbe != null ? dropProbe.position : clawHead.position;
            bool hitObject = Physics.Raycast(
                probeOrigin, Vector3.down,
                groundCheckDistance, dropBlockingLayers, QueryTriggerInteraction.Ignore);

            if (hitBottom || hitObject)
            {
                if (hitBottom) p.y = yBottom;
                clawHead.localPosition = p;
                EnterGripping();
            }
            else
            {
                clawHead.localPosition = p;
            }
        }

        // ---------- C1 + C2: Gripping ----------

        private void EnterGripping()
        {
            State = ClawState.Gripping;
            stateTimer = 0f;

            // ตัดสินรอบ payout ก่อนหุบ เพื่อกำหนดแรง C3 ที่จะใช้ตอนยก
            isPayoutRound = payoutManager.RegisterPlayAndEvaluate();
            gripSystem.CloseArms(GripPhase.C1); // หุบแรงเต็ม
        }

        private void TickGripping()
        {
            // C2: ค้างแรงหุบไว้ ให้ขา physics หุบจริงจนสุด
            stateTimer += Time.deltaTime;
            if (stateTimer >= holdDuration)
            {
                // ไม่มีการยึดใดๆ — ของจะติดขึ้นไปก็ต่อเมื่อ shovel ช้อนรับไว้ได้จริง
                EnterLifting();
            }
        }

        // ---------- C3: Lifting ----------

        private void EnterLifting()
        {
            State = ClawState.Lifting;
            // หัวใจของกลไก: รอบปกติแรงอ่อน (ของลื่นหลุด), รอบ payout แรงเต็ม
            gripSystem.SetGripPhase(isPayoutRound ? GripPhase.C3Payout : GripPhase.C3Normal);
        }

        private void TickLifting()
        {
            Vector3 p = clawHead.localPosition;
            p.y += ascentSpeed * Time.deltaTime;
            if (p.y >= yTop)
            {
                p.y = yTop;
                clawHead.localPosition = p;
                if (returnToChute)
                    EnterReturning();
                else
                    EnterReleasingInPlace(); // ปล่อยตรงจุด (hashi-watashi)
            }
            else
            {
                clawHead.localPosition = p;
            }
        }

        private void EnterReleasingInPlace()
        {
            State = ClawState.Releasing;
            stateTimer = 0f;
        }

        // ---------- C4: Returning ----------

        private void EnterReturning()
        {
            State = ClawState.Returning;
            gripSystem.SetGripPhase(GripPhase.C4Transport);
        }

        private void TickReturning()
        {
            Vector3 p = gantry.localPosition;
            p.x = Mathf.MoveTowards(p.x, chuteHome.x, horizontalSpeed * Time.deltaTime);
            p.z = Mathf.MoveTowards(p.z, chuteHome.y, horizontalSpeed * Time.deltaTime);
            gantry.localPosition = p;

            bool arrived = Mathf.Approximately(p.x, chuteHome.x) &&
                           Mathf.Approximately(p.z, chuteHome.y);
            if (arrived)
            {
                State = ClawState.Releasing;
                stateTimer = 0f;
            }
        }

        // ---------- Releasing ----------

        private void TickReleasing()
        {
            stateTimer += Time.deltaTime;
            if (stateTimer >= releaseDelay)
            {
                gripSystem.OpenArms(); // ปล่อยของลงท่อ
                EnterAiming();         // พร้อมเล่นรอบใหม่
            }
        }

        // ---------- Helpers ----------

        private void ResetToHome()
        {
            Vector3 head = clawHead.localPosition;
            head.y = yTop;
            clawHead.localPosition = head;
        }
    }
}
