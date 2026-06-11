using UnityEngine;

namespace ClawMachine
{
    /// <summary>
    /// HUD แสดงสถานะระบบสำหรับพัฒนา/จูนฟิสิกส์
    /// แสดง: phase ปัจจุบัน, play count, payout countdown, แรง C3, สถานะจับของ
    /// </summary>
    public class DebugHUD : MonoBehaviour
    {
        [SerializeField] private ClawController claw;
        [SerializeField] private ClawGripSystem grip;
        [SerializeField] private PayoutManager payout;
        [SerializeField] private PrizeCatchZone chute;

        [Header("ปุ่ม debug")]
        [SerializeField] private KeyCode forcePayoutKey = KeyCode.P;

        private GUIStyle style;

        private void Update()
        {
            if (payout != null && Input.GetKeyDown(forcePayoutKey))
                payout.ForceNextPayout();
        }

        private void OnGUI()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    normal = { textColor = Color.white }
                };
            }

            int untilPayout = payout != null ? payout.PlaysUntilPayout : 0;
            bool payoutSoon = untilPayout == 1;

            GUILayout.BeginArea(new Rect(12, 12, 360, 240), GUI.skin.box);
            GUILayout.Label("<b>CLAW DEBUG</b>", Rich(Color.cyan));
            if (claw != null)
                GUILayout.Label($"Phase: {claw.State}", style);
            if (payout != null)
            {
                GUILayout.Label($"Play count: {payout.PlayCount}", style);
                GUILayout.Label(
                    $"Payout in: {untilPayout} (ทุก {payout.PayoutEveryN})",
                    Rich(payoutSoon ? Color.green : Color.white));
            }
            if (grip != null)
                GUILayout.Label($"Grip: {grip.Phase}  Holding: {grip.IsHolding}", style);
            if (chute != null)
                GUILayout.Label($"คีบได้รวม: {chute.TotalCaught}", style);

            GUILayout.Space(6);
            GUILayout.Label($"[{forcePayoutKey}] บังคับจ่ายรอบหน้า", Rich(Color.gray));
            GUILayout.Label("[Arrows] เลื่อน  [Space] ดิ่ง  [C] สลับกล้อง", Rich(Color.gray));
            GUILayout.EndArea();
        }

        private GUIStyle Rich(Color c)
        {
            var s = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                richText = true,
                normal = { textColor = c }
            };
            return s;
        }
    }
}
