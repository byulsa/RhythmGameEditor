using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using SFB;
using System.IO;
using Unity.VisualScripting;

[System.Serializable]
public class EventData
{
    public float time;
    public string type;
    public bool isOn;
    public int side;
    public float duration = 1.0f;
}

public class LongNoteGroup
{
    public GameObject startNode;
    public GameObject endNode;
    public GameObject bar;
    public int laneIndex;

    public void SetActive(bool active)
    {
        startNode.SetActive(active);
        endNode.SetActive(active);
        bar.SetActive(active);
    }
}

public class NoteManager : MonoBehaviour
{
    public ChartDataManager dataManager;
    public ScrollEditor scrollEditor;
    public RectTransform content;
    public RectTransform noteContainer;
    public GameObject notePrefab;
    public GameObject longBarPrefab;

    public int laneCount = 6;
    public float fixedNoteHeight = 20f;
    public float laneSpacing = 100f;
    [Header("Sync Settings")]
    public float songOffset = 0.0f;

    [Header("Mode Settings")]
    public bool isLongNoteMode = false;

    public List<GameObject> spawnedNotes = new List<GameObject>();
    public List<LongNoteGroup> spawnedLongNotes = new List<LongNoteGroup>();

    private List<GameObject> notePool = new List<GameObject>();
    private List<GameObject> barPool = new List<GameObject>();

    private GameObject pendingStartNode = null;

    [Header("Gear Event Settings")]
    public GearManager gearManager;
    public List<GameObject> spawnedGearMarkers = new List<GameObject>();
    public List<EventData> gearEvents = new List<EventData>();

    [Header("File Save Settings")]
    public InputField songNameInput;
    public InputField artistNameInput;
    public InputField bpmInput;
    public InputField levelInput;
    public Dropdown difficultyDropdown;
    public InputField OffsetInput;
    private string currentFilePath = string.Empty;

    [Header("Gear UI Settings Panel")]
    public GameObject gearSettingsPanel;
    public Slider durationSlider;
    public InputField durationInput;
    private EventData selectedGearEvent;
    public RectTransform songSliderRect;

    [Header("Gear Visual Settings")]
    public GameObject gearIndicatorPrefab;
    private List<GameObject> activeGearVisuals = new List<GameObject>();

    void Start()
    {
        if (durationSlider != null) durationSlider.onValueChanged.AddListener(OnDurationSliderChanged);
        if (durationInput != null) durationInput.onEndEdit.AddListener(OnDurationInputChanged);

        // BPM 입력이 완료되었을 때 위치를 재계산하도록 연결
        if (bpmInput != null) bpmInput.onEndEdit.AddListener(OnBPMInputFieldChanged);

        if (gearSettingsPanel != null) gearSettingsPanel.SetActive(false);
    }

    // --- [BPM 변경 대응 로직] ---

    public void OnBPMInputFieldChanged(string value)
    {
        if (float.TryParse(value, out float newBPM))
        {
            scrollEditor.bpm = newBPM;
            RefreshNotesPosition();
        }
    }
    public void OnOffsetInputFieldChanged(string value)
    {
        if (float.TryParse(value, out float newOffset))
        {
            songOffset = newOffset;
        }
    }

