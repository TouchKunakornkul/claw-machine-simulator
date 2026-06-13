using UnityEngine;

namespace ClawMachine
{
    /// <summary>
    /// แผงปรับตู้ในเกม (กด Tab) — จัดวางตาม SEGA UFO Catcher Manual จริง:
    ///   11-1 POWER knob (00–99)
    ///   13-3 ตำแหน่งสปริง 3 ระดับ + ขนาดขา S/M/L
    ///   13-4 ปลายขา shovel W30/W40/W60 + สกรูปรับระยะห่างตอนหุบ
    ///   13-5 มุมกางขา (เลื่อน A/B)
    /// ค่า physics ดิบ (สปริง/damper/ความเร็ว) อยู่ใน Advanced สำหรับ calibrate ตัวซิมเท่านั้น
    /// </summary>
    public class TuningPanel : MonoBehaviour
    {
        [SerializeField] private ClawGripSystem grip;
        [SerializeField] private ClawController claw;
        [SerializeField] private PayoutManager payout;
        [SerializeField] private BarSurface barSurface;
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

        private bool visible;
        private bool advanced;
        private Rect winRect = new Rect(20, 20, 400, 560);
        private Vector2 scroll;

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible)
            {
                GUI.Label(new Rect(12, Screen.height - 28, 400, 24),
                    $"[{toggleKey}] เปิดแผงปรับตู้ (เหมือนหลังตู้จริง)");
                return;
            }

