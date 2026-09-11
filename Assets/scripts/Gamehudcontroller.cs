using UnityEngine;
using TMPro;

/// <summary>
/// ควบคุม HUD ที่ลอยติดหน้าจอผู้เล่นตลอดเวลา (Canvas เป็นลูกของกล้อง)
/// แสดง 2 อย่าง:
///   1. สถานะขั้นตอน P.A.S.S. ปัจจุบัน (Pull / Squeeze / Sweep) — เปลี่ยนสีเมื่อทำสำเร็จ
///   2. ความคืบหน้ารวมของเกม (ดับไฟไปแล้วกี่จุดจากทั้งหมด) + ข้อความตอนชนะ
/// </summary>
public class GameHUDController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ถังดับเพลิงที่จะดึงสถานะ P.A.S.S. มาแสดง (ถ้ามีถังเดียวในเกม ลากตัวเดียวพอ)")]
    [SerializeField] private FireExtinguisherController extinguisherController;

    [Tooltip("GameManager ของเกม เพื่อดึงความคืบหน้ารวม")]
    [SerializeField] private GameManager gameManager;

    [Header("P.A.S.S Step Texts")]
    [SerializeField] private TextMeshProUGUI pullStepText;
    [SerializeField] private TextMeshProUGUI squeezeStepText;
    [SerializeField] private TextMeshProUGUI sweepStepText;

    [Header("Progress")]
    [SerializeField] private TextMeshProUGUI progressText;

    [Header("Win Banner")]
    [Tooltip("Panel/GameObject ที่จะโชว์ตอนดับไฟครบทุกจุด (ปิดไว้ก่อนโดย default)")]
    [SerializeField] private GameObject winBannerPanel;
    [SerializeField] private TextMeshProUGUI winTimeText;

    [Header("Colors")]
    [SerializeField] private Color pendingColor = Color.gray;
    [SerializeField] private Color completedColor = Color.green;

    private void OnEnable()
    {
        if (extinguisherController != null)
        {
            extinguisherController.onPassStepChanged.AddListener(OnPassStepChanged);
        }

        if (gameManager != null)
        {
            gameManager.onFireExtinguishedProgress.AddListener(OnProgressChanged);
            gameManager.onGameWon.AddListener(OnGameWon);
        }
    }

    private void OnDisable()
    {
        if (extinguisherController != null)
        {
            extinguisherController.onPassStepChanged.RemoveListener(OnPassStepChanged);
        }

        if (gameManager != null)
        {
            gameManager.onFireExtinguishedProgress.RemoveListener(OnProgressChanged);
            gameManager.onGameWon.RemoveListener(OnGameWon);
        }
    }

    private void Start()
    {
        // ตั้งค่าเริ่มต้นให้ทุก step เป็นสีเทา (ยังไม่ทำ) และซ่อน win banner
        SetStepColor(pullStepText, false);
        SetStepColor(squeezeStepText, false);
        SetStepColor(sweepStepText, false);

        if (winBannerPanel != null)
        {
            winBannerPanel.SetActive(false);
        }

        if (gameManager != null)
        {
            OnProgressChanged(gameManager.ExtinguishedCount, gameManager.TotalFireCount);
        }
    }

    /// <summary>
    /// เรียกทุกครั้งที่สถานะ P.A.S.S. ของถังเปลี่ยน (Pull / Squeeze / Sweep)
    /// อ้างอิง enum จาก FireExtinguisherController.PassStep
    /// </summary>
    private void OnPassStepChanged(FireExtinguisherController.PassStep step)
    {
        switch (step)
        {
            case FireExtinguisherController.PassStep.PinPulled:
                SetStepColor(pullStepText, true);
                break;

            case FireExtinguisherController.PassStep.Spraying:
                SetStepColor(pullStepText, true);
                SetStepColor(squeezeStepText, true);
                break;

            case FireExtinguisherController.PassStep.SweepDetected:
                SetStepColor(pullStepText, true);
                SetStepColor(squeezeStepText, true);
                SetStepColor(sweepStepText, true);
                break;
        }
    }

    private void SetStepColor(TextMeshProUGUI text, bool completed)
    {
        if (text == null) return;
        text.color = completed ? completedColor : pendingColor;
    }

    private void OnProgressChanged(int extinguished, int total)
    {
        if (progressText != null)
        {
            progressText.text = $"ไฟที่ดับแล้ว: {extinguished} / {total}";
        }
    }

    private void OnGameWon(float elapsedTime)
    {
        if (winBannerPanel != null)
        {
            winBannerPanel.SetActive(true);
        }

        if (winTimeText != null)
        {
            int minutes = Mathf.FloorToInt(elapsedTime / 60f);
            int seconds = Mathf.FloorToInt(elapsedTime % 60f);
            winTimeText.text = $"ดับไฟสำเร็จ! ใช้เวลา {minutes:00}:{seconds:00}";
        }
    }
}