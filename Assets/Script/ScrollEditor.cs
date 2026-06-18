using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ScrollEditor : MonoBehaviour
{
    [Header("Grid Settings")]
    public int gridDivision = 4;
    public RectTransform content;
    public RectTransform Gridcontent;
    public GameObject gridLinePrefab;
    public NoteManager nm;

    [Header("Settings")]
    public float bpm = 120f;
    public float scrollSpeed = 600f;
    public int laneCount = 6;
    public float judgmentLineY = -380f;

    [Header("Scroll Speed UI")]
    public Slider speedSlider;
    public InputField speedInput;

    private const float BASE_SPEED = 200f;

    // [핵심 수정] beatHeight는 BPM만으로 결정되는 고정 픽셀값
    // scrollSpeed(viewScale)는 오직 화면 표시 배율에만 영향
    private const float PIXELS_PER_BEAT = 100f;
    public float beatHeight { get; private set; }   // BPM 기반 고정값 (타이밍 계산용)
    public float viewScale { get; private set; }    // 시각적 배율 (화면 표시용)

    private int lastGeneratedBeat = 0;

    public List<float> activeGridYPositions = new List<float>();
    private List<GameObject> gridPool = new List<GameObject>();
    private List<GameObject> activeGridObjects = new List<GameObject>();

    private float currentAudioTime = 0f;

    void Start()
    {
        UpdateBeatHeight();
        GenerateVerticalLanes();
        GenerateMoreGrid(30);
        InitScrollSpeedUI();
    }

    void Update()
    {
        HandleScrollAndZoom();
    }

    void LateUpdate()
    {
        //syncScrollToAudio(currentAudioTime);
    }

    public void InitScrollSpeedUI()
    {
        if (speedSlider != null)
        {
            speedSlider.minValue = 100f;
            speedSlider.maxValue = 2000f;
            speedSlider.value = scrollSpeed;
            speedSlider.onValueChanged.AddListener(OnScrollSpeedSliderChanged);
        }

        if (speedInput != null)
        {
            speedInput.text = (scrollSpeed / BASE_SPEED).ToString("F1");
            speedInput.onEndEdit.AddListener(OnScrollSpeedInputEndEdit);
        }
    }

    public void OnScrollSpeedSliderChanged(float value)
    {
        UpdateScrollSpeed(value);
        if (speedInput != null) speedInput.SetTextWithoutNotify((value / BASE_SPEED).ToString("F1"));
    }

    public void OnScrollSpeedInputEndEdit(string input)
    {
        if (float.TryParse(input, out float displayValue))
        {
            float newInternalSpeed = displayValue * BASE_SPEED;
            newInternalSpeed = Mathf.Clamp(newInternalSpeed, 100f, 2000f);
            UpdateScrollSpeed(newInternalSpeed);
            if (speedSlider != null) speedSlider.SetValueWithoutNotify(newInternalSpeed);
        }
    }

    private void UpdateScrollSpeed(float newInternalSpeed)
    {
        float oldViewScale = viewScale;
        scrollSpeed = newInternalSpeed;
        UpdateBeatHeight(); // viewScale 갱신

        // 그리드와 노트 위치를 새 viewScale에 맞춰 재설정
        SyncPositionsAfterZoom(oldViewScale);

        // 현재 재생 시간 기준으로 Content 위치 즉시 재계산
        SyncScrollToAudio(currentAudioTime);
        nm.UpdateGearVisualScale();
    }
    public void SetBPMAndRefresh(float newBpm)
    {
        this.bpm = newBpm;
        // BPM에 따른 새로운 속도 계산 (기존에 속도 계산하던 공식 적용)
        float newSpeed = (newBpm / 60f) * beatHeight * viewScale;
        UpdateScrollSpeed(newSpeed);
    }

    public void SyncScrollToAudio(float currentTime)
    {
        currentAudioTime = currentTime;

        float secondsPerBeat = 60f / bpm;
        float visualTime = currentTime + nm.songOffset;
        float currentBeat = visualTime / secondsPerBeat;

        Vector2 newPos = content.anchoredPosition;

        newPos.y = -(currentBeat * beatHeight * viewScale) + judgmentLineY;
        content.anchoredPosition = newPos;
    }

    public void UpdateNotePosition(RectTransform noteRect, float noteBeatIndex)
    {
        float yPos = noteBeatIndex * beatHeight * viewScale;
        noteRect.anchoredPosition = new Vector2(noteRect.anchoredPosition.x, yPos);
    }

    void UpdateBeatHeight()
    {
        beatHeight = (60f / bpm) * BASE_SPEED;
        viewScale = scrollSpeed / BASE_SPEED;
    }
    public void SyncPositionsAfterZoom(float oldViewScale)
    {
        if (oldViewScale == 0) return;

        float ratio = viewScale / oldViewScale;

        // 그리드 위치 재계산
        for (int i = 0; i < activeGridYPositions.Count; i++)
        {
            activeGridYPositions[i] *= ratio;
        }

        foreach (GameObject lineObj in activeGridObjects)
        {
            RectTransform rt = lineObj.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, rt.anchoredPosition.y * ratio);
        }

        // 노트 위치 재계산
        if (nm != null) nm.UpdateNoteScales(oldViewScale, viewScale);
    }

    // --- [이하 기존 로직 동일] ---
    public void OnBPMInputEndEdit(string input)
    {
        if (string.IsNullOrEmpty(input) || !float.TryParse(input, out float newBPM)) return;
        if (newBPM <= 0) return;

        float oldViewScale = viewScale;
        bpm = newBPM;
        UpdateBeatHeight();
        ClearGrid();
        int currentLimit = lastGeneratedBeat > 30 ? lastGeneratedBeat : 30;
        lastGeneratedBeat = 0;
        GenerateMoreGrid(currentLimit);
        if (nm != null) nm.UpdateNoteScales(oldViewScale, viewScale);
    }

    public void SetGridDivision(int division)
    {
        gridDivision = division;
        ClearGrid();
        int currentLimit = lastGeneratedBeat > 30 ? lastGeneratedBeat : 30;
        lastGeneratedBeat = 0;
        GenerateMoreGrid(currentLimit);
    }

    void ClearGrid()
    {
        activeGridYPositions.Clear();
        foreach (GameObject line in activeGridObjects)
        {
            line.SetActive(false);
            gridPool.Add(line);
        }
        activeGridObjects.Clear();
    }

    void GenerateMoreGrid(int count)
    {
        float step = 4f / gridDivision;
        for (int i = 0; i < count; i++)
        {
            lastGeneratedBeat++;
            for (float j = 0; j < 4f; j += step)
            {
                // [수정] 그리드 Y위치에 viewScale 적용
                float yPos = (lastGeneratedBeat + j) * beatHeight * viewScale;
                bool isMeasure = (lastGeneratedBeat + j) % 4 == 0;
                bool isBeat = j % 1 == 0;
                Color lineColor = isMeasure ? Color.white : (isBeat ? Color.gray : new Color(0.2f, 0.2f, 0.2f));
                float thickness = isMeasure ? 3f : (isBeat ? 1.5f : 0.8f);
                CreateLine(yPos, lineColor, thickness);
            }
        }
    }

    void CreateLine(float yPos, Color color, float thickness)
    {
        GameObject line;
        if (gridPool.Count > 0)
        {
            line = gridPool[0];
            gridPool.RemoveAt(0);
            line.SetActive(true);
        }
        else
        {
            line = Instantiate(gridLinePrefab, Gridcontent);
            line.name = "HorizontalLine";
        }
        RectTransform rt = line.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0, yPos);
        rt.sizeDelta = new Vector2(content.rect.width, thickness);
        line.GetComponent<Image>().color = color;
        activeGridObjects.Add(line);
        activeGridYPositions.Add(yPos);
    }

    void GenerateVerticalLanes()
    {
        float laneWidth = content.rect.width / laneCount;
        for (int i = 0; i <= laneCount; i++)
        {
            float xPos = (i * laneWidth) - (content.rect.width / 2f);
            GameObject vLine = Instantiate(gridLinePrefab, Gridcontent);
            vLine.name = "VerticalLine";
            RectTransform rt = vLine.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(xPos, 0);
            rt.sizeDelta = new Vector2(1.5f, 99999999f);
            vLine.GetComponent<Image>().color = new Color(1, 1, 1, 0.15f);
        }
    }

    void HandleScrollAndZoom()
    {
        float wheel = Input.mouseScrollDelta.y;
        if (wheel == 0) return;

        if (Input.GetKey(KeyCode.LeftControl))
        {
            float newSpeed = Mathf.Clamp(scrollSpeed + (wheel * 50f), 100f, 2000f);
            UpdateScrollSpeed(newSpeed);
            if (speedSlider != null) speedSlider.SetValueWithoutNotify(scrollSpeed);
            if (speedInput != null) speedInput.SetTextWithoutNotify((scrollSpeed / BASE_SPEED).ToString("F1"));
            return;
        }

        int direction = wheel > 0 ? -1 : 1;
        float moveAmount = (beatHeight * viewScale / 2f) * direction;
        Vector2 newPos = content.anchoredPosition;
        newPos.y += moveAmount;
        if (newPos.y > 0) newPos.y = 0;
        content.anchoredPosition = newPos;

        float currentViewBottom = -newPos.y;
        if (currentViewBottom > (lastGeneratedBeat * beatHeight * viewScale) - (beatHeight * viewScale * 10))
        {
            GenerateMoreGrid(20);
        }
    }
}
