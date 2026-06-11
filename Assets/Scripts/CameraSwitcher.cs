using UnityEngine;

namespace ClawMachine
{
    /// <summary>
    /// สลับมุมกล้องหน้า/ข้าง เพื่อช่วยเล็งพิกัด X (หน้า) และ Z/ความลึก (ข้าง)
    /// เทคนิคดู 2 มุมคือ skill gap ใหญ่สุดของผู้เล่นตู้คีบจริง
    /// </summary>
    public class CameraSwitcher : MonoBehaviour
    {
        [Header("กล้อง 2 มุม")]
        [SerializeField] private Camera frontCamera;
        [SerializeField] private Camera sideCamera;

        [Header("Input")]
        [SerializeField] private KeyCode switchKey = KeyCode.C;

        private bool sideActive;

        private void Start()
        {
            Apply();
        }

        private void Update()
        {
            if (Input.GetKeyDown(switchKey))
            {
                sideActive = !sideActive;
                Apply();
            }
        }

        private void Apply()
        {
            if (frontCamera != null) frontCamera.enabled = !sideActive;
            if (sideCamera != null) sideCamera.enabled = sideActive;
        }
    }
}
