using UnityEngine;
using System.Collections.Generic;

public struct BMSNoteData
{
    public float beatIndex; // 절대 박자 위치
    public int lane;        // 레인 번호
    public string value;    // 노트 종류
}

public struct BMSLongNoteData
{
    public float startBeat; // 시작 박자
    public float endBeat;   // 끝 박자
    public int lane;        // 레인 번호
}

public class BMSParser
{
    public float BPM { get; private set; } = 120f;
    public List<BMSNoteData> Notes = new List<BMSNoteData>();
    public List<BMSLongNoteData> LongNotes = new List<BMSLongNoteData>();

    // LNTYPE 1: 채널 5x에서 같은 noteId가 첫 등장 = 시작, 두 번째 등장 = 끝
    // key: lane번호 / value: 아직 끝을 못 찾은 시작 beatIndex
    private Dictionary<int, float> pendingLongNoteStarts = new Dictionary<int, float>();

    public void Parse(string bmsText)
    {
        Notes.Clear();
        LongNotes.Clear();
        pendingLongNoteStarts.Clear();

        string[] lines = bmsText.Split('\n');

        // 먼저 모든 롱노트 이벤트를 시간 순서대로 처리해야 하므로
        // (measure, lane, beatIndex, isLongChannel) 형태로 모아서 정렬 후 처리
        var allEvents = new List<(float beatIndex, int lane, bool isLong)>();

        foreach (string line in lines)
        {
            string tLine = line.Trim();
            if (string.IsNullOrEmpty(tLine) || !tLine.StartsWith("#")) continue;

            // BPM 추출
            if (tLine.StartsWith("#BPM "))
            {
                string bpmStr = tLine.Substring(5).Trim();
                if (float.TryParse(bpmStr, out float result))
                    BPM = result;
                continue;
            }

            // 노트 데이터 파싱
            if (tLine.Contains(":") && tLine.Length > 1 && char.IsDigit(tLine[1]))
            {
                ParseDataLine(tLine, allEvents);
            }
        }

        // beatIndex 순으로 정렬하여 롱노트 시작/끝 매칭
        allEvents.Sort((a, b) => a.beatIndex.CompareTo(b.beatIndex));

        foreach (var ev in allEvents)
        {
            if (!ev.isLong)
            {
                Notes.Add(new BMSNoteData
                {
                    beatIndex = ev.beatIndex,
                    lane = ev.lane,
                    value = "01"
                });
            }
            else
            {
                // LNTYPE 1: 해당 레인에 미완성 시작이 없으면 → 시작, 있으면 → 끝
                if (!pendingLongNoteStarts.ContainsKey(ev.lane))
                {
                    pendingLongNoteStarts[ev.lane] = ev.beatIndex;
                }
                else
                {
                    float startBeat = pendingLongNoteStarts[ev.lane];
                    pendingLongNoteStarts.Remove(ev.lane);

                    LongNotes.Add(new BMSLongNoteData
                    {
                        startBeat = startBeat,
                        endBeat = ev.beatIndex,
                        lane = ev.lane
                    });
                }
            }
        }

        Debug.Log($"[BMSParser] 일반노트: {Notes.Count}개, 롱노트: {LongNotes.Count}개 파싱 완료 (BPM: {BPM})");
    }

    private void ParseDataLine(string line, List<(float, int, bool)> allEvents)
    {
        string[] split = line.Substring(1).Split(':');
        if (split.Length < 2) return;

        string header = split[0].Trim();
        string data = split[1].Trim();

        if (header.Length < 5) return;

        if (!int.TryParse(header.Substring(0, 3), out int measure)) return;
        if (!int.TryParse(header.Substring(3, 2), out int channel)) return;

        bool isNormal = IsNormalChannel(channel);
        bool isLong = IsLongChannel(channel);

        if (!isNormal && !isLong) return;

        int lane = MapChannelToLane(channel);
        if (lane == -1) return;

        int totalSlots = data.Length / 2;
        for (int i = 0; i < totalSlots; i++)
        {
            string noteId = data.Substring(i * 2, 2);
            if (noteId == "00") continue;

            float relativePos = (float)i / totalSlots;
            float beatIndex = (measure * 4f) + (relativePos * 4f);

            allEvents.Add((beatIndex, lane, isLong));
        }
    }

    // 일반 노트 채널: 11~16
    private bool IsNormalChannel(int channel) => channel >= 11 && channel <= 16;

    // 롱노트 채널: 51~56
    private bool IsLongChannel(int channel) => channel >= 51 && channel <= 56;

    private int MapChannelToLane(int channel)
    {
        if (channel >= 11 && channel <= 16) return channel - 11;
        if (channel >= 51 && channel <= 56) return channel - 51;
        return -1;
    }
}