    public void RefreshNotesPosition()
    {
        // 1. 일반 노트 위치 갱신
        foreach (var noteObj in spawnedNotes)
        {
            if (noteObj == null) continue;
            NoteScript ns = noteObj.GetComponent<NoteScript>();
            float newY = ns.time * scrollEditor.scrollSpeed;
            noteObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(noteObj.GetComponent<RectTransform>().anchoredPosition.x, newY);
        }

        // 2. 롱노트 위치 및 바(Bar) 갱신
        foreach (var group in spawnedLongNotes)
        {
            if (group == null || group.startNode == null || group.endNode == null) continue;

            NoteScript sNs = group.startNode.GetComponent<NoteScript>();
            NoteScript eNs = group.endNode.GetComponent<NoteScript>();

            // 현재 scrollSpeed 기준 새 좌표 계산
            float newStartY = sNs.time * scrollEditor.scrollSpeed;
            float newEndY = eNs.time * scrollEditor.scrollSpeed;
            float xPos = group.startNode.GetComponent<RectTransform>().anchoredPosition.x;

            Vector2 startPos = new Vector2(xPos, newStartY);
            Vector2 endPos = new Vector2(xPos, newEndY);

            // 노드 위치 변경
            group.startNode.GetComponent<RectTransform>().anchoredPosition = startPos;
            group.endNode.GetComponent<RectTransform>().anchoredPosition = endPos;

            // [중요] 계산된 새 좌표로 바(Bar) 형태 업데이트
            if (group.bar != null)
            {
                UpdateBarTransform(group.bar.GetComponent<RectTransform>(), startPos, endPos);
            }
        }
    }

    // --- [저장/불러오기] ---

    public void OnFastSave()
    {
        if (string.IsNullOrEmpty(currentFilePath))
        {
            // 저장된 경로가 없으면 기존처럼 브라우저 열기
            SaveCurrentChart();
        }
        else
        {
            // 기존 경로에 바로 저장
            SaveToPath(currentFilePath);
            Debug.Log($"<color=cyan>[Fast Save] {currentFilePath}</color>");
        }
    }
    public void SaveCurrentChart()
    {
        var extensions = new[] { new ExtensionFilter("Chart Files", "json") };
        var path = StandaloneFileBrowser.SaveFilePanel("Save Chart", "", "NewChart", extensions);

        if (!string.IsNullOrEmpty(path))
        {
            currentFilePath = path; // 경로 기억
            SaveToPath(path);
        }
    }

    public void SaveToPath(string path)
    {
        var extensions = new[] { new ExtensionFilter("Chart Files", "json") };
        // var path = StandaloneFileBrowser.SaveFilePanel("Save Chart", "", "NewChart", extensions);

        if (string.IsNullOrEmpty(path)) return;

        SaveData data = new SaveData();
        data.songName = string.IsNullOrEmpty(songNameInput.text) ? "NewSong" : songNameInput.text;
        data.bpm = scrollEditor.bpm;
        data.audioFileName = data.songName + ".mp3";
        data.Artist = artistNameInput.text;
        data.Level = int.TryParse(levelInput.text, out int lvl) ? lvl : 1;
        data.difficulty = difficultyDropdown.options[difficultyDropdown.value].text;
        data.songOffset = songOffset;

        foreach (var noteObj in spawnedNotes)
        {
            if (noteObj == null) continue;
            NoteScript ns = noteObj.GetComponent<NoteScript>();
            if (ns != null)
            {
                data.notes.Add(new NoteSaveData { time = ns.time, lane = ns.lane });
            }
        }

        foreach (var group in spawnedLongNotes)
        {
            if (group == null || group.startNode == null || group.endNode == null) continue;
            NoteScript sNs = group.startNode.GetComponent<NoteScript>();
            NoteScript eNs = group.endNode.GetComponent<NoteScript>();

            data.longNotes.Add(new LongNoteSaveData
            {
                startTime = sNs.time,
                endTime = eNs.time,
                lane = group.laneIndex
            });
        }

        data.notes.Sort((a, b) => a.time.CompareTo(b.time));
        data.longNotes.Sort((a, b) => a.startTime.CompareTo(b.startTime));

        data.gearEvents = new List<EventData>(this.gearEvents);
        data.gearEvents.Sort((a, b) => a.time.CompareTo(b.time));

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(path, json);

        Debug.Log("<color=green>[Save Success]</color>");
    }

