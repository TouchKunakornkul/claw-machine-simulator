# คู่มือเซ็ต Scene ใน Unity (MVP ตู้คีบ 2 ขา)

> scripts อยู่ใน `Assets/Scripts/` ออกแบบให้ assign ผ่าน Inspector
> Unity version แนะนำ: 2021 LTS ขึ้นไป (built-in 3D physics)

---

## 1. Project Settings (สำคัญต่อความสมจริง)

**Edit > Project Settings > Time**
- Fixed Timestep = `0.01` (ชนแม่นยำ ขาไม่ทะลุของ)

**Edit > Project Settings > Physics**
- Default Solver Iterations = `12` ขึ้นไป
- Default Solver Velocity Iterations = `4` ขึ้นไป

**Layers** (Edit > Project Settings > Tags and Layers)
- เพิ่ม layer `Prize` สำหรับของรางวัล
- (option) layer `ClawArm` สำหรับขา

---

## 2. โครงสร้าง GameObject ใน Scene

```
ClawMachine (root)
├── Gantry                         ← ClawController.gantry (เลื่อน X/Z)
│   └── ClawHead                   ← ClawController.clawHead (ดิ่ง Y)
│       ├── GripSystem             ← ClawGripSystem (วางที่นี่หรือบน ClawHead)
│       │   ├── LeftArm            ← leftArm (pivot ขาซ้าย)
│       │   ├── RightArm           ← rightArm (pivot ขาขวา)
│       │   └── GrabPoint          ← grabPoint (empty กึ่งกลางปลายขา)
├── Cabinet (โมเดลตู้ + กระจก + ผนัง collider)
├── Floor (พื้นตู้ collider)
├── Chute (Trigger zone มุมตู้)    ← PrizeCatchZone
├── FrontCamera                    ← CameraSwitcher.frontCamera
├── SideCamera (disabled)          ← CameraSwitcher.sideCamera
└── Systems (empty)
    ├── PayoutManager
    ├── CameraSwitcher
    └── DebugHUD
```

---

## 3. ขั้นตอนเซ็ต (เร็วสุดด้วย primitive)

1. **ตู้:** สร้าง Cube ใหญ่เป็นกรอบ, ใส่ collider ผนัง 4 ด้าน + พื้น (กันของกระเด็นหลุด)
2. **Gantry:** Empty ที่ตำแหน่งบนตู้ — ใส่ `ClawController`
3. **ClawHead:** Empty ลูกของ Gantry — ตั้ง local Y = 0 (yTop)
4. **ขา 2 ข้าง:**
   - `LeftArm`, `RightArm` เป็น Cube ยาวๆ (หรือโมเดลขา) ลูกของ GripSystem
   - **จุด pivot สำคัญ:** วาง mesh ให้ปลายบนอยู่ที่ origin ของ transform (หมุนรอบ Z แล้วปลายล่างกาง/หุบ)
   - ใส่ Box/Mesh Collider ที่ขา + PhysicsMaterial friction ต่ำ (0.15/0.20)
5. **GrabPoint:** Empty กึ่งกลางระหว่างปลายขา 2 ข้าง
6. **GripSystem:** ใส่ `ClawGripSystem` assign leftArm/rightArm/grabPoint, ตั้ง prizeLayer = Prize
7. **Figure (Prize):**
   - Cube/โมเดล + `Rigidbody` (mass 0.3) + Convex Mesh/Box Collider
   - ใส่ `Prize.cs`, ตั้ง layer = `Prize`
   - Rigidbody > Collision Detection = `Continuous`
   - สร้าง prefab แล้ววางหลายตัวกองในตู้
8. **Chute:** Cube ที่มุมตู้ ติ๊ก `Is Trigger` + ใส่ `PrizeCatchZone`
9. **กล้อง:** FrontCamera (มองหน้าตู้), SideCamera (มองด้านข้าง, ปิด enabled ไว้)
10. **Systems:** Empty ใส่ `PayoutManager`, `CameraSwitcher`, `DebugHUD` แล้ว assign reference ให้ครบ

---

## 4. การ wire reference (Inspector)

**ClawController** (บน Gantry)
| field | assign |
|---|---|
| gantry | Gantry (ตัวเอง) |
| clawHead | ClawHead |
| gripSystem | GripSystem |
| payoutManager | PayoutManager |
| xLimits / zLimits | ปรับตามขนาดตู้ |
| yTop / yBottom | บนสุด / ล่างสุดของการดิ่ง |
| chuteHome | ตำแหน่ง X/Z เหนือ Chute |

**ClawGripSystem** (บน GripSystem)
| field | assign |
|---|---|
| leftArm / rightArm | LeftArm / RightArm |
| grabPoint | GrabPoint |
| prizeLayer | Prize |

**PrizeCatchZone** → อยู่บน Chute (auto ตั้ง isTrigger)
**CameraSwitcher** → assign frontCamera / sideCamera
**DebugHUD** → assign claw / grip / payout / chute

---

## 5. ทดสอบ (ตาม Acceptance Criteria ใน PRD)

1. กด Play → ขากางอยู่ที่บนสุด
2. Arrow keys เลื่อนขาเหนือ figure
3. Space → ขาดิ่งลงหยุดเมื่อชนของ
4. ขาหุบ ค้าง ~1.2s แล้วยก
5. **รอบปกติ: ของหลุดตอนยก** (ดู DebugHUD: Holding -> false)
6. กด `P` บังคับ payout → รอบถัดไปคีบติด ยกถึงท่อ ของตก → counter +1
7. กด `C` สลับกล้องหน้า/ข้าง

---

## 6. จุดจูนให้สมจริง (ลำดับความสำคัญ)

1. **forceC3Normal** (ใน ClawGripSystem) — ตั้งให้ของหลุดรอบปกติ แต่ไม่หลุดทันทีจนดูปลอม
2. **friction ขา** — ต่ำพอให้ลื่น (metal-on-plastic 0.15)
3. **holdDuration** — 1.0–1.4s ให้รู้สึกว่าคีบติดก่อนหลุด
4. **grabRadius** — ให้จับเฉพาะของที่อยู่ในวงหุบจริง
5. **descentSpeed / ascentSpeed** — ยกช้ากว่าดิ่งเล็กน้อย (ไม่เหวี่ยงของ)

> หมายเหตุระบบจับ (MVP): ClawGripSystem ใช้ "kinematic follow" — ตอนหุบจะ snap prize ที่ใกล้ grabPoint ให้ติดตามขา แล้วเช็คทุก FixedUpdate ว่า น้ำหนัก > แรงยึด หรือไม่ ถ้าเกินก็ปล่อยให้ของตกตาม physics จริง
> วิธีนี้คุมความรู้สึก "คีบติดแล้วหลุด" ได้ง่ายและเสถียรกว่าใช้ joint จริงในเฟสแรก — เฟสถัดไปค่อยอัปเกรดเป็น ConfigurableJoint ถ้าต้องการการงัด/หมุนแบบ tatehame
