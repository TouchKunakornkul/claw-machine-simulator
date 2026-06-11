using UnityEngine;

namespace ClawMachine
{
    /// <summary>
    /// หน้าตั้งค่าสดในเกม (กด Tab) — จูนแรง/มุม/สปริงขณะเล่นได้ทันทีโดยไม่ต้องแก้โค้ด
    /// เลียนแบบแผงปรับหลังตู้ของร้านจริง (POWER knob ฯลฯ) + ค่า physics ที่ตู้จริงไม่มีให้หมุน
    /// หมายเหตุ: ใน Editor ค่าที่แก้ลง MachineSettings asset จะคงอยู่หลังออก Play mode
    /// </summary>
    public class TuningPanel : MonoBehaviour
    {
        [SerializeField] private ClawGripSystem grip;
        [SerializeField] private ClawController claw;
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

        private bool visible;
        private Rect winRect = new Rect(20, 20, 380, 560);

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible)
            {
                GUI.Label(new Rect(12, Screen.height - 28, 400, 24),
                    $"[{toggleKey}] เปิดหน้าตั้งค่าตู้ (Tuning)");
                return;
            }

            winRect = GUI.Window(7711, winRect, DrawWindow, "ตั้งค่าตู้ (Machine Tuning)");
        }

        private void DrawWindow(int id)
        {
            var settings = grip != null ? grip.Settings : null;

            GUILayout.BeginVertical();

            // ===== แผงปรับแบบตู้จริง =====
            GUILayout.Label("<b>— แผงปรับร้าน (ตาม SEGA manual) —</b>", Rich());

            if (settings != null)
            {
                int newPower = Mathf.RoundToInt(
                    Slider($"POWER (แรงสปริง): {settings.power:00}", settings.power, 0, 99));
                if (newPower != settings.power)
                {
                    settings.power = newPower;
                    grip.ApplyFromSettings();
                }

                bool newSega = GUILayout.Toggle(settings.segaMode,
                    settings.segaMode ? " โหมด SEGA (แรงคงที่ทุกตา)" : " โหมด Kakuritsu (อ่อนปกติ/แรงรอบจ่าย)");
                if (newSega != settings.segaMode)
                {
                    settings.segaMode = newSega;
                    grip.ApplyFromSettings();
                }
            }
            else
            {
                GUILayout.Label("(ไม่มี MachineSettings asset)");
            }

            if (grip != null)
            {
                grip.OpenAngleDeg = Slider($"มุมกางขา: {grip.OpenAngleDeg:0}°", grip.OpenAngleDeg, 25f, 65f);
                grip.ClosedAngleDeg = Slider($"มุมหุบขา: {grip.ClosedAngleDeg:0}°", grip.ClosedAngleDeg, 5f, 30f);

                GUILayout.Space(6);
                GUILayout.Label("<b>— สปริง (N·m/องศา) —</b>", Rich());
                grip.SpringStrongVal = Slider($"สปริงหุบแรงเต็ม: {grip.SpringStrongVal:0.00}", grip.SpringStrongVal, 0.05f, 6f);
                grip.SpringWeakVal = Slider($"สปริงตอนยกรอบอ่อน: {grip.SpringWeakVal:0.00}", grip.SpringWeakVal, 0.02f, 3f);
                grip.SpringOpenVal = Slider($"สปริงตอนกาง: {grip.SpringOpenVal:0.00}", grip.SpringOpenVal, 0.05f, 3f);
                grip.DamperVal = Slider($"Damper: {grip.DamperVal:0.000}", grip.DamperVal, 0.005f, 0.5f);

                GUILayout.Space(6);
                GUILayout.Label("<b>— การดิ่ง/ยก —</b>", Rich());
                grip.ResistanceAngleDeg = Slider($"มุมต้านที่หยุดดิ่ง: {grip.ResistanceAngleDeg:0}°", grip.ResistanceAngleDeg, 5f, 35f);
            }

            if (claw != null)
            {
                claw.DescentSpeedVal = Slider($"ความเร็วดิ่ง: {claw.DescentSpeedVal:0.00} m/s", claw.DescentSpeedVal, 0.05f, 0.4f);
                claw.AscentSpeedVal = Slider($"ความเร็วยก: {claw.AscentSpeedVal:0.00} m/s", claw.AscentSpeedVal, 0.05f, 0.4f);
                claw.HoldDurationSec = Slider($"เวลาค้างหุบ (C2): {claw.HoldDurationSec:0.0}s", claw.HoldDurationSec, 0.3f, 3f);
            }

            // ===== สถานะ live =====
            GUILayout.Space(6);
            GUILayout.Label("<b>— สถานะ —</b>", Rich());
            if (grip != null)
            {
                GUILayout.Label($"Phase: {grip.Phase}   Holding: {grip.IsHolding}");
                GUILayout.Label($"มุมขา L/R: {grip.LeftHingeAngle:0.0}° / {grip.RightHingeAngle:0.0}°");
            }

            if (grip != null && grip.Settings != null && GUILayout.Button("รีเซ็ตเป็นค่าจาก Settings asset"))
            {
                grip.ApplyFromSettings();
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
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
