# PRD — MVP: ตู้คีบ 2 ขา คีบ Figure (สมจริง)

> Product Requirements Document สำหรับ MVP แรกของ Claw Machine Simulator
> อ้างอิงกลไกจาก [research-claw-mechanics.md](research-claw-mechanics.md)
> Engine: Unity (C#) · Platform: PC (Windows) · Scope: Offline single-player

---

## 1. เป้าหมายของ MVP

สร้างตู้คีบ **1 ตู้ 2 ขา** ที่ผู้เล่นควบคุมและคีบ figure ได้จริง โดย**ฟิสิกส์สมจริงที่สุด** ตามกลไก UFO Catcher ญี่ปุ่น — เน้นความรู้สึก "คีบติดแล้วหลุด" ที่เป็นเอกลักษณ์ของตู้คีบจริง

**ไม่ใช่ scope ของ MVP นี้:** ระบบเงิน/economy, ร้านค้า/tycoon, multiplayer/server, Steam, ห้องโชว์ของสะสม, เสียง/กราฟิกขั้น polish

**Definition of Done:** ผู้เล่นเลื่อนขา → กดดิ่ง → ขาหุบ → ยก → เลื่อนไปท่อ → ของหลุด/ตกท่อ ตามแรงคีบที่ระบบกำหนด และรู้สึกได้ถึงความสมจริง (ของลื่น, หลุดตอนยก, payout cycle)

---

## 2. User Story หลัก

> "ในฐานะผู้เล่น ฉันเลื่อนขาคีบไปเหนือ figure ที่ต้องการ กดปุ่มให้ขาดิ่งลงไปหุบ แล้วลุ้นว่าตอนยกขึ้นของจะหลุดหรือไม่ — เหมือนเล่นตู้คีบจริงในญี่ปุ่น"

---

## 3. Features (MVP Scope)

### F1 — ระบบควบคุมขา 3 จังหวะ (Core)
| จังหวะ | input | พฤติกรรม |
|---|---|---|
| Phase 1 — เล็ง | Arrow keys (X/Z) | เลื่อน gantry ซ้าย-ขวา-หน้า-หลัง ในขอบเขตตู้, ขากางเปิด |
| Phase 2 — ดิ่ง | Spacebar | ขาดิ่งลงแกน Y จนชนของ/พื้น (auto) |
| Phase 3 — เก็บ | (auto) | ขาหุบ → ยกขึ้น → เลื่อนกลับ home เหนือท่อ → ขากาง |

- ความเร็ว: เลื่อน 0.07 m/s, ดิ่ง 0.20 m/s, ยก 0.14 m/s
- เลื่อนได้เฉพาะ Phase 1 เท่านั้น (ระหว่าง Phase 2-3 ล็อก input)
- หนึ่งรอบจบ → กลับ Phase 1 พร้อมเล่นใหม่

### F2 — ระบบแรงคีบ 4 phase (C1–C4) ⭐ หัวใจ
- **C1** ตอนถึงล่างสุด: หุบแรงเต็ม
- **C2** hold 1.0–1.4 วินาที
- **C3** ตอนยก: แรงตามรอบ (ปกติ 15% / payout 100%)
- **C4** ตอนเลื่อน: แรงระดับกลาง
- จำลองแรงผ่าน joint break force / grip constraint — ถ้าน้ำหนัก × gravity เกินแรงยึด → ของลื่นหลุด

### F3 — ระบบ Payout Cycle
- ตั้งค่า `payoutEveryN` (default 12) — ทุกครั้งที่ N ขาจะใช้แรง C3 เต็ม
- รอบอื่น C3 อ่อน → คีบยาก/หลุด
- โหมด debug: แสดง counter ปัจจุบัน + บังคับ payout ได้ (สำหรับเทสต์)

### F4 — Figure + ฟิสิกส์ของรางวัล (แบบ 橋渡し Hashi-watashi)
- กล่อง figure ทรงสี่เหลี่ยมผืนผ้า (ด้านยาว 0.14 / ด้านแคบ 0.07) Rigidbody + Box Collider
- **วางพาดขวางคาน 2 อัน** ปลายเกยคาน กลางลอยเหนือช่อง
- น้ำหนัก ~0.3 kg, PhysicsMaterial friction ต่ำ (0.15/0.20)
- ช่องระหว่างคาน 0.10 > ด้านแคบกล่อง → พอกล่องหมุนจนขนานคานก็ร่วง (tatehame)
- วางหลายมุมไล่ความยาก (ตั้งฉาก=ยาก → เกือบขนาน=ง่าย)

### F5 — ช่องรับของใต้คาน (Win Zone)
- Trigger zone ใต้ช่องระหว่างคาน — กล่องร่วงผ่านคานลงมาโดน → "คีบได้" → counter +1
- คลุมเฉพาะใต้ช่อง ไม่คลุมพื้นด้านนอก (ของที่หล่นออกข้างไม่นับ)
- ตัวหนีบ **ปล่อยตรงจุดที่คีบ** (`returnToChute = false`) ไม่ลากไปมุม

### F6 — กล้อง 2 มุม
- มุมหน้า (default) — เล็งแกน X
- มุมข้าง (toggle ปุ่ม C) — เล็งแกน Z/ความลึก

### F7 — Debug HUD (ช่วยพัฒนา/จูน)
- แสดง: phase ปัจจุบัน, play count, payout in N, แรง C3 ปัจจุบัน, สถานะจับ (holding/dropped)

---

## 4. ฟิสิกส์ที่ต้องจูนให้สมจริง

| พารามิเตอร์ | ค่าเริ่มต้น | หมายเหตุ |
|---|---|---|
| Fixed Timestep | 0.01–0.02 | ชนแม่นยำ ขาไม่ทะลุของ |
| Solver Iterations | สูงขึ้น (≥12) | joint นิ่ง |
| grip C1 force | พอยก 0.6 kg | แรงเต็ม |
| grip C3 normal | 15% C1 | ของ >0.1 kg หลุด |
| grip C3 payout | 100% C1 | คีบได้ |
| C2 hold | 1.0–1.4 s | ตามขนาด |
| metal-on-plastic | 0.15 / 0.20 | ลื่น |
| collision detection | Continuous (ของ) | กันทะลุตอนเร็ว |

---

## 5. สถาปัตยกรรมโค้ด (Scripts)

```
Assets/Scripts/
├── ClawController.cs      // state machine 3 จังหวะ + input + การเคลื่อนที่
├── ClawGripSystem.cs      // ระบบ C1–C4 + หุบ/กางขา + แรงยึด
├── PayoutManager.cs       // นับ play count + ตัดสินรอบ payout
├── PrizeCatchZone.cs      // trigger ท่อรับของ
├── CameraSwitcher.cs      // สลับมุมหน้า/ข้าง
└── DebugHUD.cs            // แสดงสถานะ on-screen
```

**State machine (ClawController):**
```
Idle → Aiming(P1) → Dropping(P2) → Gripping(C1+C2) → Lifting(C3) → Returning(C4) → Releasing → Idle
```

---

## 6. Acceptance Criteria

- [ ] เลื่อนขาด้วย arrow keys ได้ลื่น อยู่ในขอบตู้
- [ ] กด space → ขาดิ่งลงหยุดเมื่อชนของ/พื้น
- [ ] ขาหุบรอบ figure แล้วยกขึ้น
- [ ] **รอบปกติ: ของหลุดระหว่างยก** (รู้สึกได้ว่าลื่น)
- [ ] **รอบ payout (ครั้งที่ N): คีบขึ้นถึงท่อสำเร็จ**
- [ ] ของตกท่อ → counter เพิ่ม + log
- [ ] สลับกล้องหน้า/ข้างได้
- [ ] Debug HUD แสดง phase + play count + payout countdown ถูกต้อง
- [ ] ฟิสิกส์ไม่บั๊ก (ขาไม่ทะลุของ, ของไม่กระเด็นหลุดตู้)

---

## 7. แผนพัฒนา (ลำดับงาน)

1. **โครงโปรเจกต์ + scripts** — ClawController state machine + การเคลื่อนที่ (ไม่มีฟิสิกส์ขาจริง)
2. **ClawGripSystem** — หุบ/กางขา + ระบบแรง C1–C4
3. **PayoutManager** — payout cycle
4. **PrizeCatchZone + DebugHUD** — detect + แสดงผล
5. **CameraSwitcher** — 2 มุม
6. **จูนฟิสิกส์** — friction, grip force, hold time ให้ "หลุดตอนยก" สมจริง

> หมายเหตุ: scene setup (โมเดลตู้, prefab figure, การ wire component) ต้องทำใน Unity Editor — scripts ออกแบบให้ assign ผ่าน Inspector ได้ง่าย
