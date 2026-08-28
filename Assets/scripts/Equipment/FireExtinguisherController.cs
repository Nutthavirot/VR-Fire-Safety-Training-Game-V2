using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// ติดสคริปต์นี้ไว้ที่ตัวถังดับเพลิงหลัก (GameObject เดียวกับ XR Grab Interactable ของถัง)
/// ควบคุมขั้นตอน P.A.S.S. ทั้งหมด:
///   P - Pull: ฟัง event จาก SafetyPin
///   A - Aim: เช็คว่าหัวฉีดเล็งไปทางไฟหรือไม่ (ใช้ประกอบการให้คะแนน ไม่ได้บังคับ)
///   S - Squeeze: รับ input จากปุ่ม Activate (trigger) ของ XR controller ตอนถือถังอยู่
///   S - Sweep: ตรวจจับการส่ายถังซ้าย-ขวาขณะกำลังพ่นโฟม
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class FireExtinguisherController : MonoBehaviour
{
    public enum PassStep { NotStarted, PinPulled, Spraying, SweepDetected }

    [Header("References")]
    [Tooltip("จุดหัวฉีดของถัง ใช้เป็นทิศทางอ้างอิงสำหรับ Aim/Sweep")]
    [SerializeField] private Transform nozzleTransform;

    [Tooltip("สคริปต์ควบคุมสายโฟม (ติดอยู่ที่ ParticleSystem ของโฟม)")]
    [SerializeField] private FoamStream foamStream;

    [Tooltip("สคริปต์สลักนิรภัย ต้องดึงออกก่อนถึงจะพ่นได้")]
    [SerializeField] private SafetyPin safetyPin;

    [Header("Sweep Settings")]
    [Tooltip("มุมส่าย (องศา) ขั้นต่ำต่อครั้งที่นับว่าเป็นการ Sweep")]
    [SerializeField] private float sweepAngleThreshold = 15f;

    [Tooltip("จำนวนครั้งที่ต้องส่ายสลับซ้าย-ขวา ถึงจะถือว่า 'ทำ Sweep ถูกต้อง'")]
    [SerializeField] private int sweepCountRequired = 3;

    [Header("Events")]
    public UnityEvent onPassStepPull;
    public UnityEvent onPassStepSqueeze;
    public UnityEvent onPassStepSweepConfirmed;
    public UnityEvent<PassStep> onPassStepChanged;

    private XRGrabInteractable grabInteractable;
    private bool isPinPulled = false;
    private bool isSqueezing = false;
    private bool isHeld = false;

    // สำหรับตรวจจับ sweep
    private float lastYaw;
    private float accumulatedSweepAngle = 0f;
    private int sweepDirection = 0; // -1 = ซ้าย, 1 = ขวา, 0 = ยังไม่ขยับ
    private int sweepReversalCount = 0;
    private bool sweepConfirmedThisSpray = false;

    private PassStep currentStep = PassStep.NotStarted;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
        grabInteractable.activated.AddListener(OnActivated);     // กด trigger (Squeeze เริ่ม)
        grabInteractable.deactivated.AddListener(OnDeactivated); // ปล่อย trigger (Squeeze หยุด)

        if (safetyPin != null)
        {
            safetyPin.onPinPulled.AddListener(OnPinPulled);
        }
    }

    private void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        grabInteractable.selectExited.RemoveListener(OnReleased);
        grabInteractable.activated.RemoveListener(OnActivated);
        grabInteractable.deactivated.RemoveListener(OnDeactivated);

        if (safetyPin != null)
        {
            safetyPin.onPinPulled.RemoveListener(OnPinPulled);
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        isHeld = true;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        isHeld = false;
        StopSpraying();
    }

    private void OnPinPulled()
    {
        isPinPulled = true;
        SetStep(PassStep.PinPulled);
        onPassStepPull?.Invoke();
    }

    // เรียกจาก XRGrabInteractable เมื่อผู้เล่นกดปุ่ม Activate (โดยปกติคือ trigger) ขณะถือถังอยู่
    private void OnActivated(ActivateEventArgs args)
    {
        if (!isHeld) return;

        // บังคับตามลำดับ P.A.S.S. — ต้องดึงสลักก่อนถึงจะพ่นได้ (Aim ไม่บังคับ เพราะเช็คยาก ปล่อยเป็น bonus score)
        if (!isPinPulled)
        {
            Debug.Log("ต้องดึงสลักนิรภัยก่อนถึงจะพ่นโฟมได้");
            return;
        }

        StartSpraying();
    }

    private void OnDeactivated(DeactivateEventArgs args)
    {
        StopSpraying();
    }

    private void StartSpraying()
    {
        if (isSqueezing) return;

        isSqueezing = true;
        sweepConfirmedThisSpray = false;
        sweepReversalCount = 0;
        sweepDirection = 0;
        accumulatedSweepAngle = 0f;
        lastYaw = GetCurrentYaw();

        SetStep(PassStep.Spraying);
        onPassStepSqueeze?.Invoke();

        if (foamStream != null)
        {
            foamStream.SetSpraying(true);
        }
    }

    private void StopSpraying()
    {
        if (!isSqueezing) return;

        isSqueezing = false;

        if (foamStream != null)
        {
            foamStream.SetSpraying(false);
        }
    }

    private void Update()
    {
        if (isSqueezing)
        {
            TrackSweep();
        }
    }

    /// <summary>
    /// ตรวจจับการส่ายถังซ้าย-ขวา โดยดูการเปลี่ยนทิศทางของมุม yaw ของหัวฉีด
    /// นับจำนวนครั้งที่ "กลับทิศ" (จากซ้ายไปขวา หรือขวาไปซ้าย) หลังจากส่ายไปสุดมุมที่กำหนด
    /// </summary>
    private void TrackSweep()
    {
        float currentYaw = GetCurrentYaw();
        float deltaYaw = Mathf.DeltaAngle(lastYaw, currentYaw);
        lastYaw = currentYaw;

        accumulatedSweepAngle += deltaYaw;

        int currentDirection = deltaYaw > 0.05f ? 1 : (deltaYaw < -0.05f ? -1 : 0);

        // ถ้าส่ายไปทางเดิมสะสมมุมเกิน threshold แล้วเปลี่ยนทิศ ให้นับเป็น 1 sweep
        if (currentDirection != 0 && sweepDirection != 0 && currentDirection != sweepDirection
            && Mathf.Abs(accumulatedSweepAngle) >= sweepAngleThreshold)
        {
            sweepReversalCount++;
            accumulatedSweepAngle = 0f;

            if (sweepReversalCount >= sweepCountRequired && !sweepConfirmedThisSpray)
            {
                sweepConfirmedThisSpray = true;
                SetStep(PassStep.SweepDetected);
                onPassStepSweepConfirmed?.Invoke();
            }
        }

        if (currentDirection != 0)
        {
            sweepDirection = currentDirection;
        }
    }

    private float GetCurrentYaw()
    {
        Transform reference = nozzleTransform != null ? nozzleTransform : transform;
        return reference.eulerAngles.y;
    }

    private void SetStep(PassStep step)
    {
        currentStep = step;
        onPassStepChanged?.Invoke(step);
    }

    public PassStep CurrentStep => currentStep;
    public bool IsPinPulled => isPinPulled;
    public bool IsSqueezing => isSqueezing;
    public bool HasSweptCorrectly => sweepConfirmedThisSpray;
}