    public void OnLoadButtonClick()
    {
        var extensions = new[] { new ExtensionFilter("Chart Files", "json") };
        var paths = StandaloneFileBrowser.OpenFilePanel("Open Chart", "", extensions, false);

        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            LoadChartFromFullPath(paths[0]);
        }
    }

    private void LoadChartFromFullPath(string path)
    {
        if (!File.Exists(path)) return;

        string json = File.ReadAllText(path);
        currentFilePath = path;
        SaveData data = JsonUtility.FromJson<SaveData>(json);
        if (data == null) return;

        ClearAll();

        songNameInput.text = data.songName;
        artistNameInput.text = data.Artist;
        bpmInput.text = data.bpm.ToString();
        levelInput.text = data.Level.ToString();
        this.songOffset = data.songOffset;
        difficultyDropdown.value = difficultyDropdown.options.FindIndex(option => option.text == data.difficulty);

        scrollEditor.SetBPMAndRefresh(data.bpm);
        foreach (var n in data.notes)
        {
            float xPos = GetXFromLane(n.lane);
            float yPos = n.time * scrollEditor.scrollSpeed;
            spawnedNotes.Add(CreateNodeObj(xPos, yPos, n.lane, n.time));
        }

        foreach (var ln in data.longNotes)
        {
            float xPos = GetXFromLane(ln.lane);
            float startY = ln.startTime * scrollEditor.scrollSpeed;
            float endY = ln.endTime * scrollEditor.scrollSpeed;

            GameObject sNode = CreateNodeObj(xPos, startY, ln.lane, ln.startTime);
            GameObject eNode = CreateNodeObj(xPos, endY, ln.lane, ln.endTime);
            GameObject bar = CreateBarObj(new Vector2(xPos, startY), new Vector2(xPos, endY), ln.lane);

            spawnedLongNotes.Add(new LongNoteGroup { startNode = sNode, endNode = eNode, bar = bar, laneIndex = ln.lane });
        }

        if (data.gearEvents != null)
        {
            this.gearEvents = new List<EventData>(data.gearEvents);
            this.gearEvents.Sort((a, b) => a.time.CompareTo(b.time));

            bool leftState = false;
            bool rightState = false;

            foreach (var ev in this.gearEvents)
            {
                if (ev.side == 0) { ev.isOn = leftState; leftState = !leftState; }
                else { ev.isOn = rightState; rightState = !rightState; }

                float yPos = ev.time * scrollEditor.scrollSpeed;
                float halfWidth = noteContainer.rect.width / 2f;
                float xPos = (ev.side == 0) ? -halfWidth - 40f : halfWidth + 40f;

                GameObject marker = gearManager.SpawnGearUI(new Vector2(xPos, yPos), ev.isOn);
                if (marker != null)
                {
                    spawnedGearMarkers.Add(marker);
                    EventData loadedEvent = ev;
                    Button btn = marker.GetComponent<Button>() ?? marker.AddComponent<Button>();
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => OnClickGearMarker(loadedEvent));
                }
            }
        }

        spawnedNotes.Sort((a, b) => a.GetComponent<NoteScript>().time.CompareTo(b.GetComponent<NoteScript>().time));
        scrollEditor.bpm = data.bpm;
        RefreshNotesPosition();
        scrollEditor.SetBPMAndRefresh(data.bpm);
        scrollEditor.SetGridDivision(4);
        //if (this.gameObject.SendMessage("RefreshNotesPosition", SendMessageOptions.DontRequireReceiver)) { }
    }

    // --- [노트 생성 및 배치] ---

    void TryPlace()
    {
        if (!GetSnappedPosition(out Vector2 snappedPos, out int laneIndex)) return;

        if (!isLongNoteMode)
        {
            if (IsPositionOccupied(snappedPos, laneIndex)) return;
            CreateShortNote(snappedPos.x, snappedPos.y, laneIndex);
        }
        else HandleLongNotePlacement(snappedPos, laneIndex);
    }

    public void CreateShortNote(float x, float y, int lane)
    {
        float time = Mathf.Abs(y) / scrollEditor.scrollSpeed;
        spawnedNotes.Add(CreateNodeObj(x, y, lane, time));
    }

    public GameObject CreateNodeObj(float x, float y, int lane, float time)
    {
        GameObject note = GetFromPool(notePool, notePrefab);
        RectTransform rt = note.GetComponent<RectTransform>();

        // 1. 부모 설정 및 스케일 초기화
        rt.SetParent(noteContainer, false);
        rt.localScale = Vector3.one;
        float laneWidth = noteContainer.rect.width / laneCount;
        rt.sizeDelta = new Vector2(laneWidth * 0.85f, notePrefab.GetComponent<RectTransform>().sizeDelta.y * 0.4f);
        rt.anchoredPosition = new Vector2(x, y);
        note.GetComponent<Image>().color = GetNoteColor(lane);

        NoteScript ns = note.GetComponent<NoteScript>() ?? note.AddComponent<NoteScript>();
        ns.time = time;
        ns.lane = lane;

        note.SetActive(true);
        return note;
    }

    void HandleLongNotePlacement(Vector2 pos, int lane)
    {
        float time = (Mathf.Abs(pos.y) / (scrollEditor.beatHeight * scrollEditor.viewScale)) * (60f / scrollEditor.bpm);

        if (pendingStartNode == null)
        {
            if (IsNodeAt(pos, lane)) return;
            pendingStartNode = CreateNodeObj(pos.x, pos.y, lane, time);
            pendingStartNode.GetComponent<Image>().color = Color.yellow;
        }
        else
        {
            RectTransform startRT = pendingStartNode.GetComponent<RectTransform>();
            if (Mathf.Abs(startRT.anchoredPosition.x - pos.x) > 1f) return;

            GameObject endNode = CreateNodeObj(pos.x, pos.y, lane, time);
            GameObject bar = CreateBarObj(startRT.anchoredPosition, pos, lane);

            spawnedLongNotes.Add(new LongNoteGroup { startNode = pendingStartNode, endNode = endNode, bar = bar, laneIndex = lane });
            pendingStartNode.GetComponent<Image>().color = GetNoteColor(lane);
            pendingStartNode = null;
        }
    }

    // --- [기존 유틸리티 및 기어 로직 유지] ---

    public void OnClickGearMarker(EventData data)
    {
        if (gearSettingsPanel == null) return;
        selectedGearEvent = data;
        gearSettingsPanel.SetActive(true);
        durationSlider.SetValueWithoutNotify(data.duration);
        durationInput.SetTextWithoutNotify(data.duration.ToString("F1"));
    }

    public void OnDurationSliderChanged(float value)
    {
        if (selectedGearEvent == null) return;
        selectedGearEvent.duration = value;
        durationInput.SetTextWithoutNotify(value.ToString("F2"));
        RefreshGearStates(selectedGearEvent.side);
    }

    void OnDurationInputChanged(string text)
    {
        if (selectedGearEvent == null) return;
        if (float.TryParse(text, out float result))
        {
            selectedGearEvent.duration = Mathf.Clamp(result, 0.01f, 10.0f);
            durationSlider.SetValueWithoutNotify(selectedGearEvent.duration);
            RefreshGearStates(selectedGearEvent.side);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.A)) isLongNoteMode = !isLongNoteMode;
        if (Input.GetMouseButtonDown(0))
        {
            if (IsMouseOverLane()) TryPlace();
            else if (!EventSystem.current.IsPointerOverGameObject()) ShowGearOnEffect();
        }
        if (Input.GetMouseButtonDown(1)) TryDelete();

        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.S))
        {
            OnFastSave();
        }
    }

    void ShowGearOnEffect()
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(noteContainer, Input.mousePosition, null, out Vector2 localPos);
        float closestY = -1f; float minDistance = float.MaxValue;
        foreach (float gridY in scrollEditor.activeGridYPositions)
        {
            float dist = Mathf.Abs(localPos.y - gridY);
            if (dist < minDistance) { minDistance = dist; closestY = gridY; }
        }
        if (minDistance > 20f) return;

        float currentTime = (Mathf.Abs(closestY) / (scrollEditor.beatHeight * scrollEditor.viewScale)) * (60f / scrollEditor.bpm);
        int currentSide = localPos.x < 0 ? 0 : 1;

        if (gearEvents.Exists(e => e.side == currentSide && Mathf.Abs(e.time - currentTime) < 0.01f)) return;

        List<EventData> sameSideEvents = gearEvents.FindAll(e => e.side == currentSide);
        sameSideEvents.Sort((a, b) => a.time.CompareTo(b.time));

        bool nextIsOn = true;
        EventData lastEvent = sameSideEvents.FindLast(e => e.time < currentTime - 0.001f);
        if (lastEvent != null) nextIsOn = !lastEvent.isOn;

        float halfWidth = noteContainer.rect.width / 2f;
        float finalX = (currentSide == 0) ? -halfWidth - 40f : halfWidth + 40f;

        if (gearManager != null)
        {
            GameObject marker = gearManager.SpawnGearUI(new Vector2(finalX, closestY), nextIsOn);
            if (marker != null)
            {
                EventData newEvent = new EventData { time = currentTime, type = "Gear", isOn = nextIsOn, side = currentSide, duration = 1.0f };
                spawnedGearMarkers.Add(marker);
                gearEvents.Add(newEvent);
                Button btn = marker.GetComponent<Button>() ?? marker.AddComponent<Button>();
                btn.onClick.AddListener(() => OnClickGearMarker(newEvent));
                RefreshGearStates(currentSide);
            }
        }
    }

    public void RefreshGearStates(int side)
    {
        List<EventData> sideEvents = gearEvents.FindAll(e => e.side == side);
        sideEvents.Sort((a, b) => a.time.CompareTo(b.time));
        ClearGearVisuals(side);

        bool currentState = true;
        foreach (var ev in sideEvents)
        {
            ev.isOn = currentState;
            CreateGearDurationIndicator(ev);
            currentState = !currentState;
        }

        foreach (var marker in spawnedGearMarkers)
        {
            RectTransform rt = marker.GetComponent<RectTransform>();
            int mSide = rt.anchoredPosition.x < 0 ? 0 : 1;
            if (mSide == side)
            {
                float mTime = (Mathf.Abs(rt.anchoredPosition.y) / (scrollEditor.beatHeight * scrollEditor.viewScale)) * (60f / scrollEditor.bpm);
                EventData data = gearEvents.Find(e => e.side == side && Mathf.Abs(e.time - mTime) < 0.05f);
                if (data != null)
                {
                    marker.GetComponent<Image>().color = data.isOn ? Color.green : Color.red;
                    Text txt = marker.GetComponentInChildren<Text>();
                    if (txt != null) txt.text = data.isOn ? "ON" : "OFF";
                }
            }
        }
    }

    void CreateGearDurationIndicator(EventData ev)
    {
        if (gearIndicatorPrefab == null) return;
        GameObject targetMarker = spawnedGearMarkers.Find(m =>
        {
            RectTransform rt = m.GetComponent<RectTransform>();
            int s = rt.anchoredPosition.x < 0 ? 0 : 1;
            float t = (Mathf.Abs(rt.anchoredPosition.y) / (scrollEditor.beatHeight * scrollEditor.viewScale)) * (60f / scrollEditor.bpm);
            return s == ev.side && Mathf.Abs(ev.time - t) < 0.05f;
        });
        if (targetMarker == null) return;

        GameObject indicator = Instantiate(gearIndicatorPrefab, noteContainer);
        indicator.name = $"GearInd_{ev.side}";
        indicator.transform.SetAsFirstSibling();
        activeGearVisuals.Add(indicator);
        RectTransform rt = indicator.GetComponent<RectTransform>();
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = targetMarker.GetComponent<RectTransform>().anchoredPosition;
        float unitHeight = (scrollEditor.bpm / 60f) * scrollEditor.beatHeight * scrollEditor.viewScale;
        rt.sizeDelta = new Vector2(20f, ev.duration * unitHeight);
        indicator.GetComponent<Image>().color = ev.isOn ? new Color(0, 1, 0, 0.4f) : new Color(1, 0, 0, 0.4f);
    }

    public void UpdateGearVisualScale() { RefreshGearStates(0); RefreshGearStates(1); }

    void ClearGearVisuals(int side)
    {
        for (int i = activeGearVisuals.Count - 1; i >= 0; i--)
        {
            if (activeGearVisuals[i].name == $"GearInd_{side}")
            {
                Destroy(activeGearVisuals[i]);
                activeGearVisuals.RemoveAt(i);
            }
        }
    }

    public void UpdateNoteScales(float oldViewScale, float newViewScale)
    {
        if (oldViewScale == 0) return;
        float ratio = newViewScale / oldViewScale;
        foreach (var note in spawnedNotes) note.GetComponent<RectTransform>().anchoredPosition *= new Vector2(1, ratio);
        foreach (var g in spawnedLongNotes)
        {
            g.startNode.GetComponent<RectTransform>().anchoredPosition *= new Vector2(1, ratio);
            g.endNode.GetComponent<RectTransform>().anchoredPosition *= new Vector2(1, ratio);
            UpdateBarTransform(g.bar.GetComponent<RectTransform>(), g.startNode.GetComponent<RectTransform>().anchoredPosition, g.endNode.GetComponent<RectTransform>().anchoredPosition);
        }
        foreach (var marker in spawnedGearMarkers) marker.GetComponent<RectTransform>().anchoredPosition *= new Vector2(1, ratio);
        UpdateGearVisualScale();
    }

    void TryDelete()
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(noteContainer, Input.mousePosition, null, out Vector2 localPos);
        float correctedY = localPos.y;

        float threshold = 30f; // 클릭 감지 범위
        for (int i = spawnedGearMarkers.Count - 1; i >= 0; i--)
        {
            if (Vector2.Distance(spawnedGearMarkers[i].GetComponent<RectTransform>().anchoredPosition, localPos) < threshold)
            {
                RectTransform rt = spawnedGearMarkers[i].GetComponent<RectTransform>();
                float mTime = (Mathf.Abs(rt.anchoredPosition.y) / (scrollEditor.beatHeight * scrollEditor.viewScale)) * (60f / scrollEditor.bpm);
                int side = rt.anchoredPosition.x < 0 ? 0 : 1;
                gearEvents.RemoveAll(e => e.side == side && Mathf.Abs(e.time - mTime) < 0.05f);
                Destroy(spawnedGearMarkers[i]);
                spawnedGearMarkers.RemoveAt(i);
                RefreshGearStates(side);
                return;
            }
        }
        for (int i = spawnedNotes.Count - 1; i >= 0; i--)
        {
            if (Vector2.Distance(spawnedNotes[i].GetComponent<RectTransform>().anchoredPosition, localPos) < threshold)
            {
                Destroy(spawnedNotes[i]);
                spawnedNotes.RemoveAt(i);
                return;
            }
        }

        // 3. 롱노트 삭제
        for (int i = spawnedLongNotes.Count - 1; i >= 0; i--)
        {
            var g = spawnedLongNotes[i];
            if (Vector2.Distance(g.startNode.GetComponent<RectTransform>().anchoredPosition, localPos) < threshold ||
                Vector2.Distance(g.endNode.GetComponent<RectTransform>().anchoredPosition, localPos) < threshold)
            {
                Destroy(g.startNode);
                Destroy(g.endNode);
                Destroy(g.bar);
                spawnedLongNotes.RemoveAt(i);
                return;
            }
        }
    }

    public GameObject CreateBarObj(Vector2 start, Vector2 end, int lane)
    {
        GameObject bar = GetFromPool(barPool, longBarPrefab);
        UpdateBarTransform(bar.GetComponent<RectTransform>(), start, end);
        bar.GetComponent<Image>().color = new Color(GetNoteColor(lane).r, GetNoteColor(lane).g, GetNoteColor(lane).b, 0.6f);
        return bar;
    }

    void UpdateBarTransform(RectTransform barRT, Vector2 start, Vector2 end)
    {
        barRT.anchoredPosition = new Vector2(start.x, (start.y + end.y) / 2f);
        barRT.sizeDelta = new Vector2((noteContainer.rect.width / laneCount) * 0.6f, Mathf.Abs(end.y - start.y));
    }

    bool GetSnappedPosition(out Vector2 pos, out int lane)
    {
        pos = Vector2.zero; lane = 0;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(noteContainer, Input.mousePosition, null, out Vector2 localPos);
        float closestY = -1f; float minD = float.MaxValue;
        foreach (float gridY in scrollEditor.activeGridYPositions)
        {
            float d = Mathf.Abs(localPos.y - gridY);
            if (d < minD) { minD = d; closestY = gridY; }
        }
        if (minD > 20f) return false;
        float lW = noteContainer.rect.width / laneCount;
        lane = Mathf.Clamp(Mathf.FloorToInt((localPos.x + (noteContainer.rect.width / 2f)) / lW), 0, laneCount - 1);
        pos = new Vector2((lane * lW) + (lW / 2f) - (noteContainer.rect.width / 2f), closestY);
        return true;
    }

    bool IsPositionOccupied(Vector2 pos, int lane)
    {
        foreach (var n in spawnedNotes) if (Vector2.Distance(n.GetComponent<RectTransform>().anchoredPosition, pos) < 1f) return true;
        return false;
    }

    bool IsNodeAt(Vector2 pos, int lane) => IsPositionOccupied(pos, lane);
    bool IsMouseOverLane()
    {
        // 1. 만약 슬라이더 위에 마우스가 있다면 무조건 false (노트 안 찍힘)
        if (songSliderRect != null && RectTransformUtility.RectangleContainsScreenPoint(songSliderRect, Input.mousePosition))
        {
            return false;
        }

        // 2. 기존 레인 판정 로직
        RectTransformUtility.ScreenPointToLocalPointInRectangle(content, Input.mousePosition, null, out Vector2 lp);
        return Mathf.Abs(lp.x) <= content.rect.width / 2f;
    }
    private float GetXFromLane(int lane)
    {
        float laneWidth = noteContainer.rect.width / laneCount;
        return (lane * laneWidth) - (noteContainer.rect.width / 2f) + (laneWidth / 2f);
    }

    private int GetLaneFromX(float x)
    {
        float laneWidth = noteContainer.rect.width / laneCount;
        return Mathf.FloorToInt((x + (noteContainer.rect.width / 2f)) / laneWidth);
    }
    Color GetNoteColor(int l) => (l == 0 || l == laneCount - 1) ? new Color(1f, 0.3f, 0.3f) : new Color(0.4f, 0.8f, 1f);
    GameObject GetFromPool(List<GameObject> p, GameObject pf)
    {
        if (p.Count > 0)
        {
            GameObject o = p[0]; p.RemoveAt(0); o.SetActive(true);
            return o;
        }
        return Instantiate(pf, noteContainer);
    }
    private void ClearAll()
    {
        foreach (var n in spawnedNotes) if (n) n.SetActive(false);
        spawnedNotes.Clear();
        foreach (var ln in spawnedLongNotes)
        {
            if (ln.startNode) ln.startNode.SetActive(false);
            if (ln.endNode) ln.endNode.SetActive(false);
            if (ln.bar) Destroy(ln.bar);
        }
        spawnedLongNotes.Clear();
        foreach (var m in spawnedGearMarkers) if (m) Destroy(m);
        spawnedGearMarkers.Clear();
        gearEvents.Clear();
    }
}