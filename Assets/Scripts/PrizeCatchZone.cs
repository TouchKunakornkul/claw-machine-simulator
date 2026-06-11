using System;
using UnityEngine;

namespace ClawMachine
{
    /// <summary>
    /// ท่อรับของ — Trigger zone ที่มุมตู้
    /// เมื่อ Prize ตกเข้ามา นับว่า "คีบได้" แล้ว raise event
    /// ติดบน Collider ที่ติ๊ก Is Trigger
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PrizeCatchZone : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool logToConsole = true;

        public int TotalCaught { get; private set; }

        /// <summary>raise เมื่อมีของตกท่อ (ส่ง Prize ที่จับได้)</summary>
        public event Action<Prize> OnPrizeCaught;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var prize = other.GetComponentInParent<Prize>();
            if (prize == null) return;

            TotalCaught++;
            if (logToConsole)
                Debug.Log($"[Chute] คีบได้: {prize.displayName} ({prize.prizeId}) — รวม {TotalCaught} ชิ้น");

            OnPrizeCaught?.Invoke(prize);

            // MVP: เอาออกจากฉากหลังเก็บ (ยังไม่ทำ inventory)
            Destroy(prize.gameObject);
        }
    }
}
