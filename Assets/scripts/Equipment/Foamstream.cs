using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ติดสคริปต์นี้ไว้ที่หัวฉีดของถังดับเพลิง (ปลายท่อ)
/// ทำงานคู่กับ Particle System ที่เป็นสาย "โฟม" ที่พ่นออกมา
/// ใช้ Particle Collision เพื่อเช็คว่าโฟมไปโดน FireSource จุดไหนบ้าง
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class FoamStream : MonoBehaviour
{
    [Tooltip("ถ้า true = กำลังบีบด้ามถังอยู่ (ควบคุมจาก script ถังดับเพลิงหลัก)")]
    [SerializeField] private bool isSpraying = false;

    private ParticleSystem foamParticles;
    private List<ParticleCollisionEvent> collisionEvents = new List<ParticleCollisionEvent>();

    private void Awake()
    {
        foamParticles = GetComponent<ParticleSystem>();

        // ต้องเปิด Collision module ใน Particle System (ดูวิธีตั้งค่าด้านล่าง)
        // และ Send Collision Messages ต้องติ๊กไว้ ถึงจะเรียก OnParticleCollision ได้
    }

    /// <summary>
    /// เรียกจากสคริปต์ถังดับเพลิงหลัก เมื่อผู้เล่นบีบ/ปล่อยด้าม (Squeeze step ของ P.A.S.S.)
    /// </summary>
    public void SetSpraying(bool spraying)
    {
        isSpraying = spraying;

        var emission = foamParticles.emission;
        emission.enabled = spraying;

        if (spraying && !foamParticles.isPlaying)
        {
            foamParticles.Play();
        }
        else if (!spraying)
        {
            // ใช้ Stop แบบ StopEmitting เพื่อให้อนุภาคที่พ่นไปแล้วยังลอยไปจนจบ ไม่ตัดดื้อๆ
            foamParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    // Unity จะเรียก method นี้อัตโนมัติเมื่อ particle ชนกับ Collider
    // (ต้องตั้งค่า Collision module ของ Particle System ก่อน)
    private void OnParticleCollision(GameObject other)
    {
        if (!isSpraying) return;

        FireSource fire = other.GetComponent<FireSource>();
        if (fire == null)
        {
            // เผื่อ Collider อยู่ที่ child/parent ไม่ตรงตัว GameObject ที่ชน
            fire = other.GetComponentInParent<FireSource>();
        }

        if (fire != null && !fire.IsExtinguished)
        {
            fire.ApplyFoamHit(Time.deltaTime);
        }
    }
}