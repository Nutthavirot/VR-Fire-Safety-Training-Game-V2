using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// ตัวกลางคุมเกมทั้งหมด — ติดไว้ที่ GameObject เดียวใน Scene (เช่น ชื่อ "GameManager")
/// หน้าที่หลัก:
///   1. รวบรวม FireSource ทุกจุดในฉาก (หาอัตโนมัติ ไม่ต้องลากใส่ทีละอัน)
///   2. ฟัง event ตอนไฟแต่ละจุดดับ นับจำนวนไฟที่เหลือ
///   3. จับเวลาเล่นทั้งหมด
///   4. เมื่อไฟดับครบทุกจุด → จบเกม ยิง event สรุปผลออกไปให้ UI ใช้
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum GameState { NotStarted, Playing, Finished }

    [Header("Fire Sources")]
    [Tooltip("ถ้าปล่อยว่างไว้ จะค้นหา FireSource ทุกตัวในฉากให้อัตโนมัติตอนเริ่มเกม")]
    [SerializeField] private List<FireSource> fireSources = new List<FireSource>();

    [Header("Game Settings")]
    [Tooltip("เวลาสูงสุดที่ให้เล่น (วินาที) ถ้าเกินจะจบเกมแบบ 'หมดเวลา' ใส่ 0 = ไม่จำกัดเวลา")]
    [SerializeField] private float timeLimit = 0f;

    [Header("Events")]
    [Tooltip("เรียกตอนเกมเริ่ม")]
    public UnityEvent onGameStart;

    [Tooltip("เรียกทุกครั้งที่ไฟจุดใดจุดหนึ่งดับ ส่งค่า (จำนวนที่ดับแล้ว, จำนวนทั้งหมด)")]
    public UnityEvent<int, int> onFireExtinguishedProgress;

    [Tooltip("เรียกตอนไฟดับครบทุกจุด (ชนะ) ส่งเวลาที่ใช้ทั้งหมดเป็นวินาที")]
    public UnityEvent<float> onGameWon;

    [Tooltip("เรียกตอนหมดเวลา (ถ้าตั้ง Time Limit ไว้)")]
    public UnityEvent onGameTimeUp;

    [Tooltip("เรียกทุกเฟรมตอนกำลังเล่นอยู่ ส่งเวลาที่ผ่านไปเป็นวินาที (ให้ UI เอาไปโชว์นาฬิกาจับเวลา)")]
    public UnityEvent<float> onTimeUpdated;

    private int totalFireCount;
    private int extinguishedCount;
    private float elapsedTime;
    private GameState currentState = GameState.NotStarted;

    private void Awake()
    {
        // ถ้าไม่ได้ลาก FireSource มาเองใน Inspector ให้หาทุกตัวในฉากอัตโนมัติ
        if (fireSources == null || fireSources.Count == 0)
        {
            fireSources = FindObjectsByType<FireSource>(FindObjectsSortMode.None).ToList();
        }

        totalFireCount = fireSources.Count;
    }

    private void Start()
    {
        StartGame();
    }

    /// <summary>
    /// เริ่มเกม — สมัครฟัง event ของ FireSource ทุกตัว
    /// </summary>
    public void StartGame()
    {
        if (totalFireCount == 0)
        {
            Debug.LogWarning("GameManager: ไม่พบ FireSource ในฉากเลย ตรวจสอบว่าใส่ FireSource ในห้องแล้วหรือยัง");
        }

        extinguishedCount = 0;
        elapsedTime = 0f;
        currentState = GameState.Playing;

        foreach (var fire in fireSources)
        {
            if (fire != null)
            {
                fire.onFireExtinguished.AddListener(OnOneFireExtinguished);
            }
        }

        onGameStart?.Invoke();
        onFireExtinguishedProgress?.Invoke(extinguishedCount, totalFireCount);
    }

    private void Update()
    {
        if (currentState != GameState.Playing) return;

        elapsedTime += Time.deltaTime;
        onTimeUpdated?.Invoke(elapsedTime);

        if (timeLimit > 0f && elapsedTime >= timeLimit)
        {
            EndGameByTimeUp();
        }
    }

    private void OnOneFireExtinguished()
    {
        if (currentState != GameState.Playing) return;

        extinguishedCount++;
        onFireExtinguishedProgress?.Invoke(extinguishedCount, totalFireCount);

        if (extinguishedCount >= totalFireCount)
        {
            EndGameByWin();
        }
    }

    private void EndGameByWin()
    {
        currentState = GameState.Finished;
        onGameWon?.Invoke(elapsedTime);
    }

    private void EndGameByTimeUp()
    {
        currentState = GameState.Finished;
        onGameTimeUp?.Invoke();
    }

    /// <summary>
    /// เรียกจากปุ่ม UI (เช่น "เล่นใหม่") เพื่อรีเซ็ตสถานะเกมทั้งหมด
    /// หมายเหตุ: ไม่ได้รีเซ็ต HP ของไฟให้อัตโนมัติ ถ้าต้องการ reload scene จะง่ายกว่า
    /// </summary>
    public void RestartGame()
    {
        foreach (var fire in fireSources)
        {
            if (fire != null)
            {
                fire.onFireExtinguished.RemoveListener(OnOneFireExtinguished);
            }
        }

        StartGame();
    }

    public GameState CurrentState => currentState;
    public int ExtinguishedCount => extinguishedCount;
    public int TotalFireCount => totalFireCount;
    public float ElapsedTime => elapsedTime;
}