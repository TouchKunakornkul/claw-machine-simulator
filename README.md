# Claw Machine Simulator

เกมจำลองตู้คีบฟิสิกส์สมจริง สไตล์ UFO Catcher ญี่ปุ่น — Unity 6 (C#)

## สถานะปัจจุบัน: MVP ตู้คีบ 2 ขา (คีบ figure)

จำลองกลไกหัวใจของตู้คีบจริง: ระบบแรงคีบ 4 phase (C1–C4) ที่ "คีบติดแล้วหลุด" + payout cycle

## เริ่มเล่น (3 ขั้นตอน)

1. เปิด **Unity Hub** → **Add** → เลือกโฟลเดอร์นี้ → เปิดด้วย Unity **6000.0.77f1**
2. รอ import เสร็จ → เมนูบนสุด **Claw Machine → Build MVP Scene**
3. กด **Play**

### ปุ่มควบคุม
| ปุ่ม | การทำงาน |
|---|---|
| ลูกศร | เลื่อนขา ซ้าย-ขวา-หน้า-หลัง (Phase 1) |
| Space | ดิ่งลงคีบ (Phase 2 → 3 อัตโนมัติ) |
| C | สลับกล้องหน้า/ข้าง (เล็งความลึก) |
| P | บังคับ payout รอบหน้า (debug) |

> รอบปกติของจะหลุดตอนยก — รอบที่ N (default 12) หรือกด `P` ถึงจะคีบติดถึงท่อ

## โครงสร้าง

```
docs/
  research-claw-mechanics.md   งานวิจัยกลไกตู้คีบญี่ปุ่น (C1–C4, payout, ฟิสิกส์)
  prd-mvp.md                   ขอบเขต MVP + acceptance criteria
  unity-setup-guide.md         คู่มือต่อ scene ด้วยมือ (ถ้าไม่ใช้ auto-builder)
Assets/
  Scripts/                     โค้ดเกมหลัก
    ClawController.cs           state machine 3 จังหวะ
    ClawGripSystem.cs           ระบบแรงคีบ C1–C4 (หัวใจ)
    PayoutManager.cs            payout cycle
    PrizeCatchZone.cs           ท่อรับของ
    Prize.cs / CameraSwitcher.cs / DebugHUD.cs
  Editor/
    ClawSceneBuilder.cs         สร้าง scene อัตโนมัติ (เมนู Claw Machine)
```

## จุดจูนให้สมจริง (หลัง build)

เลือก GameObject `ClawMachine/Gantry/ClawHead` (component **ClawGripSystem**) แล้วปรับ:
- `forceC3Normal` — แรงตอนยกรอบปกติ (ยิ่งต่ำของยิ่งหลุดง่าย)
- `holdDuration` (บน ClawController) — เวลาค้างก่อนยก
- friction ปรับที่ `Assets/Physics/LowFriction`

ดูรายละเอียดใน [docs/unity-setup-guide.md](docs/unity-setup-guide.md) หัวข้อ 6
