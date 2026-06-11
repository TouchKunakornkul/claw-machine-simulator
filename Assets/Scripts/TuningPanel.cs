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
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

        private bool visible;
        private bool advanced;
        private Rect winRect = new Rect(20, 20, 400, 620);

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
            GUILayout.BeginVertical();

            if (s == null)
            {
                GUILayout.Label("(ไม่มี MachineSettings asset — rebuild scene ก่อน)");
                GUILayout.EndVertical();
                GUI.DragWindow();
                return;
            }

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

            // ===== 13-5: มุมกางขา =====
            GUILayout.Space(8);
            GUILayout.Label($"<b>13-5  มุมกางขา</b>  {s.openArmAngle:0}°  (เลื่อน A=กว้าง / B=แคบ)", Rich());
            float newOpen = GUILayout.HorizontalSlider(s.openArmAngle, 20f, 60f);
            if (!Mathf.Approximately(newOpen, s.openArmAngle)) { s.openArmAngle = newOpen; changed = true; }

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

            if (changed && grip != null) grip.ApplyFromSettings();

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
                grip.ResistanceAngleDeg = Slider($"มุมต้านหยุดดิ่ง: {grip.ResistanceAngleDeg:0}°", grip.ResistanceAngleDeg, 5f, 35f);
                if (claw != null)
                {
                    claw.DescentSpeedVal = Slider($"ความเร็วดิ่ง: {claw.DescentSpeedVal:0.00} m/s", claw.DescentSpeedVal, 0.05f, 0.4f);
                    claw.AscentSpeedVal = Slider($"ความเร็วยก: {claw.AscentSpeedVal:0.00} m/s", claw.AscentSpeedVal, 0.05f, 0.4f);
                    claw.HoldDurationSec = Slider($"เวลาค้างหุบ C2: {claw.HoldDurationSec:0.0}s", claw.HoldDurationSec, 0.3f, 3f);
                }
                GUILayout.Label($"(สปริงตอนนี้ แข็ง {grip.SpringStrongVal:0.00} / อ่อน {grip.SpringWeakVal:0.00} — คำนวณจากแผงด้านบน)");
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
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
