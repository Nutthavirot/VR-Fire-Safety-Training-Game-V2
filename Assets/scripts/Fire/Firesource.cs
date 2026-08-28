using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ควบคุมพฤติกรรมของ "ไฟ" หนึ่งจุดในฉาก
/// - มี HP ที่ลดลงเมื่อโดนโฟมดับเพลิงที่ "โคนไฟ" (จุดที่มี Collider นี้ติดอยู่)
/// - ปรับขนาด/ความแรงของ particle ตาม HP ที่เหลือ เพื่อให้เห็นภาพว่าไฟกำลังเล็กลง
/// - ยิง event ออกไปตอนไฟดับ ให้ GameManager หรือ UI ไปฟังต่อได้
/// </summary>
[RequireComponent(typeof(Collider))]
public class FireSource : MonoBehaviour
{
    [Header("Fire Settings")]
    [Tooltip("HP เริ่มต้นของไฟจุดนี้")]
    [SerializeField] private float maxHealth = 100f;

    [Tooltip("อัตราการลด HP ต่อวินาที ขณะโดนโฟมพ่นใส่ต่อเนื่อง")]
    [SerializeField] private float damagePerSecond = 25f;

    [Tooltip("ถ้าไม่โดนโฟมนานเกินค่านี้ (วินาที) ไฟจะเริ่มฟื้น HP กลับคืน (สอนเรื่องต้องพ่นต่อเนื่อง)")]
    [SerializeField] private float regenDelay = 2f;

    [SerializeField] private float regenPerSecond = 5f;

    [Header("Visual")]
    [Tooltip("Particle System ของเปลวไฟ ที่จะปรับขนาดตาม HP")]
    [SerializeField] private ParticleSystem fireParticles;

    [Tooltip("Particle System ของควัน (ถ้ามี จะเพิ่มควันตอนไฟใกล้ดับ)")]
    [SerializeField] private ParticleSystem smokeParticles;

    [Header("Audio")]
    [SerializeField] private AudioSource fireAudioSource;

    [Header("Events")]
    public UnityEvent onFireExtinguished;
    public UnityEvent<float> onHealthChanged; // ส่งค่า HP % (0-1) ออกไปให้ UI ใช้

    private float currentHealth;
    private float lastHitTime = -999f;
    private bool isExtinguished = false;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void Start()
    {
        UpdateVisuals();
    }

    private void Update()
    {
        if (isExtinguished) return;

        // ถ้าไม่โดนโฟมมาสักพัก ให้ไฟฟื้น HP กลับ (กันคนพ่นมั่วๆ ไม่ตรงจุด)
        if (Time.time - lastHitTime > regenDelay && currentHealth < maxHealth)
        {
            currentHealth += regenPerSecond * Time.deltaTime;
            currentHealth = Mathf.Min(currentHealth, maxHealth);
            UpdateVisuals();
        }
    }

    /// <summary>
    /// เรียกจากสคริปต์โฟม (FoamProjectile หรือ FoamStream) ทุกครั้งที่ตรวจพบว่าโฟมโดนโคนไฟนี้
    /// </summary>
    public void ApplyFoamHit(float deltaTime)
    {
        if (isExtinguished) return;

        lastHitTime = Time.time;
        currentHealth -= damagePerSecond * deltaTime;
        currentHealth = Mathf.Max(currentHealth, 0f);

        UpdateVisuals();

        if (currentHealth <= 0f)
        {
            Extinguish();
        }
    }

    private void UpdateVisuals()
    {
        float healthPercent = currentHealth / maxHealth;
        onHealthChanged?.Invoke(healthPercent);

        if (fireParticles != null)
        {
            var main = fireParticles.main;
            // ปรับขนาดเปลวไฟตาม HP ที่เหลือ ให้เห็นว่าไฟเล็กลงเรื่อยๆ
            main.startSizeMultiplier = Mathf.Lerp(0.2f, 1f, healthPercent);

            var emission = fireParticles.emission;
            emission.rateOverTimeMultiplier = Mathf.Lerp(0.1f, 1f, healthPercent);
        }

        if (smokeParticles != null)
        {
            // ไฟใกล้ดับ (HP ต่ำ) ให้ควันเยอะขึ้นเป็นสัญญาณภาพ
            var emission = smokeParticles.emission;
            emission.rateOverTimeMultiplier = Mathf.Lerp(1.5f, 0.3f, healthPercent);
        }

        if (fireAudioSource != null)
        {
            fireAudioSource.volume = Mathf.Lerp(0f, 1f, healthPercent);
        }
    }

    private void Extinguish()
    {
        isExtinguished = true;

        if (fireParticles != null) fireParticles.Stop();
        if (smokeParticles != null) smokeParticles.Stop();
        if (fireAudioSource != null) fireAudioSource.Stop();

        onFireExtinguished?.Invoke();
    }

    public bool IsExtinguished => isExtinguished;
    public float HealthPercent => currentHealth / maxHealth;
}