using System.Collections.Generic;

[System.Serializable]
public class SaveData
{
    public string songName;     // 곡 제목
    public string Artist;       // 아티스트 이름
    public string audioFileName; // 음원 파일 이름 (예: "music.mp3")
    public float bpm; 
    public int Level;
    public string difficulty; 
    public float songOffset = 0.0f;
    
    // 리스트들을 담아줍니다.
    public List<NoteSaveData> notes = new List<NoteSaveData>();
    public List<LongNoteSaveData> longNotes = new List<LongNoteSaveData>();
    public List<EventData> gearEvents = new List<EventData>();
}

[System.Serializable]
public class NoteSaveData
{
    public float time;
    public int lane;
}

[System.Serializable]
public class LongNoteSaveData
{
    public float startTime;
    public float endTime;
    public int lane;
}