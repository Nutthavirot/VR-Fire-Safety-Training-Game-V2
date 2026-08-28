using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// ติดสคริปต์นี้ไว้ที่ตัว "สลักนิรภัย" (Safety Pin) ซึ่งเป็น GameObject ลูกของถังดับเพลิง
/// สลักต้องมี XRGrabInteractable ของตัวเองแยกจากตัวถัง เพื่อให้ผู้เล่นหยิบดึงออกได้อิสระ
///
/// วิธีทำงาน: เมื่อผู้เล่นหยิบสลัก (SelectEntered) และดึงออกห่างจากตำแหน่งเดิมเกินระยะที่กำหนด
/// จะถือว่า "ดึงสลักสำเร็จ" (Pull step ผ่าน) แล้วยิง event ออกไปให้ FireExtinguisherController ฟัง
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class SafetyPin : MonoBehaviour
{
    [Header("Pull Settings")]
    [Tooltip("ระยะห่างจากตำแหน่งเดิม (เมตร) ที่ถือว่าดึงสลักออกสำเร็จ")]
    [SerializeField] private float pullDistanceThreshold = 0.15f;

    [Header("Events")]
    public UnityEvent onPinPulled;

    private XRGrabInteractable grabInteractable;
    private Vector3 originalLocalPosition;
    private Transform originalParent;
    private bool isPulled = false;
    private bool isHeld = false;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        originalLocalPosition = transform.localPosition;
        originalParent = transform.parent;
    }

    private void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnGrabbed);
    }

    private void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrabbed);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        isHeld = true;
    }

    private void Update()
    {
        if (isPulled || !isHeld) return;

        // เช็คระยะห่างจากตำแหน่งเดิม (ใช้ world position เทียบกับตำแหน่งเดิมของ parent เดิม)
        Vector3 originalWorldPos = originalParent != null
            ? originalParent.TransformPoint(originalLocalPosition)
            : originalLocalPosition;

        float distance = Vector3.Distance(transform.position, originalWorldPos);

        if (distance >= pullDistanceThreshold)
        {
            PullPin();
        }
    }

    private void PullPin()
    {
        isPulled = true;
        onPinPulled?.Invoke();

        // ตัดสลักออกจากการเป็นลูกของถัง ให้มันเป็นอิสระ (ผู้เล่นถือแยกได้ หรือจะปล่อยทิ้งก็ได้)
        transform.SetParent(null);
    }

    public bool IsPulled => isPulled;
}