            winRect = GUI.Window(7711, winRect, DrawWindow, "แผงปรับตู้ — SEGA UFO Catcher");
        }

        private void DrawWindow(int id)
        {
            var s = grip != null ? grip.Settings : null;

            // แถบหัวสำหรับลากหน้าต่าง (ต้องอยู่นอก ScrollView ไม่งั้น scroll ไม่ทำงาน)
            GUI.DragWindow(new Rect(0, 0, winRect.width, 20));

            if (s == null)
            {
                GUILayout.Space(22);
                GUILayout.Label("(ไม่มี MachineSettings asset — rebuild scene ก่อน)");
                return;
            }

            GUILayout.Space(20);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.BeginVertical();

            bool changed = false;

            // ===== 11-1: POWER =====
            GUILayout.Label("<b>11-1  POWER (แรงสปริงกลไก UFO)</b>", Rich());
            GUILayout.Label($"<size=22><b>{s.power:00}</b></size>  (00 อ่อนสุด – 99 แรงสุด)", Rich());
            int newPower = Mathf.RoundToInt(GUILayout.HorizontalSlider(s.power, 0, 99));
            if (newPower != s.power) { s.power = newPower; changed = true; }

            // ===== 13-3: ตำแหน่งสปริง =====
            GUILayout.Space(8);
            GUILayout.Label("<b>13-3  ตำแหน่งสปริง (shift bracket)</b>", Rich());
            var stage = (MachineSettings.SpringStage)Toolbar((int)s.springStage,
                new[] { "ใกล้สปริง\n(อ่อน/ยาก)", "กลาง", "ไกลสปริง\n(แรง/ง่าย)" });
            if (stage != s.springStage) { s.springStage = stage; changed = true; }

            // ===== 13-3: ขนาดขา =====
            GUILayout.Space(8);
            GUILayout.Label("<b>13-3  ขนาดขา (arm size)</b>", Rich());
            var arm = (MachineSettings.ArmSize)Toolbar((int)s.armSize, new[] { "S", "M", "L" });
            if (arm != s.armSize) { s.armSize = arm; changed = true; }

            // ===== 13-4: shovel =====
            GUILayout.Space(8);
            GUILayout.Label("<b>13-4  ปลายขา shovel (เลือกตามของรางวัล)</b>", Rich());
            var shovel = (MachineSettings.ShovelType)Toolbar((int)s.shovel,
                new[] { "W30", "W40", "W60" });
            if (shovel != s.shovel) { s.shovel = shovel; changed = true; }

            GUILayout.Label($"สกรูปรับระยะห่างตอนหุบ: {s.shovelGapCm:0.0} cm (ห้าม overlap)");
            float newGap = GUILayout.HorizontalSlider(s.shovelGapCm, 0f, 3f);
            if (!Mathf.Approximately(newGap, s.shovelGapCm)) { s.shovelGapCm = newGap; changed = true; }

            // ===== สกรูเงิน UFO9: แรงดันลงก่อนหยุด =====
            GUILayout.Space(8);
            GUILayout.Label($"<b>สกรูเงิน (UFO9) แรงดันลงก่อนหยุด</b>  {s.pushForceKnob:0}°", Rich());
            GUILayout.Label("น้อย = เซนเซอร์ไว ดันเบา หยุดเร็ว / มาก = ดันแรงกว่าก่อนหยุด");
            float newPush = GUILayout.HorizontalSlider(s.pushForceKnob, 4f, 40f);
            if (!Mathf.Approximately(newPush, s.pushForceKnob)) { s.pushForceKnob = newPush; changed = true; }

            // ===== 13-5: มุมกางขา =====
            GUILayout.Space(8);
            GUILayout.Label($"<b>13-5  มุมกางขา</b>  {s.openArmAngle:0}°  (เลื่อน A=กว้าง / B=แคบ)", Rich());
            float newOpen = GUILayout.HorizontalSlider(s.openArmAngle, 40f, 88f);
            if (!Mathf.Approximately(newOpen, s.openArmAngle)) { s.openArmAngle = newOpen; changed = true; }

            // ===== ทิศการกางขาเทียบคาน (การติดตั้ง — ไม่มีใน manual) =====
            GUILayout.Space(8);
            GUILayout.Label($"<b>ทิศการกางขาเทียบคาน</b>  {s.clawYaw:0}°  (ตู้จริงส่วนใหญ่ ~45°)", Rich());
            int yawPreset = GUILayout.Toolbar(
                Mathf.Approximately(s.clawYaw, 0f) ? 0 :
                Mathf.Approximately(s.clawYaw, 45f) ? 1 :
                Mathf.Approximately(s.clawYaw, 90f) ? 2 : -1,
                new[] { "ขนานคาน 0°", "ทแยง 45°", "ตั้งฉาก 90°" }, GUILayout.Height(26));
            if (yawPreset >= 0)
            {
                float presetVal = yawPreset == 0 ? 0f : yawPreset == 1 ? 45f : 90f;
                if (!Mathf.Approximately(presetVal, s.clawYaw)) { s.clawYaw = presetVal; changed = true; }
            }
            float newYaw = GUILayout.HorizontalSlider(s.clawYaw, 0f, 90f);
            if (!Mathf.Approximately(newYaw, s.clawYaw)) { s.clawYaw = newYaw; changed = true; }

            // ===== ระยะเยื้องขา (ดีไซน์ hub — ขาขนานกันแต่ไม่ตรงกัน) =====
            GUILayout.Space(4);
            GUILayout.Label(
                $"<b>ระยะเยื้องขาซ้าย-ขวา</b>  {s.armOffsetCm:0.0} cm  " +
                "(ขาขนานแต่ไม่ตรงกัน — ติดลบ = สลับฝั่ง)", Rich());
            float newOff = GUILayout.HorizontalSlider(s.armOffsetCm, -4f, 4f);
            if (!Mathf.Approximately(newOff, s.armOffsetCm)) { s.armOffsetCm = newOff; changed = true; }

            // ===== ปลอกคานคู่กลาง (การจัดตู้) =====
            GUILayout.Space(8);
            GUILayout.Label("<b>ปลอกคานคู่กลาง</b>  (ยิ่งหนึบยิ่งยาก)", Rich());
            var cover = (MachineSettings.BarCover)Toolbar((int)s.barCover,
                new[] { "ไม่มีปลอก\n(ลื่น/ง่าย)", "ปลอกใส", "Pink tube\n(หนึบ/ยาก)" });
            if (cover != s.barCover)
            {
                s.barCover = cover;
                changed = true;
                if (barSurface != null) barSurface.ApplyFromSettings();
            }

            // ===== ประเภทตู้ =====
            GUILayout.Space(8);
            GUILayout.Label("<b>ประเภทตู้</b>", Rich());
            bool newSega = GUILayout.Toggle(s.segaMode,
                s.segaMode ? " SEGA แท้ — แรงคงที่ทุกตา (สู้ด้วยการจัดวาง)"
                           : " Kakuritsu — อ่อนปกติ / แรงเต็มเมื่อถึงรอบจ่าย");
            if (newSega != s.segaMode) { s.segaMode = newSega; changed = true; }
            if (!s.segaMode && payout != null)
            {
                GUILayout.Label($"จ่ายทุก {s.payoutEveryN} ตา (เหลืออีก {payout.PlaysUntilPayout})");
                int n = Mathf.RoundToInt(GUILayout.HorizontalSlider(s.payoutEveryN, 3, 30));
                if (n != s.payoutEveryN) { s.payoutEveryN = n; changed = true; }
            }

            if (changed)
            {
                if (grip != null) grip.ApplyFromSettings();
                if (claw != null) claw.ApplyFromSettings(); // 13-2: ขอบเขตเลื่อนตามขนาดขา
            }

            // ===== 13-2: sensor bracket (อัตโนมัติ) =====
            if (claw != null)
            {
                GUILayout.Space(4);
                GUILayout.Label(
                    $"<b>13-2  Sensor bracket</b>  ระยะเลื่อน X ±{claw.CurrentTravelLimit:0.00} / " +
                    $"Z ±{claw.CurrentTravelLimitZ:0.00} m (คำนวณจากขนาดขา+มุมกาง)", Rich());
            }

            // ===== สถานะ =====
            GUILayout.Space(8);
            if (grip != null)
            {
                GUILayout.Label(
                    $"Phase: {grip.Phase}   ของในง่าม: {(grip.IsHolding ? "มี" : "ไม่มี")}   " +
                    $"มุมขา {grip.LeftHingeAngle:0}°/{grip.RightHingeAngle:0}°");
            }

            // ===== Advanced (calibrate ตัวซิม — ตู้จริงไม่มีให้หมุน) =====
            GUILayout.Space(6);
            advanced = GUILayout.Toggle(advanced, " Advanced: calibrate ฟิสิกส์ (debug)");
            if (advanced && grip != null)
            {
                grip.SpringOpenVal = Slider($"สปริงตอนกาง: {grip.SpringOpenVal:0.00}", grip.SpringOpenVal, 0.05f, 3f);
                grip.DamperVal = Slider($"Damper: {grip.DamperVal:0.000}", grip.DamperVal, 0.005f, 0.5f);
                grip.ArmMotorSpeedVal = Slider($"ความเร็วมอเตอร์กาง/หุบ: {grip.ArmMotorSpeedVal:0}°/s", grip.ArmMotorSpeedVal, 30f, 360f);
                float newShovelScale = Slider($"สเกล shovel (ตามตา): {grip.ShovelScaleVal:0.00}", grip.ShovelScaleVal, 0.5f, 1.5f);
                if (!Mathf.Approximately(newShovelScale, grip.ShovelScaleVal)) grip.ShovelScaleVal = newShovelScale;
                // มุม V ของหน้า shovel ตอนหุบ — วัดจากรูป manual ≈ 6° (0 = ราบสนิท)
                float newTilt = Slider($"มุม V หน้า shovel ตอนหุบ: {grip.ShovelScoopTiltVal:0}° (วัดจริง ~6)", grip.ShovelScoopTiltVal, 0f, 30f);
                if (!Mathf.Approximately(newTilt, grip.ShovelScoopTiltVal)) grip.ShovelScoopTiltVal = newTilt;
                if (claw != null)
                {
                    claw.DescentSpeedVal = Slider($"ความเร็วดิ่ง: {claw.DescentSpeedVal:0.00} m/s", claw.DescentSpeedVal, 0.05f, 0.4f);
                    claw.AscentSpeedVal = Slider($"ความเร็วยก: {claw.AscentSpeedVal:0.00} m/s", claw.AscentSpeedVal, 0.05f, 0.4f);
                    claw.HoldDurationSec = Slider($"เวลาค้างหุบ C2: {claw.HoldDurationSec:0.0}s", claw.HoldDurationSec, 0.3f, 3f);
                }
                GUILayout.Label($"(สปริงตอนนี้ แข็ง {grip.SpringStrongVal:0.00} / อ่อน {grip.SpringWeakVal:0.00} — คำนวณจากแผงด้านบน)");
            }

            GUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        private int Toolbar(int selected, string[] labels)
        {
            return GUILayout.Toolbar(selected, labels, GUILayout.Height(labels[0].Contains("\n") ? 40 : 26));
        }

        private float Slider(string label, float value, float min, float max)
        {
            GUILayout.Label(label);
            return GUILayout.HorizontalSlider(value, min, max);
        }

        private GUIStyle Rich()
        {
            return new GUIStyle(GUI.skin.label) { richText = true };
        }
    }